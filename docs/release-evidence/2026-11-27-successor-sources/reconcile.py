"""Fresh readback and exact-payload reconciliation for successor app sources."""
from pathlib import Path
import csv,hashlib,io,json,re,sys,tarfile
BASE=Path(__file__).resolve().parent; Q=BASE.parent
ROOT=Q.parents[1]; OLD=Q/'sources/validated-source-materials'; C=Q/'successor-candidate'
sys.path.insert(0,str(Q/'successor-source/deploy/release'))
from source_inventory import archived_json
def sha(p):
 with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def write(n,x):(BASE/n).write_text(json.dumps(x,indent=2,sort_keys=True)+'\n')
prior=json.loads((OLD/'coverage.json').read_text()); inv=json.loads((C/'source-inventory.json').read_text())
report={'revision':json.loads((C/'candidate-manifest.json').read_text())['revision'],'prior_materials':{},'os_archives':{},'images':{},'gaps':[]}
files={}
for e in prior['images'].values():
 for n,h in e['files'].items():
  assert n not in files or files[n]==h
  files[n]=h
for n,h in files.items():
 assert sha(OLD/n)==h,n
 report['prior_materials'][n]={'sha256':h,'bytes':(OLD/n).stat().st_size}
archives={n:OLD/n for n in ['ratefreak-candidate-os-sources.tar','security-os-sources.tar','debian13-os-sources.tar','os-source-updates.tar','lower-layer-os-sources.tar']}
archives.update({'application-os-updates.tar':Q/'application-sources/application-os-updates.tar','reference-debian-os-sources.tar':Q/'reference-sources/materials/reference-debian-os-sources.tar'})
pairs={}
for n,p in archives.items():
 with tarfile.open(p) as t:
  m=json.load(t.extractfile('SOURCE-MANIFEST.json')); names={x.name for x in t.getmembers()};checked=0
  if 'files' in m:
   for f in m['files']:
    member=t.getmember(f['local_file']);assert member.isfile() and member.size==f['size']
    assert hashlib.file_digest(t.extractfile(member),'sha256').hexdigest()==f['sha256'];checked+=1
   rows=[row for values in m['images'].values() for row in values]
   for row in rows:
    assert all(f in names for f in row['files']);pairs.setdefault((row['source_package'],row['source_version']),set()).add(n)
  else:
   for row in m['sources']:
    for f in row.get('files',row.get('inputs',[])):
     paths=[x for x in names if x.endswith('/'+f['file']) or x==f['file']];assert len(paths)==1,(n,f['file'])
     member=t.getmember(paths[0]);assert member.size==f.get('bytes',f.get('size',member.size))
     assert hashlib.file_digest(t.extractfile(member),'sha256').hexdigest()==f['sha256'];checked+=1
    pairs.setdefault((row['source_package'],row['source_version']),set()).add(n)
  report['os_archives'][n]={'sha256':sha(p),'verifiedSourceInputs':checked,'authenticationScope':'Retained original acquisition/authentication records; fresh input hash readback, not new upstream signature verification.'}
 print('Verified',n,checked,flush=True)
for role,e in inv['images'].items():
 before={(x['scope'],x['name']):x for x in prior['images'][role]['components']};after={(x['scope'],x['name']):x for x in e['components']};assert before.keys()==after.keys()
 changed=[{'scope':k[0],'name':k[1],'before':before[k]['version'],'after':after[k]['version']} for k in before if before[k]['version']!=after[k]['version']]
 assert all(x['scope']=='os' for x in changed)
 assert len(changed)==(4 if role=='api' else 2)
 pre=role+'-notices/';p=C/(role+'-notices.tar.gz');old=OLD/p.name
 assert archived_json(old,pre+'manifest.json')==archived_json(p,pre+'manifest.json')
 a=archived_json(old,pre+'runtime/evidence.json');b=archived_json(p,pre+'runtime/evidence.json');assert a.keys()==b.keys()
 for k in a:
  if k=='native_binaries':
   clean=lambda rows:[{key:v for key,v in x.items() if key!='ldd'} for x in rows]
   assert clean(a[k])==clean(b[k]),(role,k)
  elif k!='os_packages_sha256':assert a[k]==b[k],(role,k)
 def notices(path):
  with tarfile.open(path) as t:
   with tarfile.open(fileobj=io.BytesIO(t.extractfile(pre+'runtime/runtime-license-texts.tar.gz').read())) as nested:
    return {x.name:hashlib.file_digest(nested.extractfile(x),'sha256').hexdigest() for x in nested if x.isfile()}
 assert notices(p)==notices(old)
 with tarfile.open(p) as t:rows=list(csv.DictReader(io.StringIO(t.extractfile(pre+'runtime/os-packages.tsv').read().decode()),delimiter='\t'))
 bindings=[]
 for row in rows:
  pair=(row['source_package'],row['source_version']);assert pair in pairs,pair
  bindings.append({**row,'source_archives':sorted(pairs[pair])})
 # Raw package snapshots retain overwritten lower-layer source obligations.
 audit=json.loads((Q/'successor-layers/all-layer-audit.json').read_text())['images'][role]
 layer_bindings=[]
 for layer in audit['layers']:
  for package in layer['installed_packages']:
   source=package.get('Source') or package['Package'];match=re.fullmatch(r'([^ ]+) \((.+)\)',source)
   pair=(match[1],match[2]) if match else (source,package['Version'])
   assert pair in pairs,(role,layer['position'],pair)
   layer_bindings.append({'layer':layer['position'],'package':package['Package'],'source_package':pair[0],'source_version':pair[1],'source_archives':sorted(pairs[pair])})
 report['images'][role]={'image_id':e['image_id'],'components':len(e['components']),'component_changes':changed,'applicationNoticeManifestUnchanged':True,'nativePayloadFingerprintsUnchanged':True,'runtimeLicenseMemberBytesUnchanged':True,'osSourceBindings':bindings,'allLayerSourceBindings':layer_bindings}
write('source-reconciliation.json',report)
print('Reconciled',sum(x['components'] for x in report['images'].values()),'app components; all installed/inherited source pairs mapped.',flush=True)
