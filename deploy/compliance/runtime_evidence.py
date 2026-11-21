#!/usr/bin/env python3
"""Capture installed OS/native/license evidence inside a built image; never a clearance verdict."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import tarfile


def sha256(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def collect(output, root=Path("/"), image_family=None):
    root = root.resolve()
    def image_path(path):
        return "/" + str(path.relative_to(root))

    output.mkdir(parents=True, exist_ok=True)
    packages = subprocess.check_output([
        "dpkg-query", "--admindir=" + str(root / "var/lib/dpkg"), "-W",
        "-f=${binary:Package}\t${Version}\t${Architecture}\t${source:Package}\t${source:Version}\n"
    ], text=True)
    (output / "os-packages.tsv").write_text("package\tversion\tarchitecture\tsource_package\tsource_version\n" + packages)
    paths = set((root / "usr/share/doc").glob("*/copyright"))
    paths.update((root / "usr/share/common-licenses").glob("*"))
    runtime_packages = []
    for directory in (root / "usr/share/dotnet", root / "usr/local", root / "opt"):
        if directory.exists():
            for path in directory.rglob("*"):
                if path.is_file() and any(word in path.name.lower() for word in ("license", "notice", "copying", "copyright")) and path.suffix.lower() in ("", ".txt", ".md"):
                    paths.add(path)
                if path.name == "package.json" and path.is_file():
                    package = json.loads(path.read_text())
                    # Bundled fixtures/configuration also use package.json; only
                    # manifests identifying a versioned package are components.
                    if all(isinstance(package.get(key), str) and package[key].strip() for key in ("name", "version")):
                        runtime_packages.append({"path": image_path(path), "name": package["name"],
                                                 "version": package["version"], "license": package.get("license"),
                                                 "sha256": sha256(path)})
    copied_files = []
    for path in sorted((root / "usr/local/bin").glob("*")):
        if path.is_file():
            copied_files.append({"path": image_path(path), "sha256": sha256(path),
                                 "size": path.stat().st_size})
    supplement_sources = []
    supplements = root / "app/runtime-supplements"
    index = supplements / "sources.json"
    if index.exists():
        if image_family not in ("api", "web", "tools"):
            raise ValueError("Runtime supplement collection requires an explicit image family")
        for entry in json.loads(index.read_text()):
            if image_family not in entry["images"]:
                continue
            if not entry["targets"]:
                raise ValueError("Runtime supplement has no pinned targets")
            originals = []
            for target in entry["targets"]:
                target_path = Path(target["path"])
                if not target_path.is_absolute() or ".." in target_path.parts:
                    raise ValueError("Unsafe runtime supplement target")
                original = root / str(target_path).lstrip("/")
                originals.append((target, original))
            # Applicability is declared by the Dockerfile, not inferred from
            # target presence: a missing file must not disable its notice rule.
            if not all(path.is_file() for _, path in originals):
                raise ValueError("Runtime supplement target missing: " + entry["file"])
            if originals:
                for target, path in originals:
                    if sha256(path) != target["sha256"]:
                        raise ValueError("Runtime supplement target changed: " + target["path"])
                filename = entry["file"]
                if not filename or Path(filename).name != filename or filename in (".", ".."):
                    raise ValueError("Unsafe runtime supplement filename")
                path = supplements / filename
                if path.is_symlink() or sha256(path) != entry["sha256"]:
                    raise ValueError("Runtime supplement hash mismatch: " + filename)
                paths.add(path)
                supplement_sources.append(entry)
        if supplement_sources:
            paths.add(index)
    files = []
    with tarfile.open(output / "runtime-license-texts.tar.gz", "w:gz", dereference=True) as archive:
        for path in sorted(paths):
            if path.is_file():
                archive.add(path, arcname=image_path(path).lstrip("/"), recursive=False)
                files.append({"path": image_path(path), "sha256": sha256(path)})
    natives = []
    for directory in (root / "app",):
        for path in sorted(directory.rglob("*")):
            if not path.is_file() or not (path.name.endswith(".node") or ".so" in path.name):
                continue
            if "notices" in path.parts:
                continue
            info = {"path": image_path(path), "sha256": sha256(path), "size": path.stat().st_size}
            ldd = subprocess.run(["ldd", str(path)], capture_output=True, text=True)
            info["ldd"] = (ldd.stdout + ldd.stderr).strip()
            info["ldd_exit_code"] = ldd.returncode
            natives.append(info)
    versions = []
    for path in (root / "app").rglob("versions.json"):
        if "sharp-libvips" in str(path):
            versions.append({"path": image_path(path), "sha256": sha256(path), "components": json.loads(path.read_text())})
    result = {"schema": 2, "image_family": image_family, "os_packages_sha256": sha256(output / "os-packages.tsv"),
              "runtime_packages": sorted(runtime_packages, key=lambda item: item["path"]),
              "copied_files": copied_files,
              "supplement_sources": supplement_sources,
              "license_texts": files, "native_binaries": natives, "native_components": versions,
              "scope": "OS metadata, runtime notices, copied runtime packages/files and application native payloads in this image",
              "limitations": ["Native embedded component license/source compliance requires separate review.",
                              "OS copyright files and source package names are not corresponding source archives.",
                              "ldd is host-architecture evidence only; other shipped architectures are not exercised."]}
    (output / "evidence.json").write_text(json.dumps(result, sort_keys=True, indent=2) + "\n")
    print(f"Runtime evidence: {len(files)} license files, {len(natives)} native binaries")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("output", type=Path)
    parser.add_argument("--image-family", required=True, choices=("api", "web", "tools"))
    args = parser.parse_args()
    collect(args.output, image_family=args.image_family)
