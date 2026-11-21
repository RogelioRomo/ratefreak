#!/usr/bin/env python3
"""Prepare exact release candidates; never publish, push or modify an instance."""
import argparse
import gzip
import hashlib
import io
import json
from pathlib import Path, PurePosixPath
import re
import shutil
import subprocess
import sys
import secrets
import tarfile
from versioning import read_identity, git
sys.path.insert(0,str(Path(__file__).resolve().parents[1]/'self-host'))
from release_bundle import validate_bundle, load_bundle, regular_file, read_manifest

SERVICES=('api','web','tools')
SOURCE_SCOPES={'api':{'os','dotnet','nuget'},'web':{'os','node','npm','native'},'tools':{'os','dotnet','nuget','postgres'}}

def new_output(path):path.mkdir(parents=True,exist_ok=False)

def safe_source(name):
    path=PurePosixPath(name)
    if path.is_absolute() or '..' in path.parts or '\\' in name:return False
    private={'.git','.local','.aws','.codex','.agents','node_modules','obj','bin','TestResults','secrets','uploads','exports','keys'}
    if any(part in private for part in path.parts):return False
    leaf=path.name.lower()
    if ('.env' in leaf and not leaf.endswith('.example')) or leaf in {'secrets.json','appsettings.local.json','appsettings.development.json'}:return False
    return not any(leaf.endswith(suffix) for suffix in ('.dump','.bak','.pem','.key','.pfx','.log','.trx','.pid'))

def source_archive(repository,output):
    entries=subprocess.check_output(['git','-C',str(repository),'ls-tree','-rz','HEAD']).split(b'\0')
    for entry in entries:
        if not entry:continue
        metadata,name=entry.split(b'\t',1);mode=metadata.split()[0]
        if mode not in (b'100644',b'100755') or not safe_source(name.decode()):raise ValueError('Unsafe tracked source file; inspect before distribution.')
    # git archive uses committed bytes, never local or untracked data.
    with output.open('xb') as target:
        with gzip.GzipFile(filename='',mode='wb',fileobj=target,mtime=0) as zipped:
            process=subprocess.Popen(['git','-C',str(repository),'archive','--format=tar','HEAD'],stdout=subprocess.PIPE)
            try:
                shutil.copyfileobj(process.stdout,zipped)
                if process.wait()!=0:raise ValueError('Source archive failed.')
            finally:process.stdout.close()

def validate_notes(path,version):
    if path.is_symlink() or path.stat().st_size>256*1024:raise ValueError('Unsafe release notes.')
    text=path.read_text()
    if version not in text or any(not re.search(r'^## '+heading+r'\b',text,re.M|re.I) for heading in ('Changes','Upgrade','Migrations','Recovery','Known issues')) or 'TBD' in text or 'TODO' in text:
        raise ValueError('Release notes require version, changes, upgrade, migrations, recovery and known issues.')

def source_evidence(directory,images,output):
    """Validate image-bound reviewed source coverage; never infer legal clearance."""
    coverage=read_manifest(directory/'coverage.json')
    if coverage.get('format')!=1 or set(coverage.get('images',{}))!=set(SERVICES):raise ValueError('Incomplete image source coverage.')
    copied={}
    for name,image_id in images.items():
        entry=coverage['images'][name]
        if entry.get('image_id')!=image_id or set(entry.get('scopes',[]))!=SOURCE_SCOPES[name] or not entry.get('review_record') or not entry.get('files') or entry['review_record'] not in entry['files']:
            raise ValueError('Missing image-bound source-delivery review.')
        for filename,expected in entry['files'].items():
            source=regular_file(directory,filename)
            with source.open('rb') as stream:digest=hashlib.file_digest(stream,'sha256').hexdigest()
            if not re.fullmatch('[a-f0-9]{64}',str(expected)) or digest!=expected:raise ValueError('Source material checksum mismatch.')
            if filename=='coverage.json':raise ValueError('Reserved source artifact name.')
            if filename in copied and copied[filename]!=digest:raise ValueError('Conflicting source material.')
            copied[filename]=digest
    output.mkdir(exist_ok=False)
    for filename in sorted(copied):shutil.copyfile(directory/filename,output/filename)
    shutil.copyfile(directory/'coverage.json',output/'coverage.json')
    # Native sidecars, when present, must also validate their internal inventory.
    from_path=Path(__file__).resolve().parents[1]/'compliance'
    sys.path.insert(0,str(from_path))
    from source_bundle import verify
    native=coverage['images']['web'].get('native_sidecar')
    if native not in coverage['images']['web']['files']:
        raise ValueError('Missing image-bound web native source sidecar.')
    manifest=verify(output/native)
    if manifest.get('image_id')!=images['web']:
        raise ValueError('Native source sidecar belongs to another image.')
    return coverage

def run(command,cwd=None,output=None):
    if output is None:return subprocess.check_output(command,cwd=cwd,text=True).strip()
    with output.open('wb') as stream:subprocess.run(command,cwd=cwd,stdout=stream,check=True)

def file_entry(path):
    with path.open('rb') as stream:digest=hashlib.file_digest(stream,'sha256').hexdigest()
    return {'sha256':digest,'bytes':path.stat().st_size}

