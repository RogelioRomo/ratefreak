#!/usr/bin/env python3
"""Explicit lifecycle commands for one private RateFreak instance."""
import argparse
from contextlib import contextmanager
import fcntl
import hashlib
import json
import os
from pathlib import Path
import re
import secrets
import stat
import subprocess
import sys
from urllib.parse import urlsplit

class OperatorError(Exception):
    pass

SECRET_NAMES = ('ingress', 'proxy', 'setup', 'postgres', 'migrator', 'runtime')
PACKAGE = Path(__file__).resolve().parent

def safe_path(path):
    path = Path(os.path.abspath(path))
    if any(c in str(path) for c in '\n\r\0'):
        raise OperatorError('Instance paths must not contain control characters.')
    for parent in [path, *path.parents]:
        if parent.is_symlink(): raise OperatorError('Symbolic links are not allowed in instance paths.')
    return path

def private_directory(path):
    path=safe_path(path)
    if not path.exists(): path.mkdir(mode=0o700, parents=True)
    if not path.is_dir() or stat.S_IMODE(path.stat().st_mode)!=0o700 or path.stat().st_uid != os.getuid():
        raise OperatorError('Instance configuration directories must be owned by the operator and mode 0700.')
    return path

def sync_directory(path):
    fd=os.open(path,os.O_RDONLY|os.O_DIRECTORY)
    try:os.fsync(fd)
    finally:os.close(fd)

def exclusive_file(path, data):
    fd=os.open(path,os.O_WRONLY|os.O_CREAT|os.O_EXCL|os.O_NOFOLLOW,0o600)
    try:
        with os.fdopen(fd,'wb') as stream:
            stream.write(data);stream.flush();os.fsync(stream.fileno())
    finally:sync_directory(path.parent)

def read_private(path):
    safe_path(path)
    if not path.is_file() or stat.S_IMODE(path.stat().st_mode)!=0o600 or path.stat().st_uid!=os.getuid():
        raise OperatorError('Configuration files must be operator-owned regular files with mode 0600.')
    return path.read_bytes()

def origin_value(origin):
    if any(c.isspace() or ord(c)<32 for c in origin): raise OperatorError('Use one HTTPS root origin.')
    value=urlsplit(origin)
    try:port=value.port
    except ValueError:raise OperatorError('Invalid origin port.') from None
    if value.scheme!='https' or not value.hostname or value.username or value.password or value.path not in ('','/') or value.query or value.fragment:
        raise OperatorError('Use one HTTPS root origin without credentials, path or query.')
    # Hostname is also used in edge configuration; exclude configuration metacharacters.
    if not re.fullmatch(r'[a-zA-Z0-9.-]+',value.hostname):raise OperatorError('Use a DNS hostname for the reference installation.')
    return 'https://'+value.hostname.lower()+(f':{port}' if port else '')

def initialize(root, origin, local_tls, repair_missing=True):
    origin=origin_value(origin)
    root=private_directory(root)
    config={'origin':origin,'localTls':bool(local_tls),'format':1}
    config_path=root/'instance.json'
    if config_path.exists() or config_path.is_symlink():
        try:existing=json.loads(read_private(config_path))
        except (ValueError,UnicodeError):raise OperatorError('Invalid existing instance configuration.') from None
        if existing!=config:raise OperatorError('Existing configuration differs; refusing implicit reconfiguration.')
    if not repair_missing and (not (root/'secrets').is_dir() or any(not (root/'secrets'/name).exists() for name in SECRET_NAMES)):
        raise OperatorError('Missing credentials; normal lifecycle commands never generate replacement secrets. Review explicit initialization.')
    directory=private_directory(root/'secrets')
    previous=[]
    for name in SECRET_NAMES:
        path=directory/name
        if path.exists() or path.is_symlink():
            value=read_private(path).decode('ascii').strip()
            if not re.fullmatch('[a-f0-9]{64}',value):raise OperatorError('Existing secret is invalid; refusing automatic replacement.')
            previous.append(value)
    if len(previous)!=len(set(previous)):raise OperatorError('Secrets must be independent.')
    # Validate everything before repairing missing outputs. Never replace existing credentials.
    if not config_path.exists():exclusive_file(config_path,(json.dumps(config,indent=2)+'\n').encode())
    for name in SECRET_NAMES:
        path=directory/name
        if not path.exists():exclusive_file(path,(secrets.token_hex(32)+'\n').encode())
    return config

