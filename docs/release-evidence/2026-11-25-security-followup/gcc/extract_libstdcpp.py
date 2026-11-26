from pathlib import Path
import subprocess,json,hashlib
b=Path(__file__).resolve().parent;images=json.loads((b.parent/'images/built-images.json').read_text());out=[]
for name,image in images.items():
 c=subprocess.check_output(['docker','create','--entrypoint','/bin/true',image],text=True).strip()
 try:
  p=b/(name+'-libstdc++.so.6');subprocess.run(['docker','cp','-L',c+':/usr/lib/x86_64-linux-gnu/libstdc++.so.6',str(p)],check=True)
 finally:subprocess.run(['docker','rm',c],stdout=subprocess.DEVNULL,check=True)
 sha=hashlib.sha256(p.read_bytes()).hexdigest()
 dis=subprocess.check_output(['objdump','-d','--disassemble=_ZnwmSt11align_val_t',str(p)],text=True);(b/(name+'-aligned-new.txt')).write_text(dis)
 assert 'posix_memalign@plt' in dis,'Unexpected aligned allocation implementation'
 out.append({'image':name,'image_id':image,'path':'/usr/lib/x86_64-linux-gnu/libstdc++.so.6','sha256':sha,'aligned_new_calls_posix_memalign':True,'disassembly':name+'-aligned-new.txt'})
(b/'libstdcpp-binding.json').write_text(json.dumps(out,indent=2)+'\n');print(json.dumps(out,indent=2))
