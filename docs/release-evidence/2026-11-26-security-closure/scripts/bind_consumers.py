import hashlib
import json
from pathlib import Path
import shlex
import subprocess

ROOT = next(parent for parent in Path(__file__).resolve().parents
            if (parent / 'deploy/self-host/compose.yaml').is_file())
BASE = ROOT / '.local/security-closure-2026-11-26'
prior = ROOT / '.local/security-runtime-guard-2026-11-25/gcc-rebind'
results = {}
for family, filename in [('web', 'web-probe-full.json'), ('tools', 'tools-probe-full.json'),
                         ('postgres', 'pg-gcc-elf-probe.json')]:
    inventory_path = prior / filename
    inventory = json.loads(inventory_path.read_text())
    image = inventory.get('image_id', inventory.get('container_image_id'))
    metadata = json.loads(subprocess.check_output(['docker', 'image', 'inspect', image], text=True))[0]
    assert metadata['Id'] == image
    paths = {row['path']: row for row in inventory['elf']
             if any('libstdc++' in library for library in row['needed']) or 'libstdc++.so' in row['path']}
    script = []
    for path in paths:
        quoted = shlex.quote(path)
        script += ['sha256sum -- ' + quoted,
                   'dpkg-query -S -- ' + quoted + ' 2>/dev/null || true']
    command = ['docker', 'run', '--rm', '-i', '--network', 'none', '--read-only',
               '--cap-drop', 'ALL', '--security-opt', 'no-new-privileges:true',
               '--entrypoint', 'sh', image, '-s']
    output = subprocess.check_output(command, input='\n'.join(script) + '\n', text=True)
    owners = {}
    seen = set()
    for line in output.splitlines():
        if len(line) > 66 and line[64:66] == '  ':
            digest, path = line[:64], line[66:]
            assert paths[path]['sha256'] == digest, path
            seen.add(path)
        elif ': /' in line:
            owner, path = line.split(': ', 1)
            if path in paths: owners[path] = owner
    assert seen == set(paths)
    package_command = ['docker', 'run', '--rm', '--network', 'none', '--read-only',
                       '--cap-drop', 'ALL', '--security-opt', 'no-new-privileges:true',
                       '--entrypoint', 'dpkg-query', image, '-W',
                       '-f=${binary:Package}\t${Version}\t${source:Package}\t${source:Version}\n']
    packages = {}
    for line in subprocess.check_output(package_command, text=True).splitlines():
        name, version, source, source_version = line.split('\t')
        packages[name] = {'package': name, 'version': version, 'source': source, 'source_version': source_version}
    records = []
    for path, row in paths.items():
        owner = owners.get(path)
        records.append({'path': path, 'sha256': row['sha256'], 'needed': row['needed'],
                        'owner': packages.get(owner) if owner else None,
                        'scope': 'package source' if owner else 'prior application/native source review or usr-merge alias'})
    results[family] = {'image_id': image, 'prior_inventory': str(inventory_path.relative_to(ROOT)),
                       'prior_inventory_sha256': hashlib.sha256(inventory_path.read_bytes()).hexdigest(),
                       'hashes_reverified': len(records), 'consumers': records}
    print(family, len(records), 'C++ payload hashes reverified', flush=True)
(BASE / 'consumer-binding.json').write_text(json.dumps(results, indent=2) + '\n')
