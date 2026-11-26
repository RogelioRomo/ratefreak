import json,hashlib
from pathlib import Path
b=Path(__file__).resolve().parent;repo=b.parents[1];comparisons={};images={}
for role in ['api','web','tools']:
 a=json.loads((b/f'phase2-{role}-probe.json').read_text());z=json.loads((b/f'final-{role}-probe.json').read_text());aa={x['path']:x for x in a['elf']};zz={x['path']:x for x in z['elf']};changes=[]
 for p in aa.keys()|zz.keys():
  if aa.get(p)!=zz.get(p):changes.append({'path':p,'before':aa.get(p),'after':zz.get(p)})
 c=json.loads((b/f'phase2-config-{role}-probe.json').read_text());d=json.loads((b/f'final-config-{role}-probe.json').read_text());c.pop('image_id');d.pop('image_id');images[role]=z['image_id']
 comparisons[role]={'source_files_equal':a['source_files']==z['source_files'],'commands_equal':a['commands']==z['commands'],'config_metadata_equal':c==d,'elf_changes':changes,'elf_count':len(z['elf'])}
 assert comparisons[role]['source_files_equal'] and comparisons[role]['commands_equal'] and comparisons[role]['config_metadata_equal']
 for x in changes:
  assert role=='tools' and x['path']=='/app/efbundle'
  assert x['before']['needed']==x['after']['needed'] and x['before']['selected_symbols']==x['after']['selected_symbols']
rows=json.loads((b/'phase2-assessments.json').read_text())
for x in rows:
 x['images']=images;x['evidence'] += ['final-probe-executions.json','final-config-executions.json','final-rebind-comparison.json'];x['rebind_limit']='API/web full native inventory and Python sources identical to phase2. Tools inventory identical except efbundle hash; DT_NEEDED/selected imports unchanged. Fixed-role app source has no added regex/glob/wide-scanf interop. Final configuration metadata is identical. This does not resolve mitigation proof gaps.'
(b/'final-assessments.json').write_text(json.dumps(rows,indent=2)+'\n');(b/'final-rebind-comparison.json').write_text(json.dumps(comparisons,indent=2)+'\n')
files=list(b.glob('final*json'))+[b/'rebind_final.py',repo/'deploy/self-host/entrypoint.py',repo/'deploy/self-host/maintenance.py',repo/'src/RateFreak.Infrastructure/Providers/Tmdb/TmdbContentPolicy.cs',repo/'src/RateFreak.Api/Middleware/GlobalExceptionHandler.cs']
(b/'final-hashes.json').write_text(json.dumps({str(p.relative_to(repo)):hashlib.sha256(p.read_bytes()).hexdigest() for p in files if p.name!='final-hashes.json'},indent=2)+'\n')
print(images)
