import ast,hashlib,json,os,re,struct,subprocess,sys,tempfile,tarfile,io,stat
from pathlib import Path
result={'files':{},'python':{},'elf':[]}
for name in ['/app/entrypoint.py','/app/maintenance.py']:
 p=Path(name)
 if p.exists():
  data=p.read_bytes();tree=ast.parse(data);imports=[];calls=[]
  for n in ast.walk(tree):
   if isinstance(n,ast.Import):imports.extend(x.name for x in n.names)
   elif isinstance(n,ast.ImportFrom):imports.append(n.module or '')
   elif isinstance(n,ast.Call):
    try:calls.append({'line':n.lineno,'call':ast.unparse(n.func),'arguments':[ast.unparse(a) for a in n.args]})
    except Exception:pass
  result['files'][name]={'sha256':hashlib.sha256(data).hexdigest(),'imports':sorted(set(imports)),'calls':calls}
for name in ['/usr/bin/nsenter','/usr/bin/mount','/bin/mount','/usr/include/c++','/usr/bin/g++','/usr/bin/gcc']:
 p=Path(name);result.setdefault('paths',{})[name]={'exists':p.exists(),'mode':oct(stat.S_IMODE(p.stat().st_mode)) if p.exists() else None}
result['fstab']=Path('/etc/fstab').read_text()
result['libmsquic_paths']=[str(p) for root in ['/app','/usr/share/dotnet','/usr/lib'] for p in Path(root).rglob('*msquic*')]
if Path('/usr/local/bin/node').exists():
 code='JSON.stringify({versions:process.versions,features:process.features,quicBuild:process.config.variables.node_quic})'
 result['node']=json.loads(subprocess.check_output(['node','-p',code],text=True))
 result['node']['gcm_siv_cipher_names']=json.loads(subprocess.check_output(['node','-p',"JSON.stringify(require('node:crypto').getCiphers().filter(x=>/gcm.?siv/i.test(x)))"],text=True))
# Check archive handling with adversarial input in disposable memory/tmpfs only.
if Path('/app/maintenance.py').exists():
 sys.path.insert(0,'/app');import maintenance
 guarded=[]
 def forbidden(*args,**kwargs):raise AssertionError('Unsafe/filter extraction path unexpectedly called')
 for name in ['extract','extractall']:
  setattr(tarfile.TarFile,name,forbidden)
 for name in ['tar_filter','data_filter']:
  if hasattr(tarfile,name):setattr(tarfile,name,forbidden)
 with tempfile.TemporaryDirectory() as tmp:
  root=Path(tmp);dest=root/'dest';dest.mkdir();checks=[]
  cases=[('parent-traversal','../outside',tarfile.REGTYPE,''),('return-to-destination','../outside/../dest/sub/file',tarfile.REGTYPE,''),('absolute','/tmp/outside',tarfile.REGTYPE,''),('symlink','link',tarfile.SYMTYPE,'../outside'),('directory-symlink','avatars/',tarfile.SYMTYPE,'../outside'),('empty-symlink','',tarfile.SYMTYPE,'../outside'),('hardlink-to-symlink','hard',tarfile.LNKTYPE,'link'),('directory','dir',tarfile.DIRTYPE,'')]
  for label,name,type,target in cases:
   archive=root/(label+'.tar')
   with tarfile.open(archive,'w') as t:
    valid=tarfile.TarInfo('valid');valid.size=2;t.addfile(valid,io.BytesIO(b'ok'))
    bad=tarfile.TarInfo(name);bad.type=type;bad.linkname=target;bad.size=1 if type==tarfile.REGTYPE else 0;t.addfile(bad,io.BytesIO(b'x') if bad.size else None)
   try:maintenance.extract_flat(archive,dest,allow_avatars=True)
   except maintenance.MaintenanceError:pass
   else:raise AssertionError('Unsafe archive accepted: '+label)
   assert not list(dest.iterdir()) and not (root/'outside').exists();checks.append({'case':label,'rejected_before_any_destination_write':True})
  archive=root/'safe.tar'
  with tarfile.open(archive,'w') as t:
   for name,data in [('plain',b'content'),('avatars/'+'a'*32+'.webp',b'avatar')]:
    m=tarfile.TarInfo(name);m.size=len(data);t.addfile(m,io.BytesIO(data))
  maintenance.extract_flat(archive,dest,True);assert (dest/'plain').read_bytes()==b'content';assert (dest/'avatars'/('a'*32+'.webp')).read_bytes()==b'avatar'
  result['python']['archive_checks']=checks;result['python']['safe_flat_and_avatar_extract_without_extract_filters']=True
 result['python']['loaded_modules_after_maintenance_and_archive_checks']=[n for n in sorted(sys.modules) if n.startswith(('ssl','html','zipfile','stringprep','encodings.idna','csv','urllib','asyncio'))]
