import hashlib
import json
from pathlib import Path
import re
import tarfile

ROOT = next(parent for parent in Path(__file__).resolve().parents
            if (parent / 'deploy/self-host/compose.yaml').is_file())
BASE = ROOT / '.local/security-closure-2026-11-26'
PATTERNS = {
    'pbds': re.compile(r'pb_ds|__gnu_pbds|binary_heap_tag'),
    'parallel': re.compile(r'_GLIBCXX_PARALLEL|__gnu_parallel|[<\"]parallel/'),
    'vector_bool': re.compile(r'\bvector\s*<\s*bool\b'),
    'bitset': re.compile(r'\bbitset\s*<|\bwistream\b|\bwifstream\b|\bwstringstream\b'),
    'filebuf': re.compile(r'\bsputc\s*\(|\bsputn\s*\(|\bpubsetbuf\s*\(|\bbasic_filebuf\b|\bfilebuf\b|\brdbuf\s*\(|\bostreambuf_iterator\b'),
}
SUFFIXES = {'.c', '.cc', '.cpp', '.cxx', '.h', '.hh', '.hpp', '.hxx', '.inc',
            '.cmake', '.in', '.am', '.ac', '.mk', '.py', '.sh', '.patch', '.diff',
            '.tcc', '.tpp', '.ipp', '.inl', '.gn', '.gni', '.gyp', '.gypi', '.td', '.def'}
report = {}
for name in ['apt', 'icu', 'z3', 'zstd', 'llvm']:
    matches = {key: [] for key in PATTERNS}
    files = []
    for archive in sorted((BASE / name).glob('*.tar.*')):
        if archive.suffix == '.asc': continue
        with tarfile.open(archive, 'r:*') as tar:
            for member in tar:
                path = Path(member.name)
                if not member.isfile() or not (path.suffix in SUFFIXES or path.name in {'rules', 'Makefile', 'CMakeLists.txt', 'series', 'configure'}):
                    continue
                data = tar.extractfile(member).read()
                files.append({'archive': archive.name, 'path': member.name,
                              'sha256': hashlib.sha256(data).hexdigest()})
                lines = data.decode('utf-8', errors='replace').splitlines()
                source = data.decode('utf-8', errors='replace')
                for key, pattern in PATTERNS.items():
                    for match in pattern.finditer(source):
                        index = source.count('\n', 0, match.start())
                        matches[key].append({'archive': archive.name, 'path': member.name,
                                             'line': index + 1, 'text': lines[index].strip()[:1500]})
    report[name] = {'files': files, 'matches': matches}
    print(name, len(files), {key: len(value) for key, value in matches.items()}, flush=True)
    (BASE / ('source-search-' + name + '.json')).write_text(json.dumps(report[name], indent=2) + '\n')
(BASE / 'os-source-search.json').write_text(json.dumps(report, indent=2) + '\n')
