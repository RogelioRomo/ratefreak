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
