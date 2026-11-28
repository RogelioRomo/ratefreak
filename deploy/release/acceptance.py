#!/usr/bin/env python3
"""Disposable HTTPS install/upgrade acceptance; no developer instance access."""
import argparse
from collections import Counter
import hashlib
import json
import os
from pathlib import Path
import secrets
import socket
import subprocess
import sys
import tempfile
import tarfile
import time
sys.path.insert(0,str(Path(__file__).resolve().parents[1]/'self-host'))
import ratefreak as operator
from acceptance import Client,seed


def image_selection(identities,preferred=None,reference_images=None):
    if set(identities)!={'api','web','tools'}:raise ValueError('Incomplete acceptance image set.')
    if reference_images is not None and set(reference_images)!={'postgres','caddy'}:
        raise ValueError('Incomplete acceptance reference image set.')
    def selected(values):
        result={}
        for name,image_id in values.items():
            metadata=json.loads(operator.execute(['docker','image','inspect',image_id],capture=True))[0]
            tags=[tag for tag in metadata.get('RepoTags',[]) if tag.startswith('ratefreak-'+name+':')]
            if preferred:tags.sort(key=lambda tag:not tag.endswith(':'+preferred))
            if not tags or metadata['Id']!=image_id:raise ValueError('Missing acceptance image identity.')
            result[name]={'id':image_id,'tag':tags[0]}
        return result
    result={'format':1,'images':selected(identities)}
    if reference_images is not None:result['reference_images']=selected(reference_images)
    return result


SONGS_ID='f11e0000-0000-0000-0000-000000000006'
UPGRADE_MIGRATIONS={'20261124233146_AddTmdbContentLifecycle',
                    '20261127171719_AddIgdbContentLifecycle','20261127222703_AddSongs'}
LIFECYCLE_COLUMNS={provider+'_'+suffix:columns for provider in ['tmdb','igdb'] for suffix,columns in [
    ('lifecycle_states',['mapping_id','next_attempt_at','last_attempt_at','failures','last_outcome','content_removed']),
    ('poster_identities',['mapping_id','filename']),
    ('refresh_schedule',['id','last_batch_at','next_allowed_at'])]}


def database_snapshot(command,env):
    """Private synthetic rows and original column order, never a public receipt."""
    def query(sql):
        return subprocess.run(command+['exec','-T','postgres','psql','-X','-q','-A','-t',
            '-v','ON_ERROR_STOP=1','-U','postgres','-d','ratefreak'],env=env,input=sql,text=True,
            stdout=subprocess.PIPE,stderr=subprocess.PIPE,check=True).stdout.strip()
    schema=json.loads(query("""SELECT coalesce(jsonb_object_agg(table_name,
        jsonb_build_object('columns', columns, 'column_types', column_types)), '{}'::jsonb)
        FROM (SELECT table_name, jsonb_agg(column_name ORDER BY ordinal_position) AS columns,
        jsonb_object_agg(column_name, jsonb_build_array(udt_name, is_nullable,
            character_maximum_length, numeric_precision, numeric_scale)) AS column_types
        FROM information_schema.columns WHERE table_schema='public' GROUP BY table_name) s;"""))
    quoted=lambda value:'"'+value.replace('"','""')+'"'
    result={}
    for name,definition in schema.items():
        columns=definition['columns']
        rows=json.loads(query("SELECT coalesce(jsonb_agg(to_jsonb(t)), '[]'::jsonb) FROM (SELECT "+
            ','.join(map(quoted,columns))+' FROM '+quoted(name)+') t;'))
        result[name]={**definition,'rows':rows}
    return result


def verify_upgrade_preservation(before,after):
    """Permit only the reviewed 9cc808a -> 0.1.0 migration/seed deltas."""
    added=set(after)-set(before)
    assert not set(before)-set(after),'Upgrade dropped an original table'
    assert added==set(LIFECYCLE_COLUMNS)-set(before),'Unreviewed upgrade table delta'
    multiset=lambda rows:Counter(json.dumps(row,sort_keys=True,separators=(',',':')) for row in rows)
    for name,original in before.items():
        assert original['columns']==after[name]['columns'],'Unreviewed original-column delta: '+name
        assert original.get('column_types')==after[name].get('column_types'),'Original column type/nullability changed: '+name
        rows=after[name]['rows']
        if name=='__EFMigrationsHistory':
            old_ids={row['MigrationId'] for row in original['rows']}
            new_rows=[row for row in rows if row['MigrationId'] not in old_ids]
            expected=UPGRADE_MIGRATIONS-old_ids
            assert len(new_rows)==len(expected) and {row['MigrationId'] for row in new_rows}==expected,'Unexpected migration history'
            rows=[row for row in rows if row['MigrationId'] in old_ids]
        if name=='catalog_categories' and not any(row['id']==SONGS_ID for row in original['rows']):
            seeds=[row for row in rows if row['id']==SONGS_ID]
            required={'id':SONGS_ID,'name':'Songs','normalized_name':'SONGS','scope':'public',
                      'is_hidden':False,'is_seeded':True,'revision':1,'owner_user_id':None}
            assert len(seeds)==1 and all(seeds[0].get(key)==value for key,value in required.items()),'Invalid Songs seed'
            rows=[row for row in rows if row['id']!=SONGS_ID]
        # Compare exact multisets, including extra/missing/duplicated rows.
        projected=lambda values:[{column:row.get(column) for column in original['columns']} for row in values]
        assert multiset(projected(original['rows']))==multiset(projected(rows)),'Upgrade changed original rows: '+name
    for name in added:
        assert after[name]['columns']==LIFECYCLE_COLUMNS[name],'Unreviewed lifecycle schema: '+name
        expected=[{'id':1,'last_batch_at':None,'next_allowed_at':None}] if name.endswith('_refresh_schedule') else []
        assert multiset(after[name]['rows'])==multiset(expected),'Unexpected new lifecycle data: '+name
    return {'original_table_count':len(before),'original_column_rows_preserved':True,
            'added_tables':sorted(added),'songs_seed_verified':True,'original_migration_history_preserved':True}


