from pathlib import Path
import hashlib,json,os,re,subprocess,time
BASE=Path(__file__).resolve().parent;ROOT=BASE.parents[2];SOURCE=BASE.parent/'successor-source';CANDIDATE=BASE.parent/'successor-candidate'
REVISION='9eea2c76550fe03920dd999f1677565e4f43d12b'
print('Waiting for completed five-image candidate manifest.',flush=True)
while not all((CANDIDATE/name).is_file() for name in ['candidate-manifest.json','built-images.json','built-reference-images.json']):time.sleep(15)
assert subprocess.check_output(['git','-C',str(SOURCE),'rev-parse','HEAD'],text=True).strip()==REVISION
images=json.loads((CANDIDATE/'built-images.json').read_text());references=json.loads((CANDIDATE/'built-reference-images.json').read_text())
assert set(images)=={'api','web','tools'} and set(references)=={'postgres','caddy'}
identities={}
for role,identity in (images|references).items():
 metadata=json.loads(subprocess.check_output(['docker','image','inspect',identity],text=True))[0]
 assert metadata['Id']==identity and metadata['Os']=='linux' and metadata['Architecture']=='amd64'
 labels=metadata['Config']['Labels'];assert labels['org.opencontainers.image.version']=='0.1.0' and labels['org.opencontainers.image.revision']==REVISION
 identities[role]={'id':identity,'platform':'linux/amd64','version':labels['org.opencontainers.image.version'],'revision':labels['org.opencontainers.image.revision']}
(BASE/'identities.json').write_text(json.dumps({'source_revision':REVISION,'images':identities,'application_images':images,'reference_images':references},indent=2)+'\n')
for label in ['fresh','upgrade']:
 command=['python3',str(SOURCE/'deploy/release/acceptance.py'),'--confirm-disposable','--report-directory',str(BASE/label),'--images-json',str(CANDIDATE/'built-images.json'),'--reference-images-json',str(CANDIDATE/'built-reference-images.json'),'--keep-disposable']
 if label=='upgrade':command+=['--baseline-images-json',str(BASE/'baseline-images.json')]
 environment=os.environ.copy();environment.update(TMPDIR=str(BASE),PYTHONDONTWRITEBYTECODE='1')
 with (BASE/(label+'.log')).open('w') as log:
  subprocess.run(command,env=environment,stdout=log,stderr=subprocess.STDOUT,check=True)
 matches=re.findall(r'^Disposable browser fixture: (.+)$',(BASE/(label+'.log')).read_text(),re.M);assert len(matches)==1
 root=Path(matches[0]);assert root.is_relative_to(BASE);(BASE/(label+'-root.txt')).write_text(str(root)+'\n')
 receipt=json.loads((BASE/label/'acceptance.json').read_text());assert receipt['candidateImages']==images and receipt['referenceImages']==references and receipt['https']
 print('Shipped '+label+' CLI passed exact five-image acceptance.',flush=True)