def load_instance(root):
    root=safe_path(root)
    try:config=json.loads(read_private(root/'instance.json'))
    except (OSError,ValueError):raise OperatorError('Initialize the instance before running this command.') from None
    initialize(root,config['origin'],config['localTls'],repair_missing=False)
    return root,config

def compose(root, config, ingress=None):
    env=os.environ.copy()
    env['RATEFREAK_INSTANCE_DIR']=str(root)
    env['RATEFREAK_PUBLIC_ORIGIN']=config['origin']
    env['RATEFREAK_HOSTNAME']=urlsplit(config['origin']).hostname
    policy=root/'journal-policy.json'
    enabled=policy.exists() and json.loads(read_private(policy)).get('independentReplicationConfirmed') is True
    env['RATEFREAK_API_ERASURE_JOURNAL_PATH']='/independent/account-erasures.jsonl' if enabled else ''
    project='ratefreak-'+hashlib.sha256(str(root).encode()).hexdigest()[:12]
    command=['docker','compose','--project-name',project,'-f',str(PACKAGE/'compose.yaml')]
    if ingress:command+=['-f',str(PACKAGE/('compose.caddy.yaml' if ingress=='caddy' else 'compose.host-proxy.yaml'))]
    override=root/'compose.override.yaml'
    if override.exists() or override.is_symlink():
        read_private(override)
        command+=['-f',str(override)]
    return command,env

def execute(command,env=None,capture=False):
    try:
        result=subprocess.run(command,env=env,check=True,text=True,stdout=subprocess.PIPE if capture else None)
        return result.stdout if capture else None
    except subprocess.CalledProcessError:raise OperatorError('Command failed. Keep application writers stopped and inspect sanitized service diagnostics.') from None

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--instance-dir',required=True,type=Path)
    sub=parser.add_subparsers(dest='command',required=True)
    init=sub.add_parser('init');init.add_argument('--origin',required=True);init.add_argument('--local-tls',action='store_true')
    for name in ['build','prepare','migrate','start','stop','status']:
        action=sub.add_parser(name)
        if name=='start':action.add_argument('--ingress',choices=['caddy','external'],default='caddy')
    deletion=sub.add_parser('enable-deletion');deletion.add_argument('--confirm-independent-journal',required=True,action='store_true')
    backup=sub.add_parser('backup');backup.add_argument('--output',required=True,type=Path)
    restore=sub.add_parser('restore');restore.add_argument('--backup',required=True,type=Path);restore.add_argument('--journal',required=True,type=Path);restore.add_argument('--confirm-restore',action='store_true',required=True)
    replay=sub.add_parser('replay');replay.add_argument('--database-name',required=True);replay.add_argument('--confirm-erasure-replay',action='store_true',required=True)
    args=parser.parse_args()
    try:
        if args.command=='init':
            root=private_directory(args.instance_dir)
            with instance_lock(root):initialize(root,args.origin,args.local_tls)
            print('Private instance configuration initialized; existing credentials preserved.')
            return 0
        root=safe_path(args.instance_dir)
        with instance_lock(root):
            root,config=load_instance(root)
            command,env=compose(root,config,getattr(args,'ingress',None))
            dispatch(args,root,config,command,env)
        return 0
    except (OperatorError,OSError,UnicodeError,ValueError,KeyError) as error:
        print(str(error) if isinstance(error,OperatorError) else 'Operation failed; check protected instance paths and configuration.',file=sys.stderr)
        return 1

