from pathlib import Path
import subprocess,json,time
B=Path(__file__).resolve().parent;IMAGE='postgres:17-trixie@sha256:87c8b42f423b45d5247e6aa8fdb2e7762fec164c68a50fe3eb07e7bfcaf4dbcf'
ctr=subprocess.check_output(['docker','run','--detach','--rm','--network','none','--tmpfs','/var/lib/postgresql/data:uid=999,gid=999','--env','POSTGRES_PASSWORD=disposable-jit-fixture-only',IMAGE],text=True).strip()
result={'image_reference':IMAGE,'persistent_instance_changed':False,'configuration':'Official defaults; no application volumes, external network or ports.'}
try:
 for _ in range(45):
  r=subprocess.run(['docker','exec',ctr,'pg_isready','--quiet','-U','postgres'],capture_output=True)
  phase=subprocess.run(['docker','exec',ctr,'cat','/proc/1/comm'],capture_output=True,text=True)
  if r.returncode==0 and phase.stdout.strip()=='postgres':break
  time.sleep(1)
 else:raise RuntimeError('Disposable PostgreSQL did not initialize.')
 def query(sql):return subprocess.check_output(['docker','exec',ctr,'psql','-X','-q','-A','-t','-v','ON_ERROR_STOP=1','-U','postgres','-c',sql],text=True).strip()
 result['jit']=query('SHOW jit');result['jit_provider']=query('SHOW jit_provider');result['pg_jit_available']=query('SELECT pg_jit_available()')
 plan=query('SET jit_above_cost=0; SET jit_inline_above_cost=0; SET jit_optimize_above_cost=0; EXPLAIN (ANALYZE,FORMAT JSON) SELECT sum(i::bigint) FROM generate_series(1,10000) AS g(i);')
 result['forced_cost_query_plan']=json.loads(plan)
 result['query_result']=query('SELECT sum(i::bigint) FROM generate_series(1,10000) AS g(i);')
 result['defaults_disable_jit']=result['jit']=='off'
 print(json.dumps(result,indent=2),flush=True)
finally:
 subprocess.run(['docker','rm','--force','--volumes',ctr],stdout=subprocess.DEVNULL,check=True)
 result['fixture_removed']=True;(B/'default-probe.json').write_text(json.dumps(result,indent=2)+'\n')
