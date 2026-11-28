from common import *
import hashlib,shutil,datetime
OUT=ROOT/'docs/release-evidence/2026-11-27-successor-acceptance';OUT.mkdir(exist_ok=True)
assert (BASE/'operational-complete.json').exists() and (BASE/'upgrade-root.txt').exists()
cleanup=[]
for instance in sorted([*BASE.glob('ratefreak-release-acceptance-*'),BASE/'maintenance-restore']):
 if not (instance/'instance.json').exists():continue
 cfg=json.loads((instance/'instance.json').read_text());cmd,environ=a.operator.compose(instance,cfg)
 subprocess.run(cmd+['down','--volumes','--remove-orphans'],env=environ,check=True)
 project=cmd[cmd.index('--project-name')+1]
 containers=subprocess.check_output(['docker','ps','-aq','--filter','label=com.docker.compose.project='+project],text=True).strip()
 volumes=subprocess.check_output(['docker','volume','ls','-q','--filter','label=com.docker.compose.project='+project],text=True).strip();assert not containers and not volumes
 cleanup.append({'project':project,'containers_remaining':False,'volumes_remaining':False})
(OUT/'cleanup.json').write_text(json.dumps(cleanup,indent=2)+'\n')
for src,dst in [('fresh/acceptance.json','fresh.json'),('upgrade/acceptance.json','upgrade.json'),('identities.json','identities.json'),('browser-results.json','browser.json'),('runtime-policy.json','runtime-policy.json'),('maintenance-acceptance.json','maintenance.json')]:shutil.copyfile(BASE/src,OUT/dst)
for label in ['fresh','upgrade']:
 receipt=json.loads((OUT/(label+'.json')).read_text());assert receipt['candidateImages']==images and receipt['referenceImages']==references and receipt['https']
capacity=json.loads((BASE/'capacity.json').read_text());assert capacity['passed'];samples=capacity.pop('resource_samples')
capacity['resource_sampling']={'sample_count':len(samples),'interval_seconds':10,'cpu_max_percent':max(float(service['CPUPerc'].rstrip('%')) for sample in samples for service in sample['services']),'memory_samples':[{'elapsed_seconds':sample['elapsed_seconds'],'services':[{'cpu_percent':service['CPUPerc'],'memory':service['MemUsage']} for service in sample['services']]} for sample in samples]}
(OUT/'capacity.json').write_text(json.dumps(capacity,indent=2)+'\n')
notification={'checked_existing_evidence':['docs/self-hosted-release-plan.md:126-128','docs/STATUS.md:31-33'],'private_reporting_enabled_existing_evidence':True,'maintainer_delivery_verified':False,'new_external_report_or_message_sent':False,'gate':'Existing delivered-notification evidence or an explicitly authorized end-to-end test report is required; read-only evidence cannot establish receipt.'}
(OUT/'notification-gate.json').write_text(json.dumps(notification,indent=2)+'\n')
summary={'source_revision':REVISION,'application_images':images,'reference_images':references,'fresh_shipped_cli':True,'pinned_upgrade_shipped_cli':True,'trusted_browser':True,'recovery_restart_csrf_logout':True,'nonempty_current_policies_restore':True,'song_row_and_artwork_preserved':True,'representative_synthetic_capacity':capacity['passed'],'all_disposable_projects_cleaned':True,'persistent_installation_changed':False,'notification_delivery_verified':False,'publication_performed':False,'utc_completed':datetime.datetime.now(datetime.timezone.utc).isoformat()}
(OUT/'summary.json').write_text(json.dumps(summary,indent=2)+'\n')
scripts=OUT/'scripts';scripts.mkdir(exist_ok=True)
for name in ['run_initial.py','common.py','run_after_fresh.py','run_browser.py','browser_acceptance.cjs','runtime.py','prepare_capacity.py','capacity.py','maintenance.py','resume_operations.py','finalize.py']:shutil.copyfile(BASE/name,scripts/name)
checksums={str(p.relative_to(OUT)):hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted(OUT.rglob('*')) if p.is_file() and p.name!='checksums.json'}
(OUT/'checksums.json').write_text(json.dumps(checksums,indent=2)+'\n')
(BASE/'phase.json').write_text(json.dumps({'running':None,'complete':True,'completed':['fresh','upgrade','run_browser','runtime','capacity','maintenance','cleanup','export']})+'\n')
with (BASE/'HANDOFF.md').open('a') as handoff:handoff.write('\nCOMPLETE: all final-five-image operational proofs passed. All three disposable projects have no containers/volumes. Sanitized checksummed receipts are in docs/release-evidence/2026-11-27-successor-acceptance. No persistent instance changes or external message/report/publication. Notification delivery remains an external unverified gate.\n')
print('Successor exact-five-image operational evidence exported; all disposable projects cleaned.')
