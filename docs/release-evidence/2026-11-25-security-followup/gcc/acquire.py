from pathlib import Path
from concurrent.futures import ThreadPoolExecutor
import urllib.request,json,hashlib,datetime
b=Path(__file__).resolve().parent
entries={
'102010.patch':'https://github.com/gcc-mirror/gcc/commit/aaa8351f4d2e636f9680a1f0a8ebc2f0a60611e6.patch',
'114690.patch':'https://github.com/gcc-mirror/gcc/commit/c1f32b67510f6f9998deadaef5090b6663adb979.patch',
'114691.patch':'https://github.com/gcc-mirror/gcc/commit/2fd8866591b5aea7098835f80ab7e176382175ae.patch',
'115456.patch':'https://github.com/gcc-mirror/gcc/commit/524847fec983eaedca147840ce46fcbb0cd38952.patch',
'95619.patch':'https://github.com/gcc-mirror/gcc/commit/59d235ffa5a69231eb42e5290d52dc8c90d28b7a.patch',
'125709-redhat.json':'https://access.redhat.com/hydra/rest/securitydata/cve/CVE-2026-125709.json',
'gcc14-charconv':'https://raw.githubusercontent.com/gcc-mirror/gcc/releases/gcc-14.2.0/libstdc++-v3/include/std/charconv',
'gcc14-fstream.tcc':'https://raw.githubusercontent.com/gcc-mirror/gcc/releases/gcc-14.2.0/libstdc++-v3/include/bits/fstream.tcc',
'gcc14-new':'https://raw.githubusercontent.com/gcc-mirror/gcc/releases/gcc-14.2.0/libstdc++-v3/libsupc++/new_opa.cc',
}
def fetch(item):
 name,url=item
 try:
  data=urllib.request.urlopen(urllib.request.Request(url,headers={'User-Agent':'RateFreak-native-caller-review'}),timeout=90).read();(b/name).write_bytes(data)
  return {'file':name,'url':url,'sha256':hashlib.sha256(data).hexdigest(),'bytes':len(data),'retrieved_utc':datetime.datetime.now(datetime.timezone.utc).isoformat()}
 except Exception as error:return {'file':name,'url':url,'error':str(error)}
r=list(ThreadPoolExecutor(max_workers=6).map(fetch,entries.items()));(b/'acquisition.json').write_text(json.dumps(r,indent=2)+'\n')
for row in r:print(row['file'],row.get('bytes',row.get('error')))
