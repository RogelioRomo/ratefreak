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
    def test_public_image_cache_is_prepared_privately_and_repeat_preserves_its_files(self):
        self.entry.prepare_image_cache(self.root,os.getuid(),os.getgid())
        path=self.root/'image-cache';(path/'cached.webp').write_bytes(b'cached')
        self.entry.prepare_image_cache(self.root,os.getuid(),os.getgid())
        self.assertEqual(stat.S_IMODE(path.stat().st_mode),0o700)
        self.assertEqual((path/'cached.webp').read_bytes(),b'cached')
    def test_image_cache_preparation_refuses_symlinks_and_unsafe_existing_modes(self):
        target=self.root/'keep';target.mkdir();(self.root/'image-cache').symlink_to(target,target_is_directory=True)
        with self.assertRaises(self.entry.StorageError):self.entry.prepare_image_cache(self.root,os.getuid(),os.getgid())
        (self.root/'image-cache').unlink();(self.root/'image-cache').mkdir(mode=0o755)
        with self.assertRaises(self.entry.StorageError):self.entry.prepare_image_cache(self.root,os.getuid(),os.getgid())
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
    def test_artwork_archive_roundtrip_keeps_catalog_and_avatars_separate(self):
        source=self.root/'source';source.mkdir();(source/'avatars').mkdir()
        (source/'catalog.webp').write_bytes(b'catalog')
        leaf='a'*32+'.webp';(source/'avatars'/leaf).write_bytes(b'avatar')
        archive=self.root/'artwork.tar'
        self.maintenance.archive_directory(source,archive,allow_avatars=True)
        target=self.root/'target';target.mkdir()
        from unittest.mock import patch
        with patch.object(self.maintenance,'UID',os.getuid()),patch.object(self.maintenance,'GID',os.getgid()):
            self.maintenance.extract_flat(archive,target,allow_avatars=True)
        self.assertEqual((target/'catalog.webp').read_bytes(),b'catalog')
        self.assertEqual((target/'avatars'/leaf).read_bytes(),b'avatar')
        with self.assertRaises(self.maintenance.MaintenanceError):self.maintenance.validate_archive(archive)

    def test_avatar_archive_refuses_nested_traversal_links_and_unexpected_folders(self):
        import io,tarfile
        for name,kind in [('avatars/../escape',tarfile.REGTYPE),('other/a.webp',tarfile.REGTYPE),('avatars/nested/a.webp',tarfile.REGTYPE),('avatars/'+'a'*32+'.webp',tarfile.SYMTYPE)]:
            archive=self.root/'bad-avatar.tar'
            with tarfile.open(archive,'w') as tar:
                item=tarfile.TarInfo(name);item.type=kind;item.linkname='/outside';tar.addfile(item,io.BytesIO())
            with self.assertRaises(self.maintenance.MaintenanceError):self.maintenance.validate_archive(archive,allow_avatars=True)

    def test_backup_rejects_reserved_avatar_file_and_roundtrips_hardlinked_avatar_bytes(self):
        source=self.root/'source';source.mkdir();(source/'avatars').write_bytes(b'not-a-directory')
        with self.assertRaises(self.maintenance.MaintenanceError):self.maintenance.archive_directory(source,self.root/'reserved.tar',allow_avatars=True)
        (source/'avatars').unlink();(source/'avatars').mkdir()
        first=source/'avatars'/('a'*32+'.webp');first.write_bytes(b'content')
        os.link(first,source/'avatars'/('b'*32+'.webp'))
        archive=self.root/'links.tar';self.maintenance.archive_directory(source,archive,allow_avatars=True)
        self.maintenance.validate_archive(archive,allow_avatars=True)

    def test_reference_verification_keeps_catalog_and_avatar_namespaces_separate(self):
        from unittest.mock import patch
        (self.root/'avatars').mkdir();leaf='a'*32+'.webp';(self.root/'avatars'/leaf).write_bytes(b'image')
        def query(sql):
            if 'to_regclass' in sql:return 't'
            if 'FROM artwork_assets' in sql:return 'avatars/'+leaf
            return ''
        with patch.object(self.maintenance,'query',side_effect=query):
            with self.assertRaises(self.maintenance.MaintenanceError):self.maintenance.verify_artwork(self.root)

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
        self.assertEqual(env['RATEFREAK_API_ERASURE_JOURNAL_PATH'],'/journal/account-erasures.jsonl')


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

