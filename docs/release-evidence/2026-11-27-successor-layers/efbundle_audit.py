"""Read .NET bundle v6 entries without running the migration executable."""
from pathlib import Path
import hashlib
import io
import json
import re
import struct
import zlib
import sys

BASE = Path(__file__).resolve().parent
bundle_name = sys.argv[1] if len(sys.argv)>1 else 'efbundle'
data = (BASE / bundle_name).read_bytes()
signature = bytes.fromhex('8b1202b96a612038727b930214d7a03213f5b9e6efae3318ee3b2dce24b36aae')
position = data.find(signature)
assert position > 8
stream = io.BytesIO(data)
stream.seek(struct.unpack_from('<q', data, position - 8)[0])


def number(fmt):
    return struct.unpack(fmt, stream.read(struct.calcsize(fmt)))


def string():
    size = shift = 0
    while True:
        byte = stream.read(1)[0]
        size |= (byte & 127) << shift
        shift += 7
        assert shift <= 35
        if byte < 128:
            break
    return stream.read(size).decode()


major, minor, count = number('<IIi')
assert (major, minor) == (6, 0) and 0 < count < 10000
bundle_id = string()
locations = number('<qqqqQ')
rows = []
deps = None
for index in range(count):
    offset, size, compressed = number('<qqq')
    kind, = number('<B')
    name = string()
    assert offset >= 0 and size >= 0 and offset + (compressed or size) <= len(data)
    blob = data[offset:offset + (compressed or size)]
    if compressed:
        blob = zlib.decompress(blob, -15)
    assert len(blob) == size
    row = {'file': name, 'type': kind, 'offset': offset, 'bytes': size,
           'compressed_bytes': compressed, 'sha256': hashlib.sha256(blob).hexdigest()}
    if kind == 2:
        row['runtime_markers'] = [m.group().decode() for m in re.finditer(rb'@\(#\)Version [0-9.]+ @Commit: [a-f0-9]{40}', blob)]
    rows.append(row)
    if name.endswith('.deps.json'):
        deps = json.loads(blob)
assert deps
inventory = json.loads((BASE.parent / 'successor-candidate/source-inventory.json').read_text())
packages = {c['name'] + '/' + c['version'] for c in inventory['images']['tools']['components'] if c['scope'] == 'nuget'}
unmapped = [key for key, value in deps['libraries'].items() if value['type'] == 'package' and key not in packages]
assert not unmapped
native = next(row for row in rows if row['file'] == 'libSkiaSharp.so')
layer_files = json.loads((BASE / 'tools-layer-files.json').read_text())
assert any(row['path'] == 'app/runtimes/linux-x64/native/libSkiaSharp.so' and row['sha256'] == native['sha256'] for row in layer_files)
apphost = next(row for row in rows if row['file'] == 'RateFreak.Api')
assert apphost['runtime_markers'] == ['@(#)Version 10.0.1526.53008 @Commit: 8e0f8ed2312954727f8919d7d1c2f78b483b4ee7']
report = {'format': 1, 'file': 'efbundle', 'sha256': hashlib.sha256(data).hexdigest(),
          'image_id': inventory['images']['tools']['image_id'], 'bundle_version': [major, minor],
          'bundle_id': bundle_id, 'files': rows, 'dependency_manifest': deps,
          'unmapped_package_versions': unmapped, 'embedded_skia_matches_reviewed_payload': True,
          'embedded_apphost_marker': apphost['runtime_markers'],
          'format_sources': ['https://raw.githubusercontent.com/dotnet/runtime/main/src/installer/managed/Microsoft.NET.HostModel/Bundle/Manifest.cs',
                             'https://raw.githubusercontent.com/dotnet/runtime/main/src/installer/managed/Microsoft.NET.HostModel/Bundle/FileEntry.cs']}
(BASE / (bundle_name + '-inventory.json')).write_text(json.dumps(report, indent=2, sort_keys=True) + '\n')
print(json.dumps({'files': len(rows), 'libraries': len(deps['libraries']), 'unmapped_package_versions': unmapped,
                  'embedded_skia_matches_reviewed_payload': True, 'embedded_apphost_marker': apphost['runtime_markers']}))
