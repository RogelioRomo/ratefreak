"""Require exact source and original-notice decisions for bundled reference images."""
import hashlib
from pathlib import Path
import re
import shutil
import sys

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'self-host'))
from release_bundle import read_manifest, regular_file

ROLES = {'postgres', 'caddy'}


def component_keys(values):
    if not isinstance(values,list) or not values:
        raise ValueError('Missing reference component inventory.')
    result=[]
    for row in values:
        if (not isinstance(row,dict) or row.get('scope') not in {'os','go','runtime'}
                or any(not isinstance(row.get(key),str) or not row[key].strip() for key in ('name','version'))):
            raise ValueError('Invalid reference component identity.')
        result.append((row['scope'],row['name'],row['version']))
    if len(result)!=len(set(result)):
        raise ValueError('Duplicate reference component identity.')
    return set(result)


def validate_inventory(inventory,images=None):
    if (not isinstance(inventory,dict) or inventory.get('format')!=1
            or not isinstance(inventory.get('images'),dict) or set(inventory['images'])!=ROLES):
        raise ValueError('Complete PostgreSQL/Caddy reference inventory is required.')
    if images is not None and set(images)!=ROLES:
        raise ValueError('Incomplete selected reference image set.')
    for role,entry in inventory['images'].items():
        if (not isinstance(entry,dict) or not re.fullmatch(r'sha256:[a-f0-9]{64}',str(entry.get('image_id','')))
                or images is not None and entry['image_id']!=images[role]):
            raise ValueError('Reference inventory differs from selected images.')
        component_keys(entry.get('components'))
    return inventory


def validate_coverage(coverage, expected):
    validate_inventory(expected)
    if (not isinstance(expected, dict) or expected.get('format') != 1
            or not isinstance(expected.get('images'), dict) or set(expected['images']) != ROLES
            or not isinstance(coverage, dict) or coverage.get('format') != 1
            or not isinstance(coverage.get('images'), dict) or set(coverage['images']) != ROLES):
        raise ValueError('Complete PostgreSQL/Caddy reference source inventory is required.')
    for role, inventory in expected['images'].items():
        entry = coverage['images'][role]
        if (not isinstance(inventory, dict) or not isinstance(entry, dict)
                or not re.fullmatch(r'sha256:[a-f0-9]{64}', str(inventory.get('image_id', '')))
                or entry.get('image_id') != inventory['image_id']):
            raise ValueError('Reference source coverage belongs to another image.')
        files = entry.get('files')
        record = entry.get('review_record')
        if not isinstance(files, dict) or not isinstance(record, str) or record not in files:
            raise ValueError('Missing reference source/license review record.')
        for name, digest in files.items():
            if (not isinstance(name, str) or not re.fullmatch(r'[A-Za-z0-9][A-Za-z0-9._-]{0,180}', name)
                    or name in {'coverage.json', 'reference-coverage.json'}
                    or not isinstance(digest, str) or not re.fullmatch(r'[a-f0-9]{64}', digest)):
                raise ValueError('Unsafe reference source artifact name or checksum.')
        required_rows = inventory.get('components')
        rows = entry.get('components')
        if not isinstance(required_rows, list) or not required_rows or not isinstance(rows, list):
            raise ValueError('Missing reference component source decisions.')
        if component_keys(rows) != component_keys(required_rows):
            raise ValueError('Every reference component/version needs an exact reviewed decision.')
        for row in rows:
            def artifacts(key):
                values = row.get(key)
                return (isinstance(values, list) and bool(values)
                        and all(isinstance(name, str) and name in files and name != record for name in values)
                        and len(values) == len(set(values)))
            if not artifacts('notice_files'):
                raise ValueError('Reference component lacks original notice material.')
            if artifacts('source_files'):
                continue
            exception = row.get('exception')
            if (not isinstance(exception, dict) or not isinstance(exception.get('reason'), str)
                    or len(exception['reason'].strip()) < 20 or not isinstance(exception.get('record'), str)
                    or exception['record'] not in files):
                raise ValueError('Reference component lacks corresponding source or a reviewed exception.')
    return coverage


def copy_evidence(directory, expected, output):
    directory, output = Path(directory), Path(output)
    if directory.is_symlink() or output.is_symlink():
        raise ValueError('Linked reference source directories are not allowed.')
    coverage = read_manifest(directory / 'reference-coverage.json')
    validate_coverage(coverage, expected)
    materials = {}
    for entry in coverage['images'].values():
        for name, digest in entry['files'].items():
            if name in materials and materials[name] != digest:
                raise ValueError('Conflicting reference source material.')
            source = regular_file(directory, name)
            with source.open('rb') as stream:
                if hashlib.file_digest(stream, 'sha256').hexdigest() != digest:
                    raise ValueError('Reference source material checksum mismatch.')
            if (output / name).exists() or (output / name).is_symlink():
                existing = regular_file(output, name)
                with existing.open('rb') as stream:
                    if hashlib.file_digest(stream, 'sha256').hexdigest() != digest:
                        raise ValueError('Reference source conflicts with application source material.')
            materials[name] = digest
    output.mkdir(parents=True, exist_ok=True)
    for name in sorted(materials):
        if not (output / name).exists():
            shutil.copyfile(directory / name, output / name)
    shutil.copyfile(directory / 'reference-coverage.json', output / 'reference-coverage.json')
    return coverage
