from concurrent.futures import ThreadPoolExecutor
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import re
import urllib.request

ROOT = next(parent for parent in Path(__file__).resolve().parents
            if (parent / 'deploy/self-host/compose.yaml').is_file())
BASE = ROOT / '.local/security-closure-2026-11-26'
SOURCES = {
    'apt': 'a/apt/apt_3.0.4.dsc',
    'icu': 'i/icu/icu_76.1-4.dsc',
    'z3': 'z/z3/z3_4.13.3-1.dsc',
    'llvm': 'l/llvm-toolchain-19/llvm-toolchain-19_19.1.7-3.dsc',
    'zstd': 'libz/libzstd/libzstd_1.5.7+dfsg-1.dsc',
}


def acquire(item):
    name, relative = item
    root = BASE / name
    root.mkdir(exist_ok=True)
    url = 'https://deb.debian.org/debian/pool/main/' + relative
    descriptor = urllib.request.urlopen(url, timeout=60).read()
    (root / relative.rsplit('/', 1)[1]).write_bytes(descriptor)
    records = [{'url': url, 'sha256': hashlib.sha256(descriptor).hexdigest(),
                'bytes': len(descriptor), 'kind': 'descriptor'}]
    block = descriptor.decode().split('Checksums-Sha256:\n')[1].split('\nFiles:')[0]
    for digest, length, filename in re.findall(r'^ ([0-9a-f]{64}) (\d+) (\S+)$', block, re.M):
        target = root / filename
        source_url = url.rsplit('/', 1)[0] + '/' + filename
        if not target.exists():
            with urllib.request.urlopen(source_url, timeout=120) as response, target.open('wb') as out:
                while chunk := response.read(1024 * 1024):
                    out.write(chunk)
        actual = hashlib.sha256(target.read_bytes()).hexdigest()
        assert target.stat().st_size == int(length) and actual == digest, filename
        records.append({'url': source_url, 'file': str(target.relative_to(BASE)),
                        'sha256': actual, 'bytes': int(length), 'descriptor_hash_match': True})
    return name, records


if __name__ == '__main__':
    with ThreadPoolExecutor(max_workers=4) as pool:
        result = dict(pool.map(acquire, SOURCES.items()))
    (BASE / 'source-acquisition.json').write_text(json.dumps({
        'retrieved_utc': datetime.now(timezone.utc).isoformat(),
        'trust_limit': 'HTTPS acquisition and descriptor SHA256 checks; descriptor signatures not independently verified.',
        'sources': result,
    }, indent=2) + '\n')
    print({name: len(records) for name, records in result.items()})
