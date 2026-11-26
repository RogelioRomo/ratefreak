from pathlib import Path
import subprocess,json
b=Path(__file__).resolve().parent
images=json.loads((b.parent/'images/built-images.json').read_text());summary={}
for name,image in images.items():
 command=['docker','run','--rm','--network','none','--read-only','--cap-drop','ALL','--security-opt','no-new-privileges:true','--env','PYTHONDONTWRITEBYTECODE=1','--entrypoint','python3','--mount',f'type=bind,src={b}/probe.py,dst=/probe.py,readonly',image,'/probe.py']
 p=subprocess.run(command,capture_output=True,text=True);p.check_returncode();data=json.loads(p.stdout);data['image_id']=image
 (b/(name+'-probe.json')).write_text(json.dumps(data,indent=2)+'\n');summary[name]={'image_id':image,'elf_files':data['elf_files'],'exit_code':p.returncode};print(name,summary[name],flush=True)
(b/'probe-executions.json').write_text(json.dumps(summary,indent=2)+'\n')
