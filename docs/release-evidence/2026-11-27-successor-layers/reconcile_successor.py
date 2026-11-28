"""Classify every application raw-layer delta and bind embedded/native payloads."""
from pathlib import Path
import hashlib,json,re
B=Path(__file__).resolve().parent;Q=B.parent;R=Q.parents[1]
OLD=R/'docs/release-evidence/2026-11-27-stable-layers'
def read(p):return json.loads(p.read_text())
def sha(p):
 with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
a=read(B/'all-layer-audit.json');p=read(OLD/'all-layer-audit.json')
out={'revision':a['revision'],'images':{},'reviewed_matches':[],'blockers':[],'scope':'All five delivered raw image layers; app deltas compare f540, reference deltas compare fully reviewed patched trial. No reproducible upstream build claim.'}
reference_files=[x for role in ['caddy','postgres'] for x in read(B/(role+'-layer-files.json'))]
ref={(x['path'],x['sha256']) for x in reference_files}
shadow_paths={x['path'] for x in read(B/'web-layer-files.json') if x['layer']==1 and (x['path'].startswith(('usr/bin/','usr/sbin/','usr/lib/tmpfiles.d/','usr/share/doc/','var/lib/dpkg/info/')))}
for role,x in a['images'].items():
 assert x['oci_identity_chain_valid'] and x['revision_label_matches'] and x['version_label_matches']
 assert all(l['rootfs_diff_id_matches'] for l in x['layers'])
 assert not x['private_value_matches'] and not x['suspicious_paths']
 info={'image_id':x['image_id'],'layers':len(x['layers']),'regularFilesScanned':x['files_scanned'],'bytesScanned':x['bytes_scanned'],'layerBlobAndDiffIDAndOCIIdentityValid':True,'privateValueMatches':[],'suspiciousPaths':[],'lowerLayerOnlyPackages':x['lower_layer_only_packages']}
 if role in ['api','web','tools']:
  old=read(OLD/(role+'-layer-files.json'));new=read(B/(role+'-layer-files.json'));om={(v['layer'],v['path']):v for v in old};nm={(v['layer'],v['path']):v for v in new}
  changes=[]
  for k in sorted(om.keys()|nm.keys()):
   before=om.get(k);after=nm.get(k)
   if before and after and before['sha256']==after['sha256']:continue
   path=k[1];category=None
   if path.startswith(('var/log/','var/cache/ldconfig/','var/lib/dpkg/')):category='Updated package transaction metadata'
   elif path.startswith('app/RateFreak.') and path.endswith(('.dll','.pdb')):category='First-party assembly or debugging symbols, rebuilt at frozen successor revision'
   elif path=='app/efbundle':category='First-party migration bundle; separately audited embedded third-party bytes unchanged'
   elif path.startswith(('app/notices/runtime/','app/public/notices/runtime/')):category='Updated exact package inventory, runtime evidence addresses, or tar container; actual license text member bytes unchanged'
   elif path.startswith('app/.next/') or path=='app/server.js':category='Generated Next output or build-ID manifests; third-party package inventory, executable chunks and copied runtime/native source fingerprints independently compared'
   elif role in ['web','tools'] and path in shadow_paths and after and (path,after['sha256']) in ref:category='Patched Debian shadow package; exact file fingerprint also present in reviewed reference package layers'
   elif role=='api' and (path.startswith('usr/lib/x86_64-linux-gnu/') or path=='usr/share/doc/libkrb5support0/changelog.Debian.gz'):category='Patched krb5 1.20.1-6ubuntu2.12 payload; exact current source closure retained'
   assert category,(role,k)
   changes.append({'layer':k[0],'path':path,'before_sha256':before['sha256'] if before else None,'sha256':after['sha256'] if after else None,'review':category})
  # Every third-party app native fingerprint from the previous inventory still exists.
  current={(v['path'],v['sha256']) for v in new}
  retained=[v for v in p['images'][role]['native_payloads'] if v['path'].startswith(('app/','usr/share/dotnet/','usr/local/')) and not v['path'].startswith('app/RateFreak.') and v['path']!='app/efbundle']
  assert all((v['path'],v['sha256']) in current for v in retained)
  assert x['font_payloads']==p['images'][role]['font_payloads'] and not x['og_wasm_payloads']
  info.update({'changes':changes,'retainedPriorThirdPartyNativeFingerprints':len(retained),'fontPayloadsIdentical':True,'ogWasmTtfPayloads':[],'priorFileInventorySHA256':sha(OLD/(role+'-layer-files.json'))})
 for hit in x['credential_pattern_matches']:
  path=hit['path'];file=next(v for v in read(B/(role+'-layer-files.json')) if v['layer']==hit['layer'] and v['path']==path)
  if 'libgnutls.so.' in path:
   disposition='Public cryptographic fixture embedded in inherited vendor GnuTLS library; exact fingerprint retained. Not a configured RateFreak value.'
   if role=='api':assert any(v['path']==path and v['sha256']==file['sha256'] for v in read(OLD/'api-layer-files.json'))
  elif role=='postgres' and path=='etc/ssl/private/ssl-cert-snakeoil.key':
   disposition='Inherited vendor snakeoil key, publicly distributed fixture. Supported PG ssl=off proof and replacement requirement tracked in successor security review; never suitable for enabled TLS.'
  else:raise AssertionError(hit)
  out['reviewed_matches'].append({'image':role,**hit,'sha256':file['sha256'],'disposition':disposition})
 out['images'][role]=info
ef=read(B/'efbundle-inventory.json');prior=read(OLD/'efbundle-inventory.json');em={v['file']:v for v in ef['files']};pm={v['file']:v for v in prior['files']};assert em.keys()==pm.keys()
changed=[k for k in em if em[k]['sha256']!=pm[k]['sha256']]
assert set(changed)=={'efbundle.dll','RateFreak.Api.dll','RateFreak.Application.dll','RateFreak.Domain.dll','RateFreak.Infrastructure.dll'}
packages=lambda v:{k:x for k,x in v['dependency_manifest']['libraries'].items() if x['type']=='package'}
assert packages(ef)==packages(prior) and not ef['unmapped_package_versions']
out['efbundle']={'image_id':ef['image_id'],'sha256':ef['sha256'],'files':len(em),'dependencyLibraries':len(ef['dependency_manifest']['libraries']),'changedEmbeddedFiles':changed,'allThirdPartyEmbeddedBytesAndPackageRecordsUnchanged':True,'apphostSHA256':em['RateFreak.Api']['sha256'],'apphostMarkers':em['RateFreak.Api']['runtime_markers']}
out['limits']=['Selected content heuristics and seven configured sensitive values >=12 bytes are not proof of secret absence.','Reference source review must bind all raw-layer source obligations and exact notices separately.','Public vendor snakeoil fixture is present; enabling PostgreSQL TLS requires a private replacement and reassessment.','No publication or persistent instance changes. Original Sharp header and font download/subsetting provenance limits remain.']
(B/'payload-reconciliation.json').write_text(json.dumps(out,indent=2,sort_keys=True)+'\n')
print(json.dumps({'images':{k:{'layers':v['layers'],'changes':len(v.get('changes',[]))} for k,v in out['images'].items()},'efbundle':out['efbundle'],'blockers':out['blockers']},indent=2))
