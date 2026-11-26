from concurrent.futures import ThreadPoolExecutor
import hashlib
import json
from pathlib import Path
import re
import subprocess
import urllib.request

ROOT = next(parent for parent in Path(__file__).resolve().parents
            if (parent / 'deploy/self-host/compose.yaml').is_file())
BASE = ROOT / '.local/security-closure-2026-11-26'
DEBUG = BASE / 'debug'
DEBUG.mkdir(exist_ok=True)
PACKAGES = [
    'a/apt/apt-dbgsym_3.0.4_amd64.deb',
    'a/apt/libapt-pkg7.0-dbgsym_3.0.4_amd64.deb',
    'i/icu/libicu76-dbgsym_76.1-4_amd64.deb',
    'libz/libzstd/zstd-dbgsym_1.5.7+dfsg-1_amd64.deb',
]


def acquire(relative):
    url = 'https://deb.debian.org/debian-debug/pool/main/' + relative
    path = DEBUG / relative.rsplit('/', 1)[1]
    if not path.exists():
        with urllib.request.urlopen(url, timeout=90) as response:
            path.write_bytes(response.read())
    subprocess.run(['dpkg-deb', '--extract', str(path), str(DEBUG / 'files')], check=True)
    return {'url': url, 'sha256': hashlib.sha256(path.read_bytes()).hexdigest(),
            'bytes': path.stat().st_size,
            'trust_limit': 'HTTPS download; no independent debug-package index signature verification.'}


with ThreadPoolExecutor(max_workers=4) as pool:
    acquisition = list(pool.map(acquire, PACKAGES))

binding = json.loads((BASE / 'consumer-binding.json').read_text())
records = []
seen = set()
for family, image in binding.items():
    selected = [row for row in image['consumers'] if row['owner']
                and row['owner']['source'] in {'apt', 'icu', 'libzstd'}
                and row['sha256'] not in seen]
    if not selected: continue
    container = subprocess.check_output(['docker', 'create', '--network', 'none',
                                         '--entrypoint', '/bin/true', image['image_id']], text=True).strip()
    try:
        for row in selected:
            if row['sha256'] in seen: continue
            seen.add(row['sha256'])
            binary = DEBUG / row['sha256']
            subprocess.run(['docker', 'cp', container + ':' + row['path'], str(binary)],
                           check=True, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
            assert hashlib.sha256(binary.read_bytes()).hexdigest() == row['sha256']
            notes = subprocess.check_output(['readelf', '--notes', str(binary)], text=True)
            build_id = re.search(r'Build ID: ([0-9a-f]+)', notes).group(1)
            companion = DEBUG / 'files/usr/lib/debug/.build-id' / build_id[:2] / (build_id[2:] + '.debug')
            assert companion.is_file(), (row['path'], build_id)
            debug_notes = subprocess.check_output(['readelf', '--notes', str(companion)], text=True)
            assert 'Build ID: ' + build_id in debug_notes
            dump = subprocess.run(['readelf', '--debug-dump=rawline', str(companion)],
                                  text=True, capture_output=True, check=True)
            header_tables = dump.stdout
            # Header tables strengthen source/build association; they are not
            # by themselves a proof of absent inlined code or safe callers.
            assert 'File Name Table' in header_tables
            matches = {name: [line.strip() for line in header_tables.splitlines() if pattern.search(line)]
                       for name, pattern in {
                           'pbds': re.compile(r'pb_ds|__gnu_pbds|binary_heap_tag'),
                           'parallel': re.compile(r'/(?:c\+\+/[^/]+/)?parallel(?:/|$)|parallel/search'),
                           'bool_vector': re.compile(r'stl_bvector\.h|vector\.tcc'),
                           'bitset': re.compile(r'\bbitset\b'),
                       }.items()}
            records.append({'image_id': image['image_id'], 'path': row['path'],
                            'binary_sha256': row['sha256'], 'package': row['owner'],
                            'build_id': build_id,
                            'debug_sha256': hashlib.sha256(companion.read_bytes()).hexdigest(),
                            'rawline_sha256': hashlib.sha256(header_tables.encode()).hexdigest(),
                            'readelf_stderr': dump.stderr, 'header_matches': matches})
            print(row['path'], build_id, {key: len(value) for key, value in matches.items()}, flush=True)
    finally:
        subprocess.run(['docker', 'rm', container], check=True, stdout=subprocess.DEVNULL)
(BASE / 'debug-header-binding.json').write_text(json.dumps({
    'acquisition': acquisition, 'records': records,
    'scope': 'Exact binary SHA256 and matching Build-ID debug companions; header tables complement source/caller review, not a standalone non-applicability proof.',
}, indent=2) + '\n')
