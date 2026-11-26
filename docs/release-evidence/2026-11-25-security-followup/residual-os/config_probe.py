import os,json,hashlib,stat
from pathlib import Path
out={'environment_present':{k:k in os.environ for k in ['GSS_MECH_CONFIG','KRB5_CONFIG','KRB5_KTNAME','LD_PRELOAD','LD_LIBRARY_PATH']},'paths':{}}
for p in [Path('/etc/gss'),Path('/etc/gss/mech'),Path('/etc/gss/mech.d'),Path('/etc/krb5.conf'),Path('/etc/krb5.conf.d'),Path('/etc/pam.d')]:
 row={'exists':p.exists()}
 if p.exists():
  s=p.stat();row.update(uid=s.st_uid,gid=s.st_gid,mode=oct(stat.S_IMODE(s.st_mode)))
  if p.is_dir():row['entries']=sorted(x.name for x in p.iterdir())
  elif p.is_file():
   d=p.read_bytes();row.update(sha256=hashlib.sha256(d).hexdigest(),auth_to_local_rule=b'RULE:' in d,pkinit_cert_match=b'pkinit_cert_match' in d)
 out['paths'][str(p)]=row
print(json.dumps(out,indent=2))
