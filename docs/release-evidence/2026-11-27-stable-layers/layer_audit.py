"""Read every distributed layer, including files hidden by later whiteouts."""
from pathlib import Path
import collections
import gzip
import hashlib
import json
import re
import subprocess
import tarfile

BASE = Path(__file__).resolve().parent
ROOT = BASE.parents[2]
CANDIDATE = ROOT / '.local/stable-candidate-2026-11-27/candidate-qualified'


def private_values():
    values = []
    env = ROOT / '.env'
    if env.exists():
        for line in env.read_text().splitlines():
            if '=' not in line or line.lstrip().startswith('#'):
                continue
            key, value = line.strip().removeprefix('export ').split('=', 1)
            value = value.strip().strip('\"\'')
            if len(value) >= 12 and re.search(r'secret|password|token|apikey|api_key|contact|email', key, re.I):
                values.append((key, value.encode()))
    local = ROOT / 'src/RateFreak.Api/appsettings.Local.json'
    if local.exists():
        def walk(obj, prefix=''):
            if isinstance(obj, dict):
                for key, value in obj.items():
                    yield from walk(value, prefix + ':' + key)
            elif isinstance(obj, str):
                yield prefix, obj
        for key, value in walk(json.loads(local.read_text())):
            if len(value) >= 12 and re.search(r'secret|password|token|apikey|api_key|contact|email', key, re.I):
                values.append((key, value.encode()))
    return values


def package_status(data):
    rows = []
    for stanza in data.decode().strip().split('\n\n'):
        fields = {}
        for line in stanza.splitlines():
            if line and not line[0].isspace() and ':' in line:
                key, value = line.split(':', 1)
                fields[key] = value.strip()
        if fields.get('Status') == 'install ok installed':
            rows.append({k: fields.get(k) for k in ['Package', 'Version', 'Architecture', 'Source']})
    return rows