def write_state(path, value):
    safe_path(path)
    if path.exists():read_private(path)
    temporary=path.with_name(path.name+'.'+secrets.token_hex(8)+'.tmp')
    exclusive_file(temporary,(json.dumps(value,indent=2)+'\n').encode())
    os.replace(temporary,path);sync_directory(path.parent)

def require_ready(root):
    if (root/'restore-pending').exists():raise OperatorError('Restore replay and verification must finish before starting writers.')
    if not (root/'migrated.json').exists():raise OperatorError('Run explicit prepare and migrate before starting applications.')
    try:return json.loads(read_private(root/'migrated.json'))
    except ValueError:raise OperatorError('Invalid migration acceptance record.') from None

def override_signature(root):
    path=root/'compose.override.yaml'
    return hashlib.sha256(read_private(path)).hexdigest() if path.exists() else ''

@contextmanager
def instance_lock(root):
    path=safe_path(root/'maintenance.lock')
    if path.exists():read_private(path)
    fd=os.open(path,os.O_WRONLY|os.O_CREAT|os.O_NOFOLLOW,0o600)
    try:
        try:fcntl.flock(fd,fcntl.LOCK_EX|fcntl.LOCK_NB)
        except BlockingIOError:raise OperatorError('Another instance lifecycle operation is active.') from None
        yield
    finally:os.close(fd)

def release_images():
    names=['api','web','tools']
    values=execute(['docker','image','inspect','--format','{{.Id}}',*['ratefreak-'+name+':local' for name in names]],capture=True).splitlines()
    if len(values)!=3 or not all(re.fullmatch(r'sha256:[a-f0-9]{64}',value) for value in values):raise OperatorError('Missing or invalid release image identities.')
    return dict(zip(names,values))

def require_release(root,images):
    marker=require_ready(root)
    if marker.get('images')!=images or marker.get('overrideSha','')!=override_signature(root):
        raise OperatorError('Release images or instance override changed; stop writers and explicitly migrate/accept the complete release.')
    return marker

def replay_state(root,images):
    if (root/'restore-pending').exists():
        receipt=root/'restore-complete.json'
        if not receipt.exists():raise OperatorError('Restore did not complete. Keep writers stopped and recover into a fresh isolated instance.')
        accepted=json.loads(read_private(receipt))
        if accepted.get('images')!=images or accepted.get('overrideSha','')!=override_signature(root):raise OperatorError('Restore receipt does not match this complete release/configuration.')
        return 'restore'
    require_release(root,images)
    return 'normal'

def stopped_writers(command,env):
    raw=execute(command+['ps','--all','--format','json'],env,capture=True).strip()
    rows=json.loads(raw) if raw.startswith('[') else [json.loads(line) for line in raw.splitlines() if line.strip()]
    writers={'api','web','caddy','migrate','tools','storage-init'}
    if any(row.get('Service') in writers and row.get('State')!='exited' for row in rows):
        raise OperatorError('Explicitly stop every application/maintenance writer before this operation, including restarting or paused containers.')

