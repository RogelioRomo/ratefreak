import copy
import hashlib
import importlib.util
import io
import json
from pathlib import Path
import subprocess
import tarfile
import tempfile
import unittest
from unittest.mock import MagicMock, patch

spec=importlib.util.spec_from_file_location('release_acceptance',Path(__file__).with_name('acceptance.py'))
a=importlib.util.module_from_spec(spec);spec.loader.exec_module(a)


def table(rows, columns=None):
    return {'columns':columns or list(rows[0]),'rows':rows}


def migration_fixture():
    before={'users':table([{'id':'owner','name':'Original'}]),
            'catalog_categories':table([{'id':'original','name':'Original'}],
                ['id','name','normalized_name','scope','is_hidden','is_seeded','revision','owner_user_id']),
            '__EFMigrationsHistory':table([{'MigrationId':'old','ProductVersion':'9.0'}])}
    after=copy.deepcopy(before)
    after['catalog_categories']['rows'].append({'id':'f11e0000-0000-0000-0000-000000000006','name':'Songs',
        'normalized_name':'SONGS','scope':'public','is_hidden':False,'is_seeded':True,'revision':1,'owner_user_id':None})
    for migration in ['20261124233146_AddTmdbContentLifecycle','20261127171719_AddIgdbContentLifecycle','20261127222703_AddSongs']:
        after['__EFMigrationsHistory']['rows'].append({'MigrationId':migration,'ProductVersion':'9.0'})
    for provider in ['tmdb','igdb']:
        after[provider+'_lifecycle_states']=table([],['mapping_id','next_attempt_at','last_attempt_at','failures','last_outcome','content_removed'])
        after[provider+'_poster_identities']=table([],['mapping_id','filename'])
        after[provider+'_refresh_schedule']=table([{'id':1,'last_batch_at':None,'next_allowed_at':None}])
    return before,after


class UpgradePreservationTests(unittest.TestCase):
    def test_accepts_only_reviewed_migrations_and_preserves_original_rows(self):
        before,after=migration_fixture()
        proof=a.verify_upgrade_preservation(before,after)
        self.assertEqual(proof['original_table_count'],3)
        self.assertEqual(len(proof['added_tables']),6)
        self.assertTrue(proof['songs_seed_verified'])

    def test_rejects_original_row_edit_deletion_duplication_and_unexpected_insert(self):
        for change in ['edit','delete','duplicate','insert']:
            with self.subTest(change=change):
                before,after=migration_fixture()
                if change=='edit':after['users']['rows'][0]['name']='Changed'
                elif change=='delete':after['users']['rows']=[]
                elif change=='duplicate':after['users']['rows']*=2
                else:after['users']['rows'].append({'id':'other','name':'Other'})
                with self.assertRaises(AssertionError):a.verify_upgrade_preservation(before,after)

    def test_rejects_unreviewed_table_column_or_migration_changes(self):
        for change in ['table','drop-table','column','drop-column','history','missing-history','history-edit']:
            with self.subTest(change=change):
                before,after=migration_fixture()
                if change=='table':after['unexpected']=table([],['id'])
                elif change=='drop-table':after.pop('users')
                elif change=='column':after['users']['columns'].append('unreviewed')
                elif change=='drop-column':after['users']['columns'].remove('name')
                elif change=='history':after['__EFMigrationsHistory']['rows'].append({'MigrationId':'unreviewed','ProductVersion':'9.0'})
                elif change=='missing-history':after['__EFMigrationsHistory']['rows'].pop()
                else:after['__EFMigrationsHistory']['rows'][0]['ProductVersion']='changed'
                with self.assertRaises(AssertionError):a.verify_upgrade_preservation(before,after)

    def test_rejects_bad_songs_seed_and_unexpected_lifecycle_data(self):
        for change in ['seed-name','seed-owner','seed-missing','seed-duplicate','lifecycle-schema','schedule','lifecycle-row']:
            with self.subTest(change=change):
                before,after=migration_fixture();rows=after['catalog_categories']['rows']
                if change=='seed-name':rows[-1]['name']='Other'
                elif change=='seed-owner':rows[-1]['owner_user_id']='owner'
                elif change=='seed-missing':rows.pop()
                elif change=='seed-duplicate':rows.append(copy.deepcopy(rows[-1]))
                elif change=='lifecycle-schema':after['tmdb_lifecycle_states']['columns'].append('unexpected')
                elif change=='schedule':after['tmdb_refresh_schedule']['rows'][0]['id']=2
                else:after['igdb_poster_identities']['rows'].append({'mapping_id':'unexpected','filename':'cover.jpg'})
                with self.assertRaises(AssertionError):a.verify_upgrade_preservation(before,after)

    def test_existing_new_schema_is_preserved_without_requiring_duplicate_seed(self):
        _,current=migration_fixture()
        proof=a.verify_upgrade_preservation(current,copy.deepcopy(current))
        self.assertEqual(proof['added_tables'],[])

    def test_rejects_an_original_column_type_change(self):
        before,after=migration_fixture()
        before['users']['column_types']={'id':['text','NO'],'name':['text','NO']}
        after['users']['column_types']={'id':['text','NO'],'name':['varchar','NO']}
        with self.assertRaises(AssertionError):a.verify_upgrade_preservation(before,after)


