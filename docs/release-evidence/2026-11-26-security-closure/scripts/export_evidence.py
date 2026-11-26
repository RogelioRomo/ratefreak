from collections import Counter
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import re
import subprocess

ROOT = next(parent for parent in Path(__file__).resolve().parents
            if (parent / 'deploy/self-host/compose.yaml').is_file())
BASE = ROOT / '.local/security-closure-2026-11-26'
DEST = ROOT / 'docs/release-evidence/2026-11-26-security-closure'
DEST.mkdir(parents=True, exist_ok=True)


def normalized(value):
    if isinstance(value, str): return value.replace(str(ROOT), '<repository>')
    if isinstance(value, list): return [normalized(item) for item in value]
    if isinstance(value, dict): return {key: normalized(item) for key, item in value.items()}
    return value


def write(name, value):
    (DEST / name).write_text(json.dumps(normalized(value), indent=2) + '\n')


def read(name):
    return json.loads((BASE / name).read_text())


original = ROOT / 'docs/release-evidence/2026-11-25-security-followup/advisory-ledger.json'
ids = {row['id'] for row in json.loads((original.parent / 'remaining-findings.json').read_text())}
ledger = json.loads(original.read_text())
selected = [row for row in ledger if row['advisory'] in ids and row['disposition'] == 'open-proof-gap']
assert len(selected) == 22
headers = {'CVE-2026-102010', 'CVE-2026-114690', 'CVE-2026-114691', 'CVE-2026-115455'}
records = []
for row in selected:
    advisory = row['advisory']
    closed = advisory in headers or advisory in {'CVE-2019-1010025', 'CVE-2026-125709'}
    reason = {
        'CVE-2019-1010022': 'Thread-stack/TCB stack-guard mitigation property remains unresolved.',
        'CVE-2019-1010024': 'Actual stack-address reuse persists even when retired mappings are evicted.',
        'CVE-2019-1010025': 'Reference startup limits malloc to its main arena; the aligned secondary-arena condition is removed.',
        'CVE-2026-102010': 'Exact OS source/build review supplements prior application closure; no affected GNU pb_ds heap instantiation.',
        'CVE-2026-114690': 'Exact OS source/build review finds no GNU parallel-search activation or caller; Clang string matches are classified.',
        'CVE-2026-114691': 'Reviewed amd64 constructor counts are uint32_t, existing larger-object container lengths, or checked growth; Z3 custom vector distinguished.',
        'CVE-2026-115455': 'No supported wide-stream bitset extraction caller in the bound OS/native closure; actual bitset header presence is retained.',
        'CVE-2026-125709': ('Node compatibility formatter excluded by C++20 source/build-rule branch; Maglev std::cout graph diagnostics disabled; Turbolev uses custom FILE* streambuf; packaged flag setter only controls GC.' if row['image'] == 'web' else
                            'APT/ICU file-stream callers reviewed; direct APT iterator targets memory, ICU output uses guarded ordinary streams; no repeated raw filebuf writes.'),
    }[advisory]
    records.append({'id': advisory, 'image': row['image'], 'image_reference': row['image_reference'],
                    'source': row['source'], 'matches': row['matches'],
                    'disposition': ('conditional-configuration-exclusion' if advisory == 'CVE-2019-1010025' else
                                    'conditional-supported-caller-exclusion') if closed else 'open-proof-gap',
                    'assessment': reason,
                    'evidence': ['../../security-closure-2026-11-26.md', 'source-review.json',
                                 'consumer-binding.json', 'debug-header-binding.json', 'glibc-probes.json',
                                 'allocator-policy-pass.json', 'node-stream-proof.json'],
                    'reassess_if': ['Image/native bytes, architecture, compiler/header/source inputs or the documented supported caller/configuration scope change.',
                                    'Allocator policy is removed/overridden, PostgreSQL JIT is enabled, or custom plugins, arbitrary SQL, native code or diagnostic startup options are introduced.']})
counts = Counter(row['disposition'] for row in records)
remaining = []
for advisory in sorted(ids):
    rows = [row for row in records if row['id'] == advisory and row['disposition'] == 'open-proof-gap']
    if not rows: continue
    remaining.append({'id': advisory, 'source': rows[0]['source'],
                       'images': sorted({row['image'] for row in rows}),
                       'matches': sum(len(row['matches']) for row in rows),
                       'severities': sorted({match['severity'] for row in rows for match in row['matches']}),
                       'reason': rows[0]['assessment']})
assert len(remaining) == 2 and counts['open-proof-gap'] == 6
write('dispositions.json', {'baseline_ledger': str(original.relative_to(ROOT)),
                           'baseline_sha256': hashlib.sha256(original.read_bytes()).hexdigest(),
                           'scope': 'Overlay of the prior 22 open image/source records; no new scan or package-fix claim. Other prior conditional assessments remain in force.',
                           'records': records})
write('remaining-findings.json', remaining)
for name in ['consumer-binding.json', 'source-acquisition.json', 'debug-header-binding.json',
             'glibc-probes.json', 'allocator-policy-failure.json', 'allocator-policy-pass.json',
             'application-acceptance.json', 'maintenance-acceptance.json', 'node-stream-proof.json']:
    write(name, read(name))

search = read('os-source-search.json')
source_summary = {}
for family, data in search.items():
    if family == 'zstd': continue
    source_summary[family] = {'file_count': len(data['files']),
                              'match_counts': {key: len(value) for key, value in data['matches'].items()}}
