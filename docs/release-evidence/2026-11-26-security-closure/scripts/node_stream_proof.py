import hashlib
import json
import os
from pathlib import Path
import re
import subprocess

ROOT = next(parent for parent in Path(__file__).resolve().parents
            if (parent / 'deploy/self-host/compose.yaml').is_file())
BASE = ROOT / '.local/security-closure-2026-11-26'
IMAGE = 'sha256:c04ceb1759155f4b84d00b0bf51f24e0280a174affb8197430f3934316952e1e'
source = BASE / 'selected/node-v24.22.3/deps/simdjson/simdjson.cpp'
rules = BASE / 'selected/node-v24.22.3/common.gypi'
assert "'-std=gnu++20'" in rules.read_text()
preprocessing = {}
for standard in ['gnu++14', 'gnu++20']:
    output = BASE / ('simdjson-' + standard + '.ii')
    command = ['g++', '-std=' + standard, '-E', str(source), '-o', str(output)]
    subprocess.run(command, check=True)
    data = output.read_bytes()
    count = len(re.findall(rb'\bwrite_padding\s*\(', data))
    preprocessing[standard] = {'command': command, 'sha256': hashlib.sha256(data).hexdigest(),
                               'write_padding_mentions': count}
assert preprocessing['gnu++14']['write_padding_mentions'] > 0
assert preprocessing['gnu++20']['write_padding_mentions'] == 0
command = ['docker', 'run', '--rm', '--network', 'none', '--read-only', '--cap-drop', 'ALL',
           '--security-opt', 'no-new-privileges:true', '--entrypoint', 'node', IMAGE, '--v8-options']
options = subprocess.check_output(command, text=True)
flags = {}
for name in ['print-maglev-graph', 'print-maglev-graphs', 'trace-maglev-regalloc']:
    match = re.search(r'^  --' + re.escape(name) + r' \([^\n]*\)\n\s+type: bool  default: --no-' + re.escape(name) + r'$', options, re.M)
    assert match, name
    flags[name] = match.group()
probe = '''import json,pathlib,re,hashlib,os
rows=[]
for p in pathlib.Path('/app').rglob('*'):
 if not p.is_file() or p.suffix not in {'.js','.cjs','.mjs'}:continue
 s=p.read_text(errors='replace')
 if 'setFlagsFromString' in s:
  rows.append({'path':str(p),'sha256':hashlib.sha256(p.read_bytes()).hexdigest(),
               'calls':re.findall(r'setFlagsFromString\\(([^)]*)\\)',s)})
print(json.dumps({'setters':rows,'NODE_OPTIONS':os.environ.get('NODE_OPTIONS')}))
'''
command = ['docker', 'run', '--rm', '--network', 'none', '--read-only', '--cap-drop', 'ALL',
           '--security-opt', 'no-new-privileges:true', '--entrypoint', 'python3', IMAGE, '-c', probe]
artifact = json.loads(subprocess.check_output(command, text=True))
assert artifact['NODE_OPTIONS'] is None
assert all(call == "'--detect-ineffective-gcs-near-heap-limit'"
           for row in artifact['setters'] for call in row['calls'])
files = json.loads((BASE / 'node-stream-callers.json').read_text()) + json.loads((BASE / 'node-stream-sinks.json').read_text())
proof = {
    'image_id': IMAGE, 'node': '24.22.3',
    'native_sha256': 'a7ec1b37e6ff9d187e4198c5b6df9a4eb1c74dd31804fb3bac55cc4ceca1ac01',
    'compiler': subprocess.check_output(['g++', '--version'], text=True).splitlines()[0],
    'preprocessing': preprocessing,
    'build_rules_sha256': hashlib.sha256(rules.read_bytes()).hexdigest(),
    'source_files': files, 'actual_default_flags': flags,
    'packaged_flag_setters': artifact,
    'sink_review': {
        'simdjson': 'Node Linux build rules select C++20. The default string_view selection uses std::string_view, so nonstd fallback write_padding is not compiled. Full source review found no selection override outside the two amalgamated copies.',
        'maglev': 'std::cout graph-printer callers require print-maglev-graph(s) or trace-maglev-regalloc; actual defaults are off and fixed reference startup does not enable them.',
        'turbolev': 'CodeTracer::StreamScope uses V8 OFStream/StdoutStream. OFStreamBase derives from std::streambuf and calls fputc/fwrite rather than GCC basic_filebuf overflow.',
        'dbg_stream': 'The DbgStreamBuf sputc branch is Windows-only and its own streambuf implementation.',
        'lief': 'ostreambuf_iterator file output match is a commented example; other apparent hits are Windows DLL ordinal-name strings.',
        'fmt': 'spdlog bundled chrono match is a decltype of sputn, not a runtime single-character filebuf retry.',
        'next': 'The only packaged JavaScript flag-setting call enables detect-ineffective-gcs-near-heap-limit, not graph printing. Its file hash and literal argument are retained.',
    },
    'limits': 'Source/build-rule branch proof using a host preprocessor plus exact image/source bindings; no independent Node rebuild or exploit reproduction. The earlier ordinary Node/Sharp filebuf caller review remains required.',
    'reassess_if': ['Native/source/compiler inputs, Node startup environment/options, diagnostic flags or supported JavaScript/native callers change.'],
}
(BASE / 'node-stream-proof.json').write_text(json.dumps(proof, indent=2) + '\n')
print('Node stream closure passed: C++14 positive control, C++20 excluded branch, exact-image diagnostic defaults and packaged flag setters.')
