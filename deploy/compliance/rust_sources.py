#!/usr/bin/env python3
"""Prepare a Cargo directory source from checksum-verified locked .crate archives."""
import argparse
import hashlib
import json
from pathlib import Path, PurePosixPath
import re
import shutil
import tarfile
import tomllib


def sha256(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def prepare(lockfile, archives, vendor):
    packages = tomllib.loads(lockfile.read_text())["package"]
    inputs = []
    for package in packages:
        if "source" not in package:
            continue  # Workspace/path package remains in the original librsvg archive.
        if package["source"] != "registry+https://github.com/rust-lang/crates.io-index":
            raise ValueError(f"Unreviewed Cargo source: {package['source']}")
        name, version = package["name"], package["version"]
        if not re.fullmatch(r"[A-Za-z0-9_-]+", name) or not re.fullmatch(r"[A-Za-z0-9.+_-]+", version):
            raise ValueError("Unsafe crate name/version")
        key = f"{name}-{version}"
        archive = archives / f"{key}.crate"
        if not archive.is_file() or archive.is_symlink() or sha256(archive) != package["checksum"]:
            raise ValueError(f"Missing or changed locked crate: {key}")
        with tarfile.open(archive) as tar:
            seen = set()
            for member in tar.getmembers():
                path = PurePosixPath(member.name)
                if path.is_absolute() or ".." in path.parts or not path.parts or path.parts[0] != key or "\\" in member.name:
                    raise ValueError(f"Unsafe crate member: {member.name}")
                if not (member.isfile() or member.isdir()):
                    raise ValueError(f"Unsupported crate link/member: {member.name}")
                if member.name in seen or (member.isfile() and len(path.parts) == 1):
                    raise ValueError(f"Invalid crate root or duplicate member: {member.name}")
                seen.add(member.name)
        inputs.append((key, archive, package["checksum"]))
    # Validate the whole set before extraction; do not mix new sources into an old vendor tree.
    if vendor.exists():
        raise ValueError("Vendor output already exists; choose a fresh directory")
    vendor.mkdir(parents=True)
    try:
        for key, archive, checksum in inputs:
            with tarfile.open(archive) as tar:
                tar.extractall(vendor, filter="data")
            target = vendor / key
            files = {str(p.relative_to(target)): sha256(p) for p in sorted(target.rglob("*")) if p.is_file() and p.name != ".cargo-checksum.json"}
            (target / ".cargo-checksum.json").write_text(json.dumps({"files": files, "package": checksum}, sort_keys=True) + "\n")
    except BaseException:
        shutil.rmtree(vendor)
        raise
    return {"crate_count": len(inputs), "lock_sha256": sha256(lockfile)}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--lockfile", type=Path, required=True)
    parser.add_argument("--archives", type=Path, required=True)
    parser.add_argument("--vendor", type=Path, required=True)
    parser.add_argument("--config", type=Path, required=True, help="write Cargo source replacement configuration")
    args = parser.parse_args()
    result = prepare(args.lockfile, args.archives, args.vendor)
    args.config.parent.mkdir(parents=True, exist_ok=True)
    args.config.write_text('[source.crates-io]\nreplace-with = "vendored-sources"\n[source.vendored-sources]\ndirectory = ' + json.dumps(str(args.vendor.resolve())) + '\n')
    print(json.dumps(result, indent=2))


if __name__ == "__main__":
    main()
