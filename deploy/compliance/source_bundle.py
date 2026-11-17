#!/usr/bin/env python3
"""Build and verify an image-bound native source sidecar without network access."""
import argparse
import gzip
import hashlib
import io
import json
from pathlib import Path, PurePosixPath
import re
import tarfile
import urllib.request


def sha256(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def safe_name(name):
    path = PurePosixPath(name)
    if not name or path.is_absolute() or ".." in path.parts or "\\" in name or str(path) != name:
        raise ValueError(f"Unsafe source path: {name}")
    return name


def validate_coverage(profile):
    names = [safe_name(entry["file"]) for entry in profile.get("files", [])]
    required = [safe_name(name) for name in profile.get("required_files", [])]
    if not required or len(required) != len(set(required)) or len(names) != len(set(names)) or set(names) != set(required):
        raise ValueError("Source inventory differs from required source coverage")
    if profile["package"]["name"] == "@img/sharp-libvips-linux-x64":
        mandatory = {"packaging/sharp-libvips-packaging.tar.gz", "meson/gvdb.tar.gz", "README.md", "LICENSE.txt",
                     "instructions/rust_sources.py", "instructions/offline_curl.py", "instructions/rebuild_native.py",
                     "instructions/source_bundle.py", "evidence/librsvg-edited.Cargo.lock", "evidence/rust-coverage.json"}
        coverage = profile.get("coverage", {})
        if not mandatory <= set(names) or profile.get("librsvg_archive") not in names:
            raise ValueError("Missing required native build input")
        for prefix, key in [("native-sources/", "native_archive_patch_files"), ("rust/crates/", "rust_registry_crates")]:
            expected = coverage.get(key)
            if not isinstance(expected, int) or expected < 1 or sum(name.startswith(prefix) for name in names) != expected:
                raise ValueError(f"Incomplete required source coverage: {key}")


def validate_image(profile, image):
    if not re.fullmatch(r"sha256:[0-9a-f]{64}", image.get("image_id", "")):
        raise ValueError("Missing immutable image ID")
    package = profile["package"]
    if not any(p["name"] == package["name"] and p["version"] == package["version"] for p in image["packages"]):
        raise ValueError("Source package does not match image")
    runtime = image["runtime"]
    if not any(p["components"] == profile["components"] for p in runtime["native_components"]):
        raise ValueError("Source component versions do not match image")
    if not any(p["sha256"] == profile["native_sha256"] for p in runtime["native_binaries"]):
        raise ValueError("Native binary fingerprint does not match image")


def fetch(profile, source_dir, repository):
    root = source_dir.resolve()
    repo = repository.resolve()
    for entry in profile["files"]:
        name = safe_name(entry["file"])
        path = root / name
        if path.exists():
            if path.is_symlink() or not path.resolve().is_relative_to(root) or sha256(path) != entry["sha256"]:
                raise ValueError(f"Cached source changed: {name}")
            continue
        if not path.parent.resolve().is_relative_to(root):
            raise ValueError(f"Unsafe source output: {name}")
        path.parent.mkdir(parents=True, exist_ok=True)
        temporary = path.with_name(path.name + ".download")
        try:
            with temporary.open("xb") as target:
                if "repository" in entry:
                    original = repo / safe_name(entry["repository"])
                    if not original.resolve().is_relative_to(repo) or original.is_symlink():
                        raise ValueError("Unsafe repository source")
                    source = original.open("rb")
                else:
                    if not entry.get("url", "").startswith("https://"):
                        raise ValueError("Source download requires an HTTPS URL")
                    source = urllib.request.urlopen(entry["url"], timeout=60)
                with source:
                    while chunk := source.read(1024 * 1024):
                        target.write(chunk)
            if sha256(temporary) != entry["sha256"]:
                raise ValueError(f"Downloaded source hash mismatch: {name}")
            temporary.rename(path)
        except FileExistsError:
            raise
        except BaseException:
            if temporary.exists():
                temporary.unlink()
            raise


def create(profile, source_dir, image, output):
    validate_image(profile, image)
    validate_coverage(profile)
    if profile.get("schema") != 1 or not profile.get("files"):
        raise ValueError("Missing source manifest entries")
    root = source_dir.resolve()
    entries, names = [], set()
    for entry in profile["files"]:
        name = safe_name(entry["file"])
        if name == "SOURCE-MANIFEST.json" or name in names:
            raise ValueError(f"Duplicate or reserved source path: {name}")
        names.add(name)
        path = root / name
        if path.is_symlink() or not path.is_file() or not path.resolve().is_relative_to(root):
            raise ValueError(f"Missing or unsafe source file: {name}")
        if not re.fullmatch(r"[0-9a-f]{64}", entry["sha256"]) or sha256(path) != entry["sha256"]:
            raise ValueError(f"Source hash mismatch: {name}")
        entries.append((name, path, path.stat().st_size))
    manifest = {**profile, "image_id": image["image_id"], "image": image.get("image"),
                "files": sorted(profile["files"], key=lambda x: x["file"])}
    raw = (json.dumps(manifest, sort_keys=True, indent=2) + "\n").encode()
    # All source validation precedes output creation; never replace an existing artifact.
    output.parent.mkdir(parents=True, exist_ok=True)
    try:
        with output.open("xb") as stream:
            with gzip.GzipFile(filename="", fileobj=stream, mode="wb", mtime=0) as zipped:
                with tarfile.open(fileobj=zipped, mode="w|", format=tarfile.PAX_FORMAT) as archive:
                    info = tarfile.TarInfo("SOURCE-MANIFEST.json")
                    info.size, info.mode, info.mtime = len(raw), 0o644, 0
                    archive.addfile(info, io.BytesIO(raw))
                    for name, path, size in sorted(entries):
                        info = tarfile.TarInfo(name)
                        info.size, info.mode, info.mtime = size, 0o644, 0
                        with path.open("rb") as source:
                            archive.addfile(info, source)
        # Read the written payload back; this catches concurrent input changes too.
        verify(output)
    except FileExistsError:
        raise
    except BaseException:
        if output.exists():
            output.unlink()
        raise
    return {"file": output.name, "sha256": sha256(output), "bytes": output.stat().st_size,
            "image_id": image["image_id"], "scope": profile["scope"]}


def verify(path):
    with tarfile.open(path, "r:gz") as archive:
        members = archive.getmembers()
        names = [safe_name(m.name) for m in members]
        if len(names) != len(set(names)) or any(not m.isfile() for m in members):
            raise ValueError("Source bundle contains duplicate or non-file entries")
        if "SOURCE-MANIFEST.json" not in names:
            raise ValueError("Source bundle lacks manifest")
        manifest = json.load(archive.extractfile("SOURCE-MANIFEST.json"))
        validate_coverage(manifest)
        expected = {safe_name(e["file"]): e for e in manifest["files"]}
        if len(expected) != len(manifest["files"]) or set(names) != {*expected, "SOURCE-MANIFEST.json"}:
            raise ValueError("Source bundle inventory mismatch")
        for name, entry in expected.items():
            with archive.extractfile(name) as source:
                actual = hashlib.file_digest(source, "sha256").hexdigest()
            if actual != entry["sha256"]:
                raise ValueError(f"Source bundle hash mismatch: {name}")
        return manifest


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest="command", required=True)
    build = sub.add_parser("create")
    build.add_argument("--manifest", type=Path, required=True)
    build.add_argument("--source-dir", type=Path, required=True)
    build.add_argument("--image", type=Path, required=True)
    build.add_argument("--output", type=Path, required=True)
    check = sub.add_parser("verify")
    check.add_argument("archive", type=Path)
    download = sub.add_parser("fetch")
    download.add_argument("--manifest", type=Path, required=True)
    download.add_argument("--source-dir", type=Path, required=True)
    download.add_argument("--repository", type=Path, default=Path.cwd())
    args = parser.parse_args()
    if args.command == "verify":
        manifest = verify(args.archive)
        print(f"Verified {len(manifest['files'])} source inputs for {manifest['image_id']}")
    elif args.command == "fetch":
        fetch(json.loads(args.manifest.read_text()), args.source_dir, args.repository)
        print("Source inputs verified and available")
    else:
        result = create(json.loads(args.manifest.read_text()), args.source_dir,
                        json.loads(args.image.read_text()), args.output)
        print(json.dumps(result, indent=2))


if __name__ == "__main__":
    main()