def dispatch(args,root,config,command,env):
    operation=args.command
    if operation=='build':
        for name in ['api','web','tools']:
            execute(['docker','build','-f',str(PACKAGE/(name+'.Dockerfile')),'-t','ratefreak-'+name+':local',str(PACKAGE.parents[1])])
    elif operation=='prepare':
        stopped_writers(command,env)
        execute(command+['run','--rm','--no-deps','storage-init'],env)
        write_state(root/'prepared.json',{'format':1})
    elif operation=='migrate':
        stopped_writers(command,env)
        if not (root/'prepared.json').exists():raise OperatorError('Prepare protected storage first.')
        # Remove prior acceptance BEFORE attempting DDL so a failed upgrade cannot restart.
        if (root/'migrated.json').exists():(root/'migrated.json').unlink()
        execute(command+['up','-d','--wait','postgres'],env)
        execute(command+['run','--rm','migrate'],env)
        write_state(root/'migrated.json',{'images':release_images(),'overrideSha':override_signature(root)})
    elif operation=='enable-deletion':
        stopped_writers(command,env)
        write_state(root/'journal-policy.json',{'independentReplicationConfirmed':True})
        print('Operator journal replication acknowledgment recorded. Start applies the deletion configuration; continue monitoring replication.')
    elif operation=='backup':
        stopped_writers(command,env)
        images=release_images()
        require_release(root,images)
        destination=safe_path(args.output)
        if destination.exists():raise OperatorError('Use a new backup destination; existing paths are never overwritten.')
        private_directory(destination)
        extra=['-e','RATEFREAK_RELEASE_IMAGES='+json.dumps(release_images(),separators=(',',':')),'-e','RATEFREAK_OPERATOR_UID='+str(os.getuid()),'-e','RATEFREAK_OPERATOR_GID='+str(os.getgid())]
        execute(command+['run','--rm','--user','0:0','-v',str(destination)+':/backup',*extra,'tools','backup'],env)
    elif operation=='restore':
        stopped_writers(command,env)
        if (root/'migrated.json').exists() or (root/'restore-pending').exists():raise OperatorError('Restore requires a fresh isolated instance; never restore over an existing installation.')
        if not (root/'prepared.json').exists():raise OperatorError('Prepare the fresh isolated target first.')
        source=safe_path(args.backup);journal=safe_path(args.journal)
        if not source.is_dir() or not journal.is_file():raise OperatorError('Supply a backup directory and the protected current journal.')
        # Preflight integrity before creating/restoring any target database data.
        sys.path.insert(0,str(PACKAGE))
        from maintenance import validate_backup, MaintenanceError
        try:validate_backup(source,release_images())
        except (MaintenanceError,ValueError,OSError):raise OperatorError('Backup integrity/release validation failed; target untouched.') from None
        exclusive_file(root/'restore-pending',b'Keep traffic stopped until replay succeeds.\n')
        execute(command+['up','-d','--wait','postgres'],env)
        execute(command+['run','--rm','--user','0:0','-e','RATEFREAK_RELEASE_IMAGES='+json.dumps(release_images(),separators=(',',':')),'-v',str(source)+':/backup:ro','-v',str(journal)+':/current-journal:ro','tools','restore'],env)
        write_state(root/'restore-complete.json',{'images':release_images(),'overrideSha':override_signature(root)})
    elif operation=='replay':
        stopped_writers(command,env)
        images=release_images()
        state=replay_state(root,images)
        execute(command+['run','--rm','tools','replay','--replay-account-erasures','--database-name',args.database_name,'--confirm-erasure-replay'],env)
        execute(command+['run','--rm','tools','verify-replay'],env)
        if state=='restore':
            write_state(root/'migrated.json',{'images':images,'overrideSha':override_signature(root)})
            (root/'restore-pending').unlink();sync_directory(root)
    elif operation=='start':
        require_release(root,release_images())
        # Require the local TLS choice to be explicit in the edge configuration.
        if args.ingress=='caddy' and urlsplit(config['origin']).port not in (None,443):raise OperatorError('Custom public ports require an operator-managed ingress configuration.')
        if args.ingress=='caddy' and config['localTls']:
            env['RATEFREAK_CADDY_TLS']='tls internal'
        else:env['RATEFREAK_CADDY_TLS']=''
        write_state(root/'deployment.json',{'ingress':args.ingress})
        services=['api','web']+(['caddy'] if args.ingress=='caddy' else [])
        execute(command+['up','-d','--wait',*services],env)
    elif operation in ['stop','status']:
        saved=root/'deployment.json'
        ingress=json.loads(read_private(saved)).get('ingress') if saved.exists() else None
        command,env=compose(root,config,ingress)
        execute(command+(['stop'] if operation=='stop' else ['ps']),env)
    else:raise OperatorError('Unknown maintenance operation.')

if __name__=='__main__':sys.exit(main())
