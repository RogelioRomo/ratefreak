import hashlib
import json
import os
from pathlib import Path
import subprocess

ROOT = next(parent for parent in Path(__file__).resolve().parents
            if (parent / 'deploy/self-host/compose.yaml').is_file())
BASE = ROOT / '.local/security-closure-2026-11-26'
IMAGES = json.loads((ROOT / '.local/security-runtime-guard-2026-11-25/images/built-images.json').read_text())
environment = os.environ.copy()
environment.update(RATEFREAK_INSTANCE_DIR='/tmp/ratefreak-allocator-config-only',
                   RATEFREAK_PUBLIC_ORIGIN='https://allocator.example',
                   **{'RATEFREAK_' + name.upper() + '_IMAGE': value for name, value in IMAGES.items()})
compose = ['docker', 'compose', '--env-file', '/dev/null', '-f',
           str(ROOT / 'deploy/self-host/compose.yaml'), '--profile', '*']
configuration = json.loads(subprocess.check_output(compose + ['config', '--format', 'json'],
                                                  env=environment, text=True))
results = []
for name in ['postgres', 'api', 'web', 'storage-init', 'migrate', 'tools']:
    service = configuration['services'][name]
    command = ['docker', 'run', '--rm', '--network', 'none', '--read-only',
               '--cap-drop', 'ALL', '--security-opt', 'no-new-privileges:true',
               '--pids-limit', '64', '--memory', '256m', '--cpus', '2',
               '--mount', f'type=bind,src={BASE}/glibc-probe,dst=/probe,readonly',
               '--entrypoint', '/probe']
    # This is the rendered policy, not a separately supplied test-only setting.
    policy = {key: value for key, value in service.get('environment', {}).items()
              if key == 'GLIBC_TUNABLES' or key.startswith('MALLOC_')}
    for key, value in policy.items(): command += ['--env', key + '=' + value]
    command.append(service['image'])
    run = subprocess.run(command, text=True, capture_output=True, timeout=45)
    run.check_returncode()
    result = json.loads(run.stdout)
    print(name, policy, result['arenas'], flush=True)
    results.append({'service': name, 'image': service['image'], 'policy': policy,
                    'command': command, 'result': result, 'passed': result['arenas'] == 1})
report = {'configuration_sha256': hashlib.sha256((ROOT / 'deploy/self-host/compose.yaml').read_bytes()).hexdigest(),
          'results': results, 'passed': all(result['passed'] for result in results)}
(BASE / ('allocator-policy-pass.json' if report['passed'] else 'allocator-policy-failure.json')).write_text(
    json.dumps(report, indent=2) + '\n')
assert report['passed'], 'Reference services create secondary arenas (CVE-2019-1010025 condition)'
