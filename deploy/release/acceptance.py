#!/usr/bin/env python3
"""Disposable HTTPS install/upgrade acceptance; no developer instance access."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import secrets
import socket
import subprocess
import sys
import tempfile
import time
sys.path.insert(0,str(Path(__file__).resolve().parents[1]/'self-host'))
import ratefreak as operator
from acceptance import Client,seed


def image_selection(identities,preferred=None):
    selected={}
    for name,image_id in identities.items():
        metadata=json.loads(operator.execute(['docker','image','inspect',image_id],capture=True))[0]
        tags=[tag for tag in metadata.get('RepoTags',[]) if tag.startswith('ratefreak-'+name+':')]
        if preferred:tags.sort(key=lambda tag:not tag.endswith(':'+preferred))
        if not tags or metadata['Id']!=image_id:raise ValueError('Missing acceptance image identity.')
        selected[name]={'id':image_id,'tag':tags[0]}
    if set(selected)!={'api','web','tools'}:raise ValueError('Incomplete acceptance image set.')
    return {'format':1,'images':selected}


def fingerprints(command,env):
    def query(sql):
        result=subprocess.run(command+['exec','-T','postgres','psql','-X','-q','-A','-t','-v','ON_ERROR_STOP=1','-U','postgres','-d','ratefreak'],env=env,input=sql,text=True,stdout=subprocess.PIPE,stderr=subprocess.PIPE,check=True)
        return result.stdout.strip()
    tables=query("SELECT tablename FROM pg_tables WHERE schemaname='public' ORDER BY tablename;").splitlines()
    result={}
    for table in tables:
        quoted='"'+table.replace('"','""')+'"'
        result[table]=query('SELECT count(*)::text || \':\' || coalesce(md5(string_agg(to_jsonb(t)::text, chr(10) ORDER BY to_jsonb(t)::text)), md5(\'\')) FROM '+quoted+' t;')
    return result


def run(identities,reports,baseline=None,keep=False):
    root=Path(tempfile.mkdtemp(prefix='ratefreak-release-acceptance-'));root.chmod(0o700)
    with socket.socket() as listener:listener.bind(('127.0.0.1',0));port=listener.getsockname()[1]
    origin='https://localhost:'+str(port)
    config=operator.initialize(root,origin,True)
    selection=image_selection(baseline or identities,'release-baseline' if baseline else 'release-proof')
    operator.write_state(root/'release-selection.json',selection)
    # An isolated edge with its own CA/port; no host 80/443 or developer web port.
    caddy={'image':'caddy:2@sha256:ce773ec7a742ce15ee6bf2bb5f65882b9e2a4487b10733e6f1484f86e6b5d8bc','entrypoint':['/bin/sh','/etc/ratefreak/edge-entrypoint.sh'],
           'ports':[f'127.0.0.1:{port}:{port}'],'environment':{'RATEFREAK_PUBLIC_ORIGIN':origin,'RATEFREAK_CADDY_TLS':'tls internal'},
           'volumes':[str(operator.PACKAGE/'Caddyfile')+':/etc/caddy/Caddyfile:ro',str(operator.PACKAGE/'edge-entrypoint.sh')+':/etc/ratefreak/edge-entrypoint.sh:ro',str(root/'runtime/web/ingress')+':/run/ratefreak/ingress:ro','release_ca:/data'],
           'networks':['ingress'],'depends_on':{'web':{'condition':'service_healthy'}}}
    operator.write_state(root/'compose.override.yaml',{'services':{'caddy':caddy,'api':{'environment':{'Updates__Enabled':'false'}}},'volumes':{'release_ca':{}}})
    command,env=operator.compose(root,config)
    def cli(action):
        operator.dispatch(argparse.Namespace(command=action),root,config,command,env)
    result={'candidateImages':identities,'baselineImages':baseline,'https':False,'freshInstall':baseline is None,'upgrade':False}
    try:
        cli('prepare');cli('migrate');operator.require_release(root,operator.release_images(root))
        operator.execute(command+['up','-d','--wait','api','web','caddy'],env)
        ca=root/'ca.crt'
        for attempt in range(40):
            copy=subprocess.run(command+['cp','caddy:/data/caddy/pki/authorities/local/root.crt',str(ca)],env=env,stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL)
            if copy.returncode==0:break
            time.sleep(.5)
        else:raise ValueError('Disposable HTTPS certificate was not ready.')
        for attempt in range(40):
            try:Client(origin,ca).call('setup/status');break
            except OSError:time.sleep(.5)
        seed(root,origin,ca)
        state=json.loads((root/'acceptance.json').read_text());password=state['password']
        owner=Client(origin,ca);owner.login('selfhost_owner',password)
        owner.call('users/me/library/'+state['public']['id'],'PUT',{'status':'completed','completedAt':'2026-11-20'})
        before=fingerprints(command,env)
        if baseline:
            operator.execute(command+['stop','api','web','caddy'],env)
            # Preserve a real coordinated synthetic-data backup outside public reports.
            operator.dispatch(argparse.Namespace(command='backup',output=root/'backup'),root,config,command,env)
            operator.write_state(root/'release-selection.json',image_selection(identities,'release-proof'))
            try:operator.require_release(root,operator.release_images(root))
            except operator.OperatorError:pass
            else:raise AssertionError('Old migration acceptance survived release replacement.')
            command,env=operator.compose(root,config);cli('prepare');cli('migrate')
            operator.require_release(root,operator.release_images(root));operator.execute(command+['up','-d','--wait','api','web','caddy'],env)
            assert before==fingerprints(command,env),'Upgrade changed existing database content'
            result['upgrade']=True
        admin=Client(origin,ca);admin.login('selfhost_admin',password)
        response,headers=admin.call('admin/updates');status=json.loads(response)
        assert status['state']=='disabled' and status['enabled'] is False and 'no-store' in headers['Cache-Control']
        admin.call('admin/updates/check','POST',{})
        owner=Client(origin,ca);owner.login('selfhost_owner',password);owner.call('admin/updates',expected=403)
        assert json.loads(owner.call('users/me/library/'+state['public']['id'])[0])['status']=='completed'
        owner.call(state['public']['imageReference'].removeprefix('/api/v1/'))
        admin.call('auth/logout','POST',{},204);admin.call('admin/updates',expected=401)
        result.update(https=True,disabledUpdateChecks=True,ordinaryDenied=True,logoutDenied=True,dataPreserved=True)
        reports.mkdir(parents=True,exist_ok=True);(reports/'acceptance.json').write_text(json.dumps(result,indent=2)+'\n')
        if keep:
            # Private path/credentials retained only for the subsequent local browser proof.
            (root/'browser-fixture.json').write_text(json.dumps({'origin':origin,'password':password,'ca':str(ca)})+'\n');(root/'browser-fixture.json').chmod(0o600)
            print('Disposable browser fixture: '+str(root),flush=True)
        return root,result
    finally:
        if not keep or not result['https']:operator.execute(command+['down','--volumes','--remove-orphans'],env)
        # Root-owned synthetic storage is deliberately retained for diagnosis; never
        # put private keys/passwords/backups into candidate verification reports.

if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--confirm-disposable',required=True,action='store_true');parser.add_argument('--report-directory',required=True,type=Path);parser.add_argument('--images-json',required=True,type=Path);parser.add_argument('--baseline-images-json',type=Path);parser.add_argument('--keep-disposable',action='store_true');args=parser.parse_args()
    images=json.loads(args.images_json.read_text());baseline=json.loads(args.baseline_images_json.read_text()) if args.baseline_images_json else None
    run(images,args.report_directory,baseline,args.keep_disposable)
