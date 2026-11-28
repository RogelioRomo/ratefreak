from pathlib import Path
import json,hashlib,tarfile,io,csv,sys,subprocess,struct,zlib,re
B=Path(__file__).resolve().parent;Q=B.parent;R=Q.parents[1];C=Q/'successor-candidate';S=Q/'successor-source';P=B/'validated-app-materials';OLD=Q/'sources/validated-source-materials';L=Q/'successor-layers'
def read(p):return json.loads(p.read_text())
def sha(p):
 with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
sys.path.insert(0,str(S/'deploy/release'));from source_inventory import validate_coverage,validate_native
inv=read(C/'source-inventory.json');coverage=read(P/'coverage.json');validate_coverage(coverage,inv);materials={n:h for e in coverage['images'].values() for n,h in e['files'].items()}
for n,h in materials.items():assert sha(P/n)==h,n
prior=read(OLD/'coverage.json');priorfiles={n:h for e in prior['images'].values() for n,h in e['files'].items()}
for n,h in priorfiles.items():assert sha(OLD/n)==h,n
result={'scope':'Independent review of final frozen successor application source/notice/layer evidence; no security or publication clearance','revision':'9eea2c76550fe03920dd999f1677565e4f43d12b','images':{r:e['image_id'] for r,e in inv['images'].items()},'blockers':[],'important_gaps':[],'minor_limitations':[],'checks':{'exact_component_entries':sum(len(e['components']) for e in inv['images'].values()),'current_material_hashes':len(materials),'prior_material_hashes':len(priorfiles)}}
pairs={};inputs=descriptors=0
for an in ['ratefreak-candidate-os-sources.tar','security-os-sources.tar','debian13-os-sources.tar','os-source-updates.tar','lower-layer-os-sources.tar','application-os-updates.tar','reference-debian-os-sources.tar']:
 with tarfile.open(P/an) as t:
  m=json.load(t.extractfile('SOURCE-MANIFEST.json'));members={x.name:x for x in t if x.isfile()};rows=[row for group in m['images'].values() for row in group] if 'images'in m else m['sources'];fs=m.get('files') or [f for row in rows for f in row.get('files',row.get('inputs',[]))];names={}
  for f in fs:
   path=f.get('local_file') or next(n for n in members if n==f['file'] or n.endswith('/'+f['file']));member=members[path];assert member.size==f.get('size',f.get('bytes'));assert hashlib.file_digest(t.extractfile(member),'sha256').hexdigest()==f['sha256'];name=f.get('original_file',f['file']);names[name]={'path':path,'sha256':f['sha256'],'bytes':member.size};inputs+=1
  for row in rows:pairs.setdefault((row['source_package'],row['source_version']),set()).add(an)
  for n,f in names.items():
   if not n.endswith('.dsc'):continue
   text=t.extractfile(f['path']).read().decode();source=next(x[8:] for x in text.splitlines() if x.startswith('Source: '));version=next(x[9:] for x in text.splitlines() if x.startswith('Version: '));assert (source,version) in pairs
   block=text.split('Checksums-Sha256:\n',1)[1].split('\n\n',1)[0]
   for line in block.splitlines():
    if not line.startswith(' '):break
    h,size,name=line.split();assert names[name]['sha256']==h and names[name]['bytes']==int(size),(an,n,name)
   descriptors+=1
 print(an,'verified',flush=True)
result['checks'].update({'os_source_input_references_sha256_and_size':inputs,'source_descriptor_closures':descriptors})
audit=read(L/'all-layer-audit.json');rec=read(B/'source-reconciliation.json')
def filemap(t):return {m.name:hashlib.file_digest(t.extractfile(m),'sha256').hexdigest() for m in t if m.isfile()}
changes={}
for role,e in inv['images'].items():
 p={(r['scope'],r['name']):r['version'] for r in prior['images'][role]['components']};now={(r['scope'],r['name']):r['version'] for r in e['components']};assert p.keys()==now.keys();delta=[{'scope':k[0],'name':k[1],'before':p[k],'after':v} for k,v in now.items() if p[k]!=v];assert all(r['scope']=='os' for r in delta);assert len(delta)==(4 if role=='api' else 2);changes[role]=delta
 with tarfile.open(C/(role+'-notices.tar.gz')) as t,tarfile.open(OLD/(role+'-notices.tar.gz')) as before:
  a=filemap(t);b=filemap(before);assert a.keys()==b.keys();actual=[n for n in a if a[n]!=b[n]];assert set(actual)=={role+'-notices/runtime/'+n for n in ['evidence.json','os-packages.tsv','runtime-license-texts.tar.gz']},actual
  with tarfile.open(fileobj=io.BytesIO(t.extractfile(role+'-notices/runtime/runtime-license-texts.tar.gz').read())) as x,tarfile.open(fileobj=io.BytesIO(before.extractfile(role+'-notices/runtime/runtime-license-texts.tar.gz').read())) as y:assert filemap(x)==filemap(y)
  rows=list(csv.DictReader(io.StringIO(t.extractfile(role+'-notices/runtime/os-packages.tsv').read().decode()),delimiter='\t'))
  for row in rows:assert (row['source_package'],row['source_version']) in pairs
 for layer in audit['images'][role]['layers']:
  for row in layer['installed_packages']:
   source=row.get('Source') or row['Package'];m=re.fullmatch(r'([^ ]+) \((.+)\)',source);pair=(m[1],m[2]) if m else (source,row['Version']);assert pair in pairs,(role,pair)
 assert rec['images'][role]['image_id']==e['image_id']
