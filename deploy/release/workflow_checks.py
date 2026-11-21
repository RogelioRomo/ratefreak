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

def validate_artifact_metadata(artifact,run,kind,repository):
    expected_name='candidate-review-' if kind=='candidate' else 'release-source-materials' if kind=='sources' else 'published-release-'
    name=artifact.get('name','')
    if (not name.startswith(expected_name) if kind!='sources' else name!=expected_name) or artifact.get('expired'):
        raise ValueError('Unexpected or expired review artifact.')
    expected_paths={'.github/workflows/release-candidate.yml'} if kind=='candidate' else {'.github/workflows/release-source-materials.yml'} if kind=='sources' else {'.github/workflows/published-release-evidence.yml'}
    if run.get('path') not in expected_paths or run.get('event')!='workflow_dispatch' or run.get('status')!='completed' or run.get('head_branch')!='main' or run.get('head_repository',{}).get('full_name','').lower()!=repository.lower():
        raise ValueError('Artifact was not produced by the expected completed main-branch workflow in this repository.')
    if artifact.get('workflow_run',{}).get('id')!=run.get('id'):raise ValueError('Artifact run identity mismatch.')
    return {'artifact_id':str(artifact['id']),'run_id':str(run['id'])}

def validate_artifact():
    import json,urllib.request
    artifact_id=os.environ['REVIEW_ARTIFACT_ID'];repository=os.environ['GITHUB_REPOSITORY'];kind=os.environ['REVIEW_ARTIFACT_KIND']
    if not re.fullmatch('[1-9][0-9]*',artifact_id) or not re.fullmatch('[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+',repository) or kind not in {'candidate','sources','baseline'}:raise ValueError('Invalid artifact selection.')
    def fetch(path):
        request=urllib.request.Request('https://api.github.com/repos/'+repository+path,headers={'Authorization':'Bearer '+os.environ['GITHUB_TOKEN'],'User-Agent':'RateFreak-release-preparation','Accept':'application/vnd.github+json'})
        with urllib.request.urlopen(request,timeout=15) as response:
            data=response.read(1024*1024+1)
            if len(data)>1024*1024:raise ValueError('Oversized artifact metadata.')
            return json.loads(data)
    artifact=fetch('/actions/artifacts/'+artifact_id)
    run=fetch('/actions/runs/'+str(artifact['workflow_run']['id']))
    values=validate_artifact_metadata(artifact,run,kind,repository)
    with open(os.environ['GITHUB_OUTPUT'],'a') as output:
        for key,value in values.items():output.write(key+'='+value+'\n')

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('command',choices=['check','resolve','validate-artifact']);args=parser.parse_args()
    root=Path(__file__).resolve().parents[2]
    if args.command=='check':
        for file in (root/'.github/workflows').glob('*.yml'):check(file)
    elif args.command=='validate-artifact':validate_artifact()
    else:
        tag=os.environ['CANDIDATE_TAG']
        if not tag.startswith('v'):raise ValueError('Expected version tag.')
        validate_version(tag[1:]);revision=git(root,'rev-parse','--verify','refs/tags/'+tag+'^{commit}')
        if not re.fullmatch('[a-f0-9]{40}',revision):raise ValueError('Invalid source commit.')
        with open(os.environ['GITHUB_OUTPUT'],'a') as output:output.write('revision='+revision+'\n')
