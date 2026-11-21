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
        self.manifest={'format':1,'version':'0.1.0','revision':'a'*40,'platform':'linux/amd64','migration':'migration-id','files':{},'images':{}}
        for i,name in enumerate(['api','web','tools']):
            file=name+'.tar'
            with tarfile.open(self.root/file,'w') as archive:
                raw=json.dumps([{'RepoTags':['ratefreak-'+name+':0.1.0'],'Config':'config.json','Layers':[]}]).encode()
                entry=tarfile.TarInfo('manifest.json');entry.size=len(raw);archive.addfile(entry,io.BytesIO(raw))
            data=(self.root/file).read_bytes()
            self.manifest['files'][file]={'sha256':hashlib.sha256(data).hexdigest(),'bytes':len(data)}
            self.manifest['images'][name]={'file':file,'id':'sha256:'+str(i)*64,'tag':'ratefreak-'+name+':0.1.0'}
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
        self.manifest=original;self.save();(self.root/'api.tar').write_text('tampered')
        with self.assertRaises(ValueError):bundle.validate_bundle(self.root)
    def test_rejects_symlink_or_duplicate_manifest_before_docker(self):
        target=self.root/'other';target.write_bytes(b'api');(self.root/'api.tar').unlink();(self.root/'api.tar').symlink_to(target)
        with self.assertRaises(ValueError):bundle.validate_bundle(self.root)
        (self.root/'release-manifest.json').write_text('{"format":1,"format":1}')
        with self.assertRaises(ValueError):bundle.validate_bundle(self.root)
    def test_failed_load_never_returns_a_selection(self):
        loaded=[]
        def load(path):loaded.append(path.name)
        def inspect(tag):return {'Id':'sha256:'+'f'*64,'Architecture':'amd64','Os':'linux','Config':{'Labels':{}}}
        with self.assertRaises(ValueError):bundle.load_bundle(self.root,inspect,load)
        self.assertEqual(loaded,['api.tar'])
    def test_all_loaded_images_must_match_labels_and_ids(self):
        def inspect(tag):
            name=tag.split(':')[0].removeprefix('ratefreak-');image=self.manifest['images'][name]
            return {'Id':image['id'],'Architecture':'amd64','Os':'linux','Config':{'Labels':{'org.opencontainers.image.version':'0.1.0','org.opencontainers.image.revision':'a'*40}}}
        result=bundle.load_bundle(self.root,inspect,lambda path:None)
        self.assertEqual(set(result['images']),{'api','web','tools'})
    def test_archive_cannot_import_unrelated_image_tags(self):
        file=self.root/'api.tar'
        with tarfile.open(file,'w') as archive:
            raw=json.dumps([{'Config':'config.json','RepoTags':['ratefreak-api:local'],'Layers':[]}]).encode()
            item=tarfile.TarInfo('manifest.json');item.size=len(raw);archive.addfile(item,io.BytesIO(raw))
        data=file.read_bytes();self.manifest['files']['api.tar']={'sha256':hashlib.sha256(data).hexdigest(),'bytes':len(data)};self.save()
        with self.assertRaises(ValueError):bundle.validate_bundle(self.root)
    def test_manifest_types_are_validated(self):
        for value in [[],None,{'format':1,'version':'0.1.0','revision':None}]:
            (self.root/'release-manifest.json').write_text(json.dumps(value))
            with self.assertRaises(ValueError):bundle.validate_bundle(self.root)
