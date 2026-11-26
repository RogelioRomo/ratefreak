import ast,hashlib,json,os,struct,subprocess
from pathlib import Path
out={'source_files':{},'commands':{},'elf':[],'aliases':{}}
for name in ['/app/entrypoint.py','/app/maintenance.py']:
 p=Path(name)
 if not p.exists():continue
 data=p.read_bytes();tree=ast.parse(data)
 out['source_files'][name]={'sha256':hashlib.sha256(data).hexdigest(),'imports':sorted({n for a in ast.walk(tree) for n in ([x.name for x in a.names] if isinstance(a,ast.Import) else [a.module] if isinstance(a,ast.ImportFrom) else [])}),'calls':[{'line':n.lineno,'function':ast.unparse(n.func)} for n in ast.walk(tree) if isinstance(n,ast.Call)]}
commands=['/usr/bin/bzip2recover','/usr/bin/getfacl','/usr/bin/setfacl','/usr/bin/getfattr','/usr/bin/setfattr','/usr/bin/infocmp','/usr/bin/xmlwf','/usr/bin/gpgtar','/usr/bin/genrb','/usr/bin/sqlite3','/usr/bin/systemd-tmpfiles','/usr/sbin/selinuxenabled','/usr/bin/chfn','/usr/bin/chsh','/usr/sbin/sshd','/sbin/init','/usr/lib/systemd/systemd','/usr/lib/systemd/systemd-journald','/usr/lib/systemd/systemd-homed','/usr/lib/systemd/systemd-oomd','/usr/lib/systemd/system-generators/systemd-gpt-auto-generator']
for name in commands:out['commands'][name]={'exists':Path(name).exists()}
roots=['/app','/usr/local/bin','/usr/share/dotnet','/usr/lib/python3.12','/usr/lib/python3.13','/usr/lib/postgresql/17/bin','/usr/lib/x86_64-linux-gnu']
files=[]
for root in roots:
 if Path(root).is_dir():
  files.extend(Path(d)/name for d,_,names in os.walk(root) for name in names)
files.extend(Path(x) for x in ['/usr/bin/python3.12','/usr/bin/python3.13','/usr/bin/curl','/usr/bin/perl','/usr/bin/bash','/usr/bin/dash'])
interesting=['wordexp','wordfree','seccomp_init','seccomp_load','seccomp_rule_add','glob','glob64','regcomp','regexec','iconv_open','iconv','swscanf','fwscanf','wscanf','sqlite3_open','sqlite3_open_v2','XML_ParserCreate','XML_ParserCreateNS','acl_get_file','acl_set_file','acl_extended_file','acl_delete_def_file']
seen=set()
for p in files:
 if p.is_symlink():
  if p.exists():out['aliases'][str(p)]=str(p.resolve())
  continue
 if not p.is_file():continue
 with p.open('rb') as f:head=f.read(16)
 if head[:4]!=b'\x7fELF':continue
 data=p.read_bytes();bits=head[4];endian='<' if head[5]==1 else '>'
 if bits==2:fields=struct.unpack_from(endian+'HHIQQQIHHHHHH',data,16);shoff=fields[5];entsize=fields[10];count=fields[11];namesindex=fields[12];sf=endian+'IIQQQQIIQQ';dynfmt=endian+'qQ';symsize=24
 else:fields=struct.unpack_from(endian+'HHIIIIIHHHHHH',data,16);shoff=fields[5];entsize=fields[10];count=fields[11];namesindex=fields[12];sf=endian+'IIIIIIIIII';dynfmt=endian+'iI';symsize=16
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
   nameoff=struct.unpack_from(endian+'I',data,pos)[0];end=strings.find(b'\0',nameoff);name=strings[nameoff:end].decode(errors='replace')
   if name in interesting or ('scanf' in name and any(x in name for x in ['wscanf','fwscanf','swscanf'])) or 'from_chars' in name or 'align_val_t' in name:
    section=struct.unpack_from(endian+'H',data,pos+(6 if bits==2 else 14))[0];symbols[name]='defined' if section else 'imported'
 out['elf'].append({'path':str(p),'sha256':hashlib.sha256(data).hexdigest(),'needed':needed,'elf_machine':struct.unpack_from(endian+'H',data,18)[0],'selected_symbols':symbols})
out['selinux_fs_present']=Path('/sys/fs/selinux').exists();out['elf_files']=len(out['elf']);print(json.dumps(out,indent=2))
