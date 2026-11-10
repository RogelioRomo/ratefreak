import sys
sys.path.insert(0, str(__import__('pathlib').Path(__file__).parent))
import importlib.util
import json
import os
from pathlib import Path
import stat
import tempfile
import unittest

spec = importlib.util.spec_from_file_location('operator_cli', Path(__file__).with_name('ratefreak.py'))
cli = importlib.util.module_from_spec(spec)
spec.loader.exec_module(cli)

class InstanceTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name) / 'instance'
    def tearDown(self): self.temp.cleanup()
    def test_init_generates_distinct_private_secrets_and_stable_repeat(self):
        cli.initialize(self.root, 'https://reader.example', False)
        before = {p.name: p.read_bytes() for p in (self.root/'secrets').iterdir()}
        self.assertEqual(len(before), 6)
        self.assertEqual(len(set(before.values())), 6)
        for p in (self.root/'secrets').iterdir():
            self.assertEqual(stat.S_IMODE(p.stat().st_mode), 0o600)
            self.assertEqual(len(p.read_text().strip()), 64)
        cli.initialize(self.root, 'https://reader.example', False)
        self.assertEqual(before, {p.name:p.read_bytes() for p in (self.root/'secrets').iterdir()})
    def test_partial_init_repairs_only_missing_secret(self):
        cli.initialize(self.root, 'https://reader.example', False)
        saved = (self.root/'secrets/ingress').read_bytes()
        (self.root/'secrets/setup').unlink()
        cli.initialize(self.root, 'https://reader.example', False)
        self.assertEqual(saved, (self.root/'secrets/ingress').read_bytes())
        self.assertTrue((self.root/'secrets/setup').exists())
    def test_different_origin_refuses_without_mutation(self):
        cli.initialize(self.root, 'https://reader.example', False)
        saved=(self.root/'instance.json').read_bytes()
        with self.assertRaises(cli.OperatorError):cli.initialize(self.root,'https://other.example',False)
        self.assertEqual(saved,(self.root/'instance.json').read_bytes())
    def test_symlink_secret_refuses_without_touching_target(self):
        cli.initialize(self.root,'https://reader.example',False)
        target=Path(self.temp.name)/'target';target.write_text('unchanged')
        (self.root/'secrets/setup').unlink();(self.root/'secrets/setup').symlink_to(target)
        with self.assertRaises(cli.OperatorError):cli.initialize(self.root,'https://reader.example',False)
        self.assertEqual(target.read_text(),'unchanged')
    def test_rejects_malformed_or_public_secret_before_repair(self):
        cli.initialize(self.root,'https://reader.example',False)
        (self.root/'secrets/setup').unlink()
        (self.root/'secrets/ingress').write_text('bad')
        with self.assertRaises(cli.OperatorError):cli.initialize(self.root,'https://reader.example',False)
        self.assertFalse((self.root/'secrets/setup').exists())
    def test_rejects_public_instance_directory(self):
        self.root.mkdir(mode=0o755)
        with self.assertRaises(cli.OperatorError):cli.initialize(self.root,'https://reader.example',False)
        self.assertFalse((self.root/'secrets').exists())
    def test_rejects_origins_with_credentials_paths_or_plain_http(self):
        for origin in ['http://reader.example','https://u:p@reader.example','https://reader.example/sub','https://reader.example?x=1','https://reader.example\nOTHER=x']:
            with self.subTest(origin=origin), self.assertRaises(cli.OperatorError):cli.initialize(self.root,origin,False)
    def test_refuses_symlink_ancestor(self):
        real=Path(self.temp.name)/'real';real.mkdir()
        link=Path(self.temp.name)/'link';link.symlink_to(real,target_is_directory=True)
        with self.assertRaises(cli.OperatorError):cli.initialize(link/'instance','https://reader.example',False)
        self.assertFalse((real/'instance').exists())


