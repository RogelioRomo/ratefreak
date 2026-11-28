"""Collect actual reference-image package, license and binary identities on the host.

Containers are exported without starting them. No executable payload is included in
notice archives. Collection is an inventory, not source/license clearance: callers
must resolve coverage_gaps against independently reviewed supplemental materials.
Go >=1.18 inline buildinfo follows Go's debug/buildinfo and runtime/debug formats.
"""
import gzip
import hashlib
import io
import json
import posixpath
from pathlib import Path, PurePosixPath
import re
import struct
import subprocess
import tarfile
import tempfile

ROLES = ('postgres', 'caddy')
REQUIRED_GO = {'postgres': '/usr/local/bin/gosu', 'caddy': '/usr/bin/caddy'}


def _records(data, separator):
    try:
        text = data.decode('utf-8')
    except UnicodeDecodeError as error:
        raise ValueError('Package database is not UTF-8.') from error
    records = []
    for block in re.split(r'\n\s*\n', text.strip()):
        fields = {}; previous = None
        for line in block.splitlines():
            if line[:1].isspace() and previous:
                fields[previous] += '\n' + line[1:]
                continue
            if separator not in line:
                raise ValueError('Malformed package database field.')
            key, value = line.split(separator, 1)
            if not key or key in fields:
                raise ValueError('Duplicate or empty package field.')
            fields[key] = value.strip(); previous = key
        if fields:
            records.append(fields)
    if not records:
        raise ValueError('Empty package database.')
    return records


def _identity(row, fields):
    if any(not row.get(key) or any(c.isspace() for c in row[key]) for key in fields):
        raise ValueError('Missing or invalid package identity.')


def _unique(rows):
    seen = set()
    for row in rows:
        key = row['name'], row['architecture']
        if key in seen:
            raise ValueError('Duplicate installed package identity.')
        seen.add(key)
    if not rows:
        raise ValueError('No installed packages.')
    return sorted(rows, key=lambda row: (row['name'], row['architecture']))


def parse_dpkg(data):
    rows = []
    for fields in _records(data, ':'):
        if fields.get('Status') != 'install ok installed':
            continue
        _identity(fields, ('Package', 'Version', 'Architecture'))
        source = fields.get('Source', fields['Package'])
        match = re.fullmatch(r'([a-z0-9][a-z0-9+.-]*)(?: \(([^\s()]+)\))?', source)
        if not match:
            raise ValueError('Malformed dpkg source package identity.')
        rows.append({'scope': 'os', 'name': fields['Package'], 'version': fields['Version'],
                     'source_package': match[1], 'source_version': match[2] or fields['Version'],
                     'architecture': fields['Architecture']})
    return _unique(rows)


def parse_apk(data):
    rows = []
    # APK's installed database repeats F/R/a/Z file-list fields. Parse package
    # identity before its file section; that section is retained verbatim separately.
    try:
        blocks = re.split(r'\n\s*\n', data.decode('utf-8').strip())
    except UnicodeDecodeError as error:
        raise ValueError('APK database is not UTF-8.') from error
    for block in blocks:
        header = []
        for line in block.splitlines():
            if line.startswith('F:'):
                break
            header.append(line)
        fields = _records(('\n'.join(header)+'\n').encode(), ':')[0]
        _identity(fields, ('P', 'V', 'A', 'o'))
        rows.append({'scope': 'os', 'name': fields['P'], 'version': fields['V'],
                     'source_package': fields['o'], 'source_version': fields['V'],
                     'architecture': fields['A'], 'license': fields.get('L', '')})
    return _unique(rows)


