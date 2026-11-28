from pathlib import Path
import datetime,json,os,subprocess
BASE=Path(__file__).resolve().parent;environment=os.environ.copy();environment['PYTHONDONTWRITEBYTECODE']='1'
completed=['run_browser']
assert json.loads((BASE/'browser-results.json').read_text())['tlsVerified']
for phase in ['runtime','prepare_capacity','capacity','maintenance']:
 (BASE/'phase.json').write_text(json.dumps({'running':phase,'completed':completed,'utc':datetime.datetime.now(datetime.timezone.utc).isoformat()})+'\n')
 with (BASE/'HANDOFF.md').open('a') as handoff:handoff.write('\nRunning '+phase+'; completed '+', '.join(completed)+'.\n')
 with (BASE/(phase+'.log')).open('w') as log:subprocess.run(['python3',str(BASE/(phase+'.py'))],env=environment,stdout=log,stderr=subprocess.STDOUT,check=True)
 completed.append(phase);print('Successor '+phase+' passed.',flush=True)
(BASE/'operational-complete.json').write_text(json.dumps({'completed':completed,'utc':datetime.datetime.now(datetime.timezone.utc).isoformat()})+'\n')
with (BASE/'HANDOFF.md').open('a') as handoff:handoff.write('\nAll operational phases passed. Next finalize.py for upgrade-project cleanup and sanitized export.\n')
