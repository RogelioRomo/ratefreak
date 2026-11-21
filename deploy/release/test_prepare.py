import io
import json
from pathlib import Path
import subprocess
import tarfile
import tempfile
import unittest
import prepare

class PreparationTests(unittest.TestCase):
    def test_candidate_source_archive_excludes_untracked_and_private_paths(self):
        with tempfile.TemporaryDirectory() as temp:
            root=Path(temp)/'repo';root.mkdir();out=Path(temp)/'source.tar.gz'
            def git(*args):return subprocess.check_output(['git','-C',str(root),*args],text=True)
            git('init','-q');(root/'LICENSE').write_text('license');(root/'untracked').write_text('private')
            git('add','LICENSE');git('-c','user.name=Test','-c','user.email=test@example.test','commit','-qm','fixture')
            prepare.source_archive(root,out)
            with tarfile.open(out) as archive:self.assertEqual(archive.getnames(),['LICENSE'])
            (root/'credentials.env').write_text('secret');git('add','credentials.env');git('-c','user.name=Test','-c','user.email=test@example.test','commit','-qm','bad')
            with self.assertRaises(ValueError):prepare.source_archive(root,Path(temp)/'bad.tar.gz')
    def test_incomplete_image_source_evidence_is_blocking(self):
        with tempfile.TemporaryDirectory() as temp:
            root=Path(temp);(root/'coverage.json').write_text(json.dumps({'format':1,'images':{}}))
            with self.assertRaises(ValueError):prepare.source_evidence(root,{'api':'sha256:'+'a'*64},root/'out')
    def test_notes_must_name_upgrade_and_migration_requirements(self):
        with tempfile.TemporaryDirectory() as temp:
            path=Path(temp)/'notes.md';path.write_text('# Release\nChanges only')
            with self.assertRaises(ValueError):prepare.validate_notes(path,'0.1.0')
            path.write_text('# 0.1.0\n## Changes\nInitial release.\n## Upgrade\nManual, back up first.\n## Migrations\nExplicit migration.\n## Recovery\nRestore matching backup.\n## Known issues\nSee status.')
            prepare.validate_notes(path,'0.1.0')
    def test_existing_candidate_is_not_overwritten(self):
        with tempfile.TemporaryDirectory() as temp:
            path=Path(temp)/'candidate';path.mkdir();(path/'keep').write_text('bytes')
            with self.assertRaises(FileExistsError):prepare.new_output(path)
            self.assertEqual((path/'keep').read_text(),'bytes')
    def test_native_source_sidecar_must_match_exact_web_image(self):
        from unittest.mock import patch
        import hashlib,sys
        sys.path.insert(0,str(Path(__file__).resolve().parents[1]/'compliance'))
        with tempfile.TemporaryDirectory() as temp:
            root=Path(temp);images={name:'sha256:'+str(i)*64 for i,name in enumerate(['api','web','tools'])}
            (root/'review.md').write_text('Reviewed component source mapping')
            (root/'web.native-sources.tar.gz').write_bytes(b'native sidecar')
            files={name:hashlib.sha256((root/name).read_bytes()).hexdigest() for name in ['review.md','web.native-sources.tar.gz']}
            entries={name:{'image_id':image,'scopes':list(prepare.SOURCE_SCOPES[name]),'review_record':'review.md','files':files,'native_sidecar':'web.native-sources.tar.gz'} for name,image in images.items()}
            (root/'coverage.json').write_text(json.dumps({'format':1,'images':entries}))
            with patch('source_bundle.verify',return_value={'image_id':'sha256:'+'f'*64}),self.assertRaises(ValueError):prepare.source_evidence(root,images,root/'out')

    def test_finalization_refuses_a_candidate_from_another_revision(self):
        with tempfile.TemporaryDirectory() as temp:
            root=Path(temp)
            (root/'candidate-manifest.json').write_text(json.dumps({'version':'0.1.0','revision':'b'*40}))
            with self.assertRaises(ValueError):prepare.resume_candidate(root,{'version':'0.1.0','revision':'a'*40},root/'sources')
