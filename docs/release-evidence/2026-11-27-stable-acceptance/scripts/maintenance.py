from common import *
import uuid,hashlib
result={'candidate_images':images,'complete':False,'synthetic_only':True}
restore_command=None
try:
 # Recovery and persistence on a separate ordinary fixture account.
 c=client('capacity_0');code=json.loads(c.call('auth/recovery-code','POST',{'password':state['password']})[0])['recoveryCode']
 owner=client();owner.call('users/me/library/'+state['public']['id'],'PUT',{'status':'completed','completedAt':'2026-11-20'})
 owner.call(state['public']['imageReference'].removeprefix('/api/v1/'))
 subprocess.run(command+['restart','postgres','api','web','caddy'],env=env,check=True)
 subprocess.run(command+['up','-d','--wait','api','web','caddy'],env=env,check=True)
 owner.call('users/me');assert json.loads(owner.call('users/me/library/'+state['public']['id'])[0])['status']=='completed'
 recovery=a.Client(fixture['origin'],Path(fixture['ca']));recovery.token();recovery.call('auth/reset-password','POST',{'handle':'capacity_0','recoveryCode':code,'password':state['password']})
 c.call('users/me',expected=401)
 recovery.call('auth/reset-password','POST',{'handle':'capacity_0','recoveryCode':code,'password':state['password']},400)
 client('capacity_0').call('users/me')
 result.update(recovery_single_use=True,recovery_old_session_invalidated=True,restart_database_keys_session_artwork_persisted=True)
 # Synthetic catalog identities have never been imported from providers.
 owner=client();items={}
 for kind,category in [('tmdb','f11e0000-0000-0000-0000-000000000001'),('igdb','f11e0000-0000-0000-0000-000000000004'),('song','f11e0000-0000-0000-0000-000000000006')]:
  item=json.loads(owner.call('media','POST',{'title':'Synthetic '+kind+' preservation','description':'Local acceptance fixture; no provider content','categoryId':category,'visibility':'public'},201)[0]);items[kind]=item['id']
  owner.call('users/me/library/'+item['id'],'PUT',{'status':'completed','completedAt':'2026-11-20'})
 for kind,ns,external,typ in [('tmdb','movie','1900000001','movie'),('igdb','game','1900000002','video_game'),('song','recording','8dc3c120-64b1-4efc-a20c-6bfc18c0ab91','song')]:
  item_id=items[kind];provider='musicbrainz' if kind=='song' else kind
  sql(f"UPDATE media_items SET media_type='{typ}', provenance='system', creator_user_id=NULL, metadata='{{\"fixture\":true}}'::jsonb WHERE id='{item_id}'; INSERT INTO media_external_ids (id,media_item_id,provider,namespace,external_id,last_synced_at) VALUES ('{uuid.uuid4()}','{item_id}','{provider}','{ns}','{external}',now());")
 song_before=sql(f"SELECT to_jsonb(t)::text FROM media_items t WHERE id='{items['song']}';")
 # Enable deletion only on this fixture and take snapshot BEFORE later erasure/removals.
 subprocess.run(command+['stop','api','web','caddy'],env=env,check=True)
 cli('enable-deletion',confirm_independent_journal=True)
 command,env=a.operator.compose(root,config)
 # common.cli closes over its module command; use explicit current composition.
 def current_cli(name,**kw):a.operator.dispatch(argparse.Namespace(command=name,**kw),root,config,command,env)
 owner_tracking=sql(f"SELECT coalesce(jsonb_agg(to_jsonb(t) ORDER BY media_item_id),'[]'::jsonb)::text FROM user_media t WHERE user_id='{state['owner']}';")
 before=a.fingerprints(command,env);backup=BASE/'maintenance-backup';current_cli('backup',output=backup)
 subprocess.run(command+['up','-d','--wait','api','web','caddy'],env=env,check=True)
 victim=client('capacity_3');victim_id=json.loads(victim.call('users/me')[0])['id']
 victim.call('users/me/data/confirm','POST',{'operation':'delete','password':state['password']},204)
 victim.call('users/me/data/delete','POST',{'handle':'capacity_3'},204);victim.call('users/me',expected=401)
 subprocess.run(command+['stop','api','web','caddy'],env=env,check=True)
 current_cli('tmdb-remove',database_name='ratefreak',identity='movie:1900000001',terminate=False,poster=['synthetic-acceptance.jpg'],confirm_tmdb_removal=True)
 current_cli('igdb-remove',database_name='ratefreak',identity='game:1900000002',terminate=False,poster=['synthetic-acceptance.jpg'],confirm_igdb_removal=True)
 # Export only this fixture's protected current independent records, after snapshot.
 copy='import shutil,os; from pathlib import Path; pairs=[("/journal/account-erasures.jsonl","current-journal.jsonl"),("/independent/tmdb-policy.json","current-tmdb-policy.json"),("/independent-igdb/igdb-policy.json","current-igdb-policy.json")]; [(shutil.copyfile(src,"/audit/"+dst),os.chown("/audit/"+dst,1000 if dst=="current-journal.jsonl" else 10001,1000 if dst=="current-journal.jsonl" else 10001),os.chmod("/audit/"+dst,0o600)) for src,dst in pairs]'
 subprocess.run(command+['run','--rm','--no-deps','--user','0:0','--entrypoint','python3','-v',str(BASE)+':/audit','tools','-c',copy],env=env,check=True)
 journal=BASE/'current-journal.jsonl';assert len({json.loads(line)['AccountId'] for line in journal.read_text().splitlines()})==1
 counts=subprocess.check_output(command+['run','--rm','--no-deps','--entrypoint','python3','tools','-c',"import json; print(len(json.load(open('/independent/tmdb-policy.json'))['blockedIdentities']),len(json.load(open('/independent-igdb/igdb-policy.json'))['blockedIdentities']))"],env=env,text=True).strip();assert counts=='1 1'
 target=BASE/'maintenance-restore';restore_config=a.operator.initialize(target,'https://restore-stable-acceptance.example',True);a.operator.write_state(target/'release-selection.json',a.image_selection(images))
 restore_command,restore_env=a.operator.compose(target,restore_config)
 def restore_cli(name,**kw):a.operator.dispatch(argparse.Namespace(command=name,**kw),target,restore_config,restore_command,restore_env)
 restore_cli('prepare');restore_cli('restore',backup=backup,journal=journal,tmdb_policy=BASE/'current-tmdb-policy.json',igdb_policy=BASE/'current-igdb-policy.json',confirm_restore=True)
 assert (target/'restore-pending').exists()
 try:a.operator.require_release(target,a.operator.release_images(target))
 except a.operator.OperatorError:pass
 else:raise AssertionError('Restore startup guard missing')
 restore_cli('replay',database_name='ratefreak',confirm_erasure_replay=True)
 assert not (target/'restore-pending').exists()
 assert sql(f"SELECT count(*) FROM users WHERE id='{victim_id}';",restore_command,restore_env)=='0'
 for kind in ['tmdb','igdb']:
  assert sql(f"SELECT title||':'||(description IS NULL)::text||':'||metadata::text FROM media_items WHERE id='{items[kind]}';",restore_command,restore_env)=='Unavailable title:true:{}'
  assert sql(f"SELECT count(*) FROM media_external_ids WHERE media_item_id='{items[kind]}';",restore_command,restore_env)=='1'
 assert song_before==sql(f"SELECT to_jsonb(t)::text FROM media_items t WHERE id='{items['song']}';",restore_command,restore_env)
 assert owner_tracking==sql(f"SELECT coalesce(jsonb_agg(to_jsonb(t) ORDER BY media_item_id),'[]'::jsonb)::text FROM user_media t WHERE user_id='{state['owner']}';",restore_command,restore_env)
 after=a.fingerprints(restore_command,restore_env);restore_cli('replay',database_name='ratefreak',confirm_erasure_replay=True);assert after==a.fingerprints(restore_command,restore_env)
 # Library contents, unrelated rows and original synthetic protected artwork remain.
 unchanged={table for table in before if before[table]==after.get(table)}
 assert {'media_external_ids','catalog_categories','catalog_category_aliases','artwork_assets','__EFMigrationsHistory'}.issubset(unchanged),str(sorted(unchanged))
 verify='import tarfile,hashlib; from pathlib import Path; pairs=[("artwork.tar","/data/artwork"),("keys.tar","/data/keys")]; [(None if hashlib.sha256(arc.extractfile(m).read()).digest()==hashlib.sha256((Path(target)/m.name).read_bytes()).digest() else (_ for _ in ()).throw(AssertionError(m.name))) for name,target in pairs for arc in [tarfile.open("/backup/"+name)] for m in arc.getmembers()]'
 subprocess.run(restore_command+['run','--rm','--no-deps','--user','0:0','--entrypoint','python3','-v',str(backup)+':/backup:ro','tools','-c',verify],env=restore_env,check=True)
 manifest=json.loads((backup/'manifest.json').read_text())
 result.update(complete=True,nonempty_current_erasure_entries=2,erased_accounts=1,nonempty_current_tmdb_identities=1,nonempty_current_igdb_identities=1,current_policies_newer_than_snapshot=True,restore_startup_guard=True,repeated_replay_idempotent=True,song_full_row_preserved=True,provider_ids_and_user_tracking_preserved=True,unaffected_table_fingerprints_preserved=sorted(unchanged),table_count=len(before),artwork_key_archives_hash_verified=True,backup_images=manifest['images'],journal_excluded=not manifest['journalIncluded'])
finally:
 if restore_command is not None:subprocess.run(restore_command+['down','--volumes','--remove-orphans'],env=restore_env,check=True)
 subprocess.run(command+['down','--volumes','--remove-orphans'],env=env,check=True)
 (BASE/'maintenance-acceptance.json').write_text(json.dumps(result,indent=2)+'\n')
print('Persistence, recovery, coordinated backup and nonempty erasure/TMDB/IGDB isolated replay passed.')
