import hashlib
import json
from pathlib import Path
import re
import tarfile

ROOT = next(parent for parent in Path(__file__).resolve().parents
            if (parent / 'deploy/self-host/compose.yaml').is_file())
BASE = ROOT / '.local/security-closure-2026-11-26'
prior = json.loads((ROOT / '.local/security-skia-update-2026-11-25/glibc-callers/source-search.json').read_text())
patterns = {'arena_override': re.compile(r'\bmallopt\b|M_ARENA_MAX|arena_max|MALLOC_ARENA_MAX'),
            'raw_output': re.compile(r'\bostreambuf_iterator\b|\bsputc\s*\(|\bsputn\s*\(')}
matches = []
archives = []
for record in prior['archives']:
    archive = ROOT / record['file']
    assert hashlib.sha256(archive.read_bytes()).hexdigest() == record['sha256']
    archives.append(record)
    with tarfile.open(archive) as tar:
        for member in tar:
            if not member.isfile() or not member.name.endswith(('.cc', '.cpp', '.cxx', '.c', '.h', '.hpp', '.hxx', '.inl', '.inc', '.ipp', '.tcc', '.sh', '.py')):
                continue
            source = tar.extractfile(member).read().decode('utf-8', errors='replace')
            lines = source.splitlines()
            for key, pattern in patterns.items():
                for match in pattern.finditer(source):
                    index = source.count('\n', 0, match.start())
                    matches.append({'archive': record['file'], 'file': member.name,
                                    'sha256': hashlib.sha256(source.encode()).hexdigest(),
                                    'kind': key, 'line': index + 1, 'text': lines[index][:1000]})
(BASE / 'additional-callers.json').write_text(json.dumps({'archives': archives, 'matches': matches}, indent=2) + '\n')
print('Additional review:', len(archives), 'archives;', len(matches), 'matches')
