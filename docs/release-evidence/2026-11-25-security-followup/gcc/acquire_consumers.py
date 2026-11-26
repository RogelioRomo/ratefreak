exec((__import__('pathlib').Path(__file__).parent/'acquire.py').read_text().split('r=list(')[0])
entries={
'node-v24.22.3.tar.xz':'https://nodejs.org/dist/v24.22.3/node-v24.22.3.tar.xz',
'dotnet-runtime-a3788b7.tar.gz':'https://codeload.github.com/dotnet/runtime/tar.gz/a3788b71495457354b64ca8040b6ded5ff6ca4f9',
}
r=list(ThreadPoolExecutor(max_workers=2).map(fetch,entries.items()));(b/'consumer-acquisition.json').write_text(json.dumps(r,indent=2)+'\n')
for row in r:print(row['file'],row.get('bytes',row.get('error')))