class ReleaseSelectionTests(unittest.TestCase):
    setUp=InstanceTests.setUp
    tearDown=InstanceTests.tearDown
    def test_source_selection_switches_all_images_and_invalidates_old_acceptance(self):
        from unittest.mock import patch
        cli.initialize(self.root,'https://reader.example',False)
        cli.write_state(self.root/'release-selection.json',{'images':{n:{'tag':'ratefreak-'+n+':0.1.0'} for n in ['api','web','tools']}})
        cli.write_state(self.root/'migrated.json',{'old':'receipt'})
        images={n:'sha256:'+str(i)*64 for i,n in enumerate(['api','web','tools'])}
        with patch.object(cli,'release_images',return_value=images):cli.select_source(self.root)
        self.assertTrue(all(tag.endswith(':local') for tag in cli.selected_images(self.root).values()))
        self.assertFalse((self.root/'migrated.json').exists())
    def test_failed_selection_write_preserves_old_receipt_and_selection(self):
        from unittest.mock import patch
        cli.initialize(self.root,'https://reader.example',False)
        cli.write_state(self.root/'release-selection.json',{'images':{n:{'tag':'ratefreak-'+n+':0.1.0'} for n in ['api','web','tools']}})
        cli.write_state(self.root/'migrated.json',{'old':'receipt'});before=(self.root/'release-selection.json').read_bytes()
        with patch.object(cli,'write_state',side_effect=OSError('disk full')),self.assertRaises(OSError):cli.activate_selection(self.root,{'images':{}})
        self.assertEqual((self.root/'release-selection.json').read_bytes(),before);self.assertTrue((self.root/'migrated.json').exists())
    def test_bundle_selection_resolves_immutable_ids_instead_of_mutable_tags(self):
        cli.initialize(self.root,'https://reader.example',False)
        images={name:{'tag':'ratefreak-'+name+':0.1.0','id':'sha256:'+str(i)*64} for i,name in enumerate(['api','web','tools'])}
        cli.write_state(self.root/'release-selection.json',{'images':images})
        self.assertEqual(cli.selected_images(self.root),{name:entry['id'] for name,entry in images.items()})
    def test_bundle_compose_refuses_independent_service_image_overrides(self):
        from unittest.mock import patch
        config=cli.initialize(self.root,'https://reader.example',False)
        images={name:{'tag':'ratefreak-'+name+':0.1.0','id':'sha256:'+str(i)*64} for i,name in enumerate(['api','web','tools'])}
        cli.write_state(self.root/'release-selection.json',{'images':images})
        with patch.object(cli,'execute',return_value=json.dumps({'services':{'api':{'image':'ratefreak-api:wrong'}}})),self.assertRaises(cli.OperatorError):cli.compose(self.root,config)


