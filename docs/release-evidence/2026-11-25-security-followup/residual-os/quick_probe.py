from pathlib import Path
import json,os,sys,shutil
print(json.dumps({'commands':{n:Path(n).exists() for n in ['/usr/bin/dpkg-source','/usr/share/perl5/Dpkg/Source/Package/V3/Quilt.pm','/usr/bin/apt-key','/usr/bin/infocmp','/usr/bin/bzip2recover','/usr/bin/getfacl','/usr/bin/setfacl','/usr/bin/getfattr','/usr/bin/setfattr']},'locale_environment':{n:os.environ.get(n) for n in ['LANG','LC_ALL','LC_MESSAGES','LANGUAGE','CHARSET','G_FILENAME_ENCODING']},'python_version':sys.version,'rmtree_fd_symlink_protection':shutil.rmtree.avoids_symlink_attacks},indent=2))
