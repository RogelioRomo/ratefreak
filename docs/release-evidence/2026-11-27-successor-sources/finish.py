from pathlib import Path
import hashlib,json,shutil,sys
B=Path(__file__).resolve().parent;Q=B.parent;R=Q.parents[1];O=B/'source-materials';S=Q/'successor-source';C=Q/'successor-candidate'
sys.path.insert(0,str(S/'deploy/release'));from prepare import source_evidence
def sha(p):
 with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def write(n,x):(O/n).write_text(json.dumps(x,indent=2,sort_keys=True)+'\n')
payload=json.loads((Q/'successor-layers/payload-reconciliation.json').read_text());assert not payload['blockers']
rec=json.loads((B/'source-reconciliation.json').read_text());assert not rec['gaps']
for n in ['payload-reconciliation.json','all-layer-audit.json','efbundle-inventory.json','source-archive-audit.json']:
 shutil.copyfile(Q/'successor-layers'/n,O/('successor-'+n))
review='''# Frozen successor application source and notice review

Revision: 9eea2c76550fe03920dd999f1677565e4f43d12b; exact final application image IDs are in coverage.json.

All502 application component entries are mapped. The only component-version changes from retained f540 coverage are four API krb5 libraries upgraded to1.20.1-6ubuntu2.12 and login.defs/passwd in each Debian web/tools image upgraded to1:4.17.4-2+deb13u1. No application npm/NuGet/native/framework/copied-runtime component version changed. Exact package notice manifests and all nested runtime license-text member names and bytes match the original reviewed candidate. Native runtime fingerprints match; ldd addresses and OS inventory hashes differ. Full original license grants and supplemental notices remain delivered; prior reviews retain their original dates and scope.

All original coverage material hashes were freshly read back. The retained five installed/inherited OS source archives contain849 verified source-input entries; new application-os-updates.tar contains3 exact krb5 inputs plus original authenticated acquisition metadata, and reference-debian-os-sources.tar contains334 verified source-input references for103 source tuples, including patched shadow. The source-reconciliation receipt maps every current OS package and every raw-layer installed/superseded source pair to supplied archives, including overwritten lower layers. Source descriptor closure/authentication records from original acquisition are retained; no new upstream binary reproduction or blanket new signature-verification claim is made. The new krb5 archive's acquisition manifest names trial IDs honestly; current image binding is established separately by the exact final installed package inventory and all-layer source reconciliation.

The406-input Sharp/libvips sidecar was freshly generated from the locked source set and verified on readback, bound to the exact final web image and source/native profile. Original LGPL source/build/replacement instructions, Rust/native build inputs, Apache/header collaborator notices and complete license texts remain supplied. Exact upstream Sharp prebuild header resolution remains unproven. The original two WOFF2 font/OFL fingerprints match all final layers; original font download/subsetting history remains unproven. No OG/satori/WASM/TTF payload was found in application layers. No new source reproducibility claim follows from matching fingerprints.

Every application raw-layer file change is itemized in successor-payload-reconciliation.json. Changed third-party OS payload is confined to the recorded compatible patches. The patched Debian shadow file fingerprints also match the reference package layers. First-party assemblies and generated Next build output reflect this exact frozen revision. Application third-party native runtime payload fingerprints and package manifests are unchanged. Migration executable104 embedded files and36 dependency libraries were parsed without execution: only5 first-party assemblies changed from f540; every third-party embedded byte, package record, dependency manifest and apphost fingerprint is unchanged. Exact .NET10.0.15 apphost commit8e0f8ed2312954727f8919d7d1c2f78b483b4ee7 retains the full acquired framework MIT/third-party supplement and documented permissive-source exception. Read prior-component-review.md, prior-runtime-input-review.md, prior-f540-source-review.md and retained original source/grant records for complete per-component decisions.

The installation archive matches all1731 committed source blobs plus generated SOURCE_REVISION. Source and selected-credential privacy checks have explicit heuristic limits; no configured sensitive-value match was found. Deliver every indexed source archive, original notice, sidecar and supplemental review with any subsequently authorized binary distribution.

This coverage.json covers application images only. Additional PostgreSQL and Debian Caddy images require the independent exact-image reference-coverage.json and all its materials; the final source-evidence validation enforces both. Alpine APK/OpenSSL grant uncertainty belongs to a nondelivered historical trial and is not cleared or included by this review. Neither source coverage nor permissive exceptions establish CVE applicability or publication approval. Security and operational acceptance remain separate records.
'''
(O/'release-source-review.md').write_text(review)
(O/'README.md').write_text('Frozen9eea2c7 application source materials. Read release-source-review.md and coverage.json. Five-image distribution also requires reference-coverage.json and its entire material set. No publication approval.\n')
coverage=json.loads((B/'coverage-pending.json').read_text())
files={p.name:sha(p) for p in sorted(O.iterdir()) if p.is_file() and p.name not in {'coverage.json','reference-coverage.json'}}
for role,e in coverage['images'].items():
 e['files']={k:v for k,v in files.items() if role=='web' or k!='web.native-sources.tar.gz'}
 if role=='web':e['native_sidecar']='web.native-sources.tar.gz'
write('coverage.json',coverage)
inv=json.loads((C/'source-inventory.json').read_text())
source_evidence(O,{r:e['image_id'] for r,e in inv['images'].items()},B/'validated-app-materials',inv)
result={'revision':rec['revision'],'valid':True,'images':{r:e['image_id'] for r,e in inv['images'].items()},'components':502,'nativeInputs':406,'sourceInputsReadBack':sum(x['verifiedSourceInputs'] for x in rec['os_archives'].values()),'materialFiles':len(files),'securityClearance':False,'publicationAuthorized':False}
(B/'source-validation.json').write_text(json.dumps(result,indent=2,sort_keys=True)+'\n');print(json.dumps(result,indent=2),flush=True)