class StorageTests(unittest.TestCase):
    def setUp(self):
        self.temp=tempfile.TemporaryDirectory();self.root=Path(self.temp.name)
        entry_spec=importlib.util.spec_from_file_location('entrypoint',Path(__file__).with_name('entrypoint.py'))
        self.entry=importlib.util.module_from_spec(entry_spec);entry_spec.loader.exec_module(self.entry)
    def tearDown(self):self.temp.cleanup()
    def test_journal_is_private_and_preserved_by_repeated_preparation(self):
        path=self.root/'independent/account-erasures.jsonl'
        self.entry.ensure_journal(path,os.getuid(),os.getgid())
        path.write_bytes(b'protected-record\n')
        self.entry.ensure_journal(path,os.getuid(),os.getgid())
        self.assertEqual(path.read_bytes(),b'protected-record\n')
        self.assertEqual(stat.S_IMODE(path.stat().st_mode),0o600)
    def test_existing_unsafe_journal_is_never_fixed_or_erased(self):
        path=self.root/'independent/account-erasures.jsonl';path.parent.mkdir(mode=0o700)
        path.write_bytes(b'protected\n');path.chmod(0o644)
        with self.assertRaises(self.entry.StorageError):self.entry.ensure_journal(path,os.getuid(),os.getgid())
        self.assertEqual(path.read_bytes(),b'protected\n');self.assertEqual(stat.S_IMODE(path.stat().st_mode),0o644)
    def test_journal_symlink_refused(self):
        path=self.root/'independent/account-erasures.jsonl';path.parent.mkdir(mode=0o700)
        target=self.root/'protected';target.write_bytes(b'keep');path.symlink_to(target)
        with self.assertRaises(self.entry.StorageError):self.entry.ensure_journal(path,os.getuid(),os.getgid())
        self.assertEqual(target.read_bytes(),b'keep')

class LifecycleTests(unittest.TestCase):
    setUp=InstanceTests.setUp
    tearDown=InstanceTests.tearDown
    def test_start_refuses_without_successful_migration(self):
        cli.initialize(self.root,'https://reader.example',False)
        with self.assertRaises(cli.OperatorError):cli.require_ready(self.root)
    def test_start_refuses_restored_instance_until_replay(self):
        cli.initialize(self.root,'https://reader.example',False)
        cli.exclusive_file(self.root/'migrated.json', b'{"release":"local"}\n')
        cli.exclusive_file(self.root/'restore-pending', b'pending\n')
        with self.assertRaises(cli.OperatorError):cli.require_ready(self.root)

class ArchiveTests(unittest.TestCase):
    def setUp(self):
        self.temp=tempfile.TemporaryDirectory();self.root=Path(self.temp.name)
        module_spec=importlib.util.spec_from_file_location('maintenance',Path(__file__).with_name('maintenance.py'))
        self.maintenance=importlib.util.module_from_spec(module_spec);module_spec.loader.exec_module(self.maintenance)
    def tearDown(self):self.temp.cleanup()
    def test_archive_traversal_or_links_are_refused_before_extracting(self):
        import io,tarfile
        for name,kind in [('../escape',tarfile.REGTYPE),('/escape',tarfile.REGTYPE),('link',tarfile.SYMTYPE)]:
            archive=self.root/'bad.tar'
            with tarfile.open(archive,'w') as tar:
                item=tarfile.TarInfo(name);item.type=kind;item.linkname='outside';tar.addfile(item,io.BytesIO())
            with self.assertRaises(self.maintenance.MaintenanceError):self.maintenance.validate_archive(archive)
            self.assertFalse((self.root/'escape').exists())
    def test_manifest_detects_changed_payload(self):
        import hashlib
        import tarfile
        (self.root/'database.dump').write_bytes(b'valid')
        for name in ['artwork.tar','keys.tar']:
            with tarfile.open(self.root/name,'w'):pass
        files={name:hashlib.sha256((self.root/name).read_bytes()).hexdigest() for name in ['database.dump','artwork.tar','keys.tar']}
        (self.root/'manifest.json').write_text(json.dumps({'format':2,'images':{'api':'a','web':'w','tools':'t'},'files':files}))
        self.maintenance.validate_backup(self.root,{'api':'a','web':'w','tools':'t'})
        (self.root/'database.dump').write_bytes(b'changed')
        with self.assertRaises(self.maintenance.MaintenanceError):self.maintenance.validate_backup(self.root,{'api':'a','web':'w','tools':'t'})