def _elf_segments(data):
    if data[:4] != b'\x7fELF' or len(data) < 52 or data[4] not in (1, 2) or data[5] not in (1, 2):
        raise ValueError('Invalid ELF binary.')
    order = '<' if data[5] == 1 else '>'
    try:
        if data[4] == 2:
            header = struct.unpack_from(order+'HHIQQQIHHHHHH', data, 16)
            offset, size, count = header[4], header[8], header[9]
            fmt = order+'IIQQQQQQ'
        else:
            header = struct.unpack_from(order+'HHIIIIIHHHHHH', data, 16)
            offset, size, count = header[4], header[8], header[9]
            fmt = order+'IIIIIIII'
        if size < struct.calcsize(fmt) or not count or offset+size*count > len(data):
            raise ValueError('Malformed ELF program headers.')
        segments = []
        for index in range(count):
            entry = struct.unpack_from(fmt, data, offset+size*index)
            if data[4] == 2:
                kind, flags, start, _, _, length, _, _ = entry
            else:
                kind, start, _, _, length, _, flags, _ = entry
            if start+length > len(data):
                raise ValueError('Truncated ELF segment.')
            if kind == 1 and flags & 2:
                segments.append((start, start+length))
        return segments
    except struct.error as error:
        raise ValueError('Truncated ELF headers.') from error


def _go_string(data, offset, end):
    length = 0
    for index in range(10):
        if offset >= end:
            raise ValueError('Truncated Go buildinfo length.')
        byte = data[offset]; offset += 1
        if index == 9 and byte > 1:
            raise ValueError('Overflowing Go buildinfo length.')
        length |= (byte & 127) << (index*7)
        if byte < 128:
            if length > end-offset:
                raise ValueError('Truncated Go buildinfo string.')
            return data[offset:offset+length], offset+length
    raise ValueError('Invalid Go buildinfo length.')


def parse_go_buildinfo(data):
    magic = b'\xff Go buildinf:'
    location = None
    for start, end in _elf_segments(data):
        at = data.find(magic, start, end)
        while at != -1:
            if at % 16 == 0:
                if location is not None:
                    raise ValueError('Ambiguous Go buildinfo.')
                location = at, end
            at = data.find(magic, at+1, end)
    if location is None:
        raise ValueError('Missing Go buildinfo in ELF writable segments.')
    at, end = location
    if at+32 > end or data[at+15] not in (2, 3):
        raise ValueError('Unsupported or truncated Go buildinfo; modern inline format required.')
    version, next_at = _go_string(data, at+32, end)
    framed, _ = _go_string(data, next_at, end)
    try:
        version = version.decode('utf-8')
        if not re.fullmatch(r'go1\.\d+(?:\.\d+)?(?:[a-z0-9.-]+)?', version):
            raise ValueError('Missing release Go toolchain version.')
        if len(framed) < 33 or framed[-17] != 10:
            raise ValueError('Missing framed Go module information.')
        text = framed[16:-16].decode('utf-8')
    except UnicodeDecodeError as error:
        raise ValueError('Invalid Go buildinfo text.') from error
    result = {'go_version': version, 'path': None, 'modules': [], 'settings': {}}
    previous = None
    for line in text.splitlines():
        fields = line.split('\t'); kind = fields[0]
        if kind == 'path' and len(fields) == 2 and fields[1] and result['path'] is None:
            result['path'] = fields[1]; previous = None
        elif kind in ('mod', 'dep', '=>') and 3 <= len(fields) <= 4 and fields[1] and fields[2]:
            module = {'path': fields[1], 'version': fields[2], 'sum': fields[3] if len(fields) == 4 else ''}
            if kind == '=>':
                if previous is None or 'replace' in previous:
                    raise ValueError('Orphan/duplicate Go module replacement.')
                previous['replace'] = module; previous = None
            else:
                module['main'] = kind == 'mod'; result['modules'].append(module); previous = module
        elif kind == 'build' and len(fields) == 2 and '=' in fields[1]:
            key, value = fields[1].split('=', 1)
            if not key or key in result['settings']:
                raise ValueError('Duplicate or invalid Go build setting.')
            result['settings'][key] = value; previous = None
        else:
            raise ValueError('Malformed Go module/build setting identity.')
    if not result['path'] or not result['modules']:
        raise ValueError('Missing Go main path/module identities.')
    seen = set()
    for module in result['modules']:
        if module['path'] in seen:
            raise ValueError('Duplicate Go module identity.')
        seen.add(module['path'])
    return result


