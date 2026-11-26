import hashlib
import json
from pathlib import Path
import subprocess

ROOT = next(parent for parent in Path(__file__).resolve().parents
            if (parent / 'deploy/self-host/compose.yaml').is_file())
BASE = ROOT / '.local/security-closure-2026-11-26'
IMAGES = {
    'api': 'sha256:3984e4af4f632a888cdb60337103b44bc8c88a9450e89631761fef1b87e822e0',
    'web': 'sha256:c04ceb1759155f4b84d00b0bf51f24e0280a174affb8197430f3934316952e1e',
    'tools': 'sha256:4a3f1d57fecb70da5aadc05d16eb604545b0de59a502b6753511298e3804f772',
    'postgres': 'sha256:87c8b42f423b45d5247e6aa8fdb2e7762fec164c68a50fe3eb07e7bfcaf4dbcf',
}
POLICIES = {'default': None, 'one-arena': 'glibc.malloc.arena_max=1',
            'no-stack-cache': 'glibc.pthread.stack_cache_size=0',
            'combined': 'glibc.malloc.arena_max=1:glibc.pthread.stack_cache_size=0'}
results = []
for family, image in IMAGES.items():
    for policy, tunables in POLICIES.items():
        command = ['docker', 'run', '--rm', '--network', 'none', '--read-only',
                   '--cap-drop', 'ALL', '--security-opt', 'no-new-privileges:true',
                   '--pids-limit', '64', '--memory', '256m', '--cpus', '2',
                   '--mount', f'type=bind,src={BASE}/glibc-probe,dst=/probe,readonly',
                   '--entrypoint', '/probe']
        if tunables is not None: command += ['--env', 'GLIBC_TUNABLES=' + tunables]
        command.append(image)
        run = subprocess.run(command, text=True, capture_output=True, timeout=45)
        run.check_returncode()
        result = {'family': family, 'image_id': image, 'policy': policy,
                  'command': command, 'exit_code': run.returncode, 'result': json.loads(run.stdout)}
        results.append(result)
        print(family, policy, result['result'], flush=True)
(BASE / 'glibc-probes.json').write_text(json.dumps({
    'probe_source_sha256': hashlib.sha256((BASE / 'glibc_probe.c').read_bytes()).hexdigest(),
    'probe_binary_sha256': hashlib.sha256((BASE / 'glibc-probe').read_bytes()).hexdigest(),
    'results': results,
}, indent=2) + '\n')
