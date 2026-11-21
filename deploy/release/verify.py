#!/usr/bin/env python3
"""Run release checks and retain command results; no publication permissions."""
import argparse
import json
import os
from pathlib import Path
import re
import subprocess
import sys
import tempfile
import xml.etree.ElementTree as ET
from versioning import read_identity

def assert_executed(path):
    tree=ET.parse(path);counters=tree.find('.//{*}Counters')
    if counters is None or int(counters.get('total','0'))<1 or counters.get('total')!=counters.get('executed') or int(counters.get('failed','0')) or int(counters.get('notExecuted','0')):
        raise ValueError('Missing, failed or skipped backend tests: '+path.name)

def verify(root,reports,tag=None):
    reports.mkdir(parents=True,exist_ok=False);results=[];identity=read_identity(root,tag)
    scratch=tempfile.TemporaryDirectory(prefix="ratefreak-release-verify-");temporary=Path(scratch.name)
    complete=False
    commands=[('restore',['dotnet','restore','RateFreak.sln']),('tools',['dotnet','tool','restore']),('web-install',['pnpm','--dir','web','install','--frozen-lockfile'])]
    for project in ['UnitTests','ProviderContractTests','IntegrationTests']:
        commands.append((project,['dotnet','test','tests/RateFreak.'+project,'--no-restore','--logger','trx;LogFileName='+project+'.trx','--results-directory',str(reports)]))
    for name,directory in [('operator','deploy/self-host'),('release','deploy/release'),('compliance','deploy/compliance')]:commands.append((name,[sys.executable,'-m','unittest','discover','-s',directory,'-p','test_*.py']))
    for name in ['lint','typecheck','test','build']:commands.append(('web-'+name,['pnpm','--dir','web',name]))
    commands.append(('api-publish',['dotnet','publish','src/RateFreak.Api','-c','Release','--no-restore','-o',str(temporary/'api-publish')]))
    commands.append(('model',['dotnet','ef','migrations','has-pending-model-changes','--project','src/RateFreak.Infrastructure','--startup-project','src/RateFreak.Api']))
    environment=os.environ.copy();environment['ConnectionStrings__Default']='Host=127.0.0.1;Database=release_model_only;Username=release_model_only'
    environment['Authentication__DataProtectionKeysPath']=str(temporary/'model-keys')
    try:
        for name,command in commands:
            print('Verifying '+name,flush=True)
            with (reports/(name+'.log')).open('w') as log:
                result=subprocess.run(command,cwd=root,env=environment,stdout=log,stderr=subprocess.STDOUT)
            results.append({'check':name,'exitCode':result.returncode})
            if result.returncode:raise ValueError('Verification failed: '+name)
        for path in reports.glob('*.trx'):assert_executed(path)
        if len(list(reports.glob('*.trx')))!=3:raise ValueError('Missing backend test result files.')
        complete=True
    finally:
        (reports/'verification.json').write_text(json.dumps({'identity':identity,'checks':results,'complete':complete},indent=2)+'\n')
        scratch.cleanup()
    return results

if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--report-directory',required=True,type=Path);parser.add_argument('--tag');args=parser.parse_args()
    try:verify(Path(__file__).resolve().parents[2],args.report_directory.resolve(),args.tag)
    except (OSError,ValueError,subprocess.CalledProcessError) as error:parser.exit(1,str(error)+'\n')
