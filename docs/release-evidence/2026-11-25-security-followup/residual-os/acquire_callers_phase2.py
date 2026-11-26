import urllib.request,json,hashlib,pathlib
root=pathlib.Path('.local/security-residual-os-2026-11-25/vendor')
urls={
'krb5-1.21.3.tar.gz':'https://web.mit.edu/kerberos/dist/krb5/1.21/krb5-1.21.3.tar.gz',
'krb5-1.20.1.tar.gz':'https://web.mit.edu/kerberos/dist/krb5/1.20/krb5-1.20.1.tar.gz',
'cve-2019-1010022.json':'https://cveawg.mitre.org/api/cve/CVE-2019-1010022',
'cve-2019-1010024.json':'https://cveawg.mitre.org/api/cve/CVE-2019-1010024',
'cve-2019-1010025.json':'https://cveawg.mitre.org/api/cve/CVE-2019-1010025',
}
rows=[]
for f,u in urls.items():
 try:
  data=urllib.request.urlopen(u,timeout=30).read();(root/f).write_bytes(data);rows.append(dict(file=f,url=u,sha256=hashlib.sha256(data).hexdigest(),bytes=len(data)))
 except Exception as e:rows.append(dict(file=f,url=u,error=str(e)))
(root/'caller-acquisition-phase2.json').write_text(json.dumps(rows,indent=2));print(json.dumps(rows,indent=2))
