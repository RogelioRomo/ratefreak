from pathlib import Path
import copy,hashlib,json,shutil,sys
ROOT=next(parent for parent in Path(__file__).resolve().parents if (parent/'deploy/release/prepare.py').is_file());BASE=Path(__file__).resolve().parent
OLD=ROOT/'.local/security-runtime-guard-2026-11-25/validated-source-materials'
CANDIDATE=ROOT/'.local/stable-candidate-2026-11-27/candidate-qualified'
SOURCE=ROOT/'.local/stable-candidate-2026-11-27/source-qualified'
OUT=BASE/'source-materials';OUT.mkdir(exist_ok=False)
sys.path.insert(0,str(SOURCE/'deploy/release'))
from prepare import SOURCE_SCOPES
from source_inventory import archived_json
sys.path.insert(0,str(SOURCE/'deploy/compliance'))
from source_bundle import create,verify

def sha(p):
 with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def write(name,value):(OUT/name).write_text(json.dumps(value,indent=2,sort_keys=True)+'\n')
reconciliation=json.loads((BASE/'source-reconciliation.json').read_text());assert not reconciliation['gaps']
replaced={'coverage.json','os-image-bindings.json','installation.tar.gz','web.native-sources.tar.gz','api-notices.tar.gz','web-notices.tar.gz','tools-notices.tar.gz','README.md','release-source-review.md'}
for path in OLD.iterdir():
 if path.is_file() and path.name not in replaced:shutil.copyfile(path,OUT/path.name)
shutil.copyfile(OLD/'release-source-review.md',OUT/'prior-security-source-review.md')
for name in ['installation.tar.gz','api-notices.tar.gz','web-notices.tar.gz','tools-notices.tar.gz']:shutil.copyfile(CANDIDATE/name,OUT/name)
shutil.copyfile(BASE/'source-reconciliation.json',OUT/'stable-source-reconciliation.json')
shutil.copyfile(BASE/'notice-content-delta.json',OUT/'stable-notice-content-delta.json')
inventory=json.loads((CANDIDATE/'source-inventory.json').read_text())
profile=json.loads((SOURCE/'deploy/compliance/native/source-bundle.lock.json').read_text())
image={'image_id':inventory['images']['web']['image_id'],'image':'ratefreak-web:0.1.0','packages':archived_json(OUT/'web-notices.tar.gz','web-notices/manifest.json')['packages'],'runtime':archived_json(OUT/'web-notices.tar.gz','web-notices/runtime/evidence.json')}
native=create(profile,ROOT/'.local/release-compliance/source-set',image,OUT/'web.native-sources.tar.gz')
assert len(verify(OUT/'web.native-sources.tar.gz')['files'])==406
write('stable-native-rebinding.json',native)
previous=json.loads((OLD/'coverage.json').read_text())
bindings={'format':1,'revision':reconciliation['revision'],'images':{}}
coverage={'format':1,'images':{}}
for role,entry in inventory['images'].items():
 prior={(c['scope'],c['name'],c['version']):c for c in previous['images'][role]['components']}
 components=[]
 for c in entry['components']:
  row=copy.deepcopy(prior[c['scope'],c['name'],c['version']])
  if 'exception' in row:row['exception']['record']='release-source-review.md'
  components.append(row)
 bindings['images'][role]={'image_id':entry['image_id'],'packages':reconciliation['images'][role]['osSourceBindings']}
 coverage['images'][role]={'image_id':entry['image_id'],'scopes':sorted(SOURCE_SCOPES[role]),'review_record':'release-source-review.md','components':components}
write('os-image-bindings.json',bindings)
(BASE/'coverage-pending.json').write_text(json.dumps(coverage,indent=2,sort_keys=True)+'\n')
print('Copied matching source materials; 406-input native sidecar verified and bound. Final review/coverage awaits layer proof.',flush=True)