class TmdbStorageTests(unittest.TestCase):
    def setUp(self):
        StorageTests.setUp(self)
        self.uid, self.gid = os.getuid(), os.getgid()
        for directory in ('storage', 'runtime', 'independent'):
            (self.root / directory).mkdir(mode=0o700)
        self.policy = self.root / 'independent/tmdb/tmdb-policy.json'

    tearDown = StorageTests.tearDown

    def prepare(self):
        self.entry.prepare_tmdb_storage(self.root, self.uid, self.gid)

    def test_first_prepare_clears_legacy_cache_and_repeat_preserves_policy_and_cache(self):
        self.entry.prepare_image_cache(self.root / 'storage', self.uid, self.gid)
        legacy = self.root / 'storage/image-cache'
        (legacy / 'optimizer-key').mkdir()
        (legacy / 'optimizer-key/old.webp').write_bytes(b'old optimized poster')
        self.prepare()
        self.assertEqual(list(legacy.iterdir()), [])
        self.assertEqual(json.loads(self.policy.read_bytes()), {
            'format': 1, 'terminated': False, 'blockedIdentities': [], 'blockedPosters': [],
        })
        for path in [self.policy.parent, self.root / 'storage/tmdb-posters']:
            self.assertEqual(stat.S_IMODE(path.stat().st_mode), 0o700)
            self.assertEqual((path.stat().st_uid, path.stat().st_gid), (self.uid, self.gid))
        self.assertEqual(stat.S_IMODE(self.policy.stat().st_mode), 0o600)
        blocked = b'{"format":1,"terminated":true,"blockedIdentities":["tv:1"],"blockedPosters":["poster.jpg"]}'
        self.policy.write_bytes(blocked)
        (legacy / 'igdb.webp').write_bytes(b'ordinary cache')
        self.prepare()
        self.assertEqual(self.policy.read_bytes(), blocked)
        self.assertEqual((legacy / 'igdb.webp').read_bytes(), b'ordinary cache')

    def test_repeat_prepare_refuses_missing_provisioned_policy(self):
        self.prepare()
        self.policy.unlink()
        with self.assertRaises(self.entry.StorageError):
            self.prepare()
        self.assertFalse(self.policy.exists())

    def test_prepare_refuses_missing_policy_when_only_host_receipt_survives(self):
        self.prepare()
        cli.write_state(self.root / 'prepared.json', {'format': 2})
        self.policy.unlink()
        self.policy.parent.rmdir()
        (self.root / 'runtime/tmdb-lifecycle-v1').unlink()
        legacy_file = self.root / 'storage/image-cache/igdb.webp'
        legacy_file.write_bytes(b'ordinary cache')

        with self.assertRaises(self.entry.StorageError):
            self.prepare()

        self.assertFalse(self.policy.parent.exists())
        self.assertEqual(legacy_file.read_bytes(), b'ordinary cache')

    def test_prepare_recovers_runtime_receipt_with_existing_policy_and_host_receipt(self):
        self.prepare()
        cli.write_state(self.root / 'prepared.json', {'format': 2})
        receipt = self.root / 'runtime/tmdb-lifecycle-v1'
        receipt.unlink()
        legacy_file = self.root / 'storage/image-cache/igdb.webp'
        legacy_file.write_bytes(b'ordinary cache')
        blocked = b'{"format":1,"terminated":true,"blockedIdentities":[],"blockedPosters":[]}'
        self.policy.write_bytes(blocked)

        self.prepare()

        self.assertEqual(self.policy.read_bytes(), blocked)
        self.assertEqual(legacy_file.read_bytes(), b'ordinary cache')
        self.assertTrue(receipt.is_file())

    def mounts_at(self, *mountpoints):
        from unittest.mock import patch
        read_bytes = Path.read_bytes

        def read(path):
            if path != Path('/proc/self/mountinfo'):
                return read_bytes(path)
            lines = []
            for mount_id, mountpoint in enumerate(mountpoints, start=101):
                escaped = os.fsencode(mountpoint).replace(b' ', b'\\040')
                lines.append(str(mount_id).encode() + b' 92 8:1 /other ' + escaped
                             + b' rw,relatime - ext4 /dev/sda1 rw\n')
            return b''.join(lines)

        return patch.object(Path, 'read_bytes', read)

    def test_prepare_upgrades_format_one_with_expected_instance_bind(self):
        cli.write_state(self.root / 'prepared.json', {'format': 1})
        self.entry.prepare_image_cache(self.root / 'storage', self.uid, self.gid)
        legacy = self.root / 'storage/image-cache'
        (legacy / 'old.jpg').write_bytes(b'old optimized poster')

        with self.mounts_at(self.root):
            self.prepare()

        self.assertTrue(self.policy.is_file())
        self.assertEqual(list(legacy.iterdir()), [])

    def test_prepare_refuses_same_device_mounts_in_legacy_cache_before_deletion(self):
        self.entry.prepare_image_cache(self.root / 'storage', self.uid, self.gid)
        legacy = self.root / 'storage/image-cache'
        nested = legacy / 'optimizer key'
        nested.mkdir()
        files = [legacy / 'ordinary.jpg', nested / 'unrelated.jpg']
        for file in files:
            file.write_bytes(b'keep')

        for mountpoint in (legacy, nested):
            with self.subTest(mountpoint=mountpoint), self.mounts_at(self.root, mountpoint):
                with self.assertRaises(self.entry.StorageError):
                    self.prepare()
                for file in files:
                    self.assertEqual(file.read_bytes(), b'keep')
                self.assertFalse(self.policy.exists())

    def test_prepare_refuses_symlinks_in_legacy_cache(self):
        self.entry.prepare_image_cache(self.root / 'storage', self.uid, self.gid)
        target = self.root / 'unrelated'
        target.write_bytes(b'keep')
        link = self.root / 'storage/image-cache/bad-link'
        link.symlink_to(target)
        with self.assertRaises(self.entry.StorageError):
            self.prepare()
        self.assertEqual(target.read_bytes(), b'keep')
        self.assertFalse(self.policy.exists())
        link.unlink()
        self.prepare()
        self.assertTrue(self.policy.is_file())

    def test_policy_refuses_unsafe_mode_owner_paths_and_noncanonical_schema(self):
        self.prepare()
        valid = self.policy.read_bytes()
        for data in [
            b'{"format":1,"format":1,"terminated":false,"blockedIdentities":[],"blockedPosters":[]}',
            b'{"format":true,"terminated":false,"blockedIdentities":[],"blockedPosters":[]}',
            b'{"format":1,"terminated":0,"blockedIdentities":[],"blockedPosters":[]}',
            b'{"format":1,"terminated":false,"blockedIdentities":["movie:01"],"blockedPosters":[]}',
            b'{"format":1,"terminated":false,"blockedIdentities":["tv:2147483648"],"blockedPosters":[]}',
            b'{"format":1,"terminated":false,"blockedIdentities":["tv:1","tv:1"],"blockedPosters":[]}',
            b'{"format":1,"terminated":false,"blockedIdentities":[],"blockedPosters":["../a.jpg"]}',
            b'{"format":1,"terminated":false,"blockedIdentities":[],"blockedPosters":[],"extra":0}',
            b' ' * (1024 * 1024 + 1),
        ]:
            with self.subTest(data=data[:160]), self.assertRaises(self.entry.StorageError):
                self.policy.write_bytes(data)
                self.prepare()
        self.policy.write_bytes(valid)
        with self.assertRaises(self.entry.StorageError):
            self.entry.read_tmdb_policy(self.policy, self.uid + 1, self.gid)
        self.policy.chmod(0o644)
        with self.assertRaises(self.entry.StorageError):
            self.prepare()
        self.policy.chmod(0o600)
        saved = self.policy.with_suffix('.saved')
        self.policy.rename(saved)
        self.policy.symlink_to(saved)
        with self.assertRaises(self.entry.StorageError):
            self.prepare()
        self.assertEqual(saved.read_bytes(), valid)

    def test_cache_clear_refuses_unexpected_entries_before_deleting_regular_files(self):
        self.prepare()
        cache = self.root / 'storage/tmdb-posters'
        (cache / 'a.jpg').write_bytes(b'cached')
        (cache / '.tmdb-lock').write_bytes(b'lock')
        (cache / 'unexpected').mkdir()
        with self.assertRaises(self.entry.StorageError):
            self.entry.clear_tmdb_cache(cache, self.uid, self.gid)
        self.assertTrue((cache / 'a.jpg').is_file())
        (cache / 'unexpected').rmdir()
        with self.mounts_at(cache):
            self.entry.clear_tmdb_cache(cache, self.uid, self.gid)
        self.assertEqual(list(cache.iterdir()), [])

    def test_restore_reads_current_policy_and_preserves_existing_blocks_on_invalid_input(self):
        import maintenance
        self.prepare()
        original = self.policy.read_bytes()
        current = self.root / 'current-tmdb-policy.json'
        current.write_bytes(b'{}')
        current.chmod(0o600)
        with self.assertRaises(maintenance.StorageError):
            maintenance.restore_tmdb_policy(current, self.policy, self.uid, self.gid)
        self.assertEqual(self.policy.read_bytes(), original)
        current.write_bytes(b'{"format":1,"terminated":true,"blockedIdentities":["tv:1"],"blockedPosters":["once.jpg"]}')
        maintenance.restore_tmdb_policy(current, self.policy, self.uid, self.gid)
        self.assertEqual(self.policy.read_bytes(), current.read_bytes())
        self.assertEqual(stat.S_IMODE(self.policy.stat().st_mode), 0o600)
        current.write_bytes(original)
        with self.assertRaises(maintenance.MaintenanceError):
            maintenance.restore_tmdb_policy(current, self.policy, self.uid, self.gid)

    def test_policy_refuses_hardlinked_file_without_modifying_it(self):
        self.prepare()
        original = self.policy.read_bytes()
        alias = self.root / 'linked-policy.json'
        os.link(self.policy, alias)
        with self.assertRaises(self.entry.StorageError):
            self.entry.read_tmdb_policy(alias, self.uid, self.gid)
        self.assertEqual(self.policy.read_bytes(), original)
        self.assertEqual(alias.stat().st_ino, self.policy.stat().st_ino)

    def test_restore_refuses_hardlink_alias_of_fresh_empty_policy(self):
        import maintenance
        self.prepare()
        original = self.policy.read_bytes()
        inode = self.policy.stat().st_ino
        alias = self.root / 'restore-policy-alias.json'
        os.link(self.policy, alias)
        with self.assertRaises((maintenance.StorageError, maintenance.MaintenanceError)):
            maintenance.restore_tmdb_policy(alias, self.policy, self.uid, self.gid)
        self.assertEqual(self.policy.read_bytes(), original)
        self.assertEqual(self.policy.stat().st_ino, inode)

    def test_restore_refuses_source_and_target_with_same_inode(self):
        import maintenance
        self.prepare()
        inode = self.policy.stat().st_ino
        with self.assertRaises(maintenance.MaintenanceError):
            maintenance.restore_tmdb_policy(self.policy, self.policy, self.uid, self.gid)
        self.assertEqual(self.policy.stat().st_ino, inode)

    def test_verify_replay_checks_blocked_posters_on_otherwise_valid_titles(self):
        import maintenance
        from unittest.mock import patch
        self.prepare()
        self.policy.write_bytes(b'{"format":1,"terminated":false,"blockedIdentities":[],"blockedPosters":["removed.jpg"]}')

        def checked(sql):
            self.assertIn("https://image.tmdb.org/t/p/w500/removed.jpg", sql)
            self.assertIn("image_reference IN", sql)
            return '0'

        with patch.object(maintenance, 'query', side_effect=checked):
            maintenance.verify_tmdb_replay(self.policy, self.uid, self.gid)

    def test_verify_replay_reports_remaining_blocked_or_expired_fields(self):
        import maintenance
        from unittest.mock import patch
        self.prepare()
        self.policy.write_bytes(b'{"format":1,"terminated":false,"blockedIdentities":["tv:2"],"blockedPosters":[]}')

        def remaining(sql):
            self.assertIn("'tv:2'", sql)
            self.assertIn("interval '6 months'", sql)
            self.assertIn("'Unavailable title'", sql)
            return '1'

        with patch.object(maintenance, 'query', side_effect=remaining), self.assertRaises(maintenance.MaintenanceError):
            maintenance.verify_tmdb_replay(self.policy, self.uid, self.gid)


