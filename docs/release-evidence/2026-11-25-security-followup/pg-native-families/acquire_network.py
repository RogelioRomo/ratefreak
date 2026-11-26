import json,hashlib,urllib.request,concurrent.futures
from pathlib import Path
b=Path(__file__).resolve().parent;rows=json.loads((b/'network-findings.json').read_text())
def get(r):
 c=r['advisory'];year=c.split('-')[1];num=c.split('-')[2];folder=num[:-3]+'xxx';u=f'https://raw.githubusercontent.com/CVEProject/cvelistV5/main/cves/{year}/{folder}/{c}.json';data=urllib.request.urlopen(u,timeout=50).read();p=b/(c+'.json');p.write_bytes(data);return {'file':p.name,'url':u,'sha256':hashlib.sha256(data).hexdigest()}
with concurrent.futures.ThreadPoolExecutor(max_workers=8) as p:r=list(p.map(get,rows))
(b/'network-acquisition.json').write_text(json.dumps(r,indent=2)+'\n');print('acquired',len(r))
