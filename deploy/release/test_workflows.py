from pathlib import Path
import tempfile
import unittest
import workflow_checks
class WorkflowTests(unittest.TestCase):
    def test_rejects_mutable_actions_and_write_or_publish_permissions(self):
        with tempfile.TemporaryDirectory() as temp:
            p=Path(temp)/'bad.yml'
            for content in ['permissions: write-all\n','steps:\n  - uses: actions/checkout@v7\n','on: pull_request_target\n','run: gh release create\n','permissions:\n  contents: write\n','run: echo "${{ inputs.tag }}"\n']:
                p.write_text(content)
                with self.subTest(content=content),self.assertRaises(ValueError):workflow_checks.check(p)
    def test_repository_workflows_are_read_only_and_pinned(self):
        root=Path(__file__).resolve().parents[2]
        for name in ['ci.yml','release-candidate.yml']:workflow_checks.check(root/'.github/workflows'/name)
    def test_candidate_resume_requires_exact_artifact_selection(self):
        text=(Path(__file__).resolve().parents[2]/'.github/workflows/release-candidate.yml').read_text()
        self.assertNotIn('pattern: candidate-review-*',text)
        self.assertIn('artifact-ids: ${{ steps.candidate_source.outputs.artifact_id }}',text)
    def test_artifact_provenance_rejects_fork_or_wrong_workflow_or_ref(self):
        artifact={'id':10,'name':'candidate-review-3-1','expired':False,'workflow_run':{'id':3}}
        run={'id':3,'path':'.github/workflows/release-candidate.yml','event':'workflow_dispatch','status':'completed','head_branch':'main','head_repository':{'full_name':'owner/repo'}}
        self.assertEqual(workflow_checks.validate_artifact_metadata(artifact,run,'candidate','owner/repo')['artifact_id'],'10')
        for key,value in [('head_branch','untrusted'),('path','.github/workflows/other.yml'),('head_repository',{'full_name':'fork/repo'})]:
            with self.subTest(key=key),self.assertRaises(ValueError):workflow_checks.validate_artifact_metadata(artifact,{**run,key:value},'candidate','owner/repo')
