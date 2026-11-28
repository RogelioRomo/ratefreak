from pathlib import Path
import importlib.util,json,os
BASE=Path(__file__).resolve().parent
ROOT=BASE.parents[2]
SOURCE=ROOT/'.local/stable-candidate-2026-11-27/source-qualified'
os.environ['TMPDIR']=str(BASE)
spec=importlib.util.spec_from_file_location('release_acceptance',SOURCE/'deploy/release/acceptance.py');a=importlib.util.module_from_spec(spec);spec.loader.exec_module(a)
images=json.loads((ROOT/'.local/stable-candidate-2026-11-27/candidate-qualified/built-images.json').read_text())
for label,baseline in [('fresh',None),('upgrade',json.loads((BASE/'baseline-images.json').read_text()))]:
 root,result=a.run(images,BASE/label,baseline,keep=True)
 (BASE/(label+'-root.txt')).write_text(str(root)+'\n')
 print(label+' complete '+str(root),flush=True)
