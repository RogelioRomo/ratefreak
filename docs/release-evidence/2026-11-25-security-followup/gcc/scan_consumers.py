from pathlib import Path
import tarfile,json,re,hashlib
b=Path(__file__).resolve().parent;root=b.parents[2]
archives=[*Path('.local/release-compliance/native-sources').glob('*.tar.*'),*Path('.local/security-skia-update-2026-11-25/notice-review/upstream/archives').glob('skia-*.tar.gz')]
archives += [p for p in [b/'node-v24.22.3.tar.xz',b/'dotnet-runtime-a3788b7.tar.gz'] if p.exists()]
archives += list(Path('.local/release-compliance/os-audit-2026-11-21/source-files').glob('*icu_74.2.orig.tar.gz'))
archives += list(Path('.local/debian-trial-2026-11-23/source-files').glob('*icu_76.1.orig.tar.gz'))
archives += [Path('.local/security-clearance-2026-11-25/source-materials/sharp-0.35.5.tgz'),Path('.local/security-clearance-2026-11-25/source-materials/node-addon-api-8.9.2.tgz'),Path('.local/release-compliance/source-set/packaging/sharp-libvips-packaging.tar.gz')]
patterns={'pbds':re.compile(r'pb_ds|__gnu_pbds|binary_heap_tag'),'parallel':re.compile(r'_GLIBCXX_PARALLEL|__gnu_parallel|[<"]parallel/(?:search|algorithm)'), 'vector_bool':re.compile(r'(?:std::)?vector\s*<\s*bool\b'),'filebuf':re.compile(r'basic_filebuf|std::(?:filebuf|ofstream)|\.sputc\s*\('),'from_chars':re.compile(r'std::from_chars\s*\(')}
rows=[];counts={};inputs=[]
for path in archives:
 try:t=tarfile.open(path)
 except tarfile.ReadError:continue
 inputs.append({'file':str(path),'sha256':hashlib.sha256(path.read_bytes()).hexdigest()});files=0
 with t:
  for member in t:
   if not member.isfile() or not member.name.lower().endswith(('.cc','.cpp','.cxx','.c','.h','.hpp','.hxx','.inl','.inc','.gn','.gni','.cmake','cmakelists.txt','.gyp','.gypi','.sh','.py','.ac','.in','meson.build','meson_options.txt')):continue
   if member.size>50*1024*1024:continue
   data=t.extractfile(member).read().decode('utf-8','replace');files+=1
   found={name:[] for name in patterns}
   for line_no,line in enumerate(data.splitlines(),1):
    for name,pattern in patterns.items():
     if pattern.search(line):found[name].append({'line':line_no,'text':line[:350]})
   found={name:matches for name,matches in found.items() if matches}
   if found:rows.append({'archive':str(path),'file':member.name,'file_sha256':hashlib.sha256(data.encode()).hexdigest(),'test_or_example':bool(re.search(r'(?:^|/)(?:tests?|testing|benchmarks?|examples?|fuzz[^/]*)(?:/|\.)',member.name,re.I)),'matches':found})
 counts[str(path)]=files
print('archives',len(inputs),'sourcefiles',sum(counts.values()),'fileswithhits',len(rows))
(b/'source-search.json').write_text(json.dumps({'archives':inputs,'source_files':counts,'matches':rows},indent=2)+'\n')
for name in patterns:
 hits=[r for r in rows if name in r['matches'] and not r['test_or_example']];print(name,len(hits))
 for row in hits[:70]:
  print(row['file'],row['matches'][name][:4])
