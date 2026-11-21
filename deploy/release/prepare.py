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
import os
import tempfile
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

def export_context(repository,revision,context):
    if not re.fullmatch('[a-f0-9]{40}',revision):raise ValueError('Build context requires one full immutable source revision.')
    context.mkdir(parents=True,exist_ok=False)
    with tempfile.TemporaryFile() as raw:
        subprocess.run(['git','-C',str(repository),'archive','--format=tar',revision],stdout=raw,check=True)
        raw.seek(0)
        with tarfile.open(fileobj=raw) as archive:
            for member in archive.getmembers():
                if not(member.isfile() or member.isdir()) or not safe_source(member.name.rstrip('/')):
                    raise ValueError('Unsafe tracked build input.')
            archive.extractall(context,filter='data')


def source_archive(repository,output):
    entries=subprocess.check_output(['git','-C',str(repository),'ls-tree','-rz','HEAD']).split(b'\0')
    for entry in entries:
        if not entry:continue
        metadata,name=entry.split(b'\t',1);mode=metadata.split()[0]
        if mode not in (b'100644',b'100755') or not safe_source(name.decode()):raise ValueError('Unsafe tracked source file; inspect before distribution.')
    # Repack committed bytes and one explicit generated provenance file; no local data.
    revision=git(repository,'rev-parse','HEAD')
    with tempfile.TemporaryFile() as raw:
        subprocess.run(['git','-C',str(repository),'archive','--format=tar','HEAD'],stdout=raw,check=True);raw.seek(0)
        with output.open('xb') as target:
            with gzip.GzipFile(filename='',mode='wb',fileobj=target,mtime=0) as zipped:
                with tarfile.open(fileobj=zipped,mode='w|') as archive,tarfile.open(fileobj=raw) as original:
                    for member in original:
                        if member.name=='SOURCE_REVISION':raise ValueError('Generated source revision must not be tracked.')
                        archive.addfile(member,original.extractfile(member) if member.isfile() else None)
                    data=(revision+'\n').encode();member=tarfile.TarInfo('SOURCE_REVISION');member.size=len(data);member.mode=0o644
                    archive.addfile(member,io.BytesIO(data))


def validate_notes(path,version):
    if path.is_symlink() or path.stat().st_size>256*1024:raise ValueError('Unsafe release notes.')
    text=path.read_text()
    if version not in text or any(not re.search(r'^## '+heading+r'\b',text,re.M|re.I) for heading in ('Changes','Upgrade','Migrations','Recovery','Known issues')) or 'TBD' in text or 'TODO' in text:
        raise ValueError('Release notes require version, changes, upgrade, migrations, recovery and known issues.')

def source_evidence(directory,images,output,inventory=None):
    """Validate image-bound reviewed source coverage; never infer legal clearance."""
    from source_inventory import validate_coverage,validate_native
    if inventory is None:raise ValueError('Actual shipped component inventory is required.')
    coverage=read_manifest(directory/'coverage.json')
    validate_coverage(coverage,inventory)
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
    validate_native(manifest,inventory)
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

def baseline_images(repository,identity,bundle=None):
    if bundle is not None:
        manifest=validate_bundle(bundle)
        def inspect(tag):
            result=subprocess.run(['docker','image','inspect',tag],text=True,capture_output=True)
            return json.loads(result.stdout)[0] if result.returncode==0 else None
        load_bundle(bundle,inspect,lambda path:run(['docker','image','load','--input',str(path)]))
        return {name:entry['id'] for name,entry in manifest['images'].items()}
    if identity['version'].split('-')[0].split('+')[0]!='0.1.0':
        raise ValueError('Successor releases require the preceding published stable --baseline-bundle.')
    revision=git(repository,'rev-parse','9cc808aa86d6510802d9189d34df5e9bffe2d0b8^{commit}');images={}
    with tempfile.TemporaryDirectory(prefix='ratefreak-upgrade-baseline-') as temporary:
        context=Path(temporary)/'source';export_context(repository,revision,context)
        for name in SERVICES:
            tag='ratefreak-'+name+':baseline-'+revision[:12]
            run(['docker','build','--platform','linux/amd64','-f',str(context/'deploy/self-host'/f'{name}.Dockerfile'),'-t',tag,str(context)],context)
            images[name]=json.loads(run(['docker','image','inspect',tag]))[0]['Id']
    return images


