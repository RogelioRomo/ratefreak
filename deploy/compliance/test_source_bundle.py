import hashlib
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest

spec = importlib.util.spec_from_file_location("source_bundle", Path(__file__).with_name("source_bundle.py"))
bundle = importlib.util.module_from_spec(spec)
spec.loader.exec_module(bundle)

class SourceBundleTests(unittest.TestCase):
    def fixture(self, root):
        sources = root / "sources"
        sources.mkdir()
        (sources / "source.tar.gz").write_bytes(b"original source archive")
        (sources / "build.sh").write_bytes(b"#!/bin/sh\necho build\n")
        profile = {"schema": 1, "scope": "libvips source inputs; excludes OS source packages",
            "package": {"name": "example-native", "version": "1.0"},
            "components": {"vips": "8.18.7"}, "native_sha256": "a" * 64,
            "files": [{"file": "source.tar.gz", "sha256": hashlib.sha256(b"original source archive").hexdigest(), "url": "https://example.org/source.tar.gz"},
                      {"file": "build.sh", "sha256": hashlib.sha256(b"#!/bin/sh\necho build\n").hexdigest(), "url": "https://example.org/build.sh"}]}
        image = {"image_id": "sha256:" + "b" * 64, "packages": [{"name": "example-native", "version": "1.0"}],
                 "runtime": {"native_components": [{"components": {"vips": "8.18.7"}}],
                             "native_binaries": [{"path": "/app/libvips-cpp.so.8.18.7", "sha256": "a" * 64}]}}
        profile["required_files"] = ["source.tar.gz", "build.sh"]
        return sources, profile, image

    def test_bundle_is_reproducible_and_binds_sources_to_image(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            sources, profile, image = self.fixture(root)
            first, second = root / "one.tar.gz", root / "two.tar.gz"
            bundle.create(profile, sources, image, first)
            (sources / "build.sh").touch()
            bundle.create(profile, sources, image, second)
            self.assertEqual(first.read_bytes(), second.read_bytes())
            result = bundle.verify(first)
            self.assertEqual(result["image_id"], "sha256:" + "b" * 64)
            self.assertEqual(len(result["files"]), 2)
            self.assertNotIn(tmp, json.dumps(result))

    def test_refuses_missing_tampered_or_traversing_inputs_before_writing(self):
        for condition in ["missing", "tampered", "traversal"]:
            with self.subTest(condition=condition), tempfile.TemporaryDirectory() as tmp:
                root = Path(tmp)
                sources, profile, image = self.fixture(root)
                if condition == "missing": (sources / "source.tar.gz").unlink()
                elif condition == "tampered": (sources / "source.tar.gz").write_bytes(b"changed")
                else: profile["files"][0]["file"] = "../source.tar.gz"
                output = root / "bundle.tar.gz"
                with self.assertRaises(ValueError): bundle.create(profile, sources, image, output)
                self.assertFalse(output.exists())

    def test_repository_material_is_fetched_by_hash_without_overwriting_changed_files(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            (root / "build.sh").write_bytes(b"build instructions")
            entry = {"file": "instructions/build.sh", "repository": "build.sh", "sha256": hashlib.sha256(b"build instructions").hexdigest()}
            profile = {"files": [entry]}
            bundle.fetch(profile, root / "cache", root)
            self.assertEqual((root / "cache/instructions/build.sh").read_bytes(), b"build instructions")
            (root / "cache/instructions/build.sh").write_bytes(b"changed")
            with self.assertRaisesRegex(ValueError, "changed"):
                bundle.fetch(profile, root / "cache", root)

    def test_refuses_inventory_with_required_source_removed(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            sources, profile, image = self.fixture(root)
            profile["files"] = [profile["files"][1]]
            with self.assertRaisesRegex(ValueError, "required"):
                bundle.create(profile, sources, image, root / "incomplete.tar.gz")

    def test_fetch_rejects_symlink_ancestor_before_creating_outside_directories(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            cache, outside = root / "cache", root / "outside"
            cache.mkdir(); outside.mkdir(); (cache / "link").symlink_to(outside)
            profile = {"files": [{"file": "link/newdir/source.tar", "sha256": "a" * 64, "url": "https://example.org/archive"}]}
            with self.assertRaises(ValueError): bundle.fetch(profile, cache, root)
            self.assertFalse((outside / "newdir").exists())

    def test_refuses_wrong_component_version_or_native_binary(self):
        for field in ["components", "native"]:
            with self.subTest(field=field), tempfile.TemporaryDirectory() as tmp:
                root = Path(tmp)
                sources, profile, image = self.fixture(root)
                if field == "components": image["runtime"]["native_components"][0]["components"]["vips"] = "8.14.0"
                else: image["runtime"]["native_binaries"][0]["sha256"] = "c" * 64
                with self.assertRaisesRegex(ValueError, "image"):
                    bundle.create(profile, sources, image, root / "bundle.tar.gz")

if __name__ == "__main__": unittest.main()
