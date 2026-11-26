import urllib.request,json,hashlib
from pathlib import Path
b=Path(__file__).resolve().parent/'vendor';urls={
'krb5-26461-fix.patch':'https://github.com/krb5/krb5/commit/c5f9c816107f70139de11b38aa02db2f1774ee0d.patch',
'krb5-107708-fix.patch':'https://github.com/krb5/krb5/commit/f6e2c397ceda6467ebbaab8ed66d4895c9f1d6a7.patch',
'krb5-107778-fix.patch':'https://github.com/krb5/krb5/commit/48afa9abb89ab2176bb20624d87d010b9984fc08.patch',
'krb5-network-announcement.html':'https://mailman.mit.edu/pipermail/kerberos-announce/2026q4/000212.html'};rows=[]
for f,u in urls.items():
 try:
  d=urllib.request.urlopen(u,timeout=30).read();(b/f).write_bytes(d);rows.append(dict(file=f,url=u,sha256=hashlib.sha256(d).hexdigest(),bytes=len(d)))
 except Exception as e:rows.append(dict(file=f,url=u,error=str(e)))
(b/'pg-network-acquisition.json').write_text(json.dumps(rows,indent=2));print(json.dumps(rows,indent=2))
