import unittest
from source_inventory import validate_coverage
class SourceInventoryTests(unittest.TestCase):
    def setUp(self):
        self.expected={'format':2,'images':{'api':{'image_id':'sha256:'+'a'*64,'runtime_evidence_schema':2,'components':[{'scope':'os','name':'libc','version':'1'}]}}}
        self.entry={'image_id':'sha256:'+'a'*64,'review_record':'review.md','files':{'review.md':'a'*64,'libc.tar.gz':'b'*64},'components':[{'scope':'os','name':'libc','version':'1','source_files':['libc.tar.gz']}]}
    def test_each_shipped_component_needs_corresponding_material_or_a_reviewed_exception(self):
        validate_coverage({'images':{'api':self.entry}},self.expected)
        self.entry['components'][0]['source_files']=['review.md']
        with self.assertRaises(ValueError):validate_coverage({'images':{'api':self.entry}},self.expected)
    def test_missing_or_wrong_component_version_is_blocking(self):
        self.entry['components'][0]['version']='2'
        with self.assertRaises(ValueError):validate_coverage({'images':{'api':self.entry}},self.expected)
    def test_exception_must_have_a_reason_and_an_inventoried_record(self):
        self.entry['components'][0].pop('source_files');self.entry['components'][0]['exception']={'reason':'No corresponding-source obligation for this reviewed component.','record':'review.md'}
        validate_coverage({'images':{'api':self.entry}},self.expected)
        self.entry['components'][0]['exception']['record']='missing.md'
        with self.assertRaises(ValueError):validate_coverage({'images':{'api':self.entry}},self.expected)

class CopiedRuntimeCoverageTests(unittest.TestCase):
    def test_collected_image_inventory_cannot_omit_copied_runtime_components(self):
        import io
        import json
        from pathlib import Path
        import tarfile
        import tempfile
        from unittest.mock import patch
        import source_inventory
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            with tarfile.open(root / 'api-notices.tar.gz', 'w:gz') as archive:
                for filename, content in {
                    'runtime/os-packages.tsv': 'package\tversion\nlibc\t1\n',
                    'manifest.json': json.dumps({'packages': []}),
                    'runtime/evidence.json': json.dumps({'schema': 2, 'runtime_packages': [],
                        'copied_files': [{'path': '/usr/local/bin/tool', 'sha256': 'b'*64}]})}.items():
                    data = content.encode(); member = tarfile.TarInfo('api-notices/' + filename)
                    member.size = len(data); archive.addfile(member, io.BytesIO(data))
            with patch('source_inventory.subprocess.check_output', return_value='Microsoft.NETCore.App 10.0.14 [/usr/share/dotnet]'):
                result = source_inventory.collect(root, {'api': 'image'}, {})
            self.assertIn({'scope': 'runtime', 'name': 'file:/usr/local/bin/tool', 'version': 'sha256:' + 'b'*64},
                          result['images']['api']['components'])

    def test_copied_components_need_individual_source_decisions(self):
        import source_inventory
        runtime = {'schema': 2, 'runtime_packages': [
            {'name': 'yarn', 'version': '1.22.22', 'path': '/opt/yarn/package.json'},
            {'name': 'yarn', 'version': '1.22.22', 'path': '/opt/duplicate/package.json'}],
            'copied_files': [{'path': '/usr/local/bin/gosu', 'sha256': 'b' * 64}]}
        components = source_inventory.runtime_components(runtime)
        self.assertEqual({(p['scope'], p['name'], p['version']) for p in components}, {
            ('runtime', 'npm:yarn', '1.22.22'),
            ('runtime', 'file:/usr/local/bin/gosu', 'sha256:' + 'b' * 64)})
        expected = {'format': 2, 'images': {'tools': {'image_id': 'image', 'runtime_evidence_schema': 2, 'components': components}}}
        entry = {'image_id': 'image', 'review_record': 'review.md', 'files': {'review.md': 'a'*64}, 'components': []}
        with self.assertRaises(ValueError):
            validate_coverage({'images': {'tools': entry}}, expected)

    def test_older_or_incomplete_runtime_evidence_cannot_hide_copied_tools(self):
        import source_inventory
        for evidence in [{'schema': 1}, {'schema': 2, 'runtime_packages': []},
                         {'schema': 2, 'runtime_packages': [], 'copied_files': [{'path': '/bin/tool', 'sha256': 'invalid'}]}]:
            with self.subTest(evidence=evidence), self.assertRaises(ValueError):
                source_inventory.runtime_components(evidence)

    def test_saved_inventory_without_copied_runtime_schema_cannot_be_finalized(self):
        expected = {'format': 1, 'images': {'api': {'image_id': 'image',
            'components': [{'scope': 'os', 'name': 'libc', 'version': '1'}]}}}
        coverage = {'images': {'api': {'image_id': 'image', 'review_record': 'review.md',
            'files': {'review.md': 'a'*64, 'libc.tar.gz': 'b'*64},
            'components': [{'scope': 'os', 'name': 'libc', 'version': '1', 'source_files': ['libc.tar.gz']}]}}}
        with self.assertRaises(ValueError):
            validate_coverage(coverage, expected)
