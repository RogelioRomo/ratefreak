import tarfile,re,json,hashlib
from pathlib import Path
root=Path('.local/security-skia-update-2026-11-25/glibc-callers');out=[]
for f in ['dotnet-runtime-a3788b7.tar.gz','node-v24.22.3.tar.xz']:
 rows=[];files=0
 with tarfile.open(root/f) as tar:
  for m in tar:
   if not m.isfile() or not m.name.endswith(('.c','.h','.cpp','.cc','.cs')):continue
   files+=1;s=tar.extractfile(m).read().decode(errors='replace')
   for n,l in enumerate(s.splitlines(),1):
    if re.search(r'\b(regcomp|regexec|glob(?:64)?|wordexp|[vfs]*wscanf)\s*\(',l) or re.search(r'["\'](regcomp|regexec|glob(?:64)?|wordexp|[vfs]*wscanf)["\']',l):rows.append({'file':m.name,'line':n,'text':l.strip()})
 out.append(dict(archive=str(root/f),sha256=hashlib.sha256((root/f).read_bytes()).hexdigest(),files_scanned=files,matches=rows));print(f,files,len(rows),flush=True)
Path('.local/security-residual-os-2026-11-25/runtime-callers.json').write_text(json.dumps(out,indent=2))