class ExportFiles:
    """Resolve archive links inside a virtual root without extracting any payload."""
    def __init__(self, archive):
        self.archive = archive; self.members = {}
        for member in archive:
            name = member.name.removeprefix('./').rstrip('/')
            if not name:
                continue
            if name.startswith('/') or '..' in PurePosixPath(name).parts or '\\' in name or name in self.members:
                raise ValueError('Unsafe or duplicate exported path.')
            self.members[name] = member

    def read(self, name, limit=None):
        name = name.lstrip('/')
        for _ in range(40):
            parts = name.split('/'); changed = False
            for index in range(len(parts)):
                prefix = '/'.join(parts[:index+1]); member = self.members.get(prefix)
                if member and (member.issym() or member.islnk()):
                    target = member.linkname
                    base = '' if target.startswith('/') or member.islnk() else posixpath.dirname(prefix)
                    resolved = posixpath.normpath(posixpath.join(base, target.lstrip('/'), *parts[index+1:]))
                    if resolved == '..' or resolved.startswith('../') or '\\' in resolved:
                        raise ValueError('Exported link escapes root.')
                    name = resolved; changed = True; break
            if changed:
                continue
            member = self.members.get(name)
            if member is None or not member.isfile():
                raise ValueError('Missing regular exported file: '+name)
            if limit is not None and member.size > limit:
                raise ValueError('Oversized exported metadata file: '+name)
            stream = self.archive.extractfile(member)
            if stream is None:
                raise ValueError('Unreadable exported file.')
            with stream:
                data = stream.read()
            if len(data) != member.size:
                raise ValueError('Truncated exported file.')
            return data
        raise ValueError('Exported link loop.')


def inventory_export(path, role, metadata):
    if role not in ROLES or not re.fullmatch(r'sha256:[0-9a-f]{64}', metadata.get('Id', '')):
        raise ValueError('Invalid reference role/image identity.')
    if metadata.get('Os') != 'linux' or metadata.get('Architecture') != 'amd64':
        raise ValueError('Reference inventory currently qualifies linux/amd64 only.')
    notices = {}; binaries = []; builds = {}
    with tarfile.open(path, 'r:*') as archive:
        files = ExportFiles(archive)
        databases = [(path, parser, kind) for path, parser, kind in
                     [('/var/lib/dpkg/status', parse_dpkg, 'dpkg'),
                      ('/lib/apk/db/installed', parse_apk, 'apk')]
                     if path.lstrip('/') in files.members]
        if len(databases) != 1:
            raise ValueError('Exactly one actual package database is required.')
        database, parser, database_format = databases[0]
        raw = files.read(database, 32*1024*1024)
        components = parser(raw)
        notices['original'+database] = raw
        for name, member in sorted(files.members.items()):
            lower = PurePosixPath(name).name.lower()
            is_notice = (name.startswith(('usr/share/doc/', 'usr/share/licenses/')) and
                         (lower.startswith(('copyright', 'license', 'licence', 'notice', 'copying')) or name.startswith('usr/share/licenses/')))
            is_notice = is_notice or name in ('usr/local/LICENSE', 'LICENSE', 'usr/share/caddy/LICENSE')
            if is_notice and (member.isfile() or member.issym() or member.islnk()):
                notices['original/'+name] = files.read(name, 8*1024*1024)
            if not member.isfile() or member.size < 4:
                continue
            stream = archive.extractfile(member)
            with stream:
                head = stream.read(4)
                if head != b'\x7fELF':
                    continue
                digest = hashlib.sha256(head)
                while chunk := stream.read(1024*1024):
                    digest.update(chunk)
            binary = {'path': '/'+name, 'sha256': digest.hexdigest(), 'bytes': member.size, 'mode': oct(member.mode)}
            # Every embedded Go ELF is accounted for, including unexpected helpers.
            payload = files.read(name)
            if b'\xff Go buildinf:' in payload:
                binary['go_buildinfo'] = parse_go_buildinfo(payload); builds['/'+name] = binary['go_buildinfo']
            binaries.append(binary)
        for required in (REQUIRED_GO[role],):
            payload = files.read(required)
            builds[required] = parse_go_buildinfo(payload)
        scripts = []
        for name in ('usr/local/bin/docker-entrypoint.sh', 'etc/caddy/Caddyfile'):
            if name in files.members:
                payload = files.read(name, 4*1024*1024)
                scripts.append({'path': '/'+name, 'sha256': hashlib.sha256(payload).hexdigest(), 'bytes': len(payload)})
    indexed = {(r['scope'], r['name'], r['version']): r for r in components}
    for path, info in builds.items():
        version = info['go_version'].removeprefix('go')
        indexed.setdefault(('go', 'stdlib', version), {'scope': 'go', 'name': 'stdlib', 'version': version})
        for original in info['modules']:
            module = original.get('replace', original)
            row = {'scope': 'go', 'name': module['path'], 'version': module['version']}
            indexed.setdefault(('go', row['name'], row['version']), row)
    record = {'image_id': metadata['Id'], 'platform': 'linux/amd64',
              'labels': metadata.get('Config', {}).get('Labels') or {},
              'upstream_runtime': {k: metadata.get('Config', {}).get(k) for k in ('Entrypoint', 'Cmd', 'User', 'WorkingDir')},
              'components': sorted(indexed.values(), key=lambda r: (r['scope'], r['name'], r['version'])),
              'package_database': {'path': database, 'format': database_format, 'sha256': hashlib.sha256(raw).hexdigest()},
              'binaries': binaries, 'go_buildinfo': builds, 'configuration_files': scripts,
              'notice_files': [{'path': name, 'sha256': hashlib.sha256(data).hexdigest(), 'bytes': len(data)} for name, data in sorted(notices.items())],
              'coverage_gaps': ['Original installed notices are retained where present; complete per-component license/source coverage requires matching independently reviewed reference-coverage.json.', 'Go toolchain, main module and transitive module original notices/sources require supplemental review; embedded module identities do not constitute license text or source delivery.'],
              'scope_limits': ['Merged exported filesystem; no execution or independent binary rebuild.', 'OS source identity is package-manager metadata; Go identity is embedded buildinfo. Neither is source-content verification.']}
    return record, notices


