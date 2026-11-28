from common import *
result=json.loads((BASE/'maintenance-acceptance.json').read_text())
restore_command=None
backup=BASE/'maintenance-backup';journal=BASE/'current-journal.jsonl'
records=[json.loads(line) for line in journal.read_text().splitlines()]
assert len(records)==2 and len({x['AccountId'] for x in records})==1
victim_id=records[0]['AccountId']
try:
 target=BASE/'maintenance-restore';restore_config=a.operator.initialize(target,'https://restore-stable-acceptance.example',True);a.operator.write_state(target/'release-selection.json',a.image_selection(images))
 restore_command,restore_env=a.operator.compose(target,restore_config)
 def restore_cli(name,**kw):a.operator.dispatch(argparse.Namespace(command=name,**kw),target,restore_config,restore_command,restore_env)
 restore_cli('prepare');restore_cli('restore',backup=backup,journal=journal,tmdb_policy=BASE/'current-tmdb-policy.json',igdb_policy=BASE/'current-igdb-policy.json',confirm_restore=True)
 before=a.fingerprints(restore_command,restore_env)
 items={kind:sql("SELECT id FROM media_items WHERE title='Synthetic "+kind+" preservation';",restore_command,restore_env) for kind in ['tmdb','igdb','song']}
 song_before=sql(f"SELECT to_jsonb(t)::text FROM media_items t WHERE id='{items['song']}';",restore_command,restore_env)
 owner_tracking=sql(f"SELECT coalesce(jsonb_agg(to_jsonb(t) ORDER BY media_item_id),'[]'::jsonb)::text FROM user_media t WHERE user_id='{state['owner']}';",restore_command,restore_env)
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
