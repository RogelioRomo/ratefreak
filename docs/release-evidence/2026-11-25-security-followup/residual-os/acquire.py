from pathlib import Path
from concurrent.futures import ThreadPoolExecutor
import urllib.request,json,hashlib,datetime
b=Path(__file__).resolve().parent/'vendor';b.mkdir(exist_ok=True)
urls={'python-tempfile-fix.patch':'https://github.com/python/cpython/commit/0478b52788117db4b831b3efd648f56b777628aa.patch'}
for n in ['0025','0027','0028','0029','0030','0031','0032']:
 name='GLIBC-SA-2026-'+n;urls[name+'.txt']='https://sourceware.org/git/?p=glibc.git;a=blob_plain;f=advisories/'+name+';hb=HEAD'
def fetch(item):
 name,url=item
 try:
  data=urllib.request.urlopen(url,timeout=60).read();(b/name).write_bytes(data)
  return {'file':name,'url':url,'sha256':hashlib.sha256(data).hexdigest(),'bytes':len(data),'retrieved_utc':datetime.datetime.now(datetime.timezone.utc).isoformat()}
 except Exception as e:return {'file':name,'url':url,'error':str(e)}
r=list(ThreadPoolExecutor(max_workers=5).map(fetch,urls.items()));(b/'acquisition.json').write_text(json.dumps(r,indent=2)+'\n');print('acquired',sum('error' not in x for x in r),'of',len(r));print([x for x in r if 'error' in x])