def resume_candidate(output,identity,compliance,baseline_bundle=None):
    candidate=read_manifest(output/'candidate-manifest.json')
    if candidate.get('version')!=identity['version'] or candidate.get('revision')!=identity['revision']:
        raise ValueError('Candidate differs from the selected tagged source.')
    candidate=validate_bundle(output,'candidate-manifest.json')
    if (output/'release').exists():raise FileExistsError('Final release artifacts already exist; never replace accepted bytes.')
    # Load the exact reviewed archives, never rebuild while resolving source evidence.
    def inspect(tag):
        result=subprocess.run(['docker','image','inspect',tag],text=True,capture_output=True)
        return json.loads(result.stdout)[0] if result.returncode==0 else None
    load_bundle(output,inspect,lambda path:run(['docker','image','load','--input',str(path)]),'candidate-manifest.json')
    staging=output/('.finalization-'+secrets.token_hex(8));staging.mkdir()
    for name in candidate['files']:shutil.copyfile(output/name,staging/name)
    images={name:entry['id'] for name,entry in candidate['images'].items()}
    source_evidence(compliance,images,staging/'sources',read_manifest(output/'source-inventory.json'));freeze_directory(staging,'sources')
    baseline=baseline_images(Path(__file__).resolve().parents[2],identity,baseline_bundle)
    baseline_file=staging/'baseline-images.json';baseline_file.write_text(json.dumps(baseline,sort_keys=True)+'\n')
    images_file=staging/'built-images.json'
    root=Path(__file__).resolve().parents[2]
    run([sys.executable,str(root/'deploy/release/acceptance.py'),'--confirm-disposable','--report-directory',str(staging/'fresh-acceptance'),'--images-json',str(images_file)],root)
    freeze_directory(staging,'fresh-acceptance')
    run([sys.executable,str(root/'deploy/release/acceptance.py'),'--confirm-disposable','--report-directory',str(staging/'acceptance'),'--images-json',str(images_file),'--baseline-images-json',str(baseline_file)],root)
    freeze_directory(staging,'acceptance')
    final={**candidate,'stage':'verified','files':inventory(staging)}
    (staging/'release-manifest.json').write_text(json.dumps(final,sort_keys=True,indent=2)+'\n')
    validate_bundle(staging)
    staging.rename(output/'release')
    return final

def prepare(repository,tag,output,compliance=None,resume=False,baseline_bundle=None):
    identity=read_identity(repository,tag)
    if resume:
        if compliance is None:raise ValueError('Finalization requires reviewed image-bound source materials.')
        return resume_candidate(output,identity,compliance,baseline_bundle)
    new_output(output)
    notes=repository/'docs/releases'/f'{identity["version"]}.md';validate_notes(notes,identity['version'])
    reports=output/'reports'
    with tempfile.TemporaryDirectory(prefix='ratefreak-private-verification-') as temporary:
        run([sys.executable,str(repository/'deploy/release/verify.py'),'--report-directory',str(Path(temporary)/'raw'),'--public-report-directory',str(reports),'--tag',tag],repository)
    images={};manifest={'format':1,**identity,'stage':'review','platform':'linux/amd64','images':{},'files':{}}
    migrations=sorted((repository/'src/RateFreak.Infrastructure/Persistence/Migrations').glob('[0-9]*.cs'))
    manifest['migration']=next(p.stem for p in reversed(migrations) if not p.name.endswith('.Designer.cs'))
    with tempfile.TemporaryDirectory(prefix='ratefreak-tagged-build-') as temporary:
        context=Path(temporary)/'source';export_context(repository,identity['revision'],context)
        (context/'.build-identity.json').write_text(json.dumps(identity)+'\n')
        for name in SERVICES:
            image='ratefreak-'+name+':'+identity['version'].replace('+','_')
            command=['docker','build','--platform','linux/amd64','--build-arg','RATEFREAK_VERSION='+identity['version'],'--build-arg','RATEFREAK_REVISION='+identity['revision'],'--build-arg','RATEFREAK_OFFICIAL=true','-f',str(context/'deploy/self-host'/f'{name}.Dockerfile'),'-t',image,str(context)]
            run(command,context);metadata=json.loads(run(['docker','image','inspect',image]))[0]
            images[name]=metadata['Id'];manifest['images'][name]={'tag':image,'id':metadata['Id'],'file':name+'.tar.gz'}
            save_image(image,output/(name+'.tar.gz'))
            container=run(['docker','create',image])
            try:
                location='/app/public/notices' if name=='web' else '/app/notices'
                run(['docker','cp',container+':'+location,str(output/(name+'-notices'))])
            finally:run(['docker','rm',container])
            freeze_directory(output,name+'-notices')
    from source_inventory import collect
    profile=json.loads((repository/'deploy/compliance/native/source-bundle.lock.json').read_text())
    (output/'source-inventory.json').write_text(json.dumps(collect(output,images,profile),sort_keys=True,indent=2)+'\n')
    (output/'built-images.json').write_text(json.dumps(images,indent=2)+'\n')
    source_archive(repository,output/'installation.tar.gz');shutil.copyfile(notes,output/'RELEASE-NOTES.md')
    freeze_directory(output,'reports')
    manifest['files']=inventory(output)
    (output/'candidate-manifest.json').write_text(json.dumps(manifest,sort_keys=True,indent=2)+'\n')
    validate_bundle(output,'candidate-manifest.json')
    if compliance is None or not (compliance/'coverage.json').exists():
        raise ValueError('Incomplete candidate: review the retained exact image archives and provide image-bound source materials, then rerun with --resume. No installable release manifest was produced.')
    return resume_candidate(output,identity,compliance,baseline_bundle)

if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--tag',required=True);parser.add_argument('--output',required=True,type=Path);parser.add_argument('--compliance',type=Path);parser.add_argument('--resume',action='store_true');parser.add_argument('--baseline-bundle',type=Path,default=Path(os.environ['RATEFREAK_BASELINE_BUNDLE']) if os.environ.get('RATEFREAK_BASELINE_BUNDLE') else None);args=parser.parse_args()
    try:prepare(Path(__file__).resolve().parents[2],args.tag,args.output.resolve(),args.compliance,args.resume,args.baseline_bundle)
    except (ValueError,OSError,subprocess.CalledProcessError) as error:parser.exit(1,str(error)+'\n')
