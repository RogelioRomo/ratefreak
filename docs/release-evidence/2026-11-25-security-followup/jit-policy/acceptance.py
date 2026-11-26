from pathlib import Path
import importlib.util,json,subprocess,hashlib
B=Path(__file__).resolve().parent;R=B.parents[2];images=json.loads((B.parent/'images/built-images.json').read_text())
spec=importlib.util.spec_from_file_location('acceptance',R/'deploy/release/acceptance.py');a=importlib.util.module_from_spec(spec);spec.loader.exec_module(a)
root,result=a.run(images,B/'fresh',keep=True)
config=json.loads((root/'instance.json').read_text());command,env=a.operator.compose(root,config)
proof={'application_images':images,'postgres_image':'postgres:17-trixie@sha256:87c8b42f423b45d5247e6aa8fdb2e7762fec164c68a50fe3eb07e7bfcaf4dbcf','configuration_sha256':hashlib.sha256((R/'deploy/self-host/compose.yaml').read_bytes()).hexdigest(),'configuration_scope':'Current Compose jit=off delta on the previously qualified ede1b61 application images; frozen installation archive remains historical until final candidate freeze.','persistent_instance_changed':False}
try:
 def query(sql):
  return subprocess.check_output(command+['exec','-T','postgres','psql','-X','-q','-A','-t','-v','ON_ERROR_STOP=1','-U','postgres','-d','ratefreak','-c',sql],env=env,text=True).strip()
 proof['jit']=query('SHOW jit');assert proof['jit']=='off'
 proof['ssl']=query('SHOW ssl');assert proof['ssl']=='off'
 proof['wal_compression']=query('SHOW wal_compression');assert proof['wal_compression']=='off'
 proof['pg_jit_available']=query('SELECT pg_jit_available()');assert proof['pg_jit_available']=='f'
 sql='SET jit_above_cost=0; SET jit_inline_above_cost=0; SET jit_optimize_above_cost=0; EXPLAIN (ANALYZE,FORMAT JSON) SELECT sum(i::bigint) FROM generate_series(1,10000) AS g(i);'
 proof['forced_cost_plan']=json.loads(query(sql));assert 'JIT' not in proof['forced_cost_plan'][0]
 proof['result']=query('SELECT sum(i::bigint) FROM generate_series(1,10000) AS g(i);');assert proof['result']=='50005000'
 # One backend tests compiler availability and the real query before reading its own mappings.
 proof['llvm_not_mapped']=query("SELECT pg_jit_available(); SELECT sum(i::bigint) FROM generate_series(1,10000) AS g(i); SELECT position('llvmjit' in pg_read_file('/proc/self/maps',0,1048576))=0 AND position('libLLVM' in pg_read_file('/proc/self/maps',0,1048576))=0;").splitlines()[-1]=='t';assert proof['llvm_not_mapped']
 proof['fresh_https_acceptance']=result['https'] and result['dataPreserved'];proof['passed']=True
finally:
 subprocess.run(command+['down','--volumes','--remove-orphans'],env=env,check=True)
 proof['fixture_removed']=True;(B/'acceptance.json').write_text(json.dumps(proof,indent=2)+'\n')
print('JIT off: real SQL/defaults/provider/forced-cost query/no LLVM mappings and fresh HTTPS application acceptance passed.')
