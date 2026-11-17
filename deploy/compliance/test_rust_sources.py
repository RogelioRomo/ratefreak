import hashlib
import importlib.util
import io
from pathlib import Path
import tarfile
import tempfile
import unittest
from unittest.mock import patch

spec = importlib.util.spec_from_file_location("rust_sources", Path(__file__).with_name("rust_sources.py"))
rust = importlib.util.module_from_spec(spec)
spec.loader.exec_module(rust)

class RustSourcesTests(unittest.TestCase):
    def fixture(self, root, member="example-1.0.0/src/lib.rs"):
        archive = root / "example-1.0.0.crate"
        with tarfile.open(archive, "w:gz") as tar:
            info = tarfile.TarInfo(member)
            info.size = 12
            tar.addfile(info, io.BytesIO(b"pub fn f(){}"))
        checksum = hashlib.sha256(archive.read_bytes()).hexdigest()
        lock = root / "Cargo.lock"
        lock.write_text('version = 4\n[[package]]\nname = "example"\nversion = "1.0.0"\nsource = "registry+https://github.com/rust-lang/crates.io-index"\nchecksum = "'+checksum+'"\n')
        return lock, archive

    def test_prepares_cargo_directory_source_with_registry_and_file_checksums(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            lock, _ = self.fixture(root)
            result = rust.prepare(lock, root, root / "vendor")
            self.assertEqual(result["crate_count"], 1)
            self.assertTrue((root / "vendor/example-1.0.0/.cargo-checksum.json").exists())
            self.assertEqual((root / "vendor/example-1.0.0/src/lib.rs").read_bytes(), b"pub fn f(){}")

    def test_failed_extraction_or_checksum_write_cleans_new_vendor_output(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            lock, _ = self.fixture(root)
            with patch.object(Path, "write_text", side_effect=OSError("disk full")):
                with self.assertRaises(OSError): rust.prepare(lock, root, root / "vendor")
            self.assertFalse((root / "vendor").exists())
            self.assertEqual(rust.prepare(lock, root, root / "vendor")["crate_count"], 1)

    def test_invalid_regular_file_root_is_rejected_without_partial_output(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            lock, _ = self.fixture(root, "example-1.0.0")
            with self.assertRaises(ValueError): rust.prepare(lock, root, root / "vendor")
            self.assertFalse((root / "vendor").exists())

    def test_refuses_changed_archives_and_traversal(self):
        for failure in ["changed", "traversal"]:
            with self.subTest(failure=failure), tempfile.TemporaryDirectory() as tmp:
                root = Path(tmp)
                lock, archive = self.fixture(root, "../outside.rs" if failure == "traversal" else "example-1.0.0/src/lib.rs")
                if failure == "changed": archive.write_bytes(b"changed")
                with self.assertRaises(ValueError): rust.prepare(lock, root, root / "vendor")
                self.assertFalse((root / "outside.rs").exists())

if __name__ == "__main__": unittest.main()