class JournalPolicyTests(unittest.TestCase):
    setUp=InstanceTests.setUp
    tearDown=InstanceTests.tearDown
    def test_deletion_unconfigured_until_independent_replication_acknowledged(self):
        config=cli.initialize(self.root,'https://reader.example',False)
        _,env=cli.compose(self.root,config)
        self.assertEqual(env['RATEFREAK_API_ERASURE_JOURNAL_PATH'],'')
        cli.write_state(self.root/'journal-policy.json',{'independentReplicationConfirmed':True})
        _,env=cli.compose(self.root,config)
        self.assertEqual(env['RATEFREAK_API_ERASURE_JOURNAL_PATH'],'/independent/account-erasures.jsonl')


class OverrideTests(unittest.TestCase):
    setUp=InstanceTests.setUp
    tearDown=InstanceTests.tearDown
    def test_private_instance_override_is_used_consistently(self):
        config=cli.initialize(self.root,'https://reader.example',False)
        override=self.root/'compose.override.yaml'
        cli.exclusive_file(override,b'services: {}\n')
        command,_=cli.compose(self.root,config,'caddy')
        self.assertEqual(command[-2:],['-f',str(override)])
    def test_public_override_is_rejected(self):
        config=cli.initialize(self.root,'https://reader.example',False)
        override=self.root/'compose.override.yaml';override.write_text('services: {}\n');override.chmod(0o644)
        with self.assertRaises(cli.OperatorError):cli.compose(self.root,config)

class RuntimeEnvironmentTests(unittest.TestCase):
    def test_completed_setup_can_remove_process_secret_without_reading_it(self):
        import entrypoint
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory)
            for name in ['proxy','runtime']:
                path=root/name;path.write_text('a'*64+'\n');path.chmod(0o600)
            command,env=entrypoint.runtime_command('api',root,[],{'RATEFREAK_SETUP_SECRET_DISABLED':'true','Installation__SetupSecret':'stale'})
            self.assertEqual(command,['dotnet','/app/RateFreak.Api.dll'])
            self.assertNotIn('Installation__SetupSecret',env)
            self.assertIn('ConnectionStrings__Default',env)

class ConnectionTests(unittest.TestCase):
    def test_runtime_loader_uses_explicit_environment_and_quotes_connection_values(self):
        import entrypoint
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory)
            for name in ['proxy','runtime']:
                path=root/name;path.write_text('a'*64+'\n');path.chmod(0o600)
            _,env=entrypoint.runtime_command('api',root,[],{'RATEFREAK_SETUP_SECRET_DISABLED':'true','RATEFREAK_DB_HOST':'private-db.example','RATEFREAK_DB_SSLMODE':'VerifyFull','RATEFREAK_DB_RUNTIME_USER':'user";Database=other'})
            self.assertIn('Host="private-db.example"',env['ConnectionStrings__Default'])
            self.assertIn('SSL Mode="VerifyFull"',env['ConnectionStrings__Default'])
            self.assertIn('Username="user"";Database=other"',env['ConnectionStrings__Default'])


