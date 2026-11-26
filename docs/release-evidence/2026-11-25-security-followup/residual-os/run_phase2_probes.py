from pathlib import Path
import json,subprocess,hashlib
b=Path(__file__).resolve().parent;root=b.parents[1];images=json.loads((root/'.local/security-skia-update-2026-11-25/images/built-images.json').read_text());rows={}
for name,id in images.items():
 cmd=['docker','run','--rm','--network','none','--read-only','--cap-drop','ALL','--security-opt','no-new-privileges:true','--env','PYTHONDONTWRITEBYTECODE=1','--entrypoint','python3','--mount',f'type=bind,src={b}/phase2_probe.py,dst=/probe.py,readonly',id,'/probe.py']
 p=subprocess.run(cmd,text=True,capture_output=True);(b/('phase2-'+name+'-probe.log')).write_text(p.stderr);p.check_returncode();d=json.loads(p.stdout);d['image_id']=id;(b/('phase2-'+name+'-probe.json')).write_text(json.dumps(d,indent=2)+'\n');rows[name]={'image_id':id,'command':cmd,'exit_code':p.returncode,'elf_files':d['elf_files'],'sha256':hashlib.sha256((b/('phase2-'+name+'-probe.json')).read_bytes()).hexdigest()};print(name,d['elf_files'],flush=True)
(b/'phase2-probe-executions.json').write_text(json.dumps(rows,indent=2)+'\n')
