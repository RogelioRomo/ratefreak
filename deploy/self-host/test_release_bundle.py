import copy
import io
import tarfile
import hashlib
import json
from pathlib import Path
import tempfile
import unittest
import release_bundle as bundle

class BundleTests(unittest.TestCase):
    def setUp(self):
        self.temp=tempfile.TemporaryDirectory();self.root=Path(self.temp.name)
        self.manifest={'format':1,'stage':'verified','version':'0.1.0','revision':'a'*40,'platform':'linux/amd64','migration':'migration-id','files':{},'images':{}}
        for i,name in enumerate(['api','web','tools']):
            file=name+'.tar.gz'
            with tarfile.open(self.root/file,'w') as archive:
                raw=json.dumps([{'RepoTags':['ratefreak-'+name+':0.1.0'],'Config':'config.json','Layers':[]}]).encode()
                entry=tarfile.TarInfo('manifest.json');entry.size=len(raw);archive.addfile(entry,io.BytesIO(raw))
            data=(self.root/file).read_bytes()
            self.manifest['files'][file]={'sha256':hashlib.sha256(data).hexdigest(),'bytes':len(data)}
            self.manifest['images'][name]={'file':file,'id':'sha256:'+str(i)*64,'tag':'ratefreak-'+name+':0.1.0'}
        def artifact(name,data):
            (self.root/name).write_bytes(data)
            self.manifest['files'][name]={'sha256':hashlib.sha256(data).hexdigest(),'bytes':len(data)}
        def archive(name,members):
            with tarfile.open(self.root/name,'w:gz') as tar:
                for leaf,value in members.items():
                    data=value if isinstance(value,bytes) else json.dumps(value).encode();item=tarfile.TarInfo(leaf);item.size=len(data);tar.addfile(item,io.BytesIO(data))
            data=(self.root/name).read_bytes();self.manifest['files'][name]={'sha256':hashlib.sha256(data).hexdigest(),'bytes':len(data)}
        images={name:entry['id'] for name,entry in self.manifest['images'].items()}
        baseline={name:'sha256:'+'f'*64 for name in images}
        inventory={'images':{name:{'image_id':image,'components':[{'scope':'os','name':'libc','version':'1'}]} for name,image in images.items()}}
        required=['packaging/sharp-libvips-packaging.tar.gz','meson/gvdb.tar.gz','README.md','LICENSE.txt','instructions/rust_sources.py','instructions/offline_curl.py','instructions/rebuild_native.py','instructions/source_bundle.py','evidence/librsvg-edited.Cargo.lock','evidence/rust-coverage.json','native-sources/source.tar.gz','rust/crates/example.crate','librsvg.tar.gz']
        native_profile={'package':{'name':'@img/sharp-libvips-linux-x64','version':'1.0'},'components':{'vips':'1.0'},'native_sha256':'a'*64}
        native_manifest={**native_profile,'image_id':images['web'],'required_files':required,'librsvg_archive':'librsvg.tar.gz','coverage':{'native_archive_patch_files':1,'rust_registry_crates':1},'files':[{'file':name,'sha256':hashlib.sha256(b'source').hexdigest()} for name in required]}
        native_stream=io.BytesIO()
        with tarfile.open(fileobj=native_stream,mode='w:gz') as tar:
            members={'SOURCE-MANIFEST.json':json.dumps(native_manifest).encode(),**{name:b'source' for name in required}}
            for name,data in members.items():
                item=tarfile.TarInfo(name);item.size=len(data);tar.addfile(item,io.BytesIO(data))
        materials={'review.md':b'Review','sources.tar.gz':b'corresponding source','native.tar.gz':native_stream.getvalue()}
        inventory['images']['web']['native_profile']={key:value for key,value in native_manifest.items() if key!='image_id'}
        coverage={'images':{name:{'image_id':image,'review_record':'review.md','native_sidecar':'native.tar.gz','files':{leaf:hashlib.sha256(data).hexdigest() for leaf,data in materials.items()},'components':[{'scope':'os','name':'libc','version':'1','source_files':['sources.tar.gz']}]} for name,image in images.items()}}
        artifact('built-images.json',json.dumps(images).encode());artifact('baseline-images.json',json.dumps(baseline).encode());artifact('source-inventory.json',json.dumps(inventory).encode());artifact('RELEASE-NOTES.md',b'Notes')
        report={'identity':{'version':'0.1.0','revision':'a'*40},'complete':True,'checks':[{'check':name,'exitCode':0} for name in ['restore','tools','web-install','UnitTests','ProviderContractTests','IntegrationTests','operator','release','compliance','web-lint','web-typecheck','web-test','web-build','api-publish','model']],'tests':[{'suite':suite,'total':1,'executed':1,'passed':1,'failed':0,'skipped':0} for suite in ['UnitTests','IntegrationTests','ProviderContractTests']]}
        archive('reports.tar.gz',{'reports/verification.json':report});archive('installation.tar.gz',{'VERSION':b'0.1.0'})
        archive('fresh-acceptance.tar.gz',{'fresh-acceptance/acceptance.json':{'candidateImages':images,'freshInstall':True,'https':True}})
        archive('acceptance.tar.gz',{'acceptance/acceptance.json':{'candidateImages':images,'baselineImages':baseline,**{key:True for key in ['https','upgrade','dataPreserved','disabledUpdateChecks','ordinaryDenied','logoutDenied']}}})
        archive('sources.tar.gz',{'sources/coverage.json':coverage,**{'sources/'+name:data for name,data in materials.items()}})
        for name in images:archive(name+'-notices.tar.gz',{name+'-notices/manifest.json':{'notice_texts_complete':True}})
        self.save()
    def tearDown(self):self.temp.cleanup()
    def save(self): (self.root/'release-manifest.json').write_text(json.dumps(self.manifest))
    def test_validates_complete_release_before_selecting(self):
        result=bundle.validate_bundle(self.root);self.assertEqual(result['version'],'0.1.0')
    def test_rejects_tampering_missing_mixed_platform_and_paths(self):
        original=copy.deepcopy(self.manifest)
        for change in [lambda m:m.update(platform='linux/arm64'),lambda m:m.update(revision='bad'),lambda m:m['images'].pop('web'),lambda m:m['images']['web'].update(tag='ratefreak-web:0.2.0'),lambda m:m['images']['web'].update(file='../outside')]:
            self.manifest=copy.deepcopy(original);change(self.manifest);self.save()
            with self.assertRaises(ValueError):bundle.validate_bundle(self.root)
        self.manifest=original;self.save();(self.root/'api.tar.gz').write_text('tampered')
        with self.assertRaises(ValueError):bundle.validate_bundle(self.root)
    def test_rejects_symlink_or_duplicate_manifest_before_docker(self):
        target=self.root/'other';target.write_bytes(b'api');(self.root/'api.tar.gz').unlink();(self.root/'api.tar.gz').symlink_to(target)
        with self.assertRaises(ValueError):bundle.validate_bundle(self.root)
        (self.root/'release-manifest.json').write_text('{"format":1,"format":1}')
        with self.assertRaises(ValueError):bundle.validate_bundle(self.root)
    def test_failed_load_never_returns_a_selection(self):
        loaded=[]
        def load(path):loaded.append(path.name)
        def inspect(tag):return {'Id':'sha256:'+'f'*64,'Architecture':'amd64','Os':'linux','Config':{'Labels':{}}}
        with self.assertRaises(ValueError):bundle.load_bundle(self.root,inspect,load)
        self.assertEqual(loaded,[])
    def test_all_loaded_images_must_match_labels_and_ids(self):
        def inspect(tag):
            name=tag.split(':')[0].removeprefix('ratefreak-');image=self.manifest['images'][name]
            return {'Id':image['id'],'Architecture':'amd64','Os':'linux','Config':{'Labels':{'org.opencontainers.image.version':'0.1.0','org.opencontainers.image.revision':'a'*40}}}
        result=bundle.load_bundle(self.root,inspect,lambda path:None)
        self.assertEqual(set(result['images']),{'api','web','tools'})
    def test_archive_cannot_import_unrelated_image_tags(self):
        file=self.root/'api.tar.gz'
        with tarfile.open(file,'w') as archive:
            raw=json.dumps([{'Config':'config.json','RepoTags':['ratefreak-api:local'],'Layers':[]}]).encode()
            item=tarfile.TarInfo('manifest.json');item.size=len(raw);archive.addfile(item,io.BytesIO(raw))
        data=file.read_bytes();self.manifest['files']['api.tar.gz']={'sha256':hashlib.sha256(data).hexdigest(),'bytes':len(data)};self.save()
        with self.assertRaises(ValueError):bundle.validate_bundle(self.root)
    def test_manifest_types_are_validated(self):
        for value in [[],None,{'format':1,'stage':'verified','version':'0.1.0','revision':None}]:
            (self.root/'release-manifest.json').write_text(json.dumps(value))
            with self.assertRaises(ValueError):bundle.validate_bundle(self.root)
    def test_existing_version_tag_cannot_be_overwritten_before_a_later_failure(self):
        loaded=[]
        def inspect(tag):
            if tag=='ratefreak-api:0.1.0':return {'Id':'sha256:'+'f'*64}
            return None
        with self.assertRaises(ValueError):bundle.load_bundle(self.root,inspect,lambda path:loaded.append(path))
        self.assertEqual(loaded,[])
    def test_review_manifest_cannot_be_renamed_into_an_installable_release(self):
        self.manifest['stage']='review';self.save()
        with self.assertRaises(ValueError):bundle.validate_bundle(self.root)
    def test_final_bundle_requires_source_installation_verification_and_acceptance(self):
        self.manifest['stage']='verified';self.manifest['files'].pop('sources.tar.gz');self.save()
        with self.assertRaises(ValueError):bundle.validate_bundle(self.root)
