"""Controlled package databases and ELF Go buildinfo exercise actual parsers."""
import importlib.util
import io
import json
from pathlib import Path
import struct
import tarfile
import tempfile
import unittest

try:
    import reference_inventory as inventory
except ImportError:
    inventory = None

DPKG = b'''Package: libexample1
Status: install ok installed
Architecture: amd64
Version: 2:1.2-3+deb13u1
Source: example (2:1.2-3)
Description: example package
 continuation

Package: removed
Status: deinstall ok config-files
Architecture: amd64
Version: 1

'''
APK = b'''P:libcrypto3
V:3.5.9-r1
A:x86_64
o:openssl
L:Apache-2.0

P:busybox
V:1.38.0-r1
A:x86_64
o:busybox
L:GPL-2.0-only

'''

def go_elf(module='example.org/main', version='v1.2.3', deps='dep\texample.org/dep\tv2.0.0\th1:abc\n'):
    """ELF64 PT_LOAD container with real post-Go1.18 inline buildinfo layout."""
    def string(data):
        n=len(data); prefix=bytearray()
        while n>=128:prefix.append((n&127)|128);n>>=7
        return bytes(prefix)+bytes([n])+data
    mod=('path\texample.org/main/cmd\nmod\t'+module+'\t'+version+'\th1:main\n'+deps+'build\tCGO_ENABLED=0\n').encode()
    framed=bytes.fromhex('3077af0c9274080241e1c107e6d618e6')+mod+bytes.fromhex('f932433186182072008242104116d8f2')
    blob=b'\xff Go buildinf:'+bytes([8,2])+b'\0'*16+string(b'go1.27.4')+string(framed)
    header=b'\x7fELF'+bytes([2,1,1])+b'\0'*9+struct.pack('<HHIQQQIHHHHHH',2,62,1,0,64,0,0,64,56,1,0,0,0)
    program=struct.pack('<IIQQQQQQ',1,6,128,0x400080,0x400080,len(blob),len(blob),16)
    return header+program+b'\0'*8+blob


def archive(path, entries):
    with tarfile.open(path,'w') as out:
        for name,data in entries.items():
            info=tarfile.TarInfo(name)
            if isinstance(data,tuple):info.type=tarfile.SYMTYPE;info.linkname=data[0];out.addfile(info)
            else:info.size=len(data);out.addfile(info,io.BytesIO(data))

