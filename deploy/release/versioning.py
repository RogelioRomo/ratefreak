"""Canonical release identity; no shell evaluation of refs or version inputs."""
import json
from pathlib import Path
import re
import subprocess

SEMVER = re.compile(r'(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(?:-([0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?(?:\+([0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?')

def validate_version(value: str) -> str:
    match=SEMVER.fullmatch(value) if isinstance(value,str) and len(value)<=128 else None
    if not match or match[4] and any(p.isdigit() and len(p)>1 and p[0]=='0' for p in match[4].split('.')):
        raise ValueError('Expected canonical SemVer (at most 128 characters).')
    return value

def git(repository: Path, *args: str) -> str:
    return subprocess.check_output(['git','-C',str(repository),*args],text=True).strip()

def read_identity(repository: Path, tag: str | None = None) -> dict:
    version=validate_version((repository/'VERSION').read_text().strip())
    if json.loads((repository/'web/package.json').read_text())['version']!=version.split('-')[0].split('+')[0]:
        raise ValueError('Package base version differs from VERSION.')
    if not (repository/'.git').exists() and (repository/'SOURCE_REVISION').is_file():
        revision=(repository/'SOURCE_REVISION').read_text().strip()
        if tag is not None or not re.fullmatch('[a-f0-9]{40}',revision):raise ValueError('Source archives cannot claim official tagged builds.')
        return dict(version=version,revision=revision,official=False,dirty=True)
    revision=git(repository,'rev-parse','HEAD')
    dirty=bool(git(repository,'status','--porcelain','--untracked-files=normal'))
    if tag is not None:
        if tag!='v'+version or validate_version(tag[1:])!=version or dirty:
            raise ValueError('Official candidates require a matching clean tag.')
        if git(repository,'rev-parse','--verify',f'refs/tags/{tag}^{{commit}}')!=revision:
            raise ValueError('Tag does not identify this source revision.')
    return dict(version=version,revision=revision,official=tag is not None,dirty=dirty)

if __name__=='__main__':
    import argparse
    parser=argparse.ArgumentParser();parser.add_argument('--repository',type=Path,default=Path(__file__).resolve().parents[2]);parser.add_argument('--tag')
    args=parser.parse_args();print(json.dumps(read_identity(args.repository,args.tag),sort_keys=True))
