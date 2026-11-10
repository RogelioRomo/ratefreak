#!/usr/bin/env python3
"""Disposable image and Compose checks; never uses the developer instance."""
import argparse
import json
import subprocess

def call(*args):return subprocess.check_output(args,text=True).strip()

def images():
    for name in ['api','web','tools']:
        image='ratefreak-'+name+':local'
        metadata=json.loads(call('docker','image','inspect',image))[0]
        assert metadata['Config']['User']=='10001:10001',(name,'non-root runtime')
        script='''import os,pathlib
assert os.getuid()==10001
assert not pathlib.Path('/app/.env').exists()
assert not pathlib.Path('/app/.local').exists()
assert not pathlib.Path('/usr/share/dotnet/sdk').exists()
p=pathlib.Path('/tmp/ratefreak-smoke');p.write_text('ok');p.unlink()
print('runtime isolation passed')'''
        print(name,call('docker','run','--rm','--entrypoint','python3',image,'-c',script))
    print('Image runtime checks passed')


def instance_checks(root):
    from ratefreak import load_instance,compose
    root,config=load_instance(root);command,env=compose(root,config)
    def sql(user,statement,expected=0):
        result=subprocess.run(command+['exec','-T','postgres','psql','-X','-v','ON_ERROR_STOP=1','-U',user,'-d','ratefreak'],env=env,input=statement,text=True,stdout=subprocess.PIPE,stderr=subprocess.PIPE)
        assert (result.returncode==0)==(expected==0),'database privilege check failed'
        return result.stdout
    try:
        sql('ratefreak_migrator','CREATE TABLE selfhost_privilege_probe(id int PRIMARY KEY, value text);')
        sql('ratefreak_runtime',"INSERT INTO selfhost_privilege_probe VALUES (1,'check'); SELECT * FROM selfhost_privilege_probe; UPDATE selfhost_privilege_probe SET value='changed'; DELETE FROM selfhost_privilege_probe;")
        for statement in ['CREATE TABLE denied(id int);','ALTER TABLE selfhost_privilege_probe ADD COLUMN denied int;','DROP TABLE selfhost_privilege_probe;']:
            sql('ratefreak_runtime',statement,expected=1)
    finally:sql('ratefreak_migrator','DROP TABLE IF EXISTS selfhost_privilege_probe;')
    for name in ['api','web','postgres']:
        container=subprocess.check_output(command+['ps','-q',name],env=env,text=True).strip()
        details=json.loads(call('docker','inspect',container))[0]
        assert not details['HostConfig']['PortBindings'],name+' unexpectedly publishes ports'
    print('Runtime DML/future-table grants, denied DDL and private service listeners passed')

if __name__=='__main__':
    from pathlib import Path
    parser=argparse.ArgumentParser();parser.add_argument('--images',action='store_true');parser.add_argument('--instance-dir',type=Path);parser.add_argument('--confirm-disposable',action='store_true');args=parser.parse_args()
    if args.images:images()
    if args.instance_dir:
        if not args.confirm_disposable:parser.error('Instance smoke requires --confirm-disposable')
        instance_checks(args.instance_dir)
