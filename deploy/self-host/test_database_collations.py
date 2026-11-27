import argparse
import importlib.util
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

spec = importlib.util.spec_from_file_location('collation_operator', Path(__file__).with_name('ratefreak.py'))
cli = importlib.util.module_from_spec(spec)
spec.loader.exec_module(cli)


class DatabaseCollationTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name) / 'instance'
        self.config = cli.initialize(self.root, 'https://reader.example', False)
        cli.write_state(self.root / 'prepared.json', {'format': 3})
        self.images = {name: 'sha256:' + str(index) * 64 for index, name in enumerate(['api', 'web', 'tools'], 1)}
        cli.write_state(self.root / 'migrated.json', {'images': self.images, 'overrideSha': ''})
        self.commands = []

    def runner(self, collation_result, writers='[]'):
        def execute(command, env=None, capture=False):
            self.commands.append(command)
            if 'ps' in command:
                return writers
            if 'psql' in command:
                return collation_result
            return None
        return execute

    def dispatch(self, operation, answer, writers='[]'):
        with patch.object(cli, 'execute', side_effect=self.runner(answer, writers)), patch.object(cli, 'release_images', return_value=self.images):
            cli.dispatch(argparse.Namespace(command=operation, ingress='external'), self.root, self.config, ['docker', 'compose'], {})

    def test_migrate_refuses_mismatch_before_schema_writes_and_invalidates_prior_acceptance(self):
        with self.assertRaisesRegex(cli.OperatorError, 'collation'):
            self.dispatch('migrate', 'f\n')
        self.assertFalse((self.root / 'migrated.json').exists())
        self.assertNotIn(['docker', 'compose', 'run', '--rm', 'migrate'], self.commands)

    def test_start_refuses_mismatch_before_application_writers_or_deployment_acceptance(self):
        with self.assertRaisesRegex(cli.OperatorError, 'collation'):
            self.dispatch('start', 'f\n')
        self.assertFalse((self.root / 'deployment.json').exists())
        self.assertFalse(any(command[:5] == ['docker', 'compose', 'up', '-d', '--wait'] and 'api' in command for command in self.commands))

    def test_current_versions_allow_migration_then_acceptance(self):
        self.dispatch('migrate', 't\n')
        query = next(index for index, command in enumerate(self.commands) if 'psql' in command)
        migration = self.commands.index(['docker', 'compose', 'run', '--rm', 'migrate'])
        self.assertLess(query, migration)
        self.assertEqual(cli.require_release(self.root, self.images)['images'], self.images)

    def test_start_refuses_running_writers_before_database_recreation(self):
        with self.assertRaisesRegex(cli.OperatorError, 'stop every'):
            self.dispatch('start', 't\n', '[{"Service":"api","State":"running"}]')
        self.assertNotIn(['docker', 'compose', 'up', '-d', '--wait', 'postgres'], self.commands)

    def test_unknown_or_failed_metadata_is_not_accepted(self):
        for answer in ['', 't\nf\n', 'unexpected']:
            with self.subTest(answer=answer), self.assertRaisesRegex(cli.OperatorError, 'collation'):
                self.dispatch('start', answer)
        self.assertFalse((self.root / 'deployment.json').exists())


if __name__ == '__main__':
    unittest.main()
