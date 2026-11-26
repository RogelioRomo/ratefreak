from pathlib import Path
import json,subprocess,hashlib
b=Path(__file__).resolve().parent;root=b.parents[1];images={k:v['image_id'] for k,v in json.loads((root/'.local/security-runtime-guard-2026-11-25/residual-probes/probe-executions.json').read_text()).items()};rows={}
for name,id in images.items():
 cmd=['docker','run','--rm','--network','none','--read-only','--cap-drop','ALL','--security-opt','no-new-privileges:true','--env','PYTHONDONTWRITEBYTECODE=1','--entrypoint','python3','--mount',f'type=bind,src={b}/config_probe.py,dst=/probe.py,readonly',id,'/probe.py']
 p=subprocess.run(cmd,text=True,capture_output=True);(b/('final-config-'+name+'-probe.log')).write_text(p.stderr);p.check_returncode();d=json.loads(p.stdout);d['image_id']=id;(b/('final-config-'+name+'-probe.json')).write_text(json.dumps(d,indent=2)+'\n');rows[name]={'image_id':id,'command':cmd,'exit_code':p.returncode,'sha256':hashlib.sha256((b/('final-config-'+name+'-probe.json')).read_bytes()).hexdigest()};print(name,flush=True)
(b/'final-config-executions.json').write_text(json.dumps(rows,indent=2)+'\n')
