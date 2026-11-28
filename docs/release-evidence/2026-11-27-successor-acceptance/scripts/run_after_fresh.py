from pathlib import Path
import datetime,json,os,subprocess,time
BASE=Path(__file__).resolve().parent
environment=os.environ.copy();environment['PYTHONDONTWRITEBYTECODE']='1'
while not (BASE/'fresh-root.txt').is_file():
 if (BASE/'initial.log').exists() and 'Traceback (most recent call last)' in (BASE/'initial.log').read_text():raise RuntimeError('Fresh shipped CLI failed; inspect private initial/fresh logs.')
 time.sleep(15)
phases=['run_browser','runtime','prepare_capacity','capacity','maintenance'];completed=[]
for phase in phases:
 (BASE/'phase.json').write_text(json.dumps({'running':phase,'completed':completed,'utc':datetime.datetime.now(datetime.timezone.utc).isoformat()})+'\n')
 with (BASE/(phase+'.log')).open('w') as log:subprocess.run(['python3',str(BASE/(phase+'.py'))],env=environment,stdout=log,stderr=subprocess.STDOUT,check=True)
 completed.append(phase);print('Successor '+phase+' passed.',flush=True)
(BASE/'operational-complete.json').write_text(json.dumps({'completed':completed,'utc':datetime.datetime.now(datetime.timezone.utc).isoformat()})+'\n')
