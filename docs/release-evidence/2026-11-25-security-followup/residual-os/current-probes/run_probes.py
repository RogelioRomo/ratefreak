from pathlib import Path
import subprocess,json,hashlib
b=Path(__file__).resolve().parent;root=b.parents[1];images=json.loads((b.parent/'images/built-images.json').read_text());executions={}
for name,image in images.items():
 cmd=['docker','run','--rm','--network','none','--read-only','--cap-drop','ALL','--security-opt','no-new-privileges:true','--env','PYTHONDONTWRITEBYTECODE=1','--entrypoint','python3','--mount',f'type=bind,src={b}/probe.py,dst=/probe.py,readonly',image,'/probe.py']
 p=subprocess.run(cmd,capture_output=True,text=True);(b/(name+'-probe.log')).write_text(p.stderr);p.check_returncode();d=json.loads(p.stdout);d['image_id']=image;(b/(name+'-probe.json')).write_text(json.dumps(d,indent=2)+'\n');executions[name]={'image_id':image,'command':cmd,'exit_code':p.returncode,'elf_files':d['elf_files'],'report_sha256':hashlib.sha256((b/(name+'-probe.json')).read_bytes()).hexdigest()};print(name,d['elf_files'],flush=True)
(b/'probe-executions.json').write_text(json.dumps(executions,indent=2)+'\n')
