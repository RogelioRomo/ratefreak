import os,json,pathlib,subprocess,hashlib,glob,ctypes
paths=['/etc/krb5.keytab','/etc/krb5.conf','/var/lib/krb5kdc','/usr/sbin/krb5kdc','/usr/sbin/kadmind','/usr/bin/kadmin','/usr/bin/kpasswd','/usr/bin/genrb']
out={'paths':{p:{'exists':pathlib.Path(p).exists()} for p in paths},'kerberos_env_names':[k for k in os.environ if k.startswith('KRB5')],'cache_path_candidates':glob.glob('/tmp/krb5cc*')+glob.glob('/run/user/*/krb5cc*')+glob.glob('/var/run/krb5cc*'),'sasl_modules':glob.glob('/usr/lib/x86_64-linux-gnu/sasl2/*'),'runtime_id':os.getuid(),'commands':{}}
for command in [['curl','--version'],['ldd','/usr/bin/curl'],['ldd','/usr/lib/x86_64-linux-gnu/libcurl.so.4'],['ldd','/usr/share/dotnet/shared/Microsoft.NETCore.App/10.0.15/libSystem.Net.Security.Native.so']]:
 p=subprocess.run(command,text=True,capture_output=True);out['commands'][' '.join(command)]={'exit':p.returncode,'stdout':p.stdout,'stderr':p.stderr}
for path in ['/etc/krb5.conf']:
 p=pathlib.Path(path)
 if p.exists():out['krb5_configuration']={'sha256':hashlib.sha256(p.read_bytes()).hexdigest(),'credential_default_directives':[line.strip() for line in p.read_text().splitlines() if 'default_ccache_name' in line or 'default_keytab_name' in line]}
out['krb_library_paths']={pattern:glob.glob(pattern) for pattern in ['/usr/lib/x86_64-linux-gnu/libgssrpc*','/usr/lib/x86_64-linux-gnu/libkadm5*','/usr/lib/x86_64-linux-gnu/krb5/plugins/kdb/*']}
gss=ctypes.CDLL('libgssapi_krb5.so.2')
fn=gss.gss_acquire_cred
fn.argtypes=[ctypes.POINTER(ctypes.c_uint32),ctypes.c_void_p,ctypes.c_uint32,ctypes.c_void_p,ctypes.c_int,ctypes.POINTER(ctypes.c_void_p),ctypes.c_void_p,ctypes.c_void_p]
fn.restype=ctypes.c_uint32
minor=ctypes.c_uint32();cred=ctypes.c_void_p()
major=fn(ctypes.byref(minor),None,0,None,1,ctypes.byref(cred),None,None)
out['gss_default_initiator_acquire']={'major':major,'minor':minor.value,'credential_handle_present':bool(cred.value),'network':'none','scope':'Default credential acquisition only; no context exchange or credential bytes inspected.'}
if cred.value:gss.gss_release_cred(ctypes.byref(minor),ctypes.byref(cred))
print(json.dumps(out,indent=2))
