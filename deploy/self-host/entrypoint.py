#!/usr/bin/env python3
"""Container-only configuration loader and explicit storage provisioning."""
import json
import os
from pathlib import Path
import re
import shutil
import stat
import sys
import tempfile

class StorageError(Exception):pass

UID=10001
GID=10001
TMDB_POLICY_LIMIT = 1024 * 1024
TMDB_POLICY_ENTRY_LIMIT = 10_000
TMDB_POLICY_PATH = Path('/independent/tmdb-policy.json')
TMDB_CACHE_PATH = Path('/data/tmdb-posters')
IGDB_POLICY_PATH = Path('/independent-igdb/igdb-policy.json')
IGDB_CACHE_PATH = Path('/data/igdb-covers')
EMPTY_IGDB_POLICY = {'format': 1, 'terminated': False, 'blockedIdentities': [], 'blockedPosters': []}
IGDB_POLICY_LIMIT = TMDB_POLICY_LIMIT
IGDB_POLICY_ENTRY_LIMIT = TMDB_POLICY_ENTRY_LIMIT
EMPTY_TMDB_POLICY = {'format': 1, 'terminated': False, 'blockedIdentities': [], 'blockedPosters': []}

def no_links(path):
    for parent in [path,*path.parents]:
        if parent.is_symlink():raise StorageError('Storage paths must not traverse symbolic links.')

def fsync_directory(path):
    fd=os.open(path,os.O_RDONLY|os.O_DIRECTORY)
    try:os.fsync(fd)
    finally:os.close(fd)

def owned_directory(path,uid=UID,gid=GID,create=True):
    no_links(path)
    if create and not path.exists():
        path.mkdir(mode=0o700,parents=True);os.chown(path,uid,gid);fsync_directory(path.parent)
    info=path.stat()
    if not path.is_dir() or info.st_uid!=uid or info.st_gid!=gid or stat.S_IMODE(info.st_mode)!=0o700:
        raise StorageError('Storage directory ownership or permissions differ; refusing implicit repair.')

def prepare_image_cache(storage,uid=UID,gid=GID):
    owned_directory(storage/'image-cache',uid,gid)

def valid_tmdb_identity(value):
    if not isinstance(value, str):
        return False
    match = re.fullmatch(r'(?:movie|tv):([1-9][0-9]{0,9})', value)
    return match is not None and int(match[1]) <= 2_147_483_647

def valid_tmdb_poster(value):
    return (isinstance(value, str) and len(value) <= 200
            and re.fullmatch(r'[A-Za-z0-9_-]+\.(?:jpg|jpeg|png|webp|avif)', value) is not None)

def valid_igdb_identity(value):
    if not isinstance(value, str):
        return False
    match = re.fullmatch(r'game:([1-9][0-9]{0,9})', value)
    return match is not None and int(match[1]) <= 2_147_483_647

def valid_igdb_poster(value):
    return (isinstance(value, str) and len(value) <= 200
            and re.fullmatch(r'[A-Za-z0-9_-]+\.jpg', value) is not None)

def read_owned_file(path, limit, uid, gid=None):
    if not path.is_absolute():
        raise StorageError('Protected file paths must be absolute.')
    no_links(path)
    try:
        fd = os.open(path, os.O_RDONLY | os.O_NOFOLLOW | os.O_NONBLOCK)
    except OSError as error:
        raise StorageError('Required protected file is unavailable.') from error
    with os.fdopen(fd, 'rb') as stream:
        info = os.fstat(stream.fileno())
        if (not stat.S_ISREG(info.st_mode) or info.st_nlink != 1
                or stat.S_IMODE(info.st_mode) != 0o600 or info.st_uid != uid
                or (gid is not None and info.st_gid != gid) or info.st_size > limit):
            raise StorageError('Protected file ownership, permissions, type or size is invalid.')
        data = stream.read(limit + 1)
    if len(data) > limit:
        raise StorageError('Protected file exceeds its size limit.')
    return data

def unique_json_object(entries):
    result = dict(entries)
    if len(result) != len(entries):
        raise ValueError('Duplicate JSON keys.')
    return result

def read_tmdb_policy(path, uid=UID, gid=GID):
    data = read_owned_file(path, TMDB_POLICY_LIMIT, uid, gid)
    try:
        policy = json.loads(data.decode('utf-8'), object_pairs_hook=unique_json_object)
    except (ValueError, RecursionError) as error:
        raise StorageError('Invalid TMDB policy JSON.') from error
    if (not isinstance(policy, dict) or policy.keys() != EMPTY_TMDB_POLICY.keys()
            or type(policy['format']) is not int or policy['format'] != 1
            or type(policy['terminated']) is not bool):
        raise StorageError('Invalid TMDB policy schema.')
    for key, validate in [('blockedIdentities', valid_tmdb_identity), ('blockedPosters', valid_tmdb_poster)]:
        values = policy[key]
        if (not isinstance(values, list) or len(values) > TMDB_POLICY_ENTRY_LIMIT
                or not all(validate(value) for value in values) or len(set(values)) != len(values)):
            raise StorageError('Invalid TMDB policy entries.')
    return policy

