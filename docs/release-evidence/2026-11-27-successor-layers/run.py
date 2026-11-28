from pathlib import Path
import time,subprocess
B=Path(__file__).resolve().parent
candidate=B.parent/'successor-candidate'
while not (candidate/'candidate-manifest.json').exists():time.sleep(5)
for name in ['layer_audit.py','source_archive_audit.py']:
 with (B/(name+'.log')).open('w') as log:subprocess.run(['python3',str(B/name)],stdout=log,stderr=subprocess.STDOUT,check=True)
print('Successor all-five-layer and source archive audits completed.',flush=True)
