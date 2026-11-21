"""Bind reviewed source decisions to the actual shipped component inventory."""
import csv
import io
import json
from pathlib import Path
import re
import subprocess
import tarfile


def archived_json(path,name,limit=4*1024*1024):
    with tarfile.open(path) as archive:
        member=archive.getmember(name)
        if not member.isfile() or member.size>limit:raise ValueError('Invalid archived evidence.')
        return json.load(archive.extractfile(member))


def collect(output,images,profile):
    result={'format':1,'images':{}}
    for name,image_id in images.items():
        archive=output/(name+'-notices.tar.gz');prefix=name+'-notices/'
        components=[]
        with tarfile.open(archive) as tar:
            data=tar.extractfile(prefix+'runtime/os-packages.tsv').read(4*1024*1024).decode()
        for row in csv.DictReader(io.StringIO(data),delimiter='\t'):
            components.append({'scope':'os','name':row['package'],'version':row['version']})
        packages=archived_json(archive,prefix+'manifest.json')['packages']
        for package in packages:components.append({'scope':'npm' if name=='web' else 'nuget','name':package['name'],'version':package['version']})
        runtime=archived_json(archive,prefix+'runtime/evidence.json')
        if name=='web':
            version=subprocess.check_output(['docker','run','--rm','--network','none','--entrypoint','node',image_id,'--version'],text=True).strip()
            components.append({'scope':'node','name':'node','version':version})
            if not any(p['name']==profile['package']['name'] and p['version']==profile['package']['version'] for p in packages) or not any(p['components']==profile['components'] for p in runtime['native_components']) or not any(p['sha256']==profile['native_sha256'] for p in runtime['native_binaries']):
                raise ValueError('Locked native source profile does not describe the shipped web image.')
            for component,version in profile['components'].items():components.append({'scope':'native','name':component,'version':version})
        else:
            runtimes=subprocess.check_output(['docker','run','--rm','--network','none','--entrypoint','dotnet',image_id,'--list-runtimes'],text=True)
            for line in runtimes.splitlines():
                values=line.split();components.append({'scope':'dotnet','name':values[0],'version':values[1]})
            if name=='tools':
                version=subprocess.check_output(['docker','run','--rm','--network','none','--entrypoint','psql',image_id,'--version'],text=True).strip()
                components.append({'scope':'postgres','name':'postgresql-client','version':version})
        entry={'image_id':image_id,'components':sorted(components,key=lambda p:(p['scope'],p['name'],p['version']))}
        if name=='web':entry['native_profile']={key:profile[key] for key in ['package','components','native_sha256']}
        result['images'][name]=entry
    return result


def validate_coverage(coverage,expected):
    if set(coverage.get('images',{}))!=set(expected.get('images',{})):raise ValueError('Source coverage image inventory differs.')
    for name,inventory in expected['images'].items():
        entry=coverage['images'][name]
        if entry.get('image_id')!=inventory['image_id']:raise ValueError('Source coverage belongs to another image.')
        required={(p['scope'],p['name'],p['version']) for p in inventory['components']}
        rows=entry.get('components',[])
        mapped={(p.get('scope'),p.get('name'),p.get('version')) for p in rows}
        if not required or required!=mapped or len(rows)!=len(mapped):raise ValueError('Every shipped component/version needs a reviewed source decision.')
        files=entry.get('files',{})
        if entry.get('review_record') not in files:raise ValueError('Missing source review record.')
        for row in rows:
            material=row.get('source_files',[]);exception=row.get('exception')
            if material and all(isinstance(file,str) and file in files and file!=entry['review_record'] for file in material):continue
            if isinstance(exception,dict) and isinstance(exception.get('reason'),str) and len(exception['reason'].strip())>=20 and exception.get('record') in files:continue
            raise ValueError('Component lacks corresponding material or a documented reviewed exception.')


def validate_native(manifest,expected):
    web=expected['images']['web'];profile=web['native_profile']
    if manifest.get('image_id')!=web['image_id'] or profile['package'].get('name')!='@img/sharp-libvips-linux-x64' or any(manifest.get(key)!=profile[key] for key in ['package','components','native_sha256']):
        raise ValueError('Native source archive does not match the actual shipped binary/profile.')