result['checks']['component_version_changes']=changes;result['checks']['actual_application_and_nested_runtime_notice_bytes_equal_prior']=True;result['checks']['all_final_installed_and_inherited_os_pairs_have_exact_sources']=True
with tarfile.open(P/'web.native-sources.tar.gz') as t:
 m=json.load(t.extractfile('SOURCE-MANIFEST.json'));validate_native(m,inv);assert len(m['files'])==406
 for f in m['files']:assert hashlib.file_digest(t.extractfile(f['file']),'sha256').hexdigest()==f['sha256']
result['checks']['fresh_exact_image_native_source_inputs']=406
# Verify every committed blob by the Git object identity, without trusting the worktree.
rev=result['revision'];tree=subprocess.check_output(['git','ls-tree','-rz',rev],cwd=S);expected={}
for entry in tree.split(b'\0'):
 if not entry:continue
 meta,path=entry.split(b'\t',1);mode,kind,oid=meta.split();assert kind==b'blob';expected[path.decode()]=oid.decode()
with tarfile.open(C/'installation.tar.gz') as t:
 files={m.name:m for m in t if m.isfile()};assert set(files)==set(expected)|{'SOURCE_REVISION'};assert t.extractfile('SOURCE_REVISION').read().decode().strip()==rev
 for path,oid in expected.items():
  data=t.extractfile(files[path]).read();assert hashlib.sha1(b'blob '+str(len(data)).encode()+b'\0'+data).hexdigest()==oid,path
result['checks']['frozen_committed_source_blobs']=len(expected)
# Read the primary final tools bundle and its Skia payload without executing either.
selected={}
with tarfile.open(C/'tools.tar.gz') as image:
 index=json.load(image.extractfile('index.json'));assert index['manifests'][0]['digest']==inv['images']['tools']['image_id'];manifest=json.load(image.extractfile('manifest.json'))[0]
 for layer in manifest['Layers']:
  with tarfile.open(fileobj=image.extractfile(layer),mode='r|*') as t:
   for member in t:
    if member.isfile() and member.name in {'app/efbundle','app/runtimes/linux-x64/native/libSkiaSharp.so'}:selected[member.name]=t.extractfile(member).read()
data=selected['app/efbundle'];signature=bytes.fromhex('8b1202b96a612038727b930214d7a03213f5b9e6efae3318ee3b2dce24b36aae');position=data.index(signature);stream=io.BytesIO(data);stream.seek(struct.unpack_from('<q',data,position-8)[0])
def number(fmt):return struct.unpack(fmt,stream.read(struct.calcsize(fmt)))
def string():
 size=shift=0
 while True:
  byte=stream.read(1)[0];size|=(byte&127)<<shift;shift+=7;assert shift<=35
  if byte<128:break
 return stream.read(size).decode()
major,minor,count=number('<IIi');assert (major,minor)==(6,0);bundleid=string();number('<qqqqQ');embedded={};deps=None
for _ in range(count):
 offset,size,compressed=number('<qqq');kind,=number('<B');name=string();payload=data[offset:offset+(compressed or size)]
 if compressed:payload=zlib.decompress(payload,-15)
 assert len(payload)==size;embedded[name]=hashlib.sha256(payload).hexdigest()
 if name.endswith('.deps.json'):deps=json.loads(payload)
 if name=='RateFreak.Api':assert b'@(#)Version 10.0.1526.53008 @Commit: 8e0f8ed2312954727f8919d7d1c2f78b483b4ee7' in payload
saved=read(L/'efbundle-inventory.json');assert hashlib.sha256(data).hexdigest()==saved['sha256'];assert embedded=={f['file']:f['sha256'] for f in saved['files']};assert deps==saved['dependency_manifest'];assert embedded['libSkiaSharp.so']==hashlib.sha256(selected['app/runtimes/linux-x64/native/libSkiaSharp.so']).hexdigest();old=read(R/'docs/release-evidence/2026-11-27-stable-layers/efbundle-inventory.json');oldembedded={f['file']:f['sha256'] for f in old['files']};assert embedded.keys()==oldembedded.keys();assert {n for n,h in embedded.items() if h!=oldembedded[n]}=={'efbundle.dll','RateFreak.Api.dll','RateFreak.Application.dll','RateFreak.Domain.dll','RateFreak.Infrastructure.dll'};assert deps==old['dependency_manifest'];result['checks']['primary_tools_bundle']={'files':count,'dependency_libraries':len(deps['libraries']),'only_five_first_party_assemblies_change':True,'dependency_manifest_all_records_equal_prior':True,'embedded_apphost_equal_prior':True,'embedded_skia_equals_primary_layer':True}
result['minor_limitations']=['Generated Next output is itemized by raw-file differences and frozen source/package inventory; its changed compiled chunks are not claimed byte-identical or rebuilt reproducibly.','Original Sharp prebuild header resolution and font download/subsetting history remain explicitly unproven; original source/build/replacement proof and permissive exceptions retain their historical scope.','Layer classifiers use path categories and fingerprints; they are not a vulnerability finding. Privacy and OG/WASM/TTF search conclusions retain recorded heuristic limits.','This review relies on the main all-layer scan for exhaustive application member enumeration, while independently checking raw final migration/Skia bytes, all source archives, source/notice mappings and frozen committed source blobs.']
(B/'independent-app-review.json').write_text(json.dumps(result,indent=2)+'\n');print(json.dumps(result,indent=2))