class ReferenceInventoryTests(unittest.TestCase):
    def setUp(self):self.assertIsNotNone(inventory,'reference inventory collector is not implemented')

    def test_debian_retains_source_version_epoch_architecture_and_ignores_removed(self):
        rows=inventory.parse_dpkg(DPKG)
        self.assertEqual(rows,[{'scope':'os','name':'libexample1','version':'2:1.2-3+deb13u1','source_package':'example','source_version':'2:1.2-3','architecture':'amd64'}])

    def test_debian_missing_source_defaults_to_binary_package(self):
        rows=inventory.parse_dpkg(DPKG.replace(b'Source: example (2:1.2-3)\n',b''))
        self.assertEqual(rows[0]['source_package'],'libexample1');self.assertEqual(rows[0]['source_version'],'2:1.2-3+deb13u1')

    def test_debian_rejects_missing_identity_duplicate_and_invalid_source(self):
        for bad in [DPKG.replace(b'Architecture: amd64\n',b'',1),DPKG+DPKG,DPKG.replace(b'Source: example (2:1.2-3)',b'Source: example (broken'),DPKG.replace(b'Version: 2:1.2-3+deb13u1',b'Version: 2:1.2-3+deb13u1\nVersion: 9')]:
            with self.subTest(data=bad),self.assertRaises(ValueError):inventory.parse_dpkg(bad)

    def test_apk_captures_origin_license_and_package_release(self):
        rows=inventory.parse_apk(APK)
        self.assertEqual(rows[0],{'scope':'os','name':'busybox','version':'1.38.0-r1','source_package':'busybox','source_version':'1.38.0-r1','architecture':'x86_64','license':'GPL-2.0-only'})
        self.assertEqual(rows[1]['source_package'],'openssl')

    def test_apk_rejects_missing_origin_duplicate_and_empty_databases(self):
        for bad in [APK.replace(b'o:openssl\n',b''),APK+APK,b'']:
            with self.subTest(data=bad),self.assertRaises(ValueError):inventory.parse_apk(bad)
        with self.assertRaises(ValueError):inventory.parse_dpkg(b'')

    def test_go_elf_retains_toolchain_module_checksum_dependencies_and_build_flags(self):
        info=inventory.parse_go_buildinfo(go_elf())
        self.assertEqual(info['go_version'],'go1.27.4');self.assertEqual(info['path'],'example.org/main/cmd')
        self.assertEqual(info['modules'][0],{'path':'example.org/main','version':'v1.2.3','sum':'h1:main','main':True})
        self.assertEqual(info['modules'][1]['path'],'example.org/dep');self.assertEqual(info['settings'],{'CGO_ENABLED':'0'})

    def test_go_replacement_keeps_original_and_actual_module_identities(self):
        info=inventory.parse_go_buildinfo(go_elf(deps='dep\texample.org/old\tv1.0.0\th1:old\n=>\texample.org/new\tv1.1.0\th1:new\n'))
        self.assertEqual(info['modules'][1]['replace'],{'path':'example.org/new','version':'v1.1.0','sum':'h1:new'})

    def test_go_rejects_truncation_unversioned_dependency_missing_path_and_outside_segment_magic(self):
        raw=go_elf()
        for bad in [raw[:150],raw[:-25],go_elf(deps='dep\texample.org/dep\t\th1:abc\n'),raw[:64]+b'\0'*56+raw[120:],raw.replace(b'path\texample.org/main/cmd',b'xxxx\texample.org/main/cmd')]:
            with self.subTest(size=len(bad)),self.assertRaises(ValueError):inventory.parse_go_buildinfo(bad)

    def test_export_collects_real_notice_bytes_go_and_native_hashes_without_binaries_in_notice(self):
        with tempfile.TemporaryDirectory() as tmp:
            p=Path(tmp)/'root.tar';raw=go_elf('github.com/tianon/gosu','(devel)')
            archive(p,{'var/lib/dpkg/status':DPKG,'usr/local/bin/gosu':raw,'usr/share/doc/libexample1/copyright':('../example/copyright',),'usr/share/doc/example/copyright':b'Original example license\n'})
            record,notices=inventory.inventory_export(p,'postgres',{'Id':'sha256:'+'a'*64,'Os':'linux','Architecture':'amd64','Config':{}})
            self.assertEqual(record['image_id'],'sha256:'+'a'*64);self.assertTrue(record['coverage_gaps'])
            self.assertTrue(any(x['scope']=='go' and x['name']=='example.org/dep' for x in record['components']))
            self.assertEqual(record['binaries'][0]['path'],'/usr/local/bin/gosu');self.assertEqual(notices['original/usr/share/doc/libexample1/copyright'],b'Original example license\n')
            self.assertFalse(any('gosu' in n for n in notices));self.assertEqual(record['package_database']['path'],'/var/lib/dpkg/status')
            out=Path(tmp)/'postgres-notices.tar.gz';inventory.write_notices(out,'postgres',record,notices)
            with tarfile.open(out) as tar:
                self.assertEqual(json.load(tar.extractfile('postgres-notices/inventory.json')),record)
                self.assertTrue(all(m.isfile() for m in tar));self.assertNotIn('postgres-notices/original/usr/local/bin/gosu',tar.getnames())

    def test_collect_publishes_both_notice_archives_on_workspace_filesystem(self):
        # Docker transport is replaced; all parsing, packaging and publication are real.
        from unittest.mock import patch
        import subprocess
        with tempfile.TemporaryDirectory(dir=Path(__file__).parent) as tmp:
            root=Path(tmp);out=root/'evidence';rootfs={}
            for role,db,binary in [('postgres','var/lib/dpkg/status','usr/local/bin/gosu'),('caddy','lib/apk/db/installed','usr/bin/caddy')]:
                path=root/(role+'.tar');archive(path,{db:DPKG if role=='postgres' else APK,binary:go_elf()});rootfs[role]=path.read_bytes()
            def output(command,**kwargs):
                role=command[-1]
                if command[1:3]==['image','inspect']:
                    return json.dumps([{'Id':'sha256:'+('a' if role=='postgres' else 'b')*64,'Os':'linux','Architecture':'amd64','Config':{}}])
                return 'postgres' if command[-1]=='sha256:'+'a'*64 else 'caddy'
            def run(command,**kwargs):
                if command[1]=='export':kwargs['stdout'].write(rootfs[command[-1]])
                return subprocess.CompletedProcess(command,0)
            with patch.object(inventory.subprocess,'check_output',side_effect=output),patch.object(inventory.subprocess,'run',side_effect=run):
                result=inventory.collect(out,{'postgres':'postgres','caddy':'caddy'})
            self.assertEqual(set(result['images']),{'postgres','caddy'})
            for role in ['postgres','caddy']:
                with tarfile.open(out/(role+'-notices.tar.gz')) as tar:
                    self.assertEqual(json.load(tar.extractfile(role+'-notices/inventory.json')),result['images'][role])
            self.assertEqual(sorted(p.name for p in out.iterdir()),['caddy-notices.tar.gz','postgres-notices.tar.gz'])

    def test_caddy_uses_actual_debian_database_and_original_notice_in_debian_runtime(self):
        with tempfile.TemporaryDirectory() as tmp:
            path=Path(tmp)/'root.tar'
            archive(path,{'var/lib/dpkg/status':DPKG,'usr/bin/caddy':go_elf(),'usr/share/doc/libexample1/copyright':b'Actual Debian copyright notice\n'})
            record,notices=inventory.inventory_export(path,'caddy',{'Id':'sha256:'+'c'*64,'Os':'linux','Architecture':'amd64','Config':{}})
            self.assertEqual(record['package_database']['path'],'/var/lib/dpkg/status')
            self.assertEqual(record['package_database']['format'],'dpkg')
            self.assertEqual(record['components'][next(i for i,r in enumerate(record['components']) if r['name']=='libexample1')]['source_package'],'example')
            self.assertEqual(notices['original/usr/share/doc/libexample1/copyright'],b'Actual Debian copyright notice\n')
            self.assertTrue(any(row['path']=='/usr/bin/caddy' for row in record['binaries']))

    def test_export_rejects_ambiguous_package_databases(self):
        with tempfile.TemporaryDirectory() as tmp:
            path=Path(tmp)/'root.tar';archive(path,{'var/lib/dpkg/status':DPKG,'lib/apk/db/installed':APK,'usr/bin/caddy':go_elf()})
            with self.assertRaises(ValueError):inventory.inventory_export(path,'caddy',{'Id':'sha256:'+'c'*64,'Os':'linux','Architecture':'amd64','Config':{}})

    def test_export_rejects_unsafe_paths_and_missing_required_binary(self):
        with tempfile.TemporaryDirectory() as tmp:
            p=Path(tmp)/'root.tar';metadata={'Id':'sha256:'+'a'*64,'Os':'linux','Architecture':'amd64','Config':{}}
            for entries in [{'../escape':b'x'},{'var/lib/dpkg/status':DPKG}]:
                archive(p,entries)
                with self.assertRaises(ValueError):inventory.inventory_export(p,'postgres',metadata)

    def test_export_rejects_notice_symlink_escape_and_incompatible_platform(self):
        with tempfile.TemporaryDirectory() as tmp:
            p=Path(tmp)/'root.tar';archive(p,{'var/lib/dpkg/status':DPKG,'usr/local/bin/gosu':go_elf(),'usr/share/doc/x/copyright':('../../../../../outside',)})
            for arch in ['amd64','arm64']:
                with self.subTest(architecture=arch),self.assertRaises(ValueError):inventory.inventory_export(p,'postgres',{'Id':'sha256:'+'a'*64,'Os':'linux','Architecture':arch,'Config':{}})

if __name__=='__main__':unittest.main()
