from pathlib import Path
import json,subprocess,shutil
BASE=Path(__file__).resolve().parent;ROOT=BASE.parents[2]
root=Path((BASE/'fresh-root.txt').read_text().strip())
fixture=json.loads((root/'browser-fixture.json').read_text());state=json.loads((root/'acceptance.json').read_text());fixture['mediaId']=state['public']['id']
(BASE/'fixture.json').write_text(json.dumps(fixture)+'\n');(BASE/'fixture.json').chmod(0o600);shutil.copyfile(root/'ca.crt',BASE/'ca.crt')
script='''mkdir -p /root/.pki/nssdb
certutil -N --empty-password -d sql:/root/.pki/nssdb
certutil -A -d sql:/root/.pki/nssdb -t 'C,,' -n RateFreakStable -i /proof/ca.crt
node /proof/browser_acceptance.cjs
'''
subprocess.run(['docker','run','--rm','--network','host','--mount',f'type=bind,src={BASE},dst=/proof','--mount',f'type=bind,src={ROOT}/.local/release-candidate-2026-11-23/browser-driver,dst=/driver,readonly','--entrypoint','sh','ratefreak-browser-acceptance:local','-ec',script],check=True)