def write_notices(path, role, record, notices):
    if role not in ROLES:
        raise ValueError('Unknown reference role.')
    payloads = dict(notices)
    payloads['inventory.json'] = (json.dumps(record, indent=2, sort_keys=True)+'\n').encode()
    with path.open('xb') as target, gzip.GzipFile(filename='', fileobj=target, mode='wb', mtime=0) as zipped:
        with tarfile.open(fileobj=zipped, mode='w') as archive:
            for name, data in sorted(payloads.items()):
                if name.startswith('/') or '..' in PurePosixPath(name).parts or '\\' in name:
                    raise ValueError('Unsafe notice path.')
                info = tarfile.TarInfo(role+'-notices/'+name); info.size = len(data); info.mode = 0o644; info.mtime = 0
                archive.addfile(info, io.BytesIO(data))


def collect(output: Path, images: dict[str, str]) -> dict:
    if set(images) != set(ROLES):
        raise ValueError('Reference inventory requires exactly postgres and caddy.')
    output.mkdir(parents=True, exist_ok=True)
    if any((output/(role+'-notices.tar.gz')).exists() for role in ROLES):
        raise ValueError('Existing reference notice archives cannot be overwritten.')
    records = {}
    with tempfile.TemporaryDirectory(prefix='.reference-inventory-', dir=output) as temp:
        work = Path(temp)
        for role in ROLES:
            metadata = json.loads(subprocess.check_output(['docker', 'image', 'inspect', images[role]], text=True))[0]
            container = subprocess.check_output(['docker', 'create', '--network', 'none', '--read-only', metadata['Id']], text=True).strip()
            try:
                archive = work/(role+'.tar')
                with archive.open('wb') as stream:
                    subprocess.run(['docker', 'export', container], stdout=stream, check=True)
                record, notices = inventory_export(archive, role, metadata)
                write_notices(work/(role+'-notices.tar.gz'), role, record, notices)
                records[role] = record
            finally:
                subprocess.run(['docker', 'rm', '-v', container], check=True, stdout=subprocess.DEVNULL)
        for role in ROLES:
            (work/(role+'-notices.tar.gz')).replace(output/(role+'-notices.tar.gz'))
    return {'format': 1, 'images': records}
