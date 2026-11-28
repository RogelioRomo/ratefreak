from common import *
import shutil,hashlib
OUT=ROOT/'docs/release-evidence/2026-11-27-stable-acceptance';OUT.mkdir(exist_ok=True)
cleanup=[]
for instance in sorted(BASE.glob('ratefreak-release-acceptance-*')):
 if not (instance/'instance.json').exists():continue
 cfg=json.loads((instance/'instance.json').read_text());cmd,environ=a.operator.compose(instance,cfg)
 subprocess.run(cmd+['down','--volumes','--remove-orphans'],env=environ,check=True)
 project=cmd[cmd.index('--project-name')+1]
 containers=subprocess.check_output(['docker','ps','-aq','--filter','label=com.docker.compose.project='+project],text=True).strip()
 volumes=subprocess.check_output(['docker','volume','ls','-q','--filter','label=com.docker.compose.project='+project],text=True).strip();assert not containers and not volumes
 cleanup.append({'project':project,'containers_remaining':False,'volumes_remaining':False})
restore=BASE/'maintenance-restore'
if restore.exists():
 cfg=json.loads((restore/'instance.json').read_text());cmd,environ=a.operator.compose(restore,cfg);project=cmd[cmd.index('--project-name')+1]
 for resource,arguments in [('containers',['docker','ps','-aq']),('volumes',['docker','volume','ls','-q'])]:assert not subprocess.check_output(arguments+['--filter','label=com.docker.compose.project='+project],text=True).strip()
 cleanup.append({'project':project,'containers_remaining':False,'volumes_remaining':False})
(OUT/'cleanup.json').write_text(json.dumps(cleanup,indent=2)+'\n')
for source,destination in [('fresh/acceptance.json','fresh.json'),('upgrade/acceptance.json','upgrade.json'),('upgrade-preservation.json','upgrade-preservation.json'),('browser-results.json','browser.json'),('runtime-policy.json','runtime-policy.json'),('maintenance-acceptance.json','maintenance.json')]:shutil.copyfile(BASE/source,OUT/destination)
for name,destination_name in [('capacity-populated','capacity'),('capacity','capacity-small'),('capacity-burst','capacity-burst')]:
 data=json.loads((BASE/(name+'.json')).read_text());data['workload'].setdefault('dataset',{'media_items':3,'authenticated_accounts':6,'library_entries_per_account':1,'hot_public_items_read':1,'synthetic_uploads':'three 2x2 fixtures'});samples=data.pop('resource_samples');data['resource_sampling']={'sample_count':len(samples),'interval_seconds':10,'cpu_max_percent':max(float(service['CPUPerc'].rstrip('%')) for sample in samples for service in sample['services']),'memory_samples':[{'elapsed_seconds':sample['elapsed_seconds'],'services':[{'cpu_percent':service['CPUPerc'],'memory':service['MemUsage']} for service in sample['services']]} for sample in samples]}
 if name=='capacity-burst':data['interpretation']='Intentional observation of an unchanged process-local shared-IP catalog budget: 60/min. Latency statistics include rejected operations; this workload did not meet the zero-error threshold.'
 (OUT/(destination_name+'.json')).write_text(json.dumps(data,indent=2)+'\n')
notification={'checked_existing_evidence':['docs/self-hosted-release-plan.md:126-128','docs/STATUS.md:31-33'],'private_reporting_enabled_existing_evidence':True,'test_report_ever_submitted_in_evidence':False,'maintainer_delivery_verified':False,'new_external_report_or_message_sent':False,'gate':'Maintainer must supply existing delivered-notification evidence or explicitly authorize an end-to-end test report; read-only checks cannot establish receipt.'}
(OUT/'notification-gate.json').write_text(json.dumps(notification,indent=2)+'\n')
scripts=OUT/'scripts';scripts.mkdir(exist_ok=True)
for name in ['run_initial.py','common.py','run_upgrade.py','run_browser.py','browser_acceptance.cjs','capacity.py','maintenance.py','runtime.py','cleanup_upgrade.py','finalize.py','prepare_maintenance.py','resume_maintenance.py','prepare_capacity.py','capacity-populated.py']:shutil.copyfile(BASE/name,scripts/name)
checksums={str(p.relative_to(OUT)):hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted(OUT.rglob('*')) if p.is_file() and p.name!='checksums.json'}
(OUT/'checksums.json').write_text(json.dumps(checksums,indent=2)+'\n')
print('Sanitized acceptance receipts saved; all acceptance services and volumes removed.')
