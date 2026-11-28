from pathlib import Path
import hashlib,json,shutil,sys
ROOT=next(parent for parent in Path(__file__).resolve().parents if (parent/'deploy/release/prepare.py').is_file());BASE=Path(__file__).resolve().parent
OUT=BASE/'source-materials';SOURCE=ROOT/'.local/stable-candidate-2026-11-27/source-qualified'
sys.path.insert(0,str(SOURCE/'deploy/release'))
from prepare import source_evidence
from source_inventory import validate_coverage

def sha(p):
 with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
inventory=json.loads((ROOT/'.local/stable-candidate-2026-11-27/candidate-qualified/source-inventory.json').read_text())
layers=ROOT/'docs/release-evidence/2026-11-27-stable-layers'
payload=json.loads((layers/'payload-reconciliation.json').read_text());assert not payload['blockers']
for role,row in payload['images'].items():
 assert row['all_installed_package_layer_snapshots_identical']
for name in ['payload-reconciliation.json','all-layer-audit.json','efbundle-inventory.json','source-archive-audit.json']:shutil.copyfile(layers/name,OUT/('stable-'+name))
review="""# Exact 0.1.0 candidate source and notice review — 2026-11-27

Scope: source revision f54048c233e405843160bc16d857f8c32082599f and the three
immutable image IDs in coverage.json. This original candidate requires a
security-fix successor. Source coverage here neither clears security nor
qualifies successor images or authorizes publication.

All502 inventoried image/component entries match the prior Nov25 source-delivery
set exactly. All47 prior material files were freshly read and hash-checked against
that reviewed coverage. Every application notice manifest and every native
path/hash/size fingerprint matches. Runtime evidence differs only in ldd addresses;
all nested runtime-license archive member names and bytes match. The historical
reviews and acquisition/source records retain their dates and scope.

All849 OS source-input entries across the five supplied installed/inherited source
archives were read back and verified against their internal SHA256 and size
records. All current installed binary packages map to their exact source package
and version, and matching archives are recorded in os-image-bindings.json.
All43 image layers were re-inspected. Their installed/superseded package snapshots
match Nov25, including the seven lower-layer-only pairs in web/tools; retain all
five archives, including lower-layer-os-sources.tar and debian13-os-sources.tar.
Original authenticated index and signature receipts are retained; no new signature
verification, upstream binary reproduction or bit-for-bit native rebuild is claimed.

The406-input Sharp/libvips native source sidecar was created from the locked source
set, hash-verified on readback and rebound to this exact web image. Original
LGPL source/build/replacement instructions, Rust/native build inputs and all grant
texts remain delivered. The prior replacement proof retains its scope.
The unchanged Sharp addon source commit, original Apache and header collaborator
notices remain supplied; exact upstream prebuild header resolution is unproven.
The two original WOFF2 fonts and complete OFL texts match; original font download
and subsetting history remains unproven. No OG/satori/WASM/TTF payload was found
in any layer; previous notice interpretations remain conditional on that absence.

The current migration executable contains104 embedded files and36 dependency
library records. No additional uninventoried package/version exists. Every
third-party embedded entry and package-library record equals Nov25. The apphost
is the exact previously reviewed .NET10.0.15 binary at commit
8e0f8ed2312954727f8919d7d1c2f78b483b4ee7 and retains its corresponding complete
framework MIT/third-party license supplement and acquisition/source-manifest
receipt. Embedded Skia matches the bound layer payload and current complete
Skia4.157 notices/build-source review. Only first-party migration/application
assemblies and generated dependency data changed. Original framework/permissive
source exceptions remain explicit in per-component decisions and prior reviews.

installation.tar.gz contains every frozen committed source byte and its generated
SOURCE_REVISION, with no local instance/Git state. Deliver this matching source,
all collected notices, every listed source archive and supplemental review file
alongside any later authorized binary distribution; do not select just a subset
from this material set. Separate PostgreSQL/Caddy service images, the container
engine and host are outside this three-application-image source coverage.
New patched service images require their own source/notice and artifact review.

Read prior-security-source-review.md, prior-component-review.md and the retained
runtime/native/OS records for full original grant decisions and provenance limits.
No conclusion about new CVE applicability follows from source coverage.
"""
(OUT/'release-source-review.md').write_text(review)
(OUT/'README.md').write_text('Exact f54048c candidate materials; security-fix successor required. Read release-source-review.md and coverage.json. Preserve all source archives/notices.\n')
coverage=json.loads((BASE/'coverage-pending.json').read_text())
files={p.name:sha(p) for p in sorted(OUT.iterdir()) if p.is_file() and p.name!='coverage.json'}
for role,e in coverage['images'].items():
 e['files']={k:v for k,v in files.items() if role=='web' or k!='web.native-sources.tar.gz'}
 if role=='web':e['native_sidecar']='web.native-sources.tar.gz'
(OUT/'coverage.json').write_text(json.dumps(coverage,indent=2,sort_keys=True)+'\n')
validate_coverage(coverage,inventory)
source_evidence(OUT,{n:e['image_id'] for n,e in inventory['images'].items()},BASE/'validated-source-materials',inventory)
result={'valid':True,'scope':'Original frozen f54048c candidate only; security successor required','images':{n:e['image_id'] for n,e in inventory['images'].items()},'componentEntries':502,'priorMaterialsReadBack':47,'osSourceInputEntriesReadBack':849,'nativeSourceInputsVerified':406,'materialFiles':len(files),'gaps':[],'securityClearance':False}
(BASE/'source-validation.json').write_text(json.dumps(result,indent=2,sort_keys=True)+'\n')
print(json.dumps(result,indent=2),flush=True)
