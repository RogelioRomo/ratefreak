"""Bind reviewed unchanged payloads to the exact frozen candidate fingerprints."""
from pathlib import Path
import hashlib,json,ast
B=Path(__file__).resolve().parent;R=B.parents[2];OLD=R/'.local/security-runtime-guard-2026-11-25';S=R/'.local/stable-candidate-2026-11-27/source-qualified'
def read(p):return json.loads(p.read_text())
def sha(p):return hashlib.file_digest(p.open('rb'),'sha256').hexdigest()
a=read(B/'all-layer-audit.json');o=read(OLD/'all-layer-audit.json');out={'revision':a['revision'],'scope':'Original frozen f54048c candidate only; does not bind any successor build. Redistribution/privacy delta review, not security or stable-release clearance.','images':{},'reviewed_matches':[],'blockers':[]}
for n,x in a['images'].items():
 y=o['images'][n];old=read(OLD/(n+'-layer-files.json'));new=read(B/(n+'-layer-files.json'));om={(v['layer'],v['path']):v for v in old};nm={(v['layer'],v['path']):v for v in new};changes=[{'layer':k[0],'path':k[1],'previous_sha256':om[k]['sha256'],'sha256':nm[k]['sha256']} for k in sorted(om.keys()&nm.keys()) if om[k]['sha256']!=nm[k]['sha256']]
 native=lambda z:{(f['path'],f['sha256']) for f in z['native_payloads'] if not f['path'].startswith('app/RateFreak.')}
 packages_equal=[l['installed_packages'] for l in x['layers']]==[l['installed_packages'] for l in y['layers']]
 assert packages_equal and x['lower_layer_only_packages']==y['lower_layer_only_packages'] and native(x)==native(y)
 i=out['images'][n]={'image_id':x['image_id'],'files':x['files_scanned'],'bytes':x['bytes_scanned'],'layers':len(x['layers']),'prior_all_layer_audit_sha256':sha(OLD/'all-layer-audit.json'),'prior_file_inventory_sha256':sha(OLD/(n+'-layer-files.json')),'current_file_inventory_sha256':sha(B/(n+'-layer-files.json')),'all_installed_package_layer_snapshots_identical':packages_equal,'lower_layer_only_packages':x['lower_layer_only_packages'],'third_party_native_path_hash_pairs_identical':True,'font_payloads_identical':x['font_payloads']==y['font_payloads'],'og_wasm_ttf_payloads':x['og_wasm_payloads'],'changed_files':changes,'added_files':[{'layer':k[0],'path':k[1]} for k in sorted(nm.keys()-om.keys())],'removed_files':[{'layer':k[0],'path':k[1]} for k in sorted(om.keys()-nm.keys())]}
 for hit in x['credential_pattern_matches']:
  k=(hit['layer'],hit['path']);assert nm[k]['sha256']==om[k]['sha256'];out['reviewed_matches'].append({'image':n,**hit,'sha256':nm[k]['sha256'],'disposition':'Inherited identical GnuTLS library cryptographic fixture content, previously reviewed; not a configured RateFreak secret. Entire library hash matches Nov25; earlier reviewer classification is retained only for these exact bytes.'})
 assert not x['private_value_matches'] and not x['suspicious_paths']
ef=read(B/'efbundle-inventory.json');prev=read(B/'efbundle-previous-inventory.json');em={r['file']:r for r in ef['files']};pm={r['file']:r for r in prev['files']};assert em.keys()==pm.keys();changed=[k for k in em if em[k]['sha256']!=pm[k]['sha256']]
assert set(changed)=={'efbundle.dll','RateFreak.Api.dll','RateFreak.Application.dll','RateFreak.Domain.dll','RateFreak.Infrastructure.dll','efbundle.deps.json'}
old_libs=prev['dependency_manifest']['libraries'];libs=ef['dependency_manifest']['libraries'];assert {k:v for k,v in old_libs.items() if v['type']=='package'}=={k:v for k,v in libs.items() if v['type']=='package'}
out['efbundle']={'sha256':ef['sha256'],'files':len(em),'dependency_libraries':len(libs),'package_library_records_identical':True,'changed_embedded_files':changed,'unmapped_package_versions':ef['unmapped_package_versions'],'embedded_third_party_files_identical':True,'embedded_apphost_sha256':em['RateFreak.Api']['sha256'],'apphost_markers':em['RateFreak.Api']['runtime_markers'],'framework_notice_acquisition_sha256':sha(OLD/'validated-source-materials/dotnet-acquisition.json'),'apphost_scope':'Exact embedded apphost fingerprint matches Nov25. Same commit source acquisition and complete MIT/third-party framework notice supplement retained. No source rebuild/reproducibility claim.'}
font=read(OLD/'validated-source-materials/font-review.json')
web=read(B/'web-layer-files.json')
for f in font['fonts']:
 assert sha(S/f['file'])==f['sha256']
 assert all(any(x['path']==p and x['sha256']==f['sha256'] for x in web) for p in f['image_paths'])
out['fonts']={'all_font_and_ofl_file_hashes_match_prior_review':True,'review_sha256':sha(OLD/'validated-source-materials/font-review.json'),'limits':'Original download/subsetting history not reconstructed; unchanged OFL and source provenance limitations retained.'}
out['limits']=['Heuristic secret scan tests seven configured sensitive values of at least12bytes and selected token/key patterns, not proof all secrets absent.','Every archived regular file scanned, including overwritten/whiteout-hidden payload; tar metadata inventory also retained. No claim about unrecorded build-stage data or host files.','All-layer package identity matching does not itself prove source-archive sufficiency or vulnerability applicability. Main source reconciliation must retain original lower-layer and updated OS archives.','Sharp prebuilt addon exact header resolution remains unproven; native replacement proof retains prior scope. No new upstream reproducible-build claim.','No publication, retagging, rebuilding or persistent instance modification performed by this audit.']
(B/'payload-reconciliation.json').write_text(json.dumps(out,indent=2,sort_keys=True)+'\n')
print(json.dumps({'images':{n:{'layers':v['layers'],'files':v['files'],'changed':len(v['changed_files'])} for n,v in out['images'].items()},'efbundle':out['efbundle'],'blockers':out['blockers']},indent=2))