class SelectionTests(unittest.TestCase):
    def test_reference_identities_are_separate_and_fully_verified(self):
        apps={role:'sha256:'+str(i)*64 for i,role in enumerate(['api','web','tools'],1)}
        refs={'postgres':'sha256:'+'4'*64,'caddy':'sha256:'+'5'*64}
        roles={value:role for role,value in (apps|refs).items()}
        def inspect(command,**kw):
            identity=command[-1];return json.dumps([{'Id':identity,'RepoTags':['ratefreak-'+roles[identity]+':trial']}])
        with patch.object(a.operator,'execute',side_effect=inspect):selection=a.image_selection(apps,reference_images=refs)
        self.assertEqual(set(selection['images']),set(apps))
        self.assertEqual({role:item['id'] for role,item in selection['reference_images'].items()},refs)
        with self.assertRaises(ValueError):a.image_selection(apps,reference_images={'postgres':refs['postgres']})

    def test_wrong_reference_id_or_tag_is_rejected(self):
        apps={role:'sha256:'+str(i)*64 for i,role in enumerate(['api','web','tools'],1)}
        refs={'postgres':'sha256:'+'4'*64,'caddy':'sha256:'+'5'*64}
        def inspect(command,**kw):
            identity=command[-1];role=next(role for role,value in (apps|refs).items() if value==identity)
            return json.dumps([{'Id':identity,'RepoTags':['foreign:caddy'] if role=='caddy' else ['ratefreak-'+role+':trial']}])
        with patch.object(a.operator,'execute',side_effect=inspect),self.assertRaises(ValueError):a.image_selection(apps,reference_images=refs)

    def test_baseline_preparation_uses_candidate_tools_without_changing_selection(self):
        env={'RATEFREAK_TOOLS_IMAGE':'baseline-tools','RATEFREAK_POSTGRES_IMAGE':'patched-postgres'}
        candidate={'images':{'tools':{'tag':'ratefreak-tools:trial','id':'sha256:'+'9'*64}}}
        with patch.object(a.operator,'execute') as execute:a.prepare_candidate_storage(['docker','compose'],env,candidate)
        command,actual=execute.call_args.args
        self.assertEqual(command,['docker','compose','run','--rm','--no-deps','storage-init'])
        self.assertEqual(actual['RATEFREAK_TOOLS_IMAGE'],'sha256:'+'9'*64)
        self.assertEqual(actual['RATEFREAK_POSTGRES_IMAGE'],'patched-postgres')
        self.assertEqual(env['RATEFREAK_TOOLS_IMAGE'],'baseline-tools')

    def test_reference_cli_option_is_accepted(self):
        parsed=a.argument_parser().parse_args(['--confirm-disposable','--report-directory','out','--images-json','apps.json','--reference-images-json','references.json'])
        self.assertEqual(parsed.reference_images_json,Path('references.json'))

    def test_upgrade_keeps_exact_references_and_orders_candidate_storage_before_old_migrations(self):
        apps={role:'sha256:'+str(i)*64 for i,role in enumerate(['api','web','tools'],1)}
        baseline={role:'sha256:'+str(i)*64 for i,role in enumerate(['api','web','tools'],6)}
        refs={'postgres':'sha256:'+'4'*64,'caddy':'sha256:'+'5'*64}
        roles={value:role for role,value in (apps|refs).items()}
        roles.update({value:role for role,value in baseline.items()})
        events=[]
        def execute(command,env=None,capture=False):
            if command[:3]==['docker','image','inspect']:
                identity=command[-1];return json.dumps([{'Id':identity,'RepoTags':['ratefreak-'+roles[identity]+':trial']}])
            events.append(('execute',command,dict(env or {})))
        def selection(root):return json.loads((root/'release-selection.json').read_text())
        def compose(root,config):
            selected=selection(root)
            self.assertEqual({name:item['id'] for name,item in selected['reference_images'].items()},refs)
            return ['compose'],{'RATEFREAK_TOOLS_IMAGE':selected['images']['tools']['id'],
                'RATEFREAK_POSTGRES_IMAGE':refs['postgres'],'RATEFREAK_CADDY_IMAGE':refs['caddy']}
        def dispatch(args,root,config,command,env):events.append((args.command,env['RATEFREAK_TOOLS_IMAGE']))
        def seed(root,origin,ca):
            (root/'acceptance.json').write_text(json.dumps({'password':'fixture','public':{'id':'item','imageReference':'/api/v1/artwork/fixture'}}))
        class Client:
            def __init__(self,*args):pass
            def login(self,*args):pass
            def call(self,path,*args,**kwargs):
                if path=='admin/updates':return json.dumps({'state':'disabled','enabled':False}).encode(),{'Cache-Control':'no-store'}
                return json.dumps({'status':'completed'}).encode(),{}
        before,after=migration_fixture()
        socket=MagicMock();socket.__enter__.return_value.getsockname.return_value=('127.0.0.1',44444)
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory)/'fixture';root.mkdir()
            with patch.object(a.tempfile,'mkdtemp',return_value=str(root)),patch.object(a.socket,'socket',return_value=socket),\
                 patch.object(a.operator,'execute',side_effect=execute),patch.object(a.operator,'compose',side_effect=compose),\
                 patch.object(a.operator,'dispatch',side_effect=dispatch),patch.object(a.operator,'release_images',side_effect=lambda root:{name:item['id'] for name,item in selection(root)['images'].items()}),\
                 patch.object(a.operator,'require_release',side_effect=[None,a.operator.OperatorError('stale'),None]),\
                 patch.object(a.subprocess,'run',return_value=subprocess.CompletedProcess([],0)),patch.object(a,'Client',Client),\
                 patch.object(a,'seed',side_effect=seed),patch.object(a,'database_snapshot',side_effect=[before,after]),\
                 patch.object(a,'verify_backup_storage') as verify:
                _,result=a.run(apps,Path(directory)/'reports',baseline,reference_images=refs)
            self.assertEqual(result['referenceImages'],refs)
            self.assertEqual(result['candidateImages'],apps)
            self.assertTrue(result['upgrade'])
            self.assertEqual(selection(root)['reference_images']['caddy']['id'],refs['caddy'])
            override=json.loads((root/'compose.override.yaml').read_text())
            self.assertEqual(override['services']['caddy']['image'],refs['caddy'])
            preparation=[event for event in events if event[0] in ['prepare','migrate']]
            self.assertEqual(preparation,[('prepare',baseline['tools']),('migrate',baseline['tools']),('prepare',apps['tools']),('migrate',apps['tools'])])
            self.assertEqual(events[0][1],['compose','run','--rm','--no-deps','storage-init'])
            self.assertEqual(events[0][2]['RATEFREAK_TOOLS_IMAGE'],apps['tools'])
            verify.assert_called_once()


class BackupPreservationTests(unittest.TestCase):
    def test_compares_original_archive_members_and_allows_new_key_files(self):
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory)
            for name,content in [('artwork',b'original image'),('keys',b'original key')]:
                with tarfile.open(root/(name+'.tar'),'w') as archive:
                    entry=tarfile.TarInfo('original');entry.size=len(content);archive.addfile(entry,io.BytesIO(content))
            actual={name:{'original':hashlib.sha256(content).hexdigest()} for name,content in [('artwork',b'original image'),('keys',b'original key')]}
            actual['keys']['new-key']='new-digest'
            with patch.object(a.subprocess,'check_output',return_value=json.dumps(actual)):
                a.verify_backup_storage(root,['docker','compose'],{})
            actual['artwork']['original']='changed'
            with patch.object(a.subprocess,'check_output',return_value=json.dumps(actual)),self.assertRaises(AssertionError):
                a.verify_backup_storage(root,['docker','compose'],{})