class TmdbOperatorTests(unittest.TestCase):
    setUp = InstanceTests.setUp
    tearDown = InstanceTests.tearDown

    def test_tmdb_maintenance_mode_runs_offline_api_command(self):
        import entrypoint
        self.root.mkdir()
        for name in ['proxy', 'runtime']:
            cli.exclusive_file(self.root / name, b'a' * 64 + b'\n')
        arguments = ['--tmdb-maintenance', '--database-name', 'ratefreak', '--block', 'movie:12']
        command, _ = entrypoint.runtime_command('replay', self.root, arguments, {})
        self.assertEqual(command, ['dotnet', '/app/RateFreak.Api.dll', *arguments])

    def test_tmdb_remove_keeps_start_blocked_until_a_cache_cleanup_retry_succeeds(self):
        from argparse import Namespace
        from unittest.mock import patch
        config = cli.initialize(self.root, 'https://reader.example', False)
        images = {'api': 'a', 'web': 'w', 'tools': 't'}
        cli.write_state(self.root / 'migrated.json', {'images': images, 'overrideSha': ''})
        args = Namespace(command='tmdb-remove', database_name='exact-db', identity='movie:12', terminate=False,
                         poster=['one.jpg', 'two.webp'])
        command = ['compose']
        calls = []

        def execute(arguments, env=None, capture=False):
            calls.append(arguments)
            if arguments[1] == 'ps':
                return '[]'
            if arguments[-1] == 'tmdb-clear-cache' and len(calls) == 3:
                raise cli.OperatorError('Interrupted cache cleanup')

        with patch.object(cli, 'execute', side_effect=execute), patch.object(cli, 'release_images', return_value=images):
            with self.assertRaisesRegex(cli.OperatorError, 'Interrupted cache cleanup'):
                cli.dispatch(args, self.root, config, command, {})
            with self.assertRaises(cli.OperatorError):
                cli.require_ready(self.root)
            self.assertFalse((self.root / 'tmdb-removal-complete.json').exists())
            cli.dispatch(args, self.root, config, command, {})
        expected = [
            ['compose', 'ps', '--all', '--format', 'json'],
            ['compose', 'run', '--rm', 'tools', 'replay', '--tmdb-maintenance', '--database-name',
             'exact-db', '--block', 'movie:12', '--poster', 'one.jpg', '--poster', 'two.webp'],
            ['compose', 'run', '--rm', '--no-deps', 'tools', 'tmdb-clear-cache'],
        ]
        self.assertEqual(calls, expected * 2)
        cli.require_ready(self.root)
        completion = json.loads((self.root / 'tmdb-removal-complete.json').read_bytes())
        self.assertEqual(completion, {'databaseName': 'exact-db', 'identity': 'movie:12',
                                      'terminated': False, 'posters': ['one.jpg', 'two.webp']})

    def test_tmdb_remove_refuses_a_live_web_writer_before_maintenance(self):
        from argparse import Namespace
        from unittest.mock import patch
        config = cli.initialize(self.root, 'https://reader.example', False)
        args = Namespace(command='tmdb-remove', database_name='ratefreak', identity=None, terminate=True, poster=[])
        with patch.object(cli, 'execute', return_value='[{"Service":"web","State":"running"}]') as execute:
            with self.assertRaisesRegex(cli.OperatorError, 'Explicitly stop'):
                cli.dispatch(args, self.root, config, ['compose'], {})
        execute.assert_called_once_with(['compose', 'ps', '--all', '--format', 'json'], {}, capture=True)

    def test_restore_requires_independent_policy_cli_argument(self):
        from contextlib import redirect_stderr
        from io import StringIO
        from unittest.mock import patch
        arguments = ['ratefreak.py', '--instance-dir', str(self.root), 'restore', '--backup', '/unused',
                     '--journal', '/unused', '--confirm-restore']
        stderr = StringIO()
        with patch.object(sys, 'argv', arguments), redirect_stderr(stderr), self.assertRaises(SystemExit) as error:
            cli.main()
        self.assertEqual(error.exception.code, 2)
        self.assertIn('--tmdb-policy', stderr.getvalue())

    def test_restore_mounts_current_policy_read_only(self):
        from argparse import Namespace
        from unittest.mock import patch
        config = cli.initialize(self.root, 'https://reader.example', False)
        cli.write_state(self.root / 'prepared.json', {'format': 3})
        backup = self.root / 'backup'
        backup.mkdir()
        journal = self.root / 'journal'
        journal.touch(mode=0o600)
        policy = self.root / 'independently-stored-policy.json'
        policy.touch(mode=0o600)
        args = Namespace(command='restore', backup=backup, journal=journal, tmdb_policy=policy, igdb_policy=policy)
        with patch('maintenance.validate_backup'), patch.object(cli, 'release_images', return_value={}), \
             patch.object(cli, 'execute', return_value='[]') as execute:
            cli.dispatch(args, self.root, config, ['compose'], {})
        self.assertIn(str(policy) + ':/current-tmdb-policy:ro', execute.call_args.args[0])
        self.assertTrue((self.root / 'restore-pending').is_file())


