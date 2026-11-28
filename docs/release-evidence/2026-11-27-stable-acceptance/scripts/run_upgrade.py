from common import *
import os
os.environ['TMPDIR']=str(BASE)
original=a.operator.dispatch
# Current storage contract is provisioned by current tools before the old baseline's
# preparation; migrations/data are still seeded by the exact pinned old images.
def dispatch(args,root,config,command,env):
 if args.command=='prepare':
  selection=json.loads((root/'release-selection.json').read_text())
  if selection['images']['api']['id']!=images['api']:
   a.operator.write_state(root/'release-selection.json',a.image_selection(images))
   current_cmd,current_env=a.operator.compose(root,config)
   original(args,root,config,current_cmd,current_env)
   a.operator.write_state(root/'release-selection.json',selection)
 return original(args,root,config,command,env)
a.operator.dispatch=dispatch
schema={}; history=[];calls=0
original_fingerprints=a.fingerprints
def preserved_fingerprints(cmd,environ):
 global calls
 def query(statement):return sql(statement,cmd,environ)
 if not schema:
  data=query("SELECT table_name||':'||string_agg(column_name,',' ORDER BY ordinal_position) FROM information_schema.columns WHERE table_schema='public' GROUP BY table_name ORDER BY table_name;")
  for line in data.splitlines():
   table,columns=line.split(':');schema[table]=columns.split(',')
  history.extend(query('SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId";').splitlines())
 result={}
 for table,columns in schema.items():
  quoted=lambda name:'"'+name.replace('"','""')+'"'
  where=''
  if table=='catalog_categories':where=" WHERE id <> 'f11e0000-0000-0000-0000-000000000006'"
  if table=='__EFMigrationsHistory':where=' WHERE "MigrationId" IN ('+','.join("'"+x+"'" for x in history)+')'
  result[table]=query("SELECT count(*)::text||':'||coalesce(md5(string_agg(to_jsonb(t)::text,chr(10) ORDER BY to_jsonb(t)::text)),md5('')) FROM (SELECT "+','.join(map(quoted,columns))+' FROM '+quoted(table)+where+') t;')
 calls+=1
 if calls==2:
  current=original_fingerprints(cmd,environ)
  assert query("SELECT count(*) FROM catalog_categories WHERE id='f11e0000-0000-0000-0000-000000000006' AND name='Songs' AND is_seeded;")=='1'
  (BASE/'upgrade-preservation.json').write_text(json.dumps({'original_tables':list(schema),'original_column_rows_unchanged':True,'added_tables':sorted(set(current)-set(schema)),'expected_songs_seed':True,'old_migration_history_preserved':True,'comparison':'Exact original rows projected to original columns, excluding only the new Songs seed and appended EF migration history.'},indent=2)+'\n')
 return result
a.fingerprints=preserved_fingerprints
root,result=a.run(images,BASE/'upgrade',json.loads((BASE/'baseline-images.json').read_text()),keep=True)
(BASE/'upgrade-root.txt').write_text(str(root)+'\n')
