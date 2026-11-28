from pathlib import Path
import collections,csv,hashlib,io,json,sys,tarfile
ROOT=next(parent for parent in Path(__file__).resolve().parents if (parent/'deploy/release/prepare.py').is_file())
BASE=Path(__file__).resolve().parent
OLD=ROOT/'.local/security-runtime-guard-2026-11-25/validated-source-materials'
CANDIDATE=ROOT/'.local/stable-candidate-2026-11-27/candidate-qualified'
SOURCE=ROOT/'.local/stable-candidate-2026-11-27/source-qualified'
sys.path.insert(0,str(SOURCE/'deploy/release'))
from source_inventory import archived_json

def sha(path):
 with path.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def write(name,value):(BASE/name).write_text(json.dumps(value,indent=2,sort_keys=True)+'\n')
def members(archive):
 return {m.name:hashlib.file_digest(archive.extractfile(m),'sha256').hexdigest() for m in archive if m.isfile()}
old=json.loads((OLD/'coverage.json').read_text());new=json.loads((CANDIDATE/'source-inventory.json').read_text())
report={'format':1,'revision':json.loads((CANDIDATE/'candidate-manifest.json').read_text())['revision'],'images':{},'prior_materials':{},'os_archives':{},'gaps':[]}
files={}
for image in old['images'].values():
 for name,digest in image['files'].items():
  assert name not in files or files[name]==digest
  files[name]=digest
for name,expected in files.items():
 actual=sha(OLD/name);assert actual==expected,name
 report['prior_materials'][name]={'sha256':actual,'bytes':(OLD/name).stat().st_size}
print('Prior material hashes verified:',len(files),flush=True)
source_pairs={}
archives=['ratefreak-candidate-os-sources.tar','security-os-sources.tar','debian13-os-sources.tar','os-source-updates.tar','lower-layer-os-sources.tar']
for name in archives:
 with tarfile.open(OLD/name) as tar:
  manifest=json.load(tar.extractfile('SOURCE-MANIFEST.json'));names={m.name for m in tar.getmembers()};verified=0
  if 'files' in manifest:
   for f in manifest['files']:
    path=f['local_file'];member=tar.getmember(path)
    assert member.isfile() and member.size==f['size'],path
    assert hashlib.file_digest(tar.extractfile(member),'sha256').hexdigest()==f['sha256'],path
    verified+=1
   for rows in manifest['images'].values():
    for row in rows:
     assert all(p in names for p in row['files'])
     source_pairs.setdefault((row['source_package'],row['source_version']),set()).add(name)
  else:
   for row in manifest['sources']:
    for f in row['files']:
     paths=[p for p in names if p.endswith('/'+f['file']) or p==f['file']]
     assert len(paths)==1,(name,f['file'],paths)
     assert hashlib.file_digest(tar.extractfile(paths[0]),'sha256').hexdigest()==f['sha256']
     verified+=1
    source_pairs.setdefault((row['source_package'],row['source_version']),set()).add(name)
  report['os_archives'][name]={'sha256':sha(OLD/name),'verifiedSourceInputs':verified,'signatureScope':'Retained original authenticated acquisition/signature evidence; hashes freshly verified; no new signature-verification claim.'}
 print('OS archive verified:',name,verified,flush=True)
for role,image in new['images'].items():
 prefix=role+'-notices/';archive=CANDIDATE/(role+'-notices.tar.gz');oldarchive=OLD/archive.name
 before={(x['scope'],x['name'],x['version']) for x in old['images'][role]['components']}
 after={(x['scope'],x['name'],x['version']) for x in image['components']};assert before==after
 om=archived_json(oldarchive,prefix+'manifest.json');nm=archived_json(archive,prefix+'manifest.json');assert om==nm
 a=archived_json(oldarchive,prefix+'runtime/evidence.json');b=archived_json(archive,prefix+'runtime/evidence.json')
 assert a.keys()==b.keys()
 for key in a:
  if key=='native_binaries':
   clean=lambda rows:[{k:v for k,v in x.items() if k!='ldd'} for x in rows]
   assert clean(a[key])==clean(b[key]),key
  else:assert a[key]==b[key],key
 with tarfile.open(oldarchive) as ot,tarfile.open(archive) as nt:
  with tarfile.open(fileobj=io.BytesIO(ot.extractfile(prefix+'runtime/runtime-license-texts.tar.gz').read())) as x,tarfile.open(fileobj=io.BytesIO(nt.extractfile(prefix+'runtime/runtime-license-texts.tar.gz').read())) as y:
   assert members(x)==members(y)
  rows=list(csv.DictReader(io.StringIO(nt.extractfile(prefix+'runtime/os-packages.tsv').read().decode()),delimiter='\t'))
 bindings=[]
 for row in rows:
  pair=(row['source_package'],row['source_version']);assert pair in source_pairs,pair
  bindings.append({**row,'source_archives':sorted(source_pairs[pair])})
 report['images'][role]={'image_id':image['image_id'],'components':len(after),'componentVersionsUnchanged':True,'applicationNoticeManifestUnchanged':True,'nativePayloadFingerprintsUnchanged':True,'runtimeLicenseMemberBytesUnchanged':True,'runtimeEvidenceOnlyLddAddressesDiffer':True,'osSourceBindings':bindings}
write('source-reconciliation.json',report)
print('Complete reconciliation:',sum(x['components'] for x in report['images'].values()),'components; no gaps.',flush=True)