class IgdbStorageTests(unittest.TestCase):
    def setUp(self):
        StorageTests.setUp(self)
        self.uid, self.gid = os.getuid(), os.getgid()
        for directory in ('storage', 'runtime', 'independent'):
            (self.root / directory).mkdir(mode=0o700)
        self.policy = self.root / 'independent/igdb/igdb-policy.json'

    tearDown = StorageTests.tearDown

    def prepare(self):
        self.entry.prepare_igdb_storage(self.root, self.uid, self.gid)

    def test_first_prepare_clears_legacy_cache_and_repeat_preserves_policy_and_cache(self):
        self.entry.prepare_image_cache(self.root / 'storage', self.uid, self.gid)
        legacy = self.root / 'storage/image-cache'
        (legacy / 'optimizer-key').mkdir()
        (legacy / 'optimizer-key/old.webp').write_bytes(b'old optimized poster')
        self.prepare()
        self.assertEqual(list(legacy.iterdir()), [])
        self.assertEqual(json.loads(self.policy.read_bytes()), {
            'format': 1, 'terminated': False, 'blockedIdentities': [], 'blockedPosters': [],
        })
        for path in [self.policy.parent, self.root / 'storage/igdb-covers']:
            self.assertEqual(stat.S_IMODE(path.stat().st_mode), 0o700)
            self.assertEqual((path.stat().st_uid, path.stat().st_gid), (self.uid, self.gid))
        self.assertEqual(stat.S_IMODE(self.policy.stat().st_mode), 0o600)
        blocked = b'{"format":1,"terminated":true,"blockedIdentities":["game:1"],"blockedPosters":["poster.jpg"]}'
        self.policy.write_bytes(blocked)
        (legacy / 'igdb.webp').write_bytes(b'ordinary cache')
        self.prepare()
        self.assertEqual(self.policy.read_bytes(), blocked)
        self.assertEqual((legacy / 'igdb.webp').read_bytes(), b'ordinary cache')

    def test_repeat_prepare_refuses_missing_provisioned_policy(self):
        self.prepare()
        self.policy.unlink()
        with self.assertRaises(self.entry.StorageError):
            self.prepare()
        self.assertFalse(self.policy.exists())

    def test_prepare_refuses_missing_policy_when_only_host_receipt_survives(self):
        self.prepare()
        cli.write_state(self.root / 'prepared.json', {'format': 3})
        self.policy.unlink()
        self.policy.parent.rmdir()
        (self.root / 'runtime/igdb-lifecycle-v1').unlink()
        legacy_file = self.root / 'storage/image-cache/igdb.webp'
        legacy_file.write_bytes(b'ordinary cache')

        with self.assertRaises(self.entry.StorageError):
            self.prepare()

        self.assertFalse(self.policy.parent.exists())
        self.assertEqual(legacy_file.read_bytes(), b'ordinary cache')

    def test_prepare_recovers_runtime_receipt_with_existing_policy_and_host_receipt(self):
        self.prepare()
        cli.write_state(self.root / 'prepared.json', {'format': 3})
        receipt = self.root / 'runtime/igdb-lifecycle-v1'
        receipt.unlink()
        legacy_file = self.root / 'storage/image-cache/igdb.webp'
        legacy_file.write_bytes(b'ordinary cache')
        blocked = b'{"format":1,"terminated":true,"blockedIdentities":[],"blockedPosters":[]}'
        self.policy.write_bytes(blocked)

        self.prepare()

        self.assertEqual(self.policy.read_bytes(), blocked)
        self.assertEqual(legacy_file.read_bytes(), b'ordinary cache')
        self.assertTrue(receipt.is_file())

    def mounts_at(self, *mountpoints):
        from unittest.mock import patch
        read_bytes = Path.read_bytes

        def read(path):
            if path != Path('/proc/self/mountinfo'):
                return read_bytes(path)
            lines = []
            for mount_id, mountpoint in enumerate(mountpoints, start=101):
                escaped = os.fsencode(mountpoint).replace(b' ', b'\\040')
                lines.append(str(mount_id).encode() + b' 92 8:1 /other ' + escaped
                             + b' rw,relatime - ext4 /dev/sda1 rw\n')
            return b''.join(lines)

        return patch.object(Path, 'read_bytes', read)

    def test_prepare_upgrades_format_one_with_expected_instance_bind(self):
        cli.write_state(self.root / 'prepared.json', {'format': 1})
        self.entry.prepare_image_cache(self.root / 'storage', self.uid, self.gid)
        legacy = self.root / 'storage/image-cache'
        (legacy / 'old.jpg').write_bytes(b'old optimized poster')

        with self.mounts_at(self.root):
            self.prepare()

        self.assertTrue(self.policy.is_file())
        self.assertEqual(list(legacy.iterdir()), [])

    def test_prepare_refuses_same_device_mounts_in_legacy_cache_before_deletion(self):
        self.entry.prepare_image_cache(self.root / 'storage', self.uid, self.gid)
        legacy = self.root / 'storage/image-cache'
        nested = legacy / 'optimizer key'
        nested.mkdir()
        files = [legacy / 'ordinary.jpg', nested / 'unrelated.jpg']
        for file in files:
            file.write_bytes(b'keep')

        for mountpoint in (legacy, nested):
            with self.subTest(mountpoint=mountpoint), self.mounts_at(self.root, mountpoint):
                with self.assertRaises(self.entry.StorageError):
                    self.prepare()
                for file in files:
                    self.assertEqual(file.read_bytes(), b'keep')
                self.assertFalse(self.policy.exists())

    def test_prepare_refuses_symlinks_in_legacy_cache(self):
        self.entry.prepare_image_cache(self.root / 'storage', self.uid, self.gid)
        target = self.root / 'unrelated'
        target.write_bytes(b'keep')
        link = self.root / 'storage/image-cache/bad-link'
        link.symlink_to(target)
        with self.assertRaises(self.entry.StorageError):
            self.prepare()
        self.assertEqual(target.read_bytes(), b'keep')
        self.assertFalse(self.policy.exists())
        link.unlink()
        self.prepare()
        self.assertTrue(self.policy.is_file())

    def test_policy_refuses_unsafe_mode_owner_paths_and_noncanonical_schema(self):
        self.prepare()
        valid = self.policy.read_bytes()
        for data in [
            b'{"format":1,"format":1,"terminated":false,"blockedIdentities":[],"blockedPosters":[]}',
            b'{"format":true,"terminated":false,"blockedIdentities":[],"blockedPosters":[]}',
            b'{"format":1,"terminated":0,"blockedIdentities":[],"blockedPosters":[]}',
            b'{"format":1,"terminated":false,"blockedIdentities":["game:01"],"blockedPosters":[]}',
            b'{"format":1,"terminated":false,"blockedIdentities":["game:2147483648"],"blockedPosters":[]}',
            b'{"format":1,"terminated":false,"blockedIdentities":["game:1","game:1"],"blockedPosters":[]}',
            b'{"format":1,"terminated":false,"blockedIdentities":[],"blockedPosters":["../a.jpg"]}',
            b'{"format":1,"terminated":false,"blockedIdentities":[],"blockedPosters":[],"extra":0}',
            b' ' * (1024 * 1024 + 1),
        ]:
            with self.subTest(data=data[:160]), self.assertRaises(self.entry.StorageError):
                self.policy.write_bytes(data)
                self.prepare()
        self.policy.write_bytes(valid)
        with self.assertRaises(self.entry.StorageError):
            self.entry.read_igdb_policy(self.policy, self.uid + 1, self.gid)
        self.policy.chmod(0o644)
        with self.assertRaises(self.entry.StorageError):
            self.prepare()
        self.policy.chmod(0o600)
        saved = self.policy.with_suffix('.saved')
        self.policy.rename(saved)
        self.policy.symlink_to(saved)
        with self.assertRaises(self.entry.StorageError):
            self.prepare()
        self.assertEqual(saved.read_bytes(), valid)

    def test_cache_clear_refuses_unexpected_entries_before_deleting_regular_files(self):
        self.prepare()
        cache = self.root / 'storage/igdb-covers'
        (cache / 'a.jpg').write_bytes(b'cached')
        (cache / '.igdb-lock').write_bytes(b'lock')
        (cache / 'unexpected').mkdir()
        with self.assertRaises(self.entry.StorageError):
            self.entry.clear_igdb_cache(cache, self.uid, self.gid)
        self.assertTrue((cache / 'a.jpg').is_file())
        (cache / 'unexpected').rmdir()
        with self.mounts_at(cache):
            self.entry.clear_igdb_cache(cache, self.uid, self.gid)
        self.assertEqual(list(cache.iterdir()), [])

    def test_restore_reads_current_policy_and_preserves_existing_blocks_on_invalid_input(self):
        import maintenance
        self.prepare()
        original = self.policy.read_bytes()
        current = self.root / 'current-igdb-policy.json'
        current.write_bytes(b'{}')
        current.chmod(0o600)
        with self.assertRaises(maintenance.StorageError):
            maintenance.restore_igdb_policy(current, self.policy, self.uid, self.gid)
        self.assertEqual(self.policy.read_bytes(), original)
        current.write_bytes(b'{"format":1,"terminated":true,"blockedIdentities":["game:1"],"blockedPosters":["once.jpg"]}')
        maintenance.restore_igdb_policy(current, self.policy, self.uid, self.gid)
        self.assertEqual(self.policy.read_bytes(), current.read_bytes())
        self.assertEqual(stat.S_IMODE(self.policy.stat().st_mode), 0o600)
        current.write_bytes(original)
        with self.assertRaises(maintenance.MaintenanceError):
            maintenance.restore_igdb_policy(current, self.policy, self.uid, self.gid)

    def test_policy_refuses_hardlinked_file_without_modifying_it(self):
        self.prepare()
        original = self.policy.read_bytes()
        alias = self.root / 'linked-policy.json'
        os.link(self.policy, alias)
        with self.assertRaises(self.entry.StorageError):
            self.entry.read_igdb_policy(alias, self.uid, self.gid)
        self.assertEqual(self.policy.read_bytes(), original)
        self.assertEqual(alias.stat().st_ino, self.policy.stat().st_ino)

    def test_restore_refuses_hardlink_alias_of_fresh_empty_policy(self):
        import maintenance
        self.prepare()
        original = self.policy.read_bytes()
        inode = self.policy.stat().st_ino
        alias = self.root / 'restore-policy-alias.json'
        os.link(self.policy, alias)
        with self.assertRaises((maintenance.StorageError, maintenance.MaintenanceError)):
            maintenance.restore_igdb_policy(alias, self.policy, self.uid, self.gid)
        self.assertEqual(self.policy.read_bytes(), original)
        self.assertEqual(self.policy.stat().st_ino, inode)

    def test_restore_refuses_source_and_target_with_same_inode(self):
        import maintenance
        self.prepare()
        inode = self.policy.stat().st_ino
        with self.assertRaises(maintenance.MaintenanceError):
            maintenance.restore_igdb_policy(self.policy, self.policy, self.uid, self.gid)
        self.assertEqual(self.policy.stat().st_ino, inode)

    def test_verify_replay_checks_blocked_posters_on_otherwise_valid_titles(self):
        import maintenance
        from unittest.mock import patch
        self.prepare()
        self.policy.write_bytes(b'{"format":1,"terminated":false,"blockedIdentities":[],"blockedPosters":["removed.jpg"]}')

        def checked(sql):
            self.assertIn("https://images.igdb.com/igdb/image/upload/t_cover_big/removed.jpg", sql)
            self.assertIn("image_reference IN", sql)
            return '0'

        with patch.object(maintenance, 'query', side_effect=checked):
            maintenance.verify_igdb_replay(self.policy, self.uid, self.gid)

    def test_verify_replay_reports_remaining_blocked_or_expired_fields(self):
        import maintenance
        from unittest.mock import patch
        self.prepare()
        self.policy.write_bytes(b'{"format":1,"terminated":false,"blockedIdentities":["game:2"],"blockedPosters":[]}')

        def remaining(sql):
            self.assertIn("'game:2'", sql)
            self.assertIn("interval '6 months'", sql)
            self.assertIn("'Unavailable title'", sql)
            return '1'

        with patch.object(maintenance, 'query', side_effect=remaining), self.assertRaises(maintenance.MaintenanceError):
            maintenance.verify_igdb_replay(self.policy, self.uid, self.gid)


