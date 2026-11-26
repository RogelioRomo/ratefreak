from pathlib import Path
import subprocess,json,hashlib,re
B=Path(__file__).resolve().parent; image='postgres:17-trixie@sha256:87c8b42f423b45d5247e6aa8fdb2e7762fec164c68a50fe3eb07e7bfcaf4dbcf';records={}
p=subprocess.run(['docker','run','--rm','--network','none','--read-only','--cap-drop','ALL','--entrypoint','/usr/lib/postgresql/17/bin/postgres',image,'--describe-config'],capture_output=True,text=True,check=True)
(B/'describe-config.txt').write_text(p.stdout)
records['describe_config_output']=[line for line in p.stdout.splitlines() if line.startswith(('ssl\t','wal_compression\t','shared_preload_libraries\t','session_preload_libraries\t','local_preload_libraries\t'))]
container=subprocess.check_output(['docker','create',image],text=True).strip()
try:
 for name,path in {'postgres':'/usr/lib/postgresql/17/bin/postgres','libpq':'/usr/lib/x86_64-linux-gnu/libpq.so.5','llvmjit':'/usr/lib/postgresql/17/lib/llvmjit.so'}.items():
  dest=B/name;subprocess.run(['docker','cp','-L',container+':'+path,str(dest)],check=True,stdout=subprocess.DEVNULL)
  result=subprocess.run(['readelf','--wide','--dyn-syms',str(dest)],capture_output=True,text=True,check=True)
  undefined=sorted({line.split()[-2 if line.rstrip().endswith(')') else -1].split('@')[0] for line in result.stdout.splitlines() if ' UND ' in line})
  (B/(name+'-symbols.txt')).write_text(result.stdout)
  needed=subprocess.check_output(['readelf','--wide','--dynamic',str(dest)],text=True)
  records[name]={'path':path,'sha256':hashlib.sha256(dest.read_bytes()).hexdigest(),'undefined_symbols':undefined,'needed':[line.strip() for line in needed.splitlines() if '(NEEDED)' in line]}
finally:subprocess.run(['docker','rm',container],check=True,stdout=subprocess.DEVNULL)
records['image_reference']=image
(B/'probe.json').write_text(json.dumps(records,indent=2)+'\n')
print(records['describe_config_output'])
