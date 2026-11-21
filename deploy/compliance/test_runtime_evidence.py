import hashlib
import json
from pathlib import Path
import tarfile
import tempfile
import unittest

import runtime_evidence


class RuntimeEvidenceTests(unittest.TestCase):
    def test_copied_runtime_packages_and_yarn_notice_survive_collection(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary) / 'image'
            status = root / 'var/lib/dpkg/status'
            status.parent.mkdir(parents=True)
            status.write_text('Package: base-files\nStatus: install ok installed\nArchitecture: all\nVersion: 1\nDescription: fixture\nMaintainer: Test <test@example.test>\n\n')
            yarn = root / 'opt/yarn-v1.22.22'
            yarn.mkdir(parents=True)
            (yarn / 'LICENSE').write_text('Yarn copyright and license\n')
            (yarn / 'package.json').write_text(json.dumps({'name': 'yarn', 'version': '1.22.22', 'license': 'BSD-2-Clause'}))
            npm = root / 'usr/local/lib/node_modules/npm'
            npm.mkdir(parents=True)
            (npm / 'package.json').write_text(json.dumps({'name': 'npm', 'version': '11.19.1', 'license': 'Artistic-2.0'}))
            nested = npm / 'node_modules/dependency'
            nested.mkdir(parents=True)
            (nested / 'package.json').write_text(json.dumps({'name': 'dependency', 'version': '2', 'license': 'MIT'}))
            binary = root / 'usr/local/bin/gosu'
            binary.parent.mkdir(parents=True)
            binary.write_bytes(b'copied executable'); binary.chmod(0o755)
            script = binary.with_name('docker-entrypoint.sh')
            script.write_text('#!/bin/sh\nexit 0\n'); script.chmod(0o755)
            output = Path(temporary) / 'evidence'
            runtime_evidence.collect(output, root=root)
            evidence = json.loads((output / 'evidence.json').read_text())
            self.assertEqual({(p['name'], p['version']) for p in evidence['runtime_packages']},
                             {('yarn', '1.22.22'), ('npm', '11.19.1'), ('dependency', '2')})
            copied = {p['path']: p for p in evidence['copied_files']}
            self.assertEqual(copied['/usr/local/bin/gosu']['sha256'], hashlib.sha256(b'copied executable').hexdigest())
            self.assertIn('/usr/local/bin/docker-entrypoint.sh', copied)
            with tarfile.open(output / 'runtime-license-texts.tar.gz') as archive:
                self.assertEqual(archive.extractfile('opt/yarn-v1.22.22/LICENSE').read(), b'Yarn copyright and license\n')

class RuntimeSupplementTests(unittest.TestCase):
    def test_exact_file_supplements_are_packaged_and_changed_inputs_are_rejected(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary) / 'image'
            status = root / 'var/lib/dpkg/status'; status.parent.mkdir(parents=True)
            status.write_text('Package: base-files\nStatus: install ok installed\nArchitecture: all\nVersion: 1\nDescription: fixture\nMaintainer: Test <test@example.test>\n\n')
            target = root / 'usr/local/bin/tool'; target.parent.mkdir(parents=True)
            target.write_bytes(b'original tool')
            companion = target.with_name('companion'); companion.write_bytes(b'companion')
            supplements = root / 'app/runtime-supplements'; supplements.mkdir(parents=True)
            notice = supplements / 'NOTICE.txt'; notice.write_bytes(b'upstream copyright and license')
            (supplements / 'sources.json').write_text(json.dumps([{
                'file': 'NOTICE.txt', 'sha256': 'a' * 64,
                'images': ['web'],
                'url': 'https://example.test/other-image/LICENSE',
                'targets': [{'path': '/usr/local/bin/tool', 'sha256': 'a' * 64},
                            {'path': '/usr/local/bin/other-image-only', 'sha256': 'a' * 64}]}, {
                'file': 'NOTICE.txt', 'sha256': hashlib.sha256(notice.read_bytes()).hexdigest(),
                'images': ['tools'],
                'url': 'https://example.test/exact-commit/LICENSE',
                'targets': [{'path': '/usr/local/bin/tool', 'sha256': hashlib.sha256(b'original tool').hexdigest()},
                            {'path': '/usr/local/bin/companion', 'sha256': hashlib.sha256(b'companion').hexdigest()}]}]))
            output = Path(temporary) / 'output'
            runtime_evidence.collect(output, root=root, image_family='tools')
            with tarfile.open(output / 'runtime-license-texts.tar.gz') as archive:
                self.assertEqual(archive.extractfile('app/runtime-supplements/NOTICE.txt').read(), b'upstream copyright and license')
            evidence = json.loads((output / 'evidence.json').read_text())
            self.assertEqual(evidence['supplement_sources'][0]['url'], 'https://example.test/exact-commit/LICENSE')
            companion.unlink()
            with self.assertRaises(ValueError):runtime_evidence.collect(Path(temporary) / 'missing', root=root, image_family='tools')
            companion.write_bytes(b'companion')
            target.write_bytes(b'changed tool')
            with self.assertRaises(ValueError):runtime_evidence.collect(Path(temporary) / 'changed', root=root, image_family='tools')
            target.write_bytes(b'original tool'); notice.write_bytes(b'changed license')
            with self.assertRaises(ValueError):runtime_evidence.collect(Path(temporary) / 'corrupt', root=root, image_family='tools')


if __name__ == '__main__':
    unittest.main()
