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
            with tarfile.open(out) as archive:self.assertEqual(archive.getnames(),['LICENSE','SOURCE_REVISION'])
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
    def test_official_context_exports_only_committed_tagged_bytes(self):
        with tempfile.TemporaryDirectory() as temp:
            root=Path(temp)/'repo';root.mkdir()
            def git(*args):return subprocess.check_output(['git','-C',str(root),*args],text=True).strip()
            git('init','-q');(root/'safe').write_text('committed');(root/'.gitignore').write_text('*.pem\n')
            git('add','.');git('-c','user.name=Test','-c','user.email=test@example.test','commit','-qm','fixture')
            (root/'secret.pem').write_text('local private data')
            context=Path(temp)/'context';prepare.export_context(root,git('rev-parse','HEAD'),context)
            self.assertEqual((context/'safe').read_text(),'committed');self.assertFalse((context/'secret.pem').exists())
    def test_public_evidence_archives_omit_local_owner_metadata(self):
        with tempfile.TemporaryDirectory() as temp:
            root=Path(temp);(root/'reports').mkdir();(root/'reports/verification.json').write_text('{}')
            prepare.freeze_directory(root,'reports')
            with tarfile.open(root/'reports.tar.gz') as archive:
                self.assertTrue(all(m.uid==0 and m.gid==0 and not m.uname and not m.gname for m in archive))
    def test_resume_produces_a_separate_verified_release_from_exact_review_bytes(self):
        import importlib.util,hashlib
        from unittest.mock import patch
        spec=importlib.util.spec_from_file_location('release_bundle_fixture',Path(__file__).resolve().parents[1]/'self-host/test_release_bundle.py')
        module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)
        fixture=module.BundleTests();fixture.setUp()
        try:
            root=fixture.root;manifest=fixture.manifest;manifest['stage']='review'
            for name in ['sources.tar.gz','acceptance.tar.gz','fresh-acceptance.tar.gz','baseline-images.json']:manifest['files'].pop(name)
            (root/'release-manifest.json').unlink();(root/'candidate-manifest.json').write_text(json.dumps(manifest))
            before={name:hashlib.sha256((root/name).read_bytes()).hexdigest() for name in manifest['files']}
            images={name:entry['id'] for name,entry in manifest['images'].items()};baseline={name:'sha256:'+'f'*64 for name in images}
            def inspected(command,**kwargs):
                name=command[-1].split(':')[0].removeprefix('ratefreak-')
                value={'Id':images[name],'Os':'linux','Architecture':'amd64','Config':{'Labels':{'org.opencontainers.image.version':'0.1.0','org.opencontainers.image.revision':'a'*40}}}
                return subprocess.CompletedProcess(command,0,stdout=json.dumps([value]))
            def source_materials(directory,actual,output,inventory):
                self.assertEqual(actual,images)
                with tarfile.open(root/'sources.tar.gz') as tar:tar.extractall(output.parent,filter='data')
            def execute(command,cwd=None,output=None):
                if '--report-directory' in command:
                    target=Path(command[command.index('--report-directory')+1]);target.mkdir()
                    upgrade='--baseline-images-json' in command
                    receipt={'candidateImages':images,'baselineImages':baseline if upgrade else None,'freshInstall':not upgrade,'upgrade':upgrade,**{key:True for key in ['https','dataPreserved','disabledUpdateChecks','ordinaryDenied','logoutDenied']}}
                    (target/'acceptance.json').write_text(json.dumps(receipt))
                return ''
            with patch.object(prepare.subprocess,'run',side_effect=inspected),patch.object(prepare,'run',side_effect=execute),patch.object(prepare,'baseline_images',return_value=baseline),patch.object(prepare,'source_evidence',side_effect=source_materials):
                result=prepare.resume_candidate(root,{'version':'0.1.0','revision':'a'*40},root/'reviewed-inputs')
            self.assertEqual(result['stage'],'verified');self.assertTrue((root/'release/release-manifest.json').exists())
            self.assertFalse((root/'release-manifest.json').exists())
            self.assertEqual(before,{name:hashlib.sha256((root/name).read_bytes()).hexdigest() for name in manifest['files']})
        finally:fixture.tearDown()
