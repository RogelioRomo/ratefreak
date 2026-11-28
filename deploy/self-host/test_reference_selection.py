"""Import preflight and immutable reference selection contracts."""
import copy,hashlib,io,json,tarfile,tempfile,unittest
from pathlib import Path
from unittest.mock import patch
import release_bundle as bundle
import ratefreak as cli
import sys
sys.path.insert(0,str(Path(__file__).resolve().parents[1]/'release'))
import versioning

class ReferenceBundleTests(unittest.TestCase):
    def setUp(self):
        self.temp=tempfile.TemporaryDirectory();self.root=Path(self.temp.name)
        self.manifest={'format':1,'stage':'verified','version':'0.1.0','revision':'a'*40,'platform':'linux/amd64','migration':'migration','files':{},'images':{},'reference_images':{}}
        self.metadata={}
        for name in ['api','web','tools','postgres','caddy']:self.image(name)
        self.save()
        # Source/evidence checks have separate integration fixtures. Archive and
        # loader operations are real; no Docker daemon or source acquisition.
        self.evidence=patch('release_manifest.validate');self.evidence.start();self.addCleanup(self.evidence.stop)
    def tearDown(self):self.temp.cleanup()
    def save(self):(self.root/'release-manifest.json').write_text(json.dumps(self.manifest))
    def image(self,name,**changes):
        config={'architecture':'amd64','os':'linux','rootfs':{'type':'layers','diff_ids':[]},'config':{'Labels':{'org.opencontainers.image.version':'0.1.0','org.opencontainers.image.revision':'a'*40}}}
        config.update(changes);raw=json.dumps(config).encode();digest=hashlib.sha256(raw).hexdigest();tag='ratefreak-'+name+':0.1.0'
        with tarfile.open(self.root/(name+'.tar.gz'),'w') as t:
            for leaf,data in [('manifest.json',json.dumps([{'RepoTags':[tag],'Config':digest+'.json','Layers':[]}]).encode()),(digest+'.json',raw)]:
                m=tarfile.TarInfo(leaf);m.size=len(data);t.addfile(m,io.BytesIO(data))
        data=(self.root/(name+'.tar.gz')).read_bytes();self.manifest['files'][name+'.tar.gz']={'sha256':hashlib.sha256(data).hexdigest(),'bytes':len(data)}
        target='reference_images' if name in ('postgres','caddy') else 'images'
        self.manifest[target][name]={'id':'sha256:'+digest,'tag':tag,'file':name+'.tar.gz'}
        self.metadata[name]={'Id':'sha256:'+digest,'Architecture':config['architecture'],'Os':config['os'],'Config':config['config']}
    def inspect(self,tag):return self.metadata[tag.split(':')[0].removeprefix('ratefreak-')]
    def test_all_five_archives_are_loaded_and_selected(self):
        loaded=[];result=bundle.load_bundle(self.root,self.inspect,lambda p:loaded.append(p.name))
        self.assertEqual(set(loaded),{'api.tar.gz','web.tar.gz','tools.tar.gz','postgres.tar.gz','caddy.tar.gz'})
        self.assertEqual(set(result['images']),{'api','web','tools'})
    def test_partial_or_mixed_reference_map_is_rejected(self):
        original=copy.deepcopy(self.manifest)
        for change in [lambda m:m['reference_images'].pop('caddy'),lambda m:m.update(reference_images={}),lambda m:m['reference_images']['caddy'].update(tag='ratefreak-caddy:0.2.0'),lambda m:m['reference_images']['caddy'].update(file='api.tar.gz'),lambda m:m['reference_images']['caddy'].update(id='bad')]:
            self.manifest=copy.deepcopy(original);change(self.manifest);self.save()
            with self.subTest(refs=self.manifest['reference_images']),self.assertRaises(ValueError):bundle.validate_bundle(self.root)
    def test_malformed_reference_ids_are_rejected_as_validation_errors(self):
        original=copy.deepcopy(self.manifest)
        for value in [None,42,[],{}]:
            self.manifest=copy.deepcopy(original);self.manifest['reference_images']['postgres']['id']=value;self.save()
            with self.subTest(value=value),self.assertRaises(ValueError):bundle.validate_bundle(self.root)

    def test_missing_or_tampered_reference_archive_stops_before_loading(self):
        (self.root/'postgres.tar.gz').write_bytes(b'tampered');loaded=[]
        with self.assertRaises(ValueError):bundle.load_bundle(self.root,self.inspect,lambda p:loaded.append(p))
        self.assertEqual(loaded,[])
    def test_last_reference_tag_conflict_stops_before_loading_any_image(self):
        loaded=[]
        def inspect(tag):return {'Id':'sha256:'+'f'*64} if tag=='ratefreak-caddy:0.1.0' else self.inspect(tag)
        with self.assertRaises(ValueError):bundle.load_bundle(self.root,inspect,lambda p:loaded.append(p))
        self.assertEqual(loaded,[])
    def test_wrong_archived_platform_labels_or_identity_stops_before_loading(self):
        for role in ('api','web','tools','postgres','caddy'):
            for changes in [{'architecture':'arm64'},{'config':{'Labels':{}}}]:
                self.image(role,**changes);self.save();loaded=[]
                with self.subTest(role=role,changes=changes),self.assertRaises(ValueError):bundle.load_bundle(self.root,self.inspect,lambda p:loaded.append(p))
                self.assertEqual(loaded,[]);self.image(role)
        self.manifest['reference_images']['caddy']['id']='sha256:'+'b'*64;self.save()
        with self.assertRaises(ValueError):bundle.load_bundle(self.root,lambda _:None,lambda _:None)
    def test_reference_load_metadata_failure_never_returns_selection(self):
        def inspect(tag):
            data=copy.deepcopy(self.inspect(tag))
            if tag=='ratefreak-caddy:0.1.0':data['Architecture']='arm64'
            return data
        with self.assertRaises(ValueError):bundle.load_bundle(self.root,inspect,lambda _:None)

    def oci_archive(self,role):
        path=self.root/(role+'.tar.gz')
        with tarfile.open(path) as t:members={m.name:t.extractfile(m).read() for m in t if m.isfile()}
        config_name=json.loads(members['manifest.json'])[0]['Config'];config_digest=hashlib.sha256(members[config_name]).hexdigest()
        raw=json.dumps({'schemaVersion':2,'config':{'digest':'sha256:'+config_digest},'layers':[]}).encode();digest=hashlib.sha256(raw).hexdigest()
        members['blobs/sha256/'+digest]=raw
        tag='ratefreak-'+role+':0.1.0';members['index.json']=json.dumps({'manifests':[{'digest':'sha256:'+digest,'annotations':{'io.containerd.image.name':tag,'org.opencontainers.image.ref.name':'0.1.0'}}]}).encode()
        with tarfile.open(path,'w') as t:
            for name,data in members.items():
                member=tarfile.TarInfo(name);member.size=len(data);t.addfile(member,io.BytesIO(data))
        data=path.read_bytes();self.manifest['files'][path.name]={'sha256':hashlib.sha256(data).hexdigest(),'bytes':len(data)}
        self.manifest['reference_images'][role]['id']='sha256:'+digest
        self.metadata[role]['Id']='sha256:'+digest
        self.save()
        return 'sha256:'+config_digest
    def test_oci_reference_identity_chain_is_checked_before_import(self):
        self.oci_archive('postgres')
        result=bundle.load_bundle(self.root,self.inspect,lambda _:None)
        self.assertEqual(result['reference_images']['postgres']['id'],self.metadata['postgres']['Id'])
    def test_oci_index_cannot_replace_selected_config_id_with_another_identity(self):
        config_id=self.oci_archive('caddy');self.manifest['reference_images']['caddy']['id']=config_id;self.metadata['caddy']['Id']=config_id;self.save()
        loaded=[]
        with self.assertRaises(ValueError):bundle.load_bundle(self.root,self.inspect,lambda p:loaded.append(p))
        self.assertEqual(loaded,[])