def save_image(image,path):
    with path.open('xb') as target:
        with gzip.GzipFile(filename='',fileobj=target,mode='wb',mtime=0) as zipped:
            process=subprocess.Popen(['docker','image','save',image],stdout=subprocess.PIPE)
            try:
                shutil.copyfileobj(process.stdout,zipped)
                if process.wait()!=0:raise ValueError('Image archive failed.')
            finally:process.stdout.close()


def freeze_directory(output,name):
    directory=output/name
    with tarfile.open(output/(name+'.tar.gz'),'w:gz') as archive:archive.add(directory,arcname=name)
    shutil.rmtree(directory)

def inventory(output):
    return {path.name:file_entry(path) for path in sorted(output.iterdir()) if path.is_file() and path.name not in {'candidate-manifest.json','release-manifest.json'}}

def resume_candidate(output,identity,compliance):
    candidate=read_manifest(output/'candidate-manifest.json')
    if candidate.get('version')!=identity['version'] or candidate.get('revision')!=identity['revision']:
        raise ValueError('Candidate differs from the selected tagged source.')
    candidate=validate_bundle(output,'candidate-manifest.json')
    if (output/'release').exists():raise FileExistsError('Final release artifacts already exist; never replace accepted bytes.')
    # Load the exact reviewed archives, never rebuild while resolving source evidence.
    def inspect(tag):return json.loads(run(['docker','image','inspect',tag]))[0]
    load_bundle(output,inspect,lambda path:run(['docker','image','load','--input',str(path)]),'candidate-manifest.json')
    staging=output/('.finalization-'+secrets.token_hex(8));staging.mkdir()
    for name in candidate['files']:shutil.copyfile(output/name,staging/name)
    images={name:entry['id'] for name,entry in candidate['images'].items()}
    source_evidence(compliance,images,staging/'sources');freeze_directory(staging,'sources')
    images_file=staging/'built-images.json'
    root=Path(__file__).resolve().parents[2]
    run([sys.executable,str(root/'deploy/release/acceptance.py'),'--confirm-disposable','--report-directory',str(staging/'acceptance'),'--images-json',str(images_file)],root)
    freeze_directory(staging,'acceptance')
    final={**candidate,'stage':'verified','files':inventory(staging)}
    (staging/'release-manifest.json').write_text(json.dumps(final,sort_keys=True,indent=2)+'\n')
    validate_bundle(staging)
    staging.rename(output/'release')
    return final

def prepare(repository,tag,output,compliance=None,resume=False):
    identity=read_identity(repository,tag)
    if resume:
        if compliance is None:raise ValueError('Finalization requires reviewed image-bound source materials.')
        return resume_candidate(output,identity,compliance)
    new_output(output)
    notes=repository/'docs/releases'/f'{identity["version"]}.md';validate_notes(notes,identity['version'])
    reports=output/'reports'
    run([sys.executable,str(repository/'deploy/release/verify.py'),'--report-directory',str(reports),'--tag',tag],repository)
    (repository/'.build-identity.json').write_text(json.dumps(identity)+'\n')
    images={};manifest={'format':1,**identity,'stage':'review','platform':'linux/amd64','images':{},'files':{}}
    migrations=sorted((repository/'src/RateFreak.Infrastructure/Persistence/Migrations').glob('[0-9]*.cs'))
    manifest['migration']=next(p.stem for p in reversed(migrations) if not p.name.endswith('.Designer.cs'))
    for name in SERVICES:
        image='ratefreak-'+name+':'+identity['version'].replace('+','_')
        command=['docker','build','--platform','linux/amd64','--build-arg','RATEFREAK_VERSION='+identity['version'],'--build-arg','RATEFREAK_REVISION='+identity['revision'],'--build-arg','RATEFREAK_OFFICIAL=true','-f',str(repository/'deploy/self-host'/f'{name}.Dockerfile'),'-t',image,str(repository)]
        run(command,repository);metadata=json.loads(run(['docker','image','inspect',image]))[0]
        images[name]=metadata['Id'];manifest['images'][name]={'tag':image,'id':metadata['Id'],'file':name+'.tar.gz'}
        save_image(image,output/(name+'.tar.gz'))
        container=run(['docker','create',image])
        try:
            location='/app/public/notices' if name=='web' else '/app/notices'
            run(['docker','cp',container+':'+location,str(output/(name+'-notices'))])
        finally:run(['docker','rm',container])
        freeze_directory(output,name+'-notices')
    (output/'built-images.json').write_text(json.dumps(images,indent=2)+'\n')
    source_archive(repository,output/'installation.tar.gz');shutil.copyfile(notes,output/'RELEASE-NOTES.md')
    freeze_directory(output,'reports')
    manifest['files']=inventory(output)
    (output/'candidate-manifest.json').write_text(json.dumps(manifest,sort_keys=True,indent=2)+'\n')
    validate_bundle(output,'candidate-manifest.json')
    if compliance is None or not (compliance/'coverage.json').exists():
        raise ValueError('Incomplete candidate: review the retained exact image archives and provide image-bound source materials, then rerun with --resume. No installable release manifest was produced.')
    return resume_candidate(output,identity,compliance)

if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--tag',required=True);parser.add_argument('--output',required=True,type=Path);parser.add_argument('--compliance',type=Path);parser.add_argument('--resume',action='store_true');args=parser.parse_args()
    try:prepare(Path(__file__).resolve().parents[2],args.tag,args.output.resolve(),args.compliance,args.resume)
    except (ValueError,OSError,subprocess.CalledProcessError) as error:parser.exit(1,str(error)+'\n')
