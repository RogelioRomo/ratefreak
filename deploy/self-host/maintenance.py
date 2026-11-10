#!/usr/bin/env python3
"""Offline backup/restore helpers. All writers must be stopped by the caller."""
import datetime
import hashlib
import json
import os
from pathlib import Path
import stat
import subprocess
import sys
import tarfile
import uuid
from entrypoint import read_secret, connection, fsync_directory, ensure_journal, UID, GID

class MaintenanceError(Exception):pass

FILES={'database.dump','artwork.tar','keys.tar'}

def digest(path):
    value=hashlib.sha256()
    with path.open('rb') as stream:
        for block in iter(lambda:stream.read(1024*1024),b''):value.update(block)
    return value.hexdigest()

def validate_archive(path):
    with tarfile.open(path,'r') as archive:
        seen=set()
        for member in archive:
            if not member.isfile() or member.name in seen or member.name in ('','.','..') or Path(member.name).name!=member.name or '\\' in member.name:
                raise MaintenanceError('Backup archive contains unsafe or duplicate entries.')
            seen.add(member.name)

def validate_backup(root,images):
    manifest_path=root/'manifest.json'
    if manifest_path.is_symlink() or not manifest_path.is_file():raise MaintenanceError('Missing backup manifest.')
    data=json.loads(manifest_path.read_text())
    if data.get('format')!=2 or data.get('images')!=images or set(data.get('files',{}))!=FILES:
        raise MaintenanceError('Backup release or inventory does not match this maintenance image.')
    for name,expected in data['files'].items():
        path=root/name
        if path.is_symlink() or not path.is_file() or digest(path)!=expected:raise MaintenanceError('Backup payload hash mismatch.')
    for name in ['artwork.tar','keys.tar']:validate_archive(root/name)
    return data

def pg_environment(migrating=False):
    env=os.environ.copy()
    env.update({'PGHOST':env.get('RATEFREAK_DB_HOST','postgres'),'PGPORT':env.get('RATEFREAK_DB_PORT','5432'),'PGDATABASE':env.get('RATEFREAK_DB_NAME','ratefreak'),
                'PGUSER':env.get('RATEFREAK_DB_MIGRATION_USER' if migrating else 'RATEFREAK_DB_RUNTIME_USER','ratefreak_migrator' if migrating else 'ratefreak_runtime'),
                'PGPASSWORD':read_secret(Path('/run/ratefreak')/('migrator' if migrating else 'runtime')),'PGSSLMODE':env.get('RATEFREAK_DB_SSLMODE','disable').lower()})
    return env

def pg_run(command,migrating=False,**kwargs):
    result=subprocess.run(command,env=pg_environment(migrating),stderr=subprocess.PIPE,**kwargs)
    if result.returncode:raise MaintenanceError('Database maintenance failed; keep writers stopped and verify database access/schema.')
    return result

def query(sql,migrating=False):
    return pg_run(['psql','-X','-A','-t','-v','ON_ERROR_STOP=1'],migrating,input=sql.encode(),stdout=subprocess.PIPE).stdout.decode().strip()

def archive_directory(source,target):
    with tarfile.open(target,'x') as archive:
        for path in sorted(source.iterdir()):
            if path.is_symlink() or not path.is_file():raise MaintenanceError('Unexpected entry in private storage; backup refused.')
            archive.add(path,arcname=path.name,recursive=False)
    target.chmod(0o600)
    with target.open('rb') as durable:os.fsync(durable.fileno())

def verify_artwork(root):
    for key in query('SELECT storage_key FROM artwork_assets;').splitlines():
        path=root/key
        if Path(key).name!=key or path.is_symlink() or not path.is_file():raise MaintenanceError('Referenced artwork is missing or unsafe.')

def backup():
    root=Path('/backup')
    if any(root.iterdir()):raise MaintenanceError('Backup destination must be empty.')
    os.umask(0o077)
    verify_artwork(Path('/data/artwork'))
    with (root/'database.dump').open('xb') as output:
        pg_run(['pg_dump','--format=custom','--no-owner','--no-privileges'],stdout=output)
        output.flush();os.fsync(output.fileno())
    pg_run(['pg_restore','--list',str(root/'database.dump')],stdout=subprocess.DEVNULL)
    archive_directory(Path('/data/artwork'),root/'artwork.tar')
    archive_directory(Path('/data/keys'),root/'keys.tar')
    manifest={'format':2,'images':json.loads(os.environ['RATEFREAK_RELEASE_IMAGES']),'createdAt':datetime.datetime.now(datetime.timezone.utc).isoformat(),
              'schema':query('SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId";').splitlines(),
              'files':{name:digest(root/name) for name in sorted(FILES)},'journalIncluded':False}
    with (root/'manifest.json').open('x') as out:json.dump(manifest,out,indent=2);out.write('\n');out.flush();os.fsync(out.fileno())
    for path in root.iterdir():
        path.chmod(0o600);os.chown(path,int(os.environ['RATEFREAK_OPERATOR_UID']),int(os.environ['RATEFREAK_OPERATOR_GID']))
    fsync_directory(root)
    print('Coordinated database/artwork/key-recovery backup complete; journal excluded. Enforce private encrypted storage and 30-day expiration.')