def run():
    inventory = json.loads((CANDIDATE / 'source-inventory.json').read_text())
    manifest = json.loads((CANDIDATE / 'candidate-manifest.json').read_text())
    values = private_values()
    report = {'format': 1, 'revision': manifest['revision'], 'configured_private_values_checked': len(values),
              'scanner_sha256': hashlib.sha256(Path(__file__).read_bytes()).hexdigest(), 'scope': 'All distributed image layers and image configuration; content heuristics are not proof of secret absence.', 'images': {}}
    patterns = {
        'github-token': re.compile(rb'\b(?:gh[pousr]_[A-Za-z0-9]{30,}|github_pat_[A-Za-z0-9_]{40,})\b'),
        'aws-access-key': re.compile(rb'\b(?:AKIA|ASIA)[A-Z0-9]{16}\b'),
        'private-key-block': re.compile(rb'-----BEGIN (?:RSA |EC |OPENSSH |DSA )?PRIVATE KEY-----\r?\n[A-Za-z0-9+/]{30,}'),
    }
    for name, entry in inventory['images'].items():
        image = {'image_id': entry['image_id'], 'layers': [], 'files_scanned': 0, 'bytes_scanned': 0,
                 'private_value_matches': [], 'credential_pattern_matches': [], 'suspicious_paths': [],
                 'og_wasm_payloads': [], 'font_payloads': [], 'global_package_manifests': [],
                 'native_payloads': [], 'lower_layer_only_packages': []}
        all_packages = {}
        files = []
        with tarfile.open(CANDIDATE / (name + '.tar.gz')) as outer:
            saved = json.load(outer.extractfile('manifest.json'))[0]
            config = json.load(outer.extractfile(saved['Config']))
            raw_config = json.dumps(config).encode()
            for key, value in values:
                if value in raw_config:
                    image['private_value_matches'].append({'setting': key, 'path': '<image-config>'})
            image['labels'] = config['config'].get('Labels', {})
            image['revision_label_matches'] = image['labels'].get('org.opencontainers.image.revision') == manifest['revision']
            image['version_label_matches'] = image['labels'].get('org.opencontainers.image.version') == manifest['version']
            config_digest = hashlib.sha256(outer.extractfile(saved['Config']).read()).hexdigest()
            assert saved['Config'].endswith(config_digest)
            # Docker's containerd store identifies an OCI index, not its config.
            index = json.load(outer.extractfile('index.json'))
            assert any(m['digest'] == entry['image_id'] for m in index['manifests'])
            def verify_descriptor(descriptor):
                blob = 'blobs/sha256/' + descriptor['digest'].removeprefix('sha256:')
                payload = outer.extractfile(blob).read()
                assert hashlib.sha256(payload).hexdigest() == descriptor['digest'].removeprefix('sha256:')
                node = json.loads(payload)
                if 'manifests' in node:
                    return any(verify_descriptor(m) for m in node['manifests'])
                return node.get('config', {}).get('digest') == 'sha256:' + config_digest
            assert any(verify_descriptor(m) for m in index['manifests'] if m['digest'] == entry['image_id'])
            image['oci_identity_chain_valid'] = True
            for position, layer_name in enumerate(saved['Layers']):
                layer_record = {'position': position, 'layer': layer_name, 'regular_files': 0, 'whiteouts': [], 'installed_packages': [], 'members': []}
                layer_digest = hashlib.sha256()
                with outer.extractfile(layer_name) as layer_bytes:
                    compressed_hash = hashlib.sha256()
                    while chunk := layer_bytes.read(1024*1024): compressed_hash.update(chunk)
                layer_record['blob_sha256'] = compressed_hash.hexdigest()
                assert layer_name.endswith(compressed_hash.hexdigest())
                with outer.extractfile(layer_name) as raw_bytes:
                    magic = raw_bytes.read(2)
                    raw_bytes.seek(0)
                    layer_bytes = gzip.GzipFile(fileobj=raw_bytes) if magic == b'\x1f\x8b' else raw_bytes
                    while chunk := layer_bytes.read(1024*1024): layer_digest.update(chunk)
                layer_record['sha256'] = layer_digest.hexdigest()
                layer_record['rootfs_diff_id_matches'] = 'sha256:' + layer_digest.hexdigest() == config['rootfs']['diff_ids'][position]
                assert layer_record['rootfs_diff_id_matches']
                with outer.extractfile(layer_name) as raw:
                    # Docker validates image content on import; inspect the actual tar here.
                    with tarfile.open(fileobj=raw, mode='r|*') as layer:
                        for member in layer:
                            path = member.name.removeprefix('./').lstrip('/')
                            layer_record['members'].append({'path':path,'type':member.type.decode(errors='replace'),'linkname':member.linkname,'bytes':member.size})
                            metadata = (member.name + '\n' + member.linkname + '\n' + json.dumps(member.pax_headers)).encode()
                            for key, value in values:
                                if value in metadata:
                                    image['private_value_matches'].append({'layer':position,'path':path,'rule':key,'location':'tar-metadata'})
                            if '/.wh.' in '/' + path:
                                layer_record['whiteouts'].append(path)
                            if not member.isfile():
                                continue
                            layer_record['regular_files'] += 1
                            image['files_scanned'] += 1
                            image['bytes_scanned'] += member.size
                            if (path.startswith('app/') and '/node_modules/' not in path and
                                any(part in {'.env', '.git', '.local', '.aws', '.codex', 'secrets', 'uploads', 'exports', 'keys'} for part in Path(path).parts)):
                                image['suspicious_paths'].append({'layer': position, 'path': path})
                            file_hash = hashlib.sha256()
                            capture = path == 'var/lib/dpkg/status' or (path.endswith('/package.json') and (path.startswith('usr/local/') or path.startswith('opt/')))
                            data = bytearray() if capture else None
                            hits = set()
                            previous = b''
                            with layer.extractfile(member) as source:
                                while chunk := source.read(1024 * 1024):
                                    file_hash.update(chunk)
                                    combined = previous + chunk
                                    for key, value in values:
                                        if value in combined:
                                            hits.add(('private-setting', key))
                                    for rule, pattern in patterns.items():
                                        if pattern.search(combined):
                                            hits.add(('credential-pattern', rule))
                                    previous = combined[-4096:]
                                    if capture:
                                        data.extend(chunk)
                            record = {'layer': position, 'path': path, 'sha256': file_hash.hexdigest(), 'bytes': member.size}
                            files.append(record)
                            for kind, key in sorted(hits):
                                target = 'private_value_matches' if kind == 'private-setting' else 'credential_pattern_matches'
                                image[target].append({'layer': position, 'path': path, 'rule': key})
                            if data is not None:
                                if path == 'var/lib/dpkg/status':
                                    rows = package_status(data)
                                    layer_record['installed_packages'] = rows
                                    for row in rows:
                                        all_packages[(row['Package'], row['Version'])] = row
                                else:
                                    package = json.loads(data)
                                    if package.get('name') and package.get('version'):
                                        image['global_package_manifests'].append({**record, 'name': package['name'], 'version': package['version']})
                            if '/@vercel/og/' in '/' + path or path.endswith(('.wasm', '.ttf')):
                                image['og_wasm_payloads'].append(record)
                            if path.endswith(('.woff', '.woff2', '.ttf', '.otf')):
                                image['font_payloads'].append(record)
                            if path.endswith(('.so', '.node', '.dll', '.dylib', '.pdb')) or re.search(r'\.so\.\d', path):
                                image['native_payloads'].append(record)
                image['layers'].append(layer_record)
                print(name, 'layer', position, 'files', layer_record['regular_files'], flush=True)
        final = {(c['name'].split(':')[0], c['version']) for c in entry['components'] if c['scope'] == 'os'}
        image['lower_layer_only_packages'] = [row for key, row in sorted(all_packages.items()) if key not in final]
        (BASE / (name + '-layer-members.json')).write_text(json.dumps([{'layer':l['position'],'members':l.pop('members')} for l in image['layers']],indent=2,sort_keys=True)+'\n')
        (BASE / (name + '-layer-files.json')).write_text(json.dumps(files, indent=2, sort_keys=True) + '\n')
        image['file_inventory_sha256'] = hashlib.sha256((BASE / (name + '-layer-files.json')).read_bytes()).hexdigest()
        report['images'][name] = image
        (BASE / 'all-layer-audit.json').write_text(json.dumps(report, indent=2, sort_keys=True) + '\n')
        print(name, 'complete', image['files_scanned'], 'files', image['bytes_scanned'], 'bytes', flush=True)
    return report


if __name__ == '__main__':
    run()
