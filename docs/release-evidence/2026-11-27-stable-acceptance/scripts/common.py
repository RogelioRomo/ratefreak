from pathlib import Path
import importlib.util,json,subprocess,argparse,os
BASE=Path(__file__).resolve().parent;ROOT=BASE.parents[2];SOURCE=ROOT/'.local/stable-candidate-2026-11-27/source-qualified'
spec=importlib.util.spec_from_file_location('release_acceptance',SOURCE/'deploy/release/acceptance.py');a=importlib.util.module_from_spec(spec);spec.loader.exec_module(a)
root=Path((BASE/os.environ.get('RATEFREAK_ACCEPTANCE_ROOT_FILE','fresh-root.txt')).read_text().strip());config=json.loads((root/'instance.json').read_text());command,env=a.operator.compose(root,config)
fixture=json.loads((root/'browser-fixture.json').read_text());state=json.loads((root/'acceptance.json').read_text());images=json.loads((ROOT/'.local/stable-candidate-2026-11-27/candidate-qualified/built-images.json').read_text())
def sql(text,cmd=command,environ=env):
 return subprocess.run(cmd+['exec','-T','postgres','psql','-X','-q','-A','-t','-v','ON_ERROR_STOP=1','-U','postgres','-d','ratefreak'],env=environ,input=text,text=True,capture_output=True,check=True).stdout.strip()
def cli(name,**kw):a.operator.dispatch(argparse.Namespace(command=name,**kw),root,config,command,env)
def client(handle='selfhost_owner',password=state['password']):
 c=a.Client(fixture['origin'],Path(fixture['ca']));c.login(handle,password);return c
