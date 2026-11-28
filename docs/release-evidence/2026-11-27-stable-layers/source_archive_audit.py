"""Read only the frozen source and archive bytes; never serialize matched secrets."""
from pathlib import Path
import ast, hashlib, json, re, subprocess, tarfile
import layer_audit as audit
B=audit.BASE; R=audit.ROOT; C=audit.CANDIDATE; S=R/'.local/stable-candidate-2026-11-27/source-qualified'
REV='f54048c233e405843160bc16d857f8c32082599f'
module=ast.parse((S/'deploy/release/prepare.py').read_text()); fn=next(x for x in module.body if isinstance(x,ast.FunctionDef) and x.name=='safe_source')
ns={'PurePosixPath':__import__('pathlib').PurePosixPath}; exec(compile(ast.Module(body=[fn],type_ignores=[]),'frozen-safe-source','exec'),ns); safe=ns['safe_source']
values=audit.private_values()
patterns={'github-token':re.compile(rb'\b(?:gh[pousr]_[A-Za-z0-9]{30,}|github_pat_[A-Za-z0-9_]{40,})\b'),'aws-access-key':re.compile(rb'\b(?:AKIA|ASIA)[A-Z0-9]{16}\b'),'private-key-block':re.compile(rb'-----BEGIN (?:RSA |EC |OPENSSH |DSA )?PRIVATE KEY-----\r?\n[A-Za-z0-9+/]{30,}')}
def scan(path,data):
 return ([{'path':path,'setting':k} for k,v in values if v in data], [{'path':path,'rule':k} for k,p in patterns.items() if p.search(data)])
def dump(n,obj): (B/n).write_text(json.dumps(obj,indent=2,sort_keys=True)+'\n')
source=[]; private=[]; credential=[];unsafe=[]
for entry in subprocess.check_output(['git','-C',str(S),'ls-tree','-rz',REV]).split(b'\0'):
 if not entry:continue
 metadata,name=entry.split(b'\t',1);name=name.decode();mode,_,oid=metadata.decode().split(); data=(S/name).read_bytes()
 if mode not in ('100644','100755') or not safe(name):unsafe.append(name)
 p,c=scan(name,data);private+=p;credential+=c
 assert hashlib.sha1(b'blob '+str(len(data)).encode()+b'\0'+data).hexdigest()==oid
 source.append({'path':name,'sha256':hashlib.sha256(data).hexdigest(),'bytes':len(data),'git_blob':oid,'mode':mode})
dump('source-files.json',source)
archives={}; manifest=json.loads((C/'candidate-manifest.json').read_text()); artifact_hashes={}
for n,e in manifest['files'].items():
 p=C/n;actual=hashlib.file_digest(p.open('rb'),'sha256').hexdigest();assert actual==e['sha256'] and p.stat().st_size==e['bytes'];artifact_hashes[n]=actual
for n in ['installation.tar.gz','api-notices.tar.gz','web-notices.tar.gz','tools-notices.tar.gz','reports.tar.gz']:
 files=[];members=[];names=set();dups=[];bad=[]
 with tarfile.open(C/n) as t:
  for m in t:
   members.append({'path':m.name,'type':m.type.decode(errors='replace'),'linkname':m.linkname,'bytes':m.size})
   if m.name in names:dups.append(m.name)
   names.add(m.name)
   if not safe(m.name.rstrip('/')) or not (m.isfile() or m.isdir()):bad.append(m.name)
   if m.isfile():
    data=t.extractfile(m).read();p,c=scan(n+':'+m.name,data);private+=p;credential+=c
    files.append({'path':m.name,'sha256':hashlib.sha256(data).hexdigest(),'bytes':len(data)})
 dump(n+'-files.json',files);dump(n+'-members.json',members)
 archives[n]={'files':len(files),'members':len(members),'duplicate_paths':dups,'policy_rejected_paths':bad}
 if n=='installation.tar.gz':
  a={x['path']:x['sha256'] for x in files};z={x['path']:x['sha256'] for x in source}
  archives[n]['source_missing']=sorted(z.keys()-a.keys());archives[n]['source_extra']=sorted(a.keys()-z.keys());archives[n]['source_changed']=sorted(k for k in a.keys()&z.keys() if a[k]!=z[k])
report={'revision':REV,'policy_source':'deploy/release/prepare.py:safe_source','policy_file_sha256':hashlib.sha256((S/'deploy/release/prepare.py').read_bytes()).hexdigest(),'configured_private_values_checked':len(values),'source_files':len(source),'source_unsafe_paths':unsafe,'source_committed_blob_identity_verified':True,'private_value_matches':private,'credential_pattern_matches':credential,'archives':archives,'artifact_sha256':artifact_hashes,'limits':'Only configured private values >=12 bytes under selected setting names and selected credential patterns are tested; heuristic absence is not proof of absence. Source tree .git is excluded as nondistributed metadata.'}
dump('source-archive-audit.json',report)
print(json.dumps({'source_files':len(source),'private_hits':len(private),'credential_hits':len(credential),'unsafe':len(unsafe),'installation':archives['installation.tar.gz']},indent=2))
