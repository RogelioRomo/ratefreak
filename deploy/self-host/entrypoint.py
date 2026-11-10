#!/usr/bin/env python3
"""Container-only configuration loader and explicit storage provisioning."""
import json
import os
from pathlib import Path
import re
import stat
import sys

class StorageError(Exception):pass

UID=10001
GID=10001

def no_links(path):
    for parent in [path,*path.parents]:
        if parent.is_symlink():raise StorageError('Storage paths must not traverse symbolic links.')

def fsync_directory(path):
    fd=os.open(path,os.O_RDONLY|os.O_DIRECTORY)
    try:os.fsync(fd)
    finally:os.close(fd)

def owned_directory(path,uid=UID,gid=GID):
    no_links(path)
    if not path.exists():
        path.mkdir(mode=0o700,parents=True);os.chown(path,uid,gid);fsync_directory(path.parent)
    info=path.stat()
    if not path.is_dir() or info.st_uid!=uid or info.st_gid!=gid or stat.S_IMODE(info.st_mode)!=0o700:
        raise StorageError('Storage directory ownership or permissions differ; refusing implicit repair.')

def ensure_journal(path,uid=UID,gid=GID):
    no_links(path);owned_directory(path.parent,uid,gid)
    if not path.exists():
        fd=os.open(path,os.O_WRONLY|os.O_CREAT|os.O_EXCL|os.O_NOFOLLOW,0o600)
        try:os.fchown(fd,uid,gid);os.fsync(fd)
        finally:os.close(fd)
        fsync_directory(path.parent)
    info=path.stat()
    if not stat.S_ISREG(info.st_mode) or info.st_uid!=uid or info.st_gid!=gid or stat.S_IMODE(info.st_mode)!=0o600:
        raise StorageError('Journal ownership or permissions differ; refusing implicit repair.')

def read_secret(path):
    no_links(path)
    info=path.stat()
    if not stat.S_ISREG(info.st_mode) or stat.S_IMODE(info.st_mode)!=0o600:
        raise StorageError('Secret files must be private regular files.')
    text=path.read_text().strip()
    if not re.fullmatch('[a-f0-9]{64}',text):raise StorageError('Invalid secret file.')
    return text

def copy_secret(source,target,uid=UID,gid=GID):
    value=read_secret(source)
    no_links(target)
    if target.exists():
        if target.stat().st_uid!=uid or read_secret(target)!=value:raise StorageError('Runtime secret differs; rotate it explicitly with writers stopped.')
        return
    fd=os.open(target,os.O_WRONLY|os.O_CREAT|os.O_EXCL|os.O_NOFOLLOW,0o600)
    with os.fdopen(fd,'w') as out:
        os.fchown(out.fileno(),uid,gid);out.write(value+'\n');out.flush();os.fsync(out.fileno())
    fsync_directory(target.parent)

def prepare():
    # Bind-mount root may be an empty Docker-created directory. Only that known
    # root is adopted; populated/conflicting directories are never recursively chowned.
    root=Path('/instance')
    for directory in [root/'storage',root/'runtime',root/'independent']:
        no_links(directory)
        if not directory.exists():directory.mkdir(mode=0o700)
        elif directory.is_dir() and not any(directory.iterdir()) and directory.stat().st_uid==0:
            directory.chmod(0o700)
    for name in ['artwork','keys']:owned_directory(root/'storage'/name)
    # The journal directory itself belongs to the API, never the ordinary backup.
    independent=root/'independent'
    if not any(independent.iterdir()) and independent.stat().st_uid==0:os.chown(independent,UID,GID)
    ensure_journal(independent/'account-erasures.jsonl')
    for service,names in {'web':['ingress','proxy'],'api':['proxy','setup','runtime'],'tools':['runtime','migrator','postgres','proxy'],'postgres':['postgres','migrator','runtime']}.items():
        uid,gid=(999,999) if service=='postgres' else (UID,GID)
        directory=root/'runtime'/service;owned_directory(directory,uid,gid)
        for name in names:copy_secret(root/'secrets'/name,directory/name,uid,gid)
    print('Protected runtime secrets, artwork, keys and independent journal prepared.')

def connection(user,password,environment=None):
    values=os.environ if environment is None else environment
    def quote(value):return '"'+value.replace('"','""')+'"'
    # Host/database overrides are for deliberate external PostgreSQL configuration.
    host=values.get('RATEFREAK_DB_HOST','postgres')
    port=values.get('RATEFREAK_DB_PORT','5432')
    database=values.get('RATEFREAK_DB_NAME','ratefreak')
    ssl=values.get('RATEFREAK_DB_SSLMODE','Disable')
    if ssl not in ['Disable','VerifyFull']:raise StorageError('Use Disable only on the private single-host network, or VerifyFull for remote PostgreSQL.')
    return ';'.join(f'{key}={quote(value)}' for key,value in [('Host',host),('Port',port),('Database',database),('Username',user),('Password',password),('SSL Mode',ssl)])

def runtime_command(mode,directory,extra,environment):
    if mode not in {'api','web','migrate','replay'}:raise StorageError('Unknown container command mode.')
    if mode=='replay' and (not extra or extra[0] not in {'--replay-account-erasures','--appoint-admin','--revoke-admin'}):
        raise StorageError('An explicit maintenance action is required.')
    env=environment.copy()
    if mode=='web':
        env['RATEFREAK_INGRESS_SECRET']=read_secret(directory/'ingress')
        env['RATEFREAK_PROXY_SECRET']=read_secret(directory/'proxy')
        command=['node','server.js']
    else:
        migrating=mode=='migrate'
        user='ratefreak_migrator' if migrating else 'ratefreak_runtime'
        user=env.get('RATEFREAK_DB_MIGRATION_USER' if migrating else 'RATEFREAK_DB_RUNTIME_USER',user)
        password=read_secret(directory/('migrator' if migrating else 'runtime'))
        env['ConnectionStrings__Default']=connection(user,password,env)
        env['BrowserProxy__SharedSecret']=read_secret(directory/'proxy')
        if mode=='api':
            if env.get('RATEFREAK_SETUP_SECRET_DISABLED')=='true':env.pop('Installation__SetupSecret',None)
            else:env['Installation__SetupSecret']=read_secret(directory/'setup')
        command=['/app/efbundle'] if migrating else ['dotnet','/app/RateFreak.Api.dll']
        command+=extra
    return command,env

def run():
    mode=sys.argv[1]
    if mode=='prepare':prepare();return
    if mode in ['backup','restore','verify-replay']:
        os.execvp('python3',['python3','/app/maintenance.py',mode])
    command,env=runtime_command(mode,Path('/run/ratefreak'),sys.argv[2:],os.environ)
    os.execvpe(command[0],command,env)

if __name__=='__main__':
    try:run()
    except (StorageError,OSError,ValueError,IndexError):
        print('Container initialization failed; verify private configuration, storage and credentials.',file=sys.stderr);sys.exit(1)
