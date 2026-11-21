"""Distinct review/final schemas and cross-artifact release evidence checks."""
import json
import hashlib
import shutil
import sys
import tempfile
from pathlib import Path
from pathlib import PurePosixPath
import tarfile

BASE_FILES={'api.tar.gz','web.tar.gz','tools.tar.gz','api-notices.tar.gz','web-notices.tar.gz','tools-notices.tar.gz','installation.tar.gz','RELEASE-NOTES.md','reports.tar.gz','built-images.json','source-inventory.json'}
FINAL_FILES={'sources.tar.gz','acceptance.tar.gz','fresh-acceptance.tar.gz','baseline-images.json'}

def archived_json(path,name):
    with tarfile.open(path) as archive:
        members=archive.getmembers();names=set()
        for member in members:
            part=PurePosixPath(member.name.rstrip('/'))
            if part.is_absolute() or '..' in part.parts or '\\' in member.name or member.name in names or not(member.isfile() or member.isdir()):raise ValueError('Unsafe evidence archive.')
            names.add(member.name)
        member=archive.getmember(name)
        if not member.isfile() or member.size>8*1024*1024:raise ValueError('Invalid evidence record.')
        return json.load(archive.extractfile(member))

def validate(manifest,root,manifest_name,read_json):
    expected='review' if manifest_name=='candidate-manifest.json' else 'verified'
    if manifest.get('stage')!=expected:raise ValueError('An incomplete review candidate is not an installable release.')
    required=BASE_FILES|(FINAL_FILES if expected=='verified' else set())
    if not required<=set(manifest['files']):raise ValueError('Missing required installation, notes, notices, verification or source/acceptance artifacts.')
    images={name:entry['id'] for name,entry in manifest['images'].items()}
    if read_json(root/'built-images.json')!=images:raise ValueError('Built image inventory differs from manifest.')
    inventory=read_json(root/'source-inventory.json')
    if set(inventory.get('images',{}))!=set(images) or any(inventory['images'][name].get('image_id')!=image for name,image in images.items()):raise ValueError('Source inventory differs from built images.')
    report=archived_json(root/'reports.tar.gz','reports/verification.json')
    identity=report.get('identity',{})
    required_checks={'restore','tools','web-install','UnitTests','ProviderContractTests','IntegrationTests','operator','release','compliance','web-lint','web-typecheck','web-test','web-build','api-publish','model'}
    if {check.get('check') for check in report.get('checks',[])}!=required_checks:raise ValueError('Missing required release verification checks.')
    if not report.get('complete') or identity.get('version')!=manifest['version'] or identity.get('revision')!=manifest['revision'] or not report.get('checks') or any(check.get('exitCode')!=0 for check in report['checks']):raise ValueError('Verification does not describe this candidate.')
    suites=report.get('tests',[])
    if {p.get('suite') for p in suites}!={'UnitTests','ProviderContractTests','IntegrationTests'} or any(p.get('total',0)<1 or p.get('executed')!=p['total'] or p.get('failed')!=0 or p.get('skipped')!=0 for p in suites):raise ValueError('Missing, failed or skipped backend verification.')
    with tarfile.open(root/'installation.tar.gz') as archive:
        for member in archive:
            path=PurePosixPath(member.name.rstrip('/'))
            if path.is_absolute() or '..' in path.parts or not(member.isfile() or member.isdir()):raise ValueError('Unsafe installation archive.')
    sys.path.insert(0,str(Path(__file__).resolve().parents[1]/'release'))
    from source_inventory import validate_runtime_inventory
    runtime_evidence={}
    for name in images:
        notices=archived_json(root/(name+'-notices.tar.gz'),name+'-notices/manifest.json')
        if notices.get('notice_texts_complete') is not True:raise ValueError('Incomplete image notices.')
        runtime_evidence[name]=archived_json(root/(name+'-notices.tar.gz'),name+'-notices/runtime/evidence.json')
    validate_runtime_inventory(inventory,runtime_evidence)
    if expected=='verified':
        fresh=archived_json(root/'fresh-acceptance.tar.gz','fresh-acceptance/acceptance.json')
        if fresh.get('candidateImages')!=images or fresh.get('freshInstall') is not True or fresh.get('https') is not True:raise ValueError('Exact-candidate fresh installation acceptance is missing.')
        acceptance=archived_json(root/'acceptance.tar.gz','acceptance/acceptance.json')
        baseline=read_json(root/'baseline-images.json')
        if acceptance.get('candidateImages')!=images or acceptance.get('baselineImages')!=baseline or set(baseline)!=set(images) or baseline==images or any(acceptance.get(key) is not True for key in ['https','upgrade','dataPreserved','disabledUpdateChecks','ordinaryDenied','logoutDenied']):raise ValueError('Exact-candidate fresh/upgrade acceptance is incomplete.')
        coverage=archived_json(root/'sources.tar.gz','sources/coverage.json')
        for name,image in images.items():
            if coverage.get('images',{}).get(name,{}).get('image_id')!=image:raise ValueError('Source evidence differs from release images.')
        # Material decisions must cover the generated shipped component inventory.
        from source_inventory import validate_coverage
        validate_coverage(coverage,inventory)
        from source_inventory import validate_native
        sys.path.insert(0,str(Path(__file__).resolve().parents[1]/'compliance'))
        from source_bundle import verify
        with tarfile.open(root/'sources.tar.gz') as archive:
            for entry in coverage['images'].values():
                for name,digest in entry['files'].items():
                    member=archive.getmember('sources/'+name)
                    if not member.isfile() or hashlib.file_digest(archive.extractfile(member),'sha256').hexdigest()!=digest:
                        raise ValueError('Missing or changed corresponding source material.')
            native=coverage['images']['web'].get('native_sidecar')
            if not native or native not in coverage['images']['web']['files']:raise ValueError('Missing native source sidecar.')
            member=archive.getmember('sources/'+native)
            with tempfile.TemporaryDirectory(prefix='ratefreak-native-check-') as temporary:
                path=Path(temporary)/'native.tar.gz'
                with path.open('wb') as target:shutil.copyfileobj(archive.extractfile(member),target)
                validate_native(verify(path),inventory)
