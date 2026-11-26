import subprocess,tarfile,json,hashlib,struct,os
from pathlib import Path
b=Path(__file__).resolve().parent
image='postgres:17-trixie@sha256:87c8b42f423b45d5247e6aa8fdb2e7762fec164c68a50fe3eb07e7bfcaf4dbcf'
create=['docker','create','--network','none','--read-only','--cap-drop','ALL','--security-opt','no-new-privileges:true',image]
c=subprocess.check_output(create,text=True).strip();out={'image_reference':image,'container_image_id':subprocess.check_output(['docker','inspect','--format','{{.Image}}',c],text=True).strip(),'create_command':create,'elf':[],'aliases':{},'files':{},'paths':{}}
try:
 p=subprocess.Popen(['docker','export',c],stdout=subprocess.PIPE)
 with tarfile.open(fileobj=p.stdout,mode='r|') as t:
  for m in t:
   name='/'+m.name.lstrip('./')
   if name.startswith(('/etc/gss','/etc/krb5','/etc/locale','/etc/default/locale')) or name in ['/usr/bin/systemd-tmpfiles','/usr/lib/systemd/systemd','/usr/bin/python3','/usr/bin/sqlite3','/usr/bin/getfacl','/usr/bin/setfacl','/usr/bin/bzip2recover']:
    out['paths'][name]={'uid':m.uid,'gid':m.gid,'mode':oct(m.mode),'type':m.type.decode(),'linkname':m.linkname}
   if m.issym():
    out['aliases'][name]=m.linkname;continue
   if not m.isfile():continue
   if name in ['/usr/local/bin/docker-entrypoint.sh','/usr/local/bin/docker-ensure-initdb.sh']:
    d=t.extractfile(m).read();(b/('pg-'+name.rsplit('/',1)[1])).write_bytes(d);out['files'][name]={'sha256':hashlib.sha256(d).hexdigest()};continue
   if not name.startswith(('/usr/lib/','/usr/bin/','/usr/sbin/','/usr/local/bin/')):continue
   f=t.extractfile(m);head=f.read(16)
   if head[:4]!=b'\x7fELF':continue
   data=head+f.read();bits=head[4];endian='<' if head[5]==1 else '>'
   if bits==2:fields=struct.unpack_from(endian+'HHIQQQIHHHHHH',data,16);sf=endian+'IIQQQQIIQQ';dynfmt=endian+'qQ'
   else:fields=struct.unpack_from(endian+'HHIIIIIHHHHHH',data,16);sf=endian+'IIIIIIIIII';dynfmt=endian+'iI'
   shoff=fields[5];entsize=fields[10];count=fields[11];namesindex=fields[12]
   sections=[struct.unpack_from(sf,data,shoff+i*entsize) for i in range(count)]
   if not sections:continue
   def payload(s):return data[s[4]:s[4]+s[5]]
   shnames=payload(sections[namesindex]);names=[shnames[s[0]:].split(b'\0',1)[0].decode(errors='replace') for s in sections]
   needed=[];symbols={}
   if '.dynamic' in names:
    dyn=sections[names.index('.dynamic')];strings=payload(sections[dyn[6]])
    for pos in range(dyn[4],dyn[4]+dyn[5],dyn[9] or struct.calcsize(dynfmt)):
     tag,value=struct.unpack_from(dynfmt,data,pos)
     if tag==1:needed.append(strings[value:].split(b'\0',1)[0].decode(errors='replace'))
   for s in sections:
    if s[1] not in (2,11) or not s[9]:continue
    strings=payload(sections[s[6]])
    for pos in range(s[4],s[4]+s[5],s[9]):
     nameoff=struct.unpack_from(endian+'I',data,pos)[0];end=strings.find(b'\0',nameoff);symbol=strings[nameoff:end].decode(errors='replace')
     if symbol in ['regcomp','regexec','glob','glob64','wordexp','iconv','iconv_open','sqlite3_open','sqlite3_open_v2','seccomp_init','seccomp_rule_add','seccomp_load','acl_get_file','acl_set_file','XML_ParserCreate','XML_ParserCreateNS'] or 'wscanf' in symbol or 'from_chars' in symbol or 'align_val_t' in symbol:
      section=struct.unpack_from(endian+'H',data,pos+(6 if bits==2 else 14))[0];symbols[symbol]='defined' if section else 'imported'
   out['elf'].append({'path':name,'sha256':hashlib.sha256(data).hexdigest(),'needed':needed,'selected_symbols':symbols})
 p.wait();assert p.returncode==0
finally:subprocess.run(['docker','rm',c],check=True,capture_output=True)
(b/'pg-gcc-elf-probe.json').write_text(json.dumps(out,indent=2)+'\n');print(out['container_image_id'],len(out['elf']))
