from pathlib import Path
import argparse
import importlib.util
import json
import subprocess
import sys

ROOT = next(parent for parent in Path(__file__).resolve().parents
            if (parent / 'deploy/self-host/compose.yaml').is_file())
BASE = ROOT / '.local/security-closure-2026-11-26'
SOURCE = BASE.parents[1]
spec = importlib.util.spec_from_file_location('release_acceptance', SOURCE / 'deploy/release/acceptance.py')
acceptance = importlib.util.module_from_spec(spec)
spec.loader.exec_module(acceptance)
operator = acceptance.operator
root = Path((BASE / 'fresh-fixture-root.txt').read_text().strip())
config = json.loads((root / 'instance.json').read_text())
command, env = operator.compose(root, config)
images = json.loads((SOURCE / '.local/security-runtime-guard-2026-11-25/images/built-images.json').read_text())
backup = BASE / 'maintenance-backup'
target = BASE / 'maintenance-restore'
result = {'format': 1, 'candidate_images': images, 'complete': False,
          'scope': 'Updated PostgreSQL client backup/isolated restore, protected key/artwork integrity and replay with an empty current fixture journal; no production data.'}
restore_command = None


def cli(name, **kwargs):
    operator.dispatch(argparse.Namespace(command=name, **kwargs), root, config, command, env)


try:
    subprocess.run(command + ['stop', 'api', 'web', 'caddy'], env=env, check=True)
    before = acceptance.fingerprints(command, env)
    cli('backup', output=backup)
    # Copy only this fixture's current journal, never a journal from its backup.
    subprocess.run(command + ['run', '--rm', '--no-deps', '--user', '0:0', '--entrypoint', 'python3',
        '-v', str(BASE) + ':/audit', 'tools', '-c',
        'import shutil,os; shutil.copyfile("/journal/account-erasures.jsonl", "/audit/current-journal.jsonl"); shutil.copyfile("/independent/tmdb-policy.json", "/audit/current-tmdb-policy.json"); os.chown("/audit/current-tmdb-policy.json",10001,10001); os.chmod("/audit/current-tmdb-policy.json",0o600); os.chown("/audit/current-journal.jsonl",1000,1000); os.chmod("/audit/current-journal.jsonl",0o600)'], env=env, check=True)
    assert (BASE / 'current-journal.jsonl').read_text() == ''
    restore_config = operator.initialize(target, 'https://restore-security-clearance.example', True)
    operator.write_state(target / 'release-selection.json', acceptance.image_selection(images))
    restore_command, restore_env = operator.compose(target, restore_config)
    def restore_cli(name, **kwargs):
        operator.dispatch(argparse.Namespace(command=name, **kwargs), target, restore_config, restore_command, restore_env)
    restore_cli('prepare')
    restore_cli('restore', backup=backup, journal=BASE / 'current-journal.jsonl', tmdb_policy=BASE / 'current-tmdb-policy.json', confirm_restore=True)
    assert (target / 'restore-pending').exists()
    restore_cli('replay', database_name='ratefreak', confirm_erasure_replay=True)
    assert not (target / 'restore-pending').exists()
    after = acceptance.fingerprints(restore_command, restore_env)
    assert before == after
    # Repeated replay must leave rows unchanged and remain accepted.
    restore_cli('replay', database_name='ratefreak', confirm_erasure_replay=True)
    assert after == acceptance.fingerprints(restore_command, restore_env)
    manifest = json.loads((backup / 'manifest.json').read_text())
    result.update(complete=True, table_count=len(before), table_fingerprints_preserved=True,
                  artwork_and_key_archives_verified=True, current_journal_entries=0,
                  restore_startup_guard=True, repeated_replay_preserved_data=True,
                  backup_images=manifest['images'], journal_excluded_from_backup=manifest['journalIncluded'] is False)
finally:
    if restore_command is not None:
        subprocess.run(restore_command + ['down', '--volumes', '--remove-orphans'], env=restore_env, check=True)
    subprocess.run(command + ['down', '--volumes', '--remove-orphans'], env=env, check=True)
    projects = [command[command.index('--project-name') + 1]]
    if restore_command is not None:
        projects.append(restore_command[restore_command.index('--project-name') + 1])
    result['cleanup'] = []
    for project in projects:
        containers = subprocess.check_output(['docker', 'ps', '-aq', '--filter', 'label=com.docker.compose.project=' + project], text=True).strip()
        volumes = subprocess.check_output(['docker', 'volume', 'ls', '-q', '--filter', 'label=com.docker.compose.project=' + project], text=True).strip()
        assert not containers and not volumes
        result['cleanup'].append({'project': project, 'containers_remaining': False, 'volumes_remaining': False})
    (BASE / 'maintenance-acceptance.json').write_text(json.dumps(result, indent=2, sort_keys=True) + '\n')
print('Updated client backup, isolated database/artwork/key restore and repeated empty-journal replay passed.')
