from pathlib import Path
import copy,hashlib,json,shutil,sys
B=Path(__file__).resolve().parent;Q=B.parent;R=Q.parents[1]
OLD=Q/'sources/validated-source-materials';C=Q/'successor-candidate';S=Q/'successor-source';OUT=B/'source-materials'
sys.path.insert(0,str(S/'deploy/release'));from source_inventory import archived_json
sys.path.insert(0,str(S/'deploy/compliance'));from source_bundle import create,verify
def sha(p):
 with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def write(n,x):(OUT/n).write_text(json.dumps(x,indent=2,sort_keys=True)+'\n')
rec=json.loads((B/'source-reconciliation.json').read_text());assert not rec['gaps']
OUT.mkdir(exist_ok=False)
replace={'coverage.json','os-image-bindings.json','installation.tar.gz','web.native-sources.tar.gz','README.md','release-source-review.md','api-notices.tar.gz','web-notices.tar.gz','tools-notices.tar.gz'}
for p in OLD.iterdir():
 if p.is_file() and p.name not in replace:shutil.copyfile(p,OUT/p.name)
shutil.copyfile(OLD/'release-source-review.md',OUT/'prior-f540-source-review.md')
for n in ['installation.tar.gz','api-notices.tar.gz','web-notices.tar.gz','tools-notices.tar.gz']:shutil.copyfile(C/n,OUT/n)
shutil.copyfile(Q/'application-sources/application-os-updates.tar',OUT/'application-os-updates.tar')
shutil.copyfile(Q/'reference-sources/materials/reference-debian-os-sources.tar',OUT/'reference-debian-os-sources.tar')
shutil.copyfile(B/'source-reconciliation.json',OUT/'successor-source-reconciliation.json')
inv=json.loads((C/'source-inventory.json').read_text());prior=json.loads((OLD/'coverage.json').read_text())
profile=json.loads((S/'deploy/compliance/native/source-bundle.lock.json').read_text())
image={'image_id':inv['images']['web']['image_id'],'image':'ratefreak-web:0.1.0','packages':archived_json(C/'web-notices.tar.gz','web-notices/manifest.json')['packages'],'runtime':archived_json(C/'web-notices.tar.gz','web-notices/runtime/evidence.json')}
native=create(profile,R/'.local/release-compliance/source-set',image,OUT/'web.native-sources.tar.gz');assert len(verify(OUT/'web.native-sources.tar.gz')['files'])==406
write('successor-native-rebinding.json',native)
coverage={'format':1,'images':{}};bindings={'format':1,'revision':rec['revision'],'images':{}}
for role,e in inv['images'].items():
 before={(x['scope'],x['name']):x for x in prior['images'][role]['components']};rows=[]
 for c in e['components']:
  row=copy.deepcopy(before[c['scope'],c['name']]);row['version']=c['version']
  if c['scope']=='os':
   binding=next(x for x in rec['images'][role]['osSourceBindings'] if x['package']==c['name'] and x['version']==c['version'])
   row['source_files']=binding['source_archives']+['os-image-bindings.json']
  if 'exception' in row:row['exception']['record']='release-source-review.md'
  rows.append(row)
 bindings['images'][role]={'image_id':e['image_id'],'packages':rec['images'][role]['osSourceBindings'],'all_layer_packages':rec['images'][role]['allLayerSourceBindings']}
 coverage['images'][role]={'image_id':e['image_id'],'scopes':prior['images'][role]['scopes'],'review_record':'release-source-review.md','components':rows}
write('os-image-bindings.json',bindings)
(B/'coverage-pending.json').write_text(json.dumps(coverage,indent=2,sort_keys=True)+'\n')
print('Fresh successor sidecar406 inputs and app coverage502 prepared; final review pending.',flush=True)