def extract_flat(source,destination):
    if any(destination.iterdir()):raise MaintenanceError('Restore storage must be empty.')
    validate_archive(source)
    with tarfile.open(source,'r') as archive:
        for member in archive:
            target=destination/member.name
            fd=os.open(target,os.O_WRONLY|os.O_CREAT|os.O_EXCL|os.O_NOFOLLOW,0o600)
            with os.fdopen(fd,'wb') as out,archive.extractfile(member) as incoming:
                os.fchown(out.fileno(),UID,GID)
                for block in iter(lambda:incoming.read(1024*1024),b''):out.write(block)
                out.flush();os.fsync(out.fileno())
            # Preserve age for bounded orphan cleanup without using unsafe archive metadata.
            os.utime(target,(member.mtime,member.mtime))
    fsync_directory(destination)

def restore():
    root=Path('/backup');validate_backup(root,json.loads(os.environ['RATEFREAK_RELEASE_IMAGES']))
    # Validate all refusal conditions before restoring any database/files.
    if query("SELECT count(*) FROM information_schema.tables WHERE table_schema='public';",True)!='0':raise MaintenanceError('Restore requires a fresh isolated database.')
    for path in [Path('/data/artwork'),Path('/data/keys')]:
        if any(path.iterdir()):raise MaintenanceError('Restore requires empty isolated artwork/key storage.')
    source=Path('/current-journal')
    if source.is_symlink() or not source.is_file() or stat.S_IMODE(source.stat().st_mode)!=0o600:raise MaintenanceError('Supply the protected current independent journal.')
    target=Path('/independent/account-erasures.jsonl');ensure_journal(target)
    if target.stat().st_size:raise MaintenanceError('Refusing to replace an existing journal with records.')
    # Destination is only the verified empty journal provisioned for this fresh restore.
    staged=target.with_name('restore-current-journal.tmp')
    fd=os.open(staged,os.O_WRONLY|os.O_CREAT|os.O_EXCL|os.O_NOFOLLOW,0o600)
    with os.fdopen(fd,'wb') as out,source.open('rb') as incoming:
        os.fchown(out.fileno(),UID,GID)
        for block in iter(lambda:incoming.read(1024*1024),b''):out.write(block)
        out.flush();os.fsync(out.fileno())
    os.replace(staged,target);fsync_directory(target.parent)
    pg_run(['pg_restore','--exit-on-error','--no-owner','--no-privileges','--dbname',os.environ.get('RATEFREAK_DB_NAME','ratefreak'),str(root/'database.dump')],True,stdout=subprocess.DEVNULL)
    extract_flat(root/'artwork.tar',Path('/data/artwork'));extract_flat(root/'keys.tar',Path('/data/keys'))
    print('Isolated snapshot restored. Traffic remains stopped until current-journal replay and verification succeed.')

def verify_replay():
    journal=Path('/independent/account-erasures.jsonl')
    ids=set()
    for line in journal.read_bytes().splitlines(keepends=True):
        if not line.endswith(b'\n'):continue
        record=json.loads(line);ids.add(str(uuid.UUID(record['AccountId'])))
    for account in ids:
        # UUID parsing above prevents arbitrary SQL; SQL travels on stdin, not process arguments/logs.
        if query(f"SELECT count(*) FROM users WHERE id='{account}';")!='0':raise MaintenanceError('A journaled account remains after replay.')
    verify_artwork(Path('/data/artwork'))
    print('Journaled accounts absent and referenced artwork present. Verify instance-specific data/moderation and independent journal replication before traffic.')

def main():
    try:
        {'backup':backup,'restore':restore,'verify-replay':verify_replay}[sys.argv[1]]()
    except (MaintenanceError,OSError,ValueError,KeyError,tarfile.TarError):
        print('Protected maintenance failed; keep writers stopped. Check target freshness, current journal, matching release and archive integrity.',file=sys.stderr);return 1
    return 0

if __name__=='__main__':sys.exit(main())
