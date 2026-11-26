from concurrent.futures import ThreadPoolExecutor
import hashlib
import importlib.util
import json
from pathlib import Path
import subprocess
import time

ROOT = next(parent for parent in Path(__file__).resolve().parents
            if (parent / 'deploy/self-host/compose.yaml').is_file())
BASE = ROOT / '.local/security-closure-2026-11-26'
spec = importlib.util.spec_from_file_location('release_acceptance', ROOT / 'deploy/release/acceptance.py')
acceptance = importlib.util.module_from_spec(spec)
spec.loader.exec_module(acceptance)
images = json.loads((ROOT / '.local/security-runtime-guard-2026-11-25/images/built-images.json').read_text())
root, result = acceptance.run(images, BASE / 'fresh', keep=True)
(BASE / 'fresh-fixture-root.txt').write_text(str(root) + '\n')
configuration = json.loads((root / 'instance.json').read_text())
command, environment = acceptance.operator.compose(root, configuration)
try:
    policy = {}
    for service in ['postgres', 'api', 'web']:
        value = subprocess.check_output(command + ['exec', '-T', service, 'printenv', 'GLIBC_TUNABLES'],
                                        env=environment, text=True).strip()
        assert value == 'glibc.malloc.arena_max=1'
        policy[service] = value
    private = json.loads((root / 'browser-fixture.json').read_text())
    origin, ca = private['origin'], Path(private['ca'])
    def request(_):
        start = time.monotonic()
        client = acceptance.Client(origin, ca)
        client.call('media/' + json.loads((root / 'acceptance.json').read_text())['public']['id'])
        return time.monotonic() - start
    start = time.monotonic()
    with ThreadPoolExecutor(max_workers=4) as pool:
        durations = sorted(pool.map(request, range(40)))
    result.update(configuration_sha256=hashlib.sha256((ROOT / 'deploy/self-host/compose.yaml').read_bytes()).hexdigest(),
                  runtime_policy=policy, persistent_instance_changed=False,
                  concurrency_smoke={'requests': len(durations), 'workers': 4,
                                     'seconds': time.monotonic() - start,
                                     'p95_seconds': durations[int(len(durations) * .95) - 1],
                                     'limit': 'Small synthetic smoke; not representative release capacity qualification.'})
    (BASE / 'application-acceptance.json').write_text(json.dumps(result, indent=2) + '\n')
    print('Fresh HTTPS/authentication/artwork/data acceptance and 40-request concurrency smoke passed.', flush=True)
except BaseException:
    subprocess.run(command + ['down', '--volumes', '--remove-orphans'], env=environment, check=True)
    raise
