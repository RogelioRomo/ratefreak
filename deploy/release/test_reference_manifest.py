"""Exercise cross-artifact reference proof, independent of Docker archive parsing."""
import copy
import hashlib
import importlib.util
import io
import json
from pathlib import Path
import tarfile
import unittest
import sys
sys.path.insert(0,str(Path(__file__).resolve().parents[1]/'self-host'))

import release_manifest
from release_bundle import read_manifest


class ReferenceManifestTests(unittest.TestCase):
    def setUp(self):
        spec=importlib.util.spec_from_file_location('reference_manifest_fixture',Path(__file__).resolve().parents[1]/'self-host/test_release_bundle.py')
        module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)
        self.fixture=module.BundleTests();self.fixture.setUp()
        self.root=self.fixture.root;self.manifest=self.fixture.manifest
        self.references={role:'sha256:'+digit*64 for role,digit in [('postgres','3'),('caddy','4')]}
        self.manifest['reference_images']={role:{'id':identity,'tag':'ratefreak-'+role+':0.1.0','file':role+'.tar.gz'} for role,identity in self.references.items()}
        self.inventory={'format':1,'images':{role:{'image_id':identity,'components':[{'scope':'os','name':'libc','version':'1'}],'notice_files':[{'path':'original/license.txt','bytes':16,'sha256':hashlib.sha256(b'Original license').hexdigest()}]} for role,identity in self.references.items()}}
        for role in self.references:
            self.artifact(role+'.tar.gz',b'image fixture')
            self.archive(role+'-notices.tar.gz',{role+'-notices/inventory.json':self.inventory['images'][role],role+'-notices/original/license.txt':b'Original license'})
        self.artifact('built-reference-images.json',json.dumps(self.references).encode())
        self.artifact('reference-inventory.json',json.dumps(self.inventory).encode())
        for name,path in [('fresh-acceptance.tar.gz','fresh-acceptance/acceptance.json'),('acceptance.tar.gz','acceptance/acceptance.json')]:
            data=self.members(name);receipt=json.loads(data[path]);receipt['referenceImages']=dict(self.references);data[path]=json.dumps(receipt).encode();self.archive(name,data)
        files=self.members('sources.tar.gz')
        files['sources/reference-review.md']=b'Exact reference source and license review.'
        files['sources/reference-notices.txt']=b'Original reference license text.'
        names=['reference-review.md','reference-notices.txt','sources.tar.gz']
        hashes={name:hashlib.sha256(files['sources/'+name]).hexdigest() for name in names}
        self.coverage={'format':1,'images':{role:{'image_id':identity,'review_record':'reference-review.md','files':dict(hashes),'components':[{'scope':'os','name':'libc','version':'1','source_files':['sources.tar.gz'],'notice_files':['reference-notices.txt']}]} for role,identity in self.references.items()}}
        files['sources/reference-coverage.json']=json.dumps(self.coverage).encode();self.archive('sources.tar.gz',files)
    def tearDown(self):self.fixture.tearDown()
    def artifact(self,name,data):
        (self.root/name).write_bytes(data);self.manifest['files'][name]={'sha256':hashlib.sha256(data).hexdigest(),'bytes':len(data)}
    def archive(self,name,members):
        stream=io.BytesIO()
        with tarfile.open(fileobj=stream,mode='w:gz') as archive:
            for path,value in members.items():
                data=value if isinstance(value,bytes) else json.dumps(value).encode();entry=tarfile.TarInfo(path);entry.size=len(data);archive.addfile(entry,io.BytesIO(data))
        self.artifact(name,stream.getvalue())
    def members(self,name):
        with tarfile.open(self.root/name) as archive:return {m.name:archive.extractfile(m).read() for m in archive if m.isfile()}
    def validate(self):release_manifest.validate(self.manifest,self.root,'release-manifest.json',read_manifest)
    def test_complete_reference_inventory_sources_and_acceptance_validate(self):self.validate()
    def test_each_reference_inventory_and_notice_artifact_is_required(self):
        for name in ['built-reference-images.json','reference-inventory.json','postgres-notices.tar.gz','caddy-notices.tar.gz']:
            entry=self.manifest['files'].pop(name)
            with self.subTest(name=name),self.assertRaises(ValueError):self.validate()
            self.manifest['files'][name]=entry
    def test_notice_inventory_must_match_selected_reference_bytes(self):
        bad=copy.deepcopy(self.inventory['images']['postgres']);bad['image_id']='sha256:'+'f'*64
        self.archive('postgres-notices.tar.gz',{'postgres-notices/inventory.json':bad})
        with self.assertRaises(ValueError):self.validate()
    def test_reference_sources_cannot_be_missing_or_bound_to_other_images(self):
        files=self.members('sources.tar.gz');entry=files.pop('sources/reference-coverage.json');self.archive('sources.tar.gz',files)
        with self.assertRaises((ValueError,KeyError)):self.validate()
        proof=json.loads(entry);proof['images']['caddy']['image_id']='sha256:'+'f'*64
        files['sources/reference-coverage.json']=json.dumps(proof).encode();self.archive('sources.tar.gz',files)
        with self.assertRaises(ValueError):self.validate()
    def test_source_material_hashes_cannot_be_bypassed_by_rehashing_outer_archive(self):
        files=self.members('sources.tar.gz');files['sources/reference-notices.txt']=b'changed original notice';self.archive('sources.tar.gz',files)
        with self.assertRaises(ValueError):self.validate()
    def test_fresh_and_upgrade_acceptance_must_bind_reference_identities(self):
        for name,path in [('fresh-acceptance.tar.gz','fresh-acceptance/acceptance.json'),('acceptance.tar.gz','acceptance/acceptance.json')]:
            original=self.members(name);bad=copy.deepcopy(original);receipt=json.loads(bad[path]);receipt.pop('referenceImages');bad[path]=json.dumps(receipt).encode();self.archive(name,bad)
            with self.subTest(name=name),self.assertRaises(ValueError):self.validate()
            self.archive(name,original)

    def test_declared_original_notice_members_must_exist_and_match_their_bytes(self):
        for content in [None,b'Modified license']:
            values={'postgres-notices/inventory.json':self.inventory['images']['postgres']}
            if content is not None:values['postgres-notices/original/license.txt']=content
            self.archive('postgres-notices.tar.gz',values)
            with self.subTest(content=content),self.assertRaises(ValueError):self.validate()