# Read ELF section/symbol metadata without installing tools or running payloads.
interesting=('SSL_CTX_set_cert_flags','SSL_new_listener','SSL_new_stream','SSL_set1_initial_peer_addr','OSSL_QUIC_client_method','EVP_CIPHER_fetch','EVP_aes_128_gcm','EVP_aes_256_gcm')
roots=['/app','/usr/local/bin','/usr/share/dotnet','/usr/lib/x86_64-linux-gnu','/usr/lib/postgresql/17/bin']
seen=set()
for root in roots:
 if not Path(root).is_dir():continue
 for directory,dirs,files in os.walk(root):
  for filename in files:
   path=Path(directory)/filename
   if path.is_symlink():continue
   try:
    with path.open('rb') as f:
     header=f.read(64)
     if header[:4]!=b'\x7fELF':continue
     assert header[4] in (1,2) and header[5] in (1,2),str(path)
     endian='<' if header[5]==1 else '>';is64=header[4]==2;machine=struct.unpack_from(endian+'H',header,18)[0]
     shoff=struct.unpack_from(endian+('Q' if is64 else 'I'),header,40 if is64 else 32)[0];entsize,num,strings=struct.unpack_from(endian+'HHH',header,58 if is64 else 46)
     if not shoff or not num:
      result['elf'].append({'path':str(path),'section_table':False});continue
     f.seek(shoff);sections=[struct.unpack(endian+('IIQQQQIIQQ' if is64 else 'IIIIIIIIII'),f.read(entsize)[:64 if is64 else 40]) for i in range(num)]
     def payload(sec):f.seek(sec[4]);return f.read(sec[5])
     strsec=payload(sections[strings])
     def string(data,offset):return data[offset:data.find(b'\0',offset)].decode('utf-8','replace')
     names=[string(strsec,s[0]) for s in sections];symbols={};bitsets=set();tables=[]
     for idx,s in enumerate(sections):
      if s[1] not in [2,11] or s[9]!=(24 if is64 else 16):continue
      tables.append(names[idx]);text=payload(sections[s[6]]);raw=payload(s)
      for offset in range(0,len(raw),s[9]):
       if is64:name,info,other,section,value,size=struct.unpack_from(endian+'IBBHQQ',raw,offset)
       else:name,value,size,info,other,section=struct.unpack_from(endian+'IIIBBH',raw,offset)
       name=string(text,name)
       if 'bitset' in name:bitsets.add(name)
       if name in interesting or re.search('gcm.?siv|quic',name,re.I):symbols[name]='defined' if section else 'imported'
     comment=payload(sections[names.index('.comment')]).replace(b'\0',b' ').decode('utf-8','replace') if '.comment' in names else None
    with path.open('rb') as f:digest=hashlib.file_digest(f,'sha256').hexdigest()
    result['elf'].append({'path':str(path),'sha256':digest,'elf_class':64 if is64 else 32,'elf_machine':machine,'symbol_tables':tables,'comment':comment,'bitset_symbols':sorted(bitsets),'relevant_crypto_symbols':symbols})
   except PermissionError:raise
result['elf_files']=len(result['elf']);print(json.dumps(result,indent=2))
