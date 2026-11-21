import unittest
from source_inventory import validate_coverage
class SourceInventoryTests(unittest.TestCase):
    def setUp(self):
        self.expected={'images':{'api':{'image_id':'sha256:'+'a'*64,'components':[{'scope':'os','name':'libc','version':'1'}]}}}
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
