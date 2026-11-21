"""Conservative workflow guardrails plus safe tag-to-commit resolution."""
from pathlib import Path
import argparse
import os
import re
from versioning import validate_version,git

def check(path):
    text=path.read_text()
    for forbidden in ['pull_request_target','write-all','gh release create','docker push','git push']:
        if forbidden in text:raise ValueError('Workflow contains forbidden write/publication behavior.')
    if re.search(r'\b(?:contents|packages|actions|id-token)\s*:\s*write\b',text):raise ValueError('Workflow must be read-only.')
    for line in text.splitlines():
        if re.search(r'\buses:',line) and not re.search(r'uses:\s+[\w.-]+/[\w./-]+@[a-f0-9]{40}(?:\s|$)',line):raise ValueError('Action must be pinned to a reviewed full SHA.')
        if re.search(r'\brun:',line) and '${{' in line:raise ValueError('Never interpolate expressions in shell commands.')
    if '${{ inputs.tag }}' in text and not re.search(r'^\s+CANDIDATE_TAG:\s*\$\{\{ inputs.tag \}\}',text,re.M):raise ValueError('Pass the tag through an environment boundary.')

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('command',choices=['check','resolve']);args=parser.parse_args()
    root=Path(__file__).resolve().parents[2]
    if args.command=='check':
        for file in (root/'.github/workflows').glob('*.yml'):check(file)
    else:
        tag=os.environ['CANDIDATE_TAG']
        if not tag.startswith('v'):raise ValueError('Expected version tag.')
        validate_version(tag[1:]);revision=git(root,'rev-parse','--verify','refs/tags/'+tag+'^{commit}')
        if not re.fullmatch('[a-f0-9]{40}',revision):raise ValueError('Invalid source commit.')
        with open(os.environ['GITHUB_OUTPUT'],'a') as output:output.write('revision='+revision+'\n')
