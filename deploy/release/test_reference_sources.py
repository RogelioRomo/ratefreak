import copy,hashlib,json,tempfile,unittest
from pathlib import Path

class ReferenceSourceTests(unittest.TestCase):
    def setUp(self):
        self.temp=tempfile.TemporaryDirectory();self.root=Path(self.temp.name);self.materials=self.root/'materials';self.materials.mkdir()
        for name,data in {'review.md':b'Exact reviewed image-bound source and license mapping.','sources.tar':b'corresponding source','notices.txt':b'original license'}.items():(self.materials/name).write_bytes(data)
        files={p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in self.materials.iterdir()}
        self.inventory={'format':1,'images':{}};self.coverage={'format':1,'images':{}}
        for role,digit in [('postgres','1'),('caddy','2')]:
            image_id='sha256:'+digit*64;component={'scope':'os','name':'libc','version':'1'}
            self.inventory['images'][role]={'image_id':image_id,'components':[component]}
            self.coverage['images'][role]={'image_id':image_id,'review_record':'review.md','files':dict(files),'components':[{**component,'source_files':['sources.tar'],'notice_files':['notices.txt']}]}
        self.save()
    def tearDown(self):self.temp.cleanup()
    def save(self):(self.materials/'reference-coverage.json').write_text(json.dumps(self.coverage))
    def test_exact_materials_copy_beside_application_sources(self):
        from reference_sources import copy_evidence
        output=self.root/'output';output.mkdir();(output/'application.txt').write_text('retained')
        self.assertEqual(copy_evidence(self.materials,self.inventory,output),self.coverage)
        self.assertEqual((output/'sources.tar').read_bytes(),b'corresponding source')
        self.assertEqual((output/'notices.txt').read_bytes(),b'original license')
        self.assertEqual((output/'application.txt').read_text(),'retained')
        self.assertEqual(json.loads((output/'reference-coverage.json').read_text()),self.coverage)
    def test_every_reference_role_image_component_and_version_must_match(self):
        from reference_sources import validate_coverage
        mutations=[lambda c:c['images'].pop('caddy'),lambda c:c['images']['postgres'].update(image_id='sha256:'+'f'*64),lambda c:c['images']['caddy']['components'][0].update(version='2'),lambda c:c['images']['caddy']['components'].append(copy.deepcopy(c['images']['caddy']['components'][0]))]
        for mutate in mutations:
            value=copy.deepcopy(self.coverage);mutate(value)
            with self.subTest(value=value),self.assertRaises(ValueError):validate_coverage(value,self.inventory)
    def test_source_and_original_notice_material_are_both_required(self):
        from reference_sources import validate_coverage
        for key in ['source_files','notice_files']:
            for invalid in [[],['review.md'],['missing.tar'],['../sources.tar']]:
                value=copy.deepcopy(self.coverage);value['images']['postgres']['components'][0][key]=invalid
                with self.subTest(key=key,invalid=invalid),self.assertRaises(ValueError):validate_coverage(value,self.inventory)
    def test_permissive_exception_still_requires_actual_notice_material(self):
        from reference_sources import validate_coverage
        row=self.coverage['images']['postgres']['components'][0];row.pop('source_files')
        row['exception']={'reason':'Reviewed permissive redistribution with complete original grant texts.','record':'review.md'}
        validate_coverage(self.coverage,self.inventory)
        row['notice_files']=[]
        with self.assertRaises(ValueError):validate_coverage(self.coverage,self.inventory)
    def test_tampered_notice_and_conflicting_application_artifact_are_rejected(self):
        from reference_sources import copy_evidence
        (self.materials/'notices.txt').write_text('tampered')
        with self.assertRaises(ValueError):copy_evidence(self.materials,self.inventory,self.root/'changed')
        (self.materials/'notices.txt').write_bytes(b'original license')
        output=self.root/'existing';output.mkdir();(output/'sources.tar').write_bytes(b'conflict')
        with self.assertRaises(ValueError):copy_evidence(self.materials,self.inventory,output)
        self.assertEqual((output/'sources.tar').read_bytes(),b'conflict')

    def test_malformed_json_material_lists_and_review_records_are_rejected(self):
        from reference_sources import validate_coverage
        for invalid in [[{}], [[]], [None], 'notices.txt']:
            value=copy.deepcopy(self.coverage);value['images']['postgres']['components'][0]['notice_files']=invalid
            with self.subTest(invalid=invalid),self.assertRaises(ValueError):validate_coverage(value,self.inventory)
        value=copy.deepcopy(self.coverage);value['images']['postgres']['review_record']={}
        with self.assertRaises(ValueError):validate_coverage(value,self.inventory)