class ReferenceSelectionTests(unittest.TestCase):
    def setUp(self):
        self.temp=tempfile.TemporaryDirectory();self.root=Path(self.temp.name)/'instance';self.config=cli.initialize(self.root,'https://reader.example',False)
        self.apps={n:{'tag':'ratefreak-'+n+':0.1.0','id':'sha256:'+str(i)*64} for i,n in enumerate(['api','web','tools'])}
        self.refs={n:{'tag':'ratefreak-'+n+':0.1.0','id':'sha256:'+str(i)*64} for i,n in enumerate(['postgres','caddy'],3)}
        cli.write_state(self.root/'release-selection.json',{'images':self.apps,'reference_images':self.refs})
    def tearDown(self):self.temp.cleanup()
    def resolved(self):return {'services':{**{s:{'image':self.apps[k]['id']} for s,k in {'api':'api','web':'web','tools':'tools','migrate':'tools','storage-init':'tools'}.items()},**{n:{'image':v['id']} for n,v in self.refs.items()}}}
    def test_selected_references_are_immutable_and_override_inherited_env(self):
        self.assertTrue(hasattr(cli,'selected_reference_images'),'reference selection API missing')
        self.assertEqual(cli.selected_reference_images(self.root),{'postgres':'sha256:'+'3'*64,'caddy':'sha256:'+'4'*64})
        with patch.dict(cli.os.environ,{'RATEFREAK_POSTGRES_IMAGE':'untrusted','RATEFREAK_CADDY_IMAGE':'untrusted'}),patch.object(cli,'execute',return_value=json.dumps(self.resolved())):
            _,env=cli.compose(self.root,self.config,'caddy')
        self.assertEqual(env['RATEFREAK_POSTGRES_IMAGE'],'sha256:'+'3'*64);self.assertEqual(env['RATEFREAK_CADDY_IMAGE'],'sha256:'+'4'*64)
    def test_malformed_selected_reference_metadata_is_rejected_as_operator_error(self):
        for key in ['id','tag']:
            for value in [None,42,[],{}]:
                refs=copy.deepcopy(self.refs);refs['postgres'][key]=value
                cli.write_state(self.root/'release-selection.json',{'images':self.apps,'reference_images':refs})
                with self.subTest(key=key,value=value),self.assertRaises(cli.OperatorError):cli.selected_reference_images(self.root)

    def test_reference_compose_override_is_rejected_for_both_bundle_and_source(self):
        for mode in ('bundle','source'):
            cli.write_state(self.root/'release-selection.json',{'mode':mode,'images':self.apps,'reference_images':self.refs})
            for role in ('postgres','caddy'):
                resolved=self.resolved();resolved['services'][role]['image']='untrusted'
                with patch.object(cli,'execute',return_value=json.dumps(resolved)),self.subTest(mode=mode,role=role),self.assertRaises(cli.OperatorError):cli.compose(self.root,self.config,'caddy')
    def test_source_selection_captures_both_reference_ids_before_replacing_receipt(self):
        cli.write_state(self.root/'migrated.json',{'old':True})
        with patch.object(cli,'release_images',return_value={n:x['id'] for n,x in self.apps.items()}),patch.object(cli,'execute',return_value='sha256:'+'3'*64+'\nsha256:'+'4'*64+'\n'):cli.select_source(self.root)
        self.assertEqual(cli.selected_images(self.root),{n:x['id'] for n,x in self.apps.items()})
        saved=json.loads((self.root/'release-selection.json').read_text());self.assertEqual(set(saved.get('reference_images',{})),{'postgres','caddy'});self.assertEqual(saved['reference_images']['postgres']['id'],'sha256:'+'3'*64);self.assertFalse((self.root/'migrated.json').exists())
    def test_failed_reference_inspection_preserves_old_selection_and_acceptance(self):
        cli.write_state(self.root/'migrated.json',{'old':True});before=(self.root/'release-selection.json').read_bytes()
        with patch.object(cli,'release_images',return_value={n:x['id'] for n,x in self.apps.items()}),patch.object(cli,'execute',return_value='missing'),self.assertRaises(cli.OperatorError):cli.select_source(self.root)
        self.assertEqual((self.root/'release-selection.json').read_bytes(),before);self.assertTrue((self.root/'migrated.json').exists())

    def test_legacy_source_without_references_keeps_local_tag_selection(self):
        cli.write_state(self.root/'release-selection.json',{'mode':'source','images':{n:{'id':x['id'],'tag':'ratefreak-'+n+':local'} for n,x in self.apps.items()}})
        self.assertEqual(cli.selected_images(self.root),{n:'ratefreak-'+n+':local' for n in self.apps})
    def test_source_build_refreshes_every_runtime_before_selection(self):
        from argparse import Namespace
        calls=[]
        package=Path(self.temp.name)/'source/deploy/self-host';package.mkdir(parents=True)
        def execute(command,env=None,capture=False):
            calls.append(command)
            if command[:3]==['docker','image','inspect']:
                return '\n'.join(['sha256:'+'1'*64]*len(command[5:]))
        with patch.object(cli,'stopped_writers'),patch.object(cli,'execute',side_effect=execute),patch('versioning.read_identity',return_value={'version':'0.1.0','revision':'a'*40}),patch.object(cli,'PACKAGE',package):
            cli.dispatch(Namespace(command='build'),self.root,self.config,['compose'],{})
        builds=[x for x in calls if x[:2]==['docker','build']]
        self.assertEqual(len(builds),5)
        for command in builds:
            self.assertIn('--pull',command)
            self.assertEqual(command[command.index('--no-cache-filter')+1],'runtime')
        saved=json.loads((self.root/'release-selection.json').read_text())
        self.assertEqual(set(saved['images']),{'api','web','tools'});self.assertEqual(set(saved['reference_images']),{'postgres','caddy'})