def read_igdb_policy(path, uid=UID, gid=GID):
    data = read_owned_file(path, IGDB_POLICY_LIMIT, uid, gid)
    try:
        policy = json.loads(data.decode('utf-8'), object_pairs_hook=unique_json_object)
    except (ValueError, RecursionError) as error:
        raise StorageError('Invalid IGDB policy JSON.') from error
    if (not isinstance(policy, dict) or policy.keys() != EMPTY_IGDB_POLICY.keys()
            or type(policy['format']) is not int or policy['format'] != 1
            or type(policy['terminated']) is not bool):
        raise StorageError('Invalid IGDB policy schema.')
    for key, validate in [('blockedIdentities', valid_igdb_identity), ('blockedPosters', valid_igdb_poster)]:
        values = policy[key]
        if (not isinstance(values, list) or len(values) > IGDB_POLICY_ENTRY_LIMIT
                or not all(validate(value) for value in values) or len(set(values)) != len(values)):
            raise StorageError('Invalid IGDB policy entries.')
    return policy

def write_owned_file(path, data, uid, gid):
    no_links(path)
    fd, name = tempfile.mkstemp(dir=path.parent, prefix=f'.{path.name}.', suffix='.tmp')
    staged = Path(name)
    try:
        with os.fdopen(fd, 'wb') as stream:
            os.fchmod(stream.fileno(), 0o600)
            os.fchown(stream.fileno(), uid, gid)
            stream.write(data)
            stream.flush()
            os.fsync(stream.fileno())
        os.replace(staged, path)
        fsync_directory(path.parent)
    finally:
        staged.unlink(missing_ok=True)

def write_tmdb_policy(path, policy, uid=UID, gid=GID):
    data = json.dumps(policy, separators=(',', ':')).encode('utf-8')
    write_owned_file(path, data, uid, gid)

def write_igdb_policy(path, policy, uid=UID, gid=GID):
    data = json.dumps(policy, separators=(',', ':')).encode('utf-8')
    write_owned_file(path, data, uid, gid)

def reject_unexpected_cache_mounts(path, allow_root_mount):
    # Linux reports bind mounts here even when they share a device ID with the parent.
    mountinfo = Path('/proc/self/mountinfo').read_bytes()
    for entry in mountinfo.rstrip(b'\n').split(b'\n'):
        mountpoint = entry.split(b' ', 5)[4]
        decoded = re.sub(rb'\\([0-7]{3})', lambda match: bytes([int(match[1], 8)]), mountpoint)
        mount = Path(os.fsdecode(decoded))
        if path in mount.parents or (mount == path and not allow_root_mount):
            raise StorageError('Unexpected mount in cache storage; refusing cache cleanup.')

def clear_cache(path, uid, gid, *, allow_directories=False, allow_root_mount=False):
    owned_directory(path, uid, gid, create=False)
    fd = os.open(path, os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW)
    try:
        reject_unexpected_cache_mounts(path, allow_root_mount)
        device = os.fstat(fd).st_dev
        def fail_walk(error):
            raise error
        # Preflight the entire cache before unlinking anything. Never follow cache links.
        for _, directories, files, current in os.fwalk('.', onerror=fail_walk, dir_fd=fd):
            for name in directories + files:
                info = os.stat(name, dir_fd=current, follow_symlinks=False)
                if info.st_dev != device or (not stat.S_ISREG(info.st_mode)
                        and not (allow_directories and stat.S_ISDIR(info.st_mode))):
                    raise StorageError('Unexpected cache entry; refusing cache cleanup.')
        for name in os.listdir(fd):
            info = os.stat(name, dir_fd=fd, follow_symlinks=False)
            if stat.S_ISDIR(info.st_mode) and allow_directories:
                shutil.rmtree(name, dir_fd=fd)
            elif stat.S_ISREG(info.st_mode):
                os.unlink(name, dir_fd=fd)
            else:
                raise StorageError('Unexpected cache entry; refusing cache cleanup.')
        os.fsync(fd)
    finally:
        os.close(fd)

def clear_tmdb_cache(path, uid=UID, gid=GID):
    clear_cache(path, uid, gid, allow_root_mount=True)

def host_prepared_tmdb(root):
    receipt = root / 'prepared.json'
    no_links(receipt)
    if not receipt.exists():
        return False
    data = read_owned_file(receipt, 64, root.stat().st_uid)
    try:
        prepared = json.loads(data, object_pairs_hook=unique_json_object)
    except (ValueError, RecursionError) as error:
        raise StorageError('Invalid instance preparation receipt.') from error
    if (not isinstance(prepared, dict) or prepared.keys() != {'format'}
            or type(prepared['format']) is not int or prepared['format'] not in (1, 2, 3)):
        raise StorageError('Invalid instance preparation receipt.')
    return prepared['format'] >= 2

