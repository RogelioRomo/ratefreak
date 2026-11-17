import importlib.util
import json
from pathlib import Path
import tempfile
import unittest

spec = importlib.util.spec_from_file_location("notices", Path(__file__).with_name("notices.py"))
notices = importlib.util.module_from_spec(spec)
spec.loader.exec_module(notices)

class NoticesTests(unittest.TestCase):
    def test_collects_nested_native_notices_and_records_hashes_without_absolute_paths(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            (root / "package.json").write_text(json.dumps({"dependencies": {"example": "1.0"}}))
            package = root / "node_modules/example"
            (package / "native").mkdir(parents=True)
            (package / "package.json").write_text(json.dumps({"name": "example", "version": "1.0", "license": "MIT"}))
            (package / "LICENSE").write_text("Copyright Example\nPermission granted\n")
            (package / "native/THIRD-PARTY-NOTICES.txt").write_text("Native dependency notices\n")
            records = notices.web_packages(root)
            manifest = notices.write_bundle(records, root / "out")
            self.assertEqual(manifest["missing"], [])
            text = (root / "out/THIRD-PARTY-NOTICES.txt").read_text()
            self.assertIn("Native dependency notices", text)
            self.assertIn("Copyright Example", text)
            self.assertNotIn(tmp, json.dumps(manifest))
            first = (root / "out/manifest.json").read_bytes()
            notices.write_bundle(records, root / "out")
            self.assertEqual(first, (root / "out/manifest.json").read_bytes())
            self.assertEqual(len(manifest["packages"][0]["texts"][0]["sha256"]), 64)

    def test_missing_text_and_missing_required_dependency_are_explicit(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            (root / "package.json").write_text(json.dumps({"dependencies": {"example": "1.0"}}))
            with self.assertRaisesRegex(ValueError, "example"):
                notices.web_packages(root)
            p = root / "node_modules/example"
            p.mkdir(parents=True)
            (p / "package.json").write_text(json.dumps({"name": "example", "version": "1.0", "license": "MIT"}))
            manifest = notices.write_bundle(notices.web_packages(root), root / "out")
            self.assertEqual(manifest["missing"], ["example@1.0"])
            self.assertFalse(manifest["notice_texts_complete"])

    def test_supplement_content_must_match_recorded_hash(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            (root / "LICENSE").write_text("changed text")
            (root / "sources.json").write_text(json.dumps([{"file": "LICENSE", "packages": ["Example@1"], "sha256": "0" * 64}]))
            record = {"name": "Example", "version": "1", "ecosystem": "npm", "license": "MIT", "texts": []}
            with self.assertRaisesRegex(ValueError, "hash mismatch"):
                notices.write_bundle([record], root / "out", root)

    def test_third_party_notice_alone_does_not_fill_missing_primary_license(self):
        with tempfile.TemporaryDirectory() as tmp:
            record = {"name": "Example", "version": "1", "ecosystem": "npm", "license": "MIT", "texts": [("NOTICE.txt", b"Third-party notice")]}
            manifest = notices.write_bundle([record], Path(tmp))
            self.assertEqual(manifest["missing"], ["Example@1"])

    def test_embedded_license_cannot_replace_the_package_primary_license(self):
        for filename in ["THIRD-PARTY-LICENSES.txt", "third_party/LICENSE"]:
            with self.subTest(filename=filename), tempfile.TemporaryDirectory() as tmp:
                record = {"name": "Example", "version": "1", "ecosystem": "npm", "license": "MIT", "texts": [(filename, b"Another component's license")]}
                manifest = notices.write_bundle([record], Path(tmp))
                self.assertEqual(manifest["missing"], ["Example@1"])

    def test_assets_required_package_cannot_disappear_from_a_partial_cache(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            p = root / "example/1"
            p.mkdir(parents=True)
            (p / "example.nuspec").write_text('<package><metadata><id>Example</id><version>1</version><license>MIT</license></metadata></package>')
            assets = root / "assets.json"
            assets.write_text(json.dumps({"libraries": {"Example/1": {"type": "package"}, "Missing/1": {"type": "package"}}}))
            with self.assertRaisesRegex(ValueError, "missing/1"):
                notices.nuget_packages(root, [assets])

    def test_rust_notice_collection_uses_only_requested_built_packages_and_fails_missing(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            p = root / "example-1.0.0"
            p.mkdir()
            (p / "Cargo.toml").write_text('[package]\nname = "example"\nversion = "1.0.0"\nlicense = "MIT"\n')
            (p / "LICENSE-MIT").write_text("Published copyright and license")
            records = notices.rust_packages(root, [{"name": "example", "version": "1.0.0"}])
            self.assertEqual(len(records), 1)
            self.assertEqual(records[0]["ecosystem"], "cargo")
            self.assertIn(b"Published copyright", records[0]["texts"][0][1])
            with self.assertRaisesRegex(ValueError, "missing@1"):
                notices.rust_packages(root, [{"name": "missing", "version": "1"}])

    def test_nuget_keeps_license_and_native_notice_and_repository_provenance(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            package = root / "example/1.0"
            package.mkdir(parents=True)
            (package / "example.nuspec").write_text('<package><metadata><id>Example</id><version>1.0</version><license type="expression">MIT</license><repository url="https://example.org/source" commit="abc" /></metadata></package>')
            (package / "LICENSE.txt").write_text("License text")
            (package / "THIRD-PARTY-NOTICES.txt").write_text("Native notice")
            records = notices.nuget_packages(root)
            manifest = notices.write_bundle(records, root / "out")
            self.assertEqual(len(manifest["packages"][0]["texts"]), 2)
            self.assertEqual(manifest["packages"][0]["repository"]["commit"], "abc")

if __name__ == "__main__": unittest.main()
