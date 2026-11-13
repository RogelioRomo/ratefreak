#!/usr/bin/env python3
"""Capture installed OS/native/license evidence inside a built image; never a clearance verdict."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import tarfile


def sha256(path):
    return hashlib.file_digest(path.open("rb"), "sha256").hexdigest()


def collect(output):
    output.mkdir(parents=True, exist_ok=True)
    packages = subprocess.check_output([
        "dpkg-query", "-W", "-f=${binary:Package}\t${Version}\t${Architecture}\t${source:Package}\t${source:Version}\n"
    ], text=True)
    (output / "os-packages.tsv").write_text("package\tversion\tarchitecture\tsource_package\tsource_version\n" + packages)
    paths = set(Path("/usr/share/doc").glob("*/copyright"))
    paths.update(Path("/usr/share/common-licenses").glob("*"))
    for root in (Path("/usr/share/dotnet"), Path("/usr/local")):
        if root.exists():
            for path in root.rglob("*"):
                if path.is_file() and any(word in path.name.lower() for word in ("license", "notice", "copying", "copyright")) and path.suffix.lower() in ("", ".txt", ".md"):
                    paths.add(path)
    files = []
    with tarfile.open(output / "runtime-license-texts.tar.gz", "w:gz", dereference=True) as archive:
        for path in sorted(paths):
            if path.is_file():
                archive.add(path, arcname=str(path).lstrip("/"), recursive=False)
                files.append({"path": str(path), "sha256": sha256(path)})
    natives = []
    for root in (Path("/app"),):
        for path in sorted(root.rglob("*")):
            if not path.is_file() or not (path.name.endswith(".node") or ".so" in path.name):
                continue
            if "notices" in path.parts:
                continue
            info = {"path": str(path), "sha256": sha256(path), "size": path.stat().st_size}
            ldd = subprocess.run(["ldd", str(path)], capture_output=True, text=True)
            info["ldd"] = (ldd.stdout + ldd.stderr).strip()
            info["ldd_exit_code"] = ldd.returncode
            natives.append(info)
    versions = []
    for path in Path("/app").rglob("versions.json"):
        if "sharp-libvips" in str(path):
            versions.append({"path": str(path), "sha256": sha256(path), "components": json.loads(path.read_text())})
    result = {"schema": 1, "os_packages_sha256": sha256(output / "os-packages.tsv"),
              "license_texts": files, "native_binaries": natives, "native_components": versions,
              "scope": "OS metadata, existing runtime notices and application native payloads in this image",
              "limitations": ["Native embedded component license/source compliance requires separate review.",
                              "OS copyright files and source package names are not corresponding source archives.",
                              "ldd is host-architecture evidence only; other shipped architectures are not exercised."]}
    (output / "evidence.json").write_text(json.dumps(result, sort_keys=True, indent=2) + "\n")
    print(f"Runtime evidence: {len(files)} license files, {len(natives)} native binaries")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("output", type=Path)
    collect(parser.parse_args().output)