def prepare_tmdb_storage(root, uid=UID, gid=GID):
    policy = root / 'independent/tmdb/tmdb-policy.json'
    receipt = root / 'runtime/tmdb-lifecycle-v1'
    no_links(receipt)
    has_receipt = receipt.exists()
    if has_receipt and read_owned_file(receipt, 64, os.geteuid(), os.getegid()) != b'1\n':
        raise StorageError('Invalid TMDB lifecycle preparation receipt.')
    initialized = host_prepared_tmdb(root) or has_receipt
    has_policy = policy.exists() or policy.is_symlink()
    if initialized and not has_policy:
        raise StorageError('Restore the current independent TMDB policy before preparing this instance.')
    owned_directory(policy.parent, uid, gid, create=not initialized)
    if has_policy:
        read_tmdb_policy(policy, uid, gid)
    owned_directory(root / 'storage/tmdb-posters', uid, gid)
    prepare_image_cache(root / 'storage', uid, gid)
    if not initialized:
        clear_cache(root / 'storage/image-cache', uid, gid, allow_directories=True)
        if not has_policy:
            write_tmdb_policy(policy, EMPTY_TMDB_POLICY, uid, gid)
    if not has_receipt:
        write_owned_file(receipt, b'1\n', os.geteuid(), os.getegid())

def clear_igdb_cache(path, uid=UID, gid=GID):
    clear_cache(path, uid, gid, allow_root_mount=True)

def host_prepared_igdb(root):
    receipt = root / 'prepared.json'
    no_links(receipt)
    if not receipt.exists():
        return False
    data = read_owned_file(receipt, 64, root.stat().st_uid)
    try:
        prepared = json.loads(data, object_pairs_hook=unique_json_object)
    except (ValueError, RecursionError) as error:
        raise StorageError('Invalid instance preparation receipt.') from error
    if (not isinstance(prepared, dict) or prepared.keys() != {'format'}
            or type(prepared['format']) is not int or prepared['format'] not in (1, 2, 3)):
        raise StorageError('Invalid instance preparation receipt.')
    return prepared['format'] >= 3

def prepare_igdb_storage(root, uid=UID, gid=GID):
    policy = root / 'independent/igdb/igdb-policy.json'
    receipt = root / 'runtime/igdb-lifecycle-v1'
    no_links(receipt)
    has_receipt = receipt.exists()
    if has_receipt and read_owned_file(receipt, 64, os.geteuid(), os.getegid()) != b'1\n':
        raise StorageError('Invalid IGDB lifecycle preparation receipt.')
    initialized = host_prepared_igdb(root) or has_receipt
    has_policy = policy.exists() or policy.is_symlink()
    if initialized and not has_policy:
        raise StorageError('Restore the current independent IGDB policy before preparing this instance.')
    owned_directory(policy.parent, uid, gid, create=not initialized)
    if has_policy:
        read_igdb_policy(policy, uid, gid)
    owned_directory(root / 'storage/igdb-covers', uid, gid)
    prepare_image_cache(root / 'storage', uid, gid)
    if not initialized:
        clear_cache(root / 'storage/image-cache', uid, gid, allow_directories=True)
        if not has_policy:
            write_igdb_policy(policy, EMPTY_IGDB_POLICY, uid, gid)
    if not has_receipt:
        write_owned_file(receipt, b'1\n', os.geteuid(), os.getegid())

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
    prepare_tmdb_storage(root)
    prepare_igdb_storage(root)
    for service,names in {'web':['ingress','proxy'],'api':['proxy','setup','runtime'],'tools':['runtime','migrator','postgres','proxy'],'postgres':['postgres','migrator','runtime']}.items():
        uid,gid=(999,999) if service=='postgres' else (UID,GID)
        directory=root/'runtime'/service;owned_directory(directory,uid,gid)
        for name in names:copy_secret(root/'secrets'/name,directory/name,uid,gid)
    print('Protected runtime secrets, artwork, keys, image caches, independent journal and TMDB/IGDB policies prepared.')

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
    if mode=='replay' and (not extra or extra[0] not in {'--replay-account-erasures','--appoint-admin','--revoke-admin','--tmdb-maintenance','--igdb-maintenance'}):
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
    if mode=='tmdb-clear-cache':clear_tmdb_cache(TMDB_CACHE_PATH);return
    if mode=='igdb-clear-cache':clear_igdb_cache(IGDB_CACHE_PATH);return
    if mode in ['backup','restore','verify-replay']:
        os.execvp('python3',['python3','/app/maintenance.py',mode])
    command,env=runtime_command(mode,Path('/run/ratefreak'),sys.argv[2:],os.environ)
    os.execvpe(command[0],command,env)

if __name__=='__main__':
    try:run()
    except (StorageError,OSError,ValueError,IndexError):
        print('Container initialization failed; verify private configuration, storage and credentials.',file=sys.stderr);sys.exit(1)