class IgdbOperatorTests(unittest.TestCase):
    setUp = InstanceTests.setUp
    tearDown = InstanceTests.tearDown

    def test_igdb_maintenance_mode_runs_offline_api_command(self):
        import entrypoint
        self.root.mkdir()
        for name in ['proxy', 'runtime']:
            cli.exclusive_file(self.root / name, b'a' * 64 + b'\n')
        arguments = ['--igdb-maintenance', '--database-name', 'ratefreak', '--block', 'game:12']
        command, _ = entrypoint.runtime_command('replay', self.root, arguments, {})
        self.assertEqual(command, ['dotnet', '/app/RateFreak.Api.dll', *arguments])

    def test_igdb_remove_keeps_start_blocked_until_a_cache_cleanup_retry_succeeds(self):
        from argparse import Namespace
        from unittest.mock import patch
        config = cli.initialize(self.root, 'https://reader.example', False)
        images = {'api': 'a', 'web': 'w', 'tools': 't'}
        cli.write_state(self.root / 'migrated.json', {'images': images, 'overrideSha': ''})
        args = Namespace(command='igdb-remove', database_name='exact-db', identity='game:12', terminate=False,
                         poster=['one.jpg', 'two.jpg'])
        command = ['compose']
        calls = []

        def execute(arguments, env=None, capture=False):
            calls.append(arguments)
            if arguments[1] == 'ps':
                return '[]'
            if arguments[-1] == 'igdb-clear-cache' and len(calls) == 3:
                raise cli.OperatorError('Interrupted cache cleanup')

        with patch.object(cli, 'execute', side_effect=execute), patch.object(cli, 'release_images', return_value=images):
            with self.assertRaisesRegex(cli.OperatorError, 'Interrupted cache cleanup'):
                cli.dispatch(args, self.root, config, command, {})
            with self.assertRaises(cli.OperatorError):
                cli.require_ready(self.root)
            self.assertFalse((self.root / 'igdb-removal-complete.json').exists())
            cli.dispatch(args, self.root, config, command, {})
        expected = [
            ['compose', 'ps', '--all', '--format', 'json'],
            ['compose', 'run', '--rm', 'tools', 'replay', '--igdb-maintenance', '--database-name',
             'exact-db', '--block', 'game:12', '--poster', 'one.jpg', '--poster', 'two.jpg'],
            ['compose', 'run', '--rm', '--no-deps', 'tools', 'igdb-clear-cache'],
        ]
        self.assertEqual(calls, expected * 2)
        cli.require_ready(self.root)
        completion = json.loads((self.root / 'igdb-removal-complete.json').read_bytes())
        self.assertEqual(completion, {'databaseName': 'exact-db', 'identity': 'game:12',
                                      'terminated': False, 'posters': ['one.jpg', 'two.jpg']})

    def test_igdb_remove_refuses_a_live_web_writer_before_maintenance(self):
        from argparse import Namespace
        from unittest.mock import patch
        config = cli.initialize(self.root, 'https://reader.example', False)
        args = Namespace(command='igdb-remove', database_name='ratefreak', identity=None, terminate=True, poster=[])
        with patch.object(cli, 'execute', return_value='[{"Service":"web","State":"running"}]') as execute:
            with self.assertRaisesRegex(cli.OperatorError, 'Explicitly stop'):
                cli.dispatch(args, self.root, config, ['compose'], {})
        execute.assert_called_once_with(['compose', 'ps', '--all', '--format', 'json'], {}, capture=True)

    def test_restore_requires_independent_policy_cli_argument(self):
        from contextlib import redirect_stderr
        from io import StringIO
        from unittest.mock import patch
        arguments = ['ratefreak.py', '--instance-dir', str(self.root), 'restore', '--backup', '/unused',
                     '--journal', '/unused', '--confirm-restore']
        stderr = StringIO()
        with patch.object(sys, 'argv', arguments), redirect_stderr(stderr), self.assertRaises(SystemExit) as error:
            cli.main()
        self.assertEqual(error.exception.code, 2)
        self.assertIn('--igdb-policy', stderr.getvalue())

    def test_restore_mounts_current_policy_read_only(self):
        from argparse import Namespace
        from unittest.mock import patch
        config = cli.initialize(self.root, 'https://reader.example', False)
        cli.write_state(self.root / 'prepared.json', {'format': 3})
        backup = self.root / 'backup'
        backup.mkdir()
        journal = self.root / 'journal'
        journal.touch(mode=0o600)
        policy = self.root / 'independently-stored-policy.json'
        policy.touch(mode=0o600)
        args = Namespace(command='restore', backup=backup, journal=journal, igdb_policy=policy, tmdb_policy=policy)
        with patch('maintenance.validate_backup'), patch.object(cli, 'release_images', return_value={}), \
             patch.object(cli, 'execute', return_value='[]') as execute:
            cli.dispatch(args, self.root, config, ['compose'], {})
        self.assertIn(str(policy) + ':/current-igdb-policy:ro', execute.call_args.args[0])
        self.assertTrue((self.root / 'restore-pending').is_file())