source_summary['zstd'] = {'file_count': len(read('source-search-zstd.json')['files']), 'relevant_matches': 0}
write('source-review.json', {
    'sources': source_summary,
    'total_source_build_patch_files': sum(value['file_count'] for value in source_summary.values()),
    'source_binding': 'Exact Debian package source/version plus descriptor hashes; not an independently reproduced build.',
    'compiled_binding': '150 reverified C++ payload paths, with aliases/foreign packaged assets distinguished; 26 unique APT/ICU/pzstd Build-ID debug companions.',
    'debug_header_limit': 'Preserve positive stl_bvector and APT bitset header observations. No GNU pbds/parallel header observations. Header tables supplement source/caller analysis; absence alone is not an exclusion.',
    'glibc_binding': read('glibc/source-binding.json'),
    'glibc_debian_allocator_patch_matches': read('glibc/debian-patch-review.json'),
    'additional_application_archives': len(read('additional-callers.json')['archives']),
    'raw_source_searches': {name: hashlib.sha256((BASE / name).read_bytes()).hexdigest()
                            for name in ['os-source-search.json', 'source-search-zstd.json', 'additional-callers.json']},
    'manual_review': {
        'apt': 'map_id_t is uint32_t; other bool counts derive from existing DescFile pointer vectors. TimeRFC1123 ostreambuf_iterator writes to ostringstream. Raw tee sputc helper is test-only. File transfers use ordinary ofstream operations.',
        'icu': 'tz2icu bool constructor uses existing vector<int32_t> length; JSON bool vectors start empty and grow. JSON output adapter uses stream.put/write; raw rdbuf adapter reads input. No bitset/wide-stream instantiation.',
        'llvm': 'Three GNU parallel matches are Clang strings. Three production bool constructors use existing vector<Product/PartialMulCandidate> lengths. Core bitsets use numeric/bit operations, no wide extraction. Runtime JIT is separately excluded by the bound configuration.',
        'z3': 'Production vector<bool> is the project custom vector array; std::vector<bool> literal occurs in an example. LLVM/Z3 load paths retain JIT-off supported-caller restriction.',
        'zstd': 'Exact pzstd source/patch set has no affected GNU heap/parallel/bool-vector/wide-bitset/raw filebuf output match; debug companion matched. It is not invoked by reference backup/restore.',
        'arena_overrides': 'Application archives have no M_ARENA_MAX override. APT/ICU/Z3/Zstd source checks likewise found none. LLVM allocator interposition matches belong to compiler-rt sanitizers/tests rather than the supported loaded library.',
        'node_stream_closure': 'Compatibility formatter excluded by C++20 source/build rules with C++14 positive control. Maglev std::cout graph diagnostics default off; Turbolev uses custom FILE* streams. Exact artifact setter only enables a GC flag. Reassess changed startup flags/native code/compiler inputs.',
    }})
for name in ['operator-tests.log', 'release-tests.log', 'release-tests-sandbox-failure.log', 'compliance-tests.log']:
    (DEST / name).write_text(normalized((BASE / name).read_text()))

scripts = DEST / 'scripts'
scripts.mkdir(exist_ok=True)
for name in ['glibc_probe.c', 'acquire_os.py', 'scan_os.py', 'scan_additional_callers.py',
             'run_glibc.py', 'verify_policy.py', 'bind_consumers.py', 'debug_headers.py',
             'acceptance.py', 'check_maintenance.py', 'node_stream_proof.py', 'export_evidence.py']:
    source = (BASE / name).read_text()
    if name.endswith('.py'):
        source = source.replace('BASE = Path(__file__).resolve().parent\nROOT = BASE.parents[1]',
                                "ROOT = next(parent for parent in Path(__file__).resolve().parents\n            if (parent / 'deploy/self-host/compose.yaml').is_file())\nBASE = ROOT / '.local/security-closure-2026-11-26'")
        source = re.sub(r'^BASE = Path\(__file__\)\.resolve\(\)\.parent$',
                        "ROOT = next(parent for parent in Path(__file__).resolve().parents\n            if (parent / 'deploy/self-host/compose.yaml').is_file())\nBASE = ROOT / '.local/security-closure-2026-11-26'", source, flags=re.M)
    (scripts / name).write_text(source)

write('verification.json', {
    'recorded_utc': datetime.now(timezone.utc).isoformat(),
    'head_at_review': subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT, text=True).strip(),
    'compose_sha256': hashlib.sha256((ROOT / 'deploy/self-host/compose.yaml').read_bytes()).hexdigest(),
    'images': read('application-acceptance.json')['candidateImages'],
    'original_open_ids': len(ids), 'current_open_ids': len(remaining),
    'original_open_records': len(selected), 'current_open_records': counts['open-proof-gap'],
    'original_associations': sum(len(row['matches']) for row in records),
    'current_open_associations': sum(row['matches'] for row in remaining),
    'closed_records': len(selected) - counts['open-proof-gap'],
    'release_gate_clear': False, 'persistent_instance_changed': False,
    'scanner_scope': 'Prior exact-image scan retained; no refreshed vulnerability-database claim.',
    'tests': {'operator': 77, 'release': 26, 'compliance': 20, 'passed': True,
              'initial_release_sandbox_failure_retained': True},
    'review': 'Independent read-only review found no blocking issue in the conditional exclusions or allocator configuration; subsequent Node source/sink review supported stream closure. Two remaining glibc IDs still block release.',
    'files': {str(path.relative_to(DEST)): hashlib.sha256(path.read_bytes()).hexdigest()
              for path in sorted(DEST.rglob('*')) if path.is_file() and path.name != 'verification.json'},
})
print(len(selected), 'records assessed;', len(remaining), 'open IDs;', counts['open-proof-gap'],
      'open records;', sum(row['matches'] for row in remaining), 'open package associations')
