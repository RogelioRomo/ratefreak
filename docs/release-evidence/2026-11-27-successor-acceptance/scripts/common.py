from pathlib import Path
import importlib.util,json,subprocess,argparse,os,sys
sys.dont_write_bytecode=True
BASE=Path(__file__).resolve().parent;ROOT=BASE.parents[2];SOURCE=BASE.parent/'successor-source';CANDIDATE=BASE.parent/'successor-candidate'
REVISION='9eea2c76550fe03920dd999f1677565e4f43d12b'
spec=importlib.util.spec_from_file_location('release_acceptance',SOURCE/'deploy/release/acceptance.py');a=importlib.util.module_from_spec(spec);spec.loader.exec_module(a)
root=Path((BASE/os.environ.get('RATEFREAK_ACCEPTANCE_ROOT_FILE','fresh-root.txt')).read_text().strip());config=json.loads((root/'instance.json').read_text());command,env=a.operator.compose(root,config)
fixture=json.loads((root/'browser-fixture.json').read_text());state=json.loads((root/'acceptance.json').read_text());images=json.loads((CANDIDATE/'built-images.json').read_text());references=json.loads((CANDIDATE/'built-reference-images.json').read_text())
assert a.operator.selected_reference_images(root)==references

def sql(text,cmd=command,environ=env):
 result=subprocess.run(cmd+['exec','-T','postgres','psql','-X','-q','-A','-t','-v','ON_ERROR_STOP=1','-U','postgres','-d','ratefreak'],env=environ,input=text,text=True,capture_output=True)
 if result.returncode:raise RuntimeError(result.stderr)
 return result.stdout.strip()
def cli(name,**kw):a.operator.dispatch(argparse.Namespace(command=name,**kw),root,config,command,env)
def client(handle='selfhost_owner',password=state['password']):
 c=a.Client(fixture['origin'],Path(fixture['ca']));c.login(handle,password);return c
