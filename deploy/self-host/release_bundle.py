"""Validate a downloaded release before loading/selecting its complete image set."""
import hashlib
import json
from pathlib import Path, PurePosixPath
import re
import tarfile

VERSION=re.compile(r'(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(?:-[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?(?:\+[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?')

def unique_object(pairs):
    result={}
    for key,value in pairs:
        if key in result:raise ValueError('Duplicate manifest property.')
        result[key]=value
    return result

def read_manifest(path):
    if path.is_symlink() or not path.is_file() or path.stat().st_size>1024*1024:raise ValueError('Missing or oversized release manifest.')
    value=json.loads(path.read_text(),object_pairs_hook=unique_object)
    if not isinstance(value,dict):raise ValueError('Expected a manifest object.')
    return value

def regular_file(root,name):
    if not isinstance(name,str) or not re.fullmatch(r'[A-Za-z0-9][A-Za-z0-9._-]{0,180}',name):raise ValueError('Unsafe artifact filename.')
    file=root/name
    if file.is_symlink() or not file.is_file():raise ValueError('Missing or linked artifact.')
    return file

def archive_members(path,tag):
    with tarfile.open(path,'r:*') as archive:
        names=set()
        for member in archive:
            normalized=member.name.rstrip('/')
            name=PurePosixPath(normalized)
            if not normalized or name.is_absolute() or '..' in name.parts or '\\' in normalized or str(name)!=normalized or normalized in names or not(member.isfile() or member.isdir()):
                raise ValueError('Unsafe or duplicate archive member.')
            names.add(normalized)
        if not names or 'manifest.json' not in names:raise ValueError('Missing Docker image manifest.')
        entry=archive.getmember('manifest.json')
        if entry.size>1024*1024:raise ValueError('Oversized Docker image manifest.')
        payload=json.load(archive.extractfile(entry),object_pairs_hook=unique_object)
        if not isinstance(payload,list) or len(payload)!=1 or not isinstance(payload[0],dict) or payload[0].get('RepoTags')!=[tag]:
            raise ValueError('Image archive must contain only the selected release tag.')
        if 'index.json' in names:
            index_entry=archive.getmember('index.json')
            if index_entry.size>1024*1024:raise ValueError('Oversized OCI index.')
            index=json.load(archive.extractfile(index_entry),object_pairs_hook=unique_object)
            manifests=index.get('manifests',[]) if isinstance(index,dict) else []
            if len(manifests)!=1 or not isinstance(manifests[0],dict):raise ValueError('OCI index must select one image.')
            annotations=manifests[0].get('annotations',{})
            if annotations.get('io.containerd.image.name') not in (tag,'docker.io/library/'+tag) or annotations.get('org.opencontainers.image.ref.name')!=tag.split(':')[1]:
                raise ValueError('OCI index contains unrelated image tags.')

def bundled_images(manifest):
    """Keep application and reference schemas separate while loading one set."""
    references=manifest.get('reference_images',{})
    if 'reference_images' in manifest and (not isinstance(references,dict) or set(references)!={'postgres','caddy'}):
        raise ValueError('Incomplete release reference images.')
    if any(not isinstance(image,dict) or set(image)!={'id','tag','file'} for image in references.values()):
        raise ValueError('Invalid release reference image metadata.')
    return {**manifest['images'],**references}

def archive_identity(path,image,manifest):
    """Preflight serialized metadata before importing any five-image archive."""
    with tarfile.open(path,'r:*') as archive:
        def payload(name):
            try:member=archive.getmember(name)
            except KeyError:raise ValueError('Missing archived image identity.') from None
            if not member.isfile() or member.size>4*1024*1024:raise ValueError('Invalid archived image identity.')
            return archive.extractfile(member).read()
        saved=json.loads(payload('manifest.json'),object_pairs_hook=unique_object)[0]
        raw=payload(saved['Config']);config=json.loads(raw,object_pairs_hook=unique_object)
        digest='sha256:'+hashlib.sha256(raw).hexdigest()
        if saved['Config'] not in (digest[7:]+'.json','blobs/sha256/'+digest[7:]):
            raise ValueError('Archived config checksum does not match its name.')
        labels=config.get('config',{}).get('Labels',{}) or {}
        if (config.get('architecture')!='amd64' or config.get('os')!='linux' or
            labels.get('org.opencontainers.image.version')!=manifest['version'] or
            labels.get('org.opencontainers.image.revision')!=manifest['revision']):
            raise ValueError('Archived image labels/platform do not match release.')
        def descriptor_matches(descriptor,depth=0):
            if depth>8 or not isinstance(descriptor,dict):raise ValueError('Invalid OCI identity chain.')
            value=descriptor.get('digest','')
            if not isinstance(value,str) or not re.fullmatch('sha256:[a-f0-9]{64}',value):raise ValueError('Invalid OCI digest.')
            raw=payload('blobs/sha256/'+value[7:])
            if 'sha256:'+hashlib.sha256(raw).hexdigest()!=value:raise ValueError('OCI identity checksum mismatch.')
            node=json.loads(raw,object_pairs_hook=unique_object)
            if 'manifests' in node:return any(descriptor_matches(child,depth+1) for child in node['manifests'])
            return node.get('config',{}).get('digest')==digest
        if 'index.json' in archive.getnames():
            index=json.loads(payload('index.json'),object_pairs_hook=unique_object)
            if not any(descriptor_matches(d) for d in index.get('manifests',[]) if d.get('digest')==image['id']):
                raise ValueError('Archived image ID does not match release.')
        elif image['id']!=digest:raise ValueError('Archived image ID does not match release.')

