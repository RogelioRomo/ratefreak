import json
from pathlib import Path
import subprocess
import tempfile
import unittest
import versioning

class VersionTests(unittest.TestCase):
    def test_strict_versions(self):
        for value in ['0.1.0-dev.1','1.10.0','1.0.0+build.4']:
            self.assertEqual(versioning.validate_version(value),value)
        for value in ['01.0.0','1.0','v1.0.0','1.0.0-01','1.0.0\n','x'*129,'1.0.0;id']:
            with self.subTest(value=value),self.assertRaises(ValueError):versioning.validate_version(value)
    def test_identity_binds_clean_tag_and_package_base(self):
        with tempfile.TemporaryDirectory() as temp:
            root=Path(temp);(root/'web').mkdir();(root/'VERSION').write_text('0.1.0\n')
            (root/'web/package.json').write_text(json.dumps({'version':'0.1.0'}))
            def git(*args):return subprocess.check_output(['git','-C',temp,*args],text=True).strip()
            git('init','-q');git('add','.');git('-c','user.name=Test','-c','user.email=test@example.test','commit','-qm','fixture')
            self.assertFalse(versioning.read_identity(root)['official'])
            git('tag','v0.1.0')
            identity=versioning.read_identity(root,'v0.1.0')
            self.assertTrue(identity['official']);self.assertEqual(identity['revision'],git('rev-parse','HEAD'))
            with self.assertRaises(ValueError):versioning.read_identity(root,'v0.2.0')
            (root/'VERSION').write_text('0.1.1\n')
            with self.assertRaises(ValueError):versioning.read_identity(root,'v0.1.0')
            (root/'VERSION').write_text('0.1.0\n');(root/'untracked').write_text('data')
            self.assertFalse(versioning.read_identity(root)['official'])
            with self.assertRaises(ValueError):versioning.read_identity(root,'v0.1.0')
    def test_package_mismatch(self):
        with tempfile.TemporaryDirectory() as temp:
            root=Path(temp);(root/'web').mkdir();(root/'VERSION').write_text('0.2.0-dev.1')
            (root/'web/package.json').write_text('{"version":"0.1.0"}')
            with self.assertRaises(ValueError):versioning.read_identity(root)