class FinalSafetyTests(unittest.TestCase):
    setUp=InstanceTests.setUp
    tearDown=InstanceTests.tearDown
    def set_config(self):return cli.initialize(self.root,'https://reader.example',False)
    def test_incomplete_restore_cannot_be_accepted_by_replay(self):
        self.set_config();cli.exclusive_file(self.root/'restore-pending',b'pending\n')
        with self.assertRaises(cli.OperatorError):cli.replay_state(self.root,{'api':'a','web':'w','tools':'t'})
    def test_normal_replay_cannot_manufacture_migration_acceptance(self):
        self.set_config()
        with self.assertRaises(cli.OperatorError):cli.replay_state(self.root,{'api':'a','web':'w','tools':'t'})
    def test_replay_accepts_only_complete_matching_restore(self):
        self.set_config();cli.exclusive_file(self.root/'restore-pending',b'pending\n')
        cli.write_state(self.root/'restore-complete.json',{'images':{'api':'a','web':'w','tools':'t'},'overrideSha':''})
        self.assertEqual(cli.replay_state(self.root,{'api':'a','web':'w','tools':'t'}),'restore')
        with self.assertRaises(cli.OperatorError):cli.replay_state(self.root,{'api':'new','web':'w','tools':'t'})
    def test_release_acceptance_includes_both_application_images(self):
        self.set_config();images={'api':'a','web':'w','tools':'t'}
        cli.write_state(self.root/'migrated.json',{'images':images,'overrideSha':''})
        cli.require_release(self.root,images)
        for name in ['api','web','tools']:
            changed=images.copy();changed[name]='changed'
            with self.assertRaises(cli.OperatorError):cli.require_release(self.root,changed)
    def test_maintenance_rejects_restarting_paused_created_and_active_tool_containers(self):
        from unittest.mock import patch
        for service,state in [('api','restarting'),('web','paused'),('api','created'),('migrate','running'),('tools','running')]:
            with self.subTest(service=service,state=state),patch.object(cli,'execute',return_value=json.dumps({'Service':service,'State':state})):
                with self.assertRaises(cli.OperatorError):cli.stopped_writers(['docker','compose'],{})
        with patch.object(cli,'execute',return_value=json.dumps([{'Service':'api','State':'exited'},{'Service':'postgres','State':'running'}])):
            cli.stopped_writers(['docker','compose'],{})
    def test_instance_maintenance_is_serialized(self):
        self.set_config()
        with cli.instance_lock(self.root):
            with self.assertRaises(cli.OperatorError):
                with cli.instance_lock(self.root):pass
    def test_reading_instance_never_regenerates_missing_credentials(self):
        self.set_config();(self.root/'secrets/setup').unlink()
        with self.assertRaises(cli.OperatorError):cli.load_instance(self.root)
        self.assertFalse((self.root/'secrets/setup').exists())
    def test_missing_or_unsafe_referenced_artwork_prevents_backup(self):
        import maintenance
        from unittest.mock import patch
        self.root.mkdir();(self.root/'present.webp').write_bytes(b'image')
        with patch.object(maintenance,'query',return_value='missing.webp'):
            with self.assertRaises(maintenance.MaintenanceError):maintenance.verify_artwork(self.root)
        with patch.object(maintenance,'query',return_value='../escape'):
            with self.assertRaises(maintenance.MaintenanceError):maintenance.verify_artwork(self.root)
        with patch.object(maintenance,'query',return_value='present.webp'):maintenance.verify_artwork(self.root)


class CommandModeTests(unittest.TestCase):
    def test_unknown_container_mode_cannot_start_the_api(self):
        import entrypoint
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory)
            for name in ['proxy','runtime']:
                path=root/name;path.write_text('a'*64+'\n');path.chmod(0o600)
            with self.assertRaises(entrypoint.StorageError):entrypoint.runtime_command('misspelled-maintenance',root,[],{})

class MaintenanceIntentTests(unittest.TestCase):
    def test_replay_mode_requires_an_explicit_maintenance_action(self):
        import entrypoint
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory)
            for name in ['proxy','runtime']:
                path=root/name;path.write_text('a'*64+'\n');path.chmod(0o600)
            for args in [[],['--help'],['misspelled']]:
                with self.subTest(args=args),self.assertRaises(entrypoint.StorageError):entrypoint.runtime_command('replay',root,args,{})
            for action in ['--replay-account-erasures','--appoint-admin','--revoke-admin']:
                command,_=entrypoint.runtime_command('replay',root,[action],{})
                self.assertEqual(command,['dotnet','/app/RateFreak.Api.dll',action])

if __name__ == "__main__":unittest.main()