def validate_bundle(directory: Path, manifest_name="release-manifest.json") -> dict:
    if directory.is_symlink():raise ValueError('Linked bundle directory.')
    root=directory.resolve();manifest=read_manifest(root/manifest_name)
    version=manifest.get('version','');revision=manifest.get('revision','')
    if not isinstance(version,str) or len(version)>128 or not VERSION.fullmatch(version) or any(p.isdigit() and len(p)>1 and p[0]=='0' for p in version.partition('-')[2].partition('+')[0].split('.')):
        raise ValueError('Invalid release version.')
    if manifest.get('format')!=1 or manifest.get('platform')!='linux/amd64' or (not isinstance(revision,str) or not re.fullmatch('[a-f0-9]{40}',revision)) or not manifest.get('migration'):
        raise ValueError('Unsupported release identity/platform.')
    files=manifest.get('files');images=manifest.get('images')
    if not isinstance(files,dict) or not files or not isinstance(images,dict) or set(images)!={'api','web','tools'}:raise ValueError('Incomplete release inventory.')
    for name,entry in files.items():
        path=regular_file(root,name)
        if not isinstance(entry,dict) or not re.fullmatch('[a-f0-9]{64}',entry.get('sha256','')) or entry.get('bytes')!=path.stat().st_size:raise ValueError('Invalid artifact size/hash.')
        with path.open('rb') as stream:actual=hashlib.file_digest(stream,'sha256').hexdigest()
        if actual!=entry['sha256']:raise ValueError('Artifact checksum mismatch.')
    archives=set()
    for name,image in bundled_images(manifest).items():
        if not isinstance(image,dict) or not all(isinstance(image.get(key),str) for key in ('file','tag','id')) or image.get('file') not in files or image['file'] in archives or image.get('tag')!='ratefreak-'+name+':'+version.replace('+','_') or not re.fullmatch('sha256:[a-f0-9]{64}',image.get('id','')):
            raise ValueError('Mixed or invalid release images.')
        archives.add(image['file']);archive_members(regular_file(root,image['file']),image['tag'])
        if 'reference_images' in manifest:archive_identity(regular_file(root,image['file']),image,manifest)
    from release_manifest import validate
    try:validate(manifest,root,manifest_name,read_manifest)
    except (KeyError,TypeError,tarfile.TarError,AttributeError) as error:raise ValueError('Malformed or incomplete release evidence.') from error
    return manifest

def load_bundle(directory: Path,inspect_image,load_image,manifest_name="release-manifest.json") -> dict:
    manifest=validate_bundle(directory,manifest_name)
    images=bundled_images(manifest)
    for image in images.values():
        existing=inspect_image(image['tag'])
        if existing is not None and existing.get('Id')!=image['id']:
            raise ValueError('Refusing to replace an existing immutable release tag.')
    for name,image in images.items():
        load_image(regular_file(directory,image['file']))
        metadata=inspect_image(image['tag']) or {};labels=metadata.get('Config',{}).get('Labels',{}) or {}
        if metadata.get('Id')!=image['id'] or metadata.get('Architecture')!='amd64' or metadata.get('Os')!='linux' or labels.get('org.opencontainers.image.version')!=manifest['version'] or labels.get('org.opencontainers.image.revision')!=manifest['revision']:
            raise ValueError('Loaded image identity does not match release manifest.')
    return manifest