def prepare_candidate_storage(command,env,candidate_selection):
    # Historical tools cannot provision the current Compose's independent policy binds.
    current_env=dict(env);current_env['RATEFREAK_TOOLS_IMAGE']=candidate_selection['images']['tools']['id']
    operator.execute(command+['run','--rm','--no-deps','storage-init'],current_env)


def verify_backup_storage(backup,command,env):
    expected={}
    for name in ['artwork','keys']:
        with tarfile.open(backup/(name+'.tar')) as archive:
            expected[name]={member.name:hashlib.sha256(archive.extractfile(member).read()).hexdigest()
                            for member in archive if member.isfile()}
    script="""import hashlib,json
from pathlib import Path
print(json.dumps({name:{str(p.relative_to(root)):hashlib.sha256(p.read_bytes()).hexdigest()
    for p in root.rglob('*') if p.is_file()} for name,root in [('artwork',Path('/data/artwork')),('keys',Path('/data/keys'))]}))"""
    actual=json.loads(subprocess.check_output(command+['run','--rm','--no-deps','--user','0:0',
        '--entrypoint','python3','tools','-c',script],env=env,text=True))
    for name,files in expected.items():
        assert all(actual.get(name,{}).get(path)==digest for path,digest in files.items()),'Original '+name+' bytes changed'


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


def run(identities,reports,baseline=None,keep=False,reference_images=None):
    root=Path(tempfile.mkdtemp(prefix='ratefreak-release-acceptance-'));root.chmod(0o700)
    with socket.socket() as listener:listener.bind(('127.0.0.1',0));port=listener.getsockname()[1]
    origin='https://localhost:'+str(port)
    config=operator.initialize(root,origin,True)
    selection=image_selection(baseline or identities,'release-baseline' if baseline else 'release-proof',reference_images)
    operator.write_state(root/'release-selection.json',selection)
    # An isolated edge with its own CA/port; no host 80/443 or developer web port.
    caddy={'image':reference_images['caddy'] if reference_images is not None else 'caddy:2@sha256:ce773ec7a742ce15ee6bf2bb5f65882b9e2a4487b10733e6f1484f86e6b5d8bc','entrypoint':['/bin/sh','/etc/ratefreak/edge-entrypoint.sh'],
           'ports':[f'127.0.0.1:{port}:{port}'],'environment':{'RATEFREAK_PUBLIC_ORIGIN':origin,'RATEFREAK_CADDY_TLS':'tls internal'},
           'volumes':[str(operator.PACKAGE/'Caddyfile')+':/etc/caddy/Caddyfile:ro',str(operator.PACKAGE/'edge-entrypoint.sh')+':/etc/ratefreak/edge-entrypoint.sh:ro',str(root/'runtime/web/ingress')+':/run/ratefreak/ingress:ro','release_ca:/data'],
           'networks':['ingress'],'depends_on':{'web':{'condition':'service_healthy'}}}
    operator.write_state(root/'compose.override.yaml',{'services':{'caddy':caddy,'api':{'environment':{'Updates__Enabled':'false'}}},'volumes':{'release_ca':{}}})
    command,env=operator.compose(root,config)
    def cli(action):
        operator.dispatch(argparse.Namespace(command=action),root,config,command,env)
    result={'candidateImages':identities,'baselineImages':baseline,'https':False,'freshInstall':baseline is None,'upgrade':False}
    if reference_images is not None:
        assert operator.selected_reference_images(root)==reference_images,'Selected reference images differ from requested identities'
        result['referenceImages']=dict(reference_images)
    try:
        if baseline:prepare_candidate_storage(command,env,image_selection(identities,'release-proof',reference_images))
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
        if baseline:
            operator.execute(command+['stop','api','web','caddy'],env)
            before=database_snapshot(command,env)
            # Preserve a real coordinated synthetic-data backup outside public reports.
            operator.dispatch(argparse.Namespace(command='backup',output=root/'backup'),root,config,command,env)
            operator.write_state(root/'release-selection.json',image_selection(identities,'release-proof',reference_images))
            if reference_images is not None:
                assert operator.selected_reference_images(root)==reference_images,'Upgrade changed reference selection'
            try:operator.require_release(root,operator.release_images(root))
            except operator.OperatorError:pass
            else:raise AssertionError('Old migration acceptance survived release replacement.')
            command,env=operator.compose(root,config);cli('prepare');cli('migrate')
            operator.require_release(root,operator.release_images(root))
            result['upgradePreservation']=verify_upgrade_preservation(before,database_snapshot(command,env))
            verify_backup_storage(root/'backup',command,env)
            result['upgradePreservation']['originalArtworkAndKeysPreserved']=True
            operator.execute(command+['up','-d','--wait','api','web','caddy'],env)
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

def argument_parser():
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--confirm-disposable',required=True,action='store_true');parser.add_argument('--report-directory',required=True,type=Path);parser.add_argument('--images-json',required=True,type=Path);parser.add_argument('--baseline-images-json',type=Path);parser.add_argument('--reference-images-json',type=Path);parser.add_argument('--keep-disposable',action='store_true')
    return parser

if __name__=='__main__':
    args=argument_parser().parse_args()
    images=json.loads(args.images_json.read_text());baseline=json.loads(args.baseline_images_json.read_text()) if args.baseline_images_json else None
    references=json.loads(args.reference_images_json.read_text()) if args.reference_images_json else None
    run(images,args.report_directory,baseline,args.keep_disposable,references)
