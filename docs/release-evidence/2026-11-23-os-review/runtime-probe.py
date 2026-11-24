import json,os,subprocess,platform,shutil
paths=['/usr/sbin/krb5kdc','/usr/sbin/kadmind','/usr/lib/systemd/systemd','/usr/lib/systemd/systemd-homed','/usr/lib/systemd/systemd-oomd','/usr/lib/systemd/systemd-journald','/usr/sbin/nscd','/usr/bin/infocmp','/usr/bin/xmlwf','/usr/bin/gpgtar','/usr/bin/genrb','/usr/bin/perl','/usr/bin/tar','/usr/bin/gzip','/usr/bin/sqlite3','/usr/lib/x86_64-linux-gnu/security/pam_namespace.so']
r={'arch':platform.machine(),'files':{p:os.path.exists(p) for p in paths},'fstab':open('/etc/fstab').read(),'ldd':{}}
for path in ['/usr/local/bin/node','/usr/lib/x86_64-linux-gnu/libpq.so.5','/usr/bin/curl']:
 if os.path.exists(path):
  v=subprocess.run(['ldd',path],capture_output=True,text=True);r['ldd'][path]={'exit':v.returncode,'stdout':v.stdout,'stderr':v.stderr}
if shutil.which('node'):
 v=subprocess.run(['node','-p','JSON.stringify(process.versions)'],capture_output=True,text=True);r['node_versions']=json.loads(v.stdout)
print(json.dumps(r,indent=2))