class ProviderPackagingTests(unittest.TestCase):
    def test_api_can_persist_confirmed_igdb_removals_but_web_cannot_edit_policy(self):
        # Atomic replacement requires the policy parent mount to be writable.
        compose = Path(__file__).with_name('compose.yaml').read_text()
        api = compose.split('  api:', 1)[1].split('  web:', 1)[0]
        web = compose.split('  web:', 1)[1].split('  storage-init:', 1)[0]
        def policy_mount(service):
            return service.split('target: /independent-igdb', 1)[1].split('      - ', 1)[0]
        self.assertNotIn('read_only: true', policy_mount(api))
        self.assertIn('read_only: true', policy_mount(web))

    def test_new_preparation_rejects_old_host_receipt_until_explicit_prepare(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            cli.write_state(root / 'prepared.json', {'format': 2})
            with self.assertRaises(cli.OperatorError):
                cli.require_prepared(root)

    def test_igdb_prepare_upgrades_tmdb_install_and_preserves_managed_artwork(self):
        import entrypoint
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            for name in ('runtime', 'storage', 'independent'):
                (root / name).mkdir(mode=0o700)
            uid, gid = os.getuid(), os.getgid()
            entrypoint.prepare_tmdb_storage(root, uid, gid)
            cli.write_state(root / 'prepared.json', {'format': 2})
            keep = root / 'storage/tmdb-posters/keep.jpg'
            keep.write_bytes(b'managed TMDB image')
            old = root / 'storage/image-cache/legacy.webp'
            old.write_bytes(b'obsolete optimized image')
            entrypoint.prepare_igdb_storage(root, uid, gid)
            self.assertFalse(old.exists())
            self.assertEqual(keep.read_bytes(), b'managed TMDB image')
            self.assertTrue((root / 'independent/igdb/igdb-policy.json').is_file())


if __name__ == "__main__":unittest.main()
