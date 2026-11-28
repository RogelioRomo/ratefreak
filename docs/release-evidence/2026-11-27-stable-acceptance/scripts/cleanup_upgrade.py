from common import *
uproot=Path((BASE/'upgrade-root.txt').read_text().strip());config=json.loads((uproot/'instance.json').read_text());cmd,environ=a.operator.compose(uproot,config)
verify='import tarfile,hashlib; from pathlib import Path; pairs=[("artwork.tar","/data/artwork"),("keys.tar","/data/keys")]; [(None if hashlib.sha256(arc.extractfile(m).read()).digest()==hashlib.sha256((Path(target)/m.name).read_bytes()).digest() else (_ for _ in ()).throw(AssertionError(m.name))) for name,target in pairs for arc in [tarfile.open("/backup/"+name)] for m in arc.getmembers()]'
try:
 subprocess.run(cmd+['run','--rm','--no-deps','--user','0:0','--entrypoint','python3','-v',str(uproot/'backup')+':/backup:ro','tools','-c',verify],env=environ,check=True)
 out=json.loads((BASE/'upgrade-preservation.json').read_text());out['original_artwork_and_key_bytes_preserved']=True;(BASE/'upgrade-preservation.json').write_text(json.dumps(out,indent=2)+'\n')
finally:subprocess.run(cmd+['down','--volumes','--remove-orphans'],env=environ,check=True)
