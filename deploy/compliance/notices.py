#!/usr/bin/env python3
"""Collect installed package license texts; metadata is evidence, not legal clearance."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import shutil
import sys
import xml.etree.ElementTree as ET

TEXT_NAME = re.compile(r"license|licence|copying|notice|copyright", re.I)


def digest(data):
    return hashlib.sha256(data).hexdigest()


def text_files(root):
    # Do not follow dependency trees or treat compiled JS/license maps as notice texts.
    result = []
    for path in sorted(root.rglob("*")):
        relative = path.relative_to(root)
        if "node_modules" in relative.parts or not path.is_file():
            continue
        if TEXT_NAME.search(path.name) and path.suffix.lower() in ("", ".txt", ".md", ".html", ".rst"):
            data = path.read_bytes()
            if b"\x00" not in data:
                result.append((str(relative), data))
    return result


def resolve_dependency(root, name):
    for parent in (root, *root.parents):
        candidate = parent / "node_modules" / name
        if (candidate / "package.json").is_file():
            return candidate.resolve()
    return None


def web_packages(root):
    records, seen, queue = [], set(), []
    app = json.loads((root / "package.json").read_text())
    for name in app.get("dependencies", {}):
        target = resolve_dependency(root, name)
        if target is None:
            raise ValueError(f"Missing required dependency: {name}")
        queue.append(target)
    while queue:
        path = queue.pop()
        if path in seen:
            continue
        seen.add(path)
        metadata = (path / "package.json").read_bytes()
        data = json.loads(metadata)
        records.append({"name": data["name"], "version": data["version"],
                        "license": data.get("license", "UNKNOWN"),
                        "repository": data.get("repository"), "ecosystem": "npm",
                        "metadata_sha256": digest(metadata), "texts": text_files(path)})
        dependencies = data.get("dependencies", {})
        optional = data.get("optionalDependencies", {})
        for name in dependencies.keys() | optional.keys():
            target = resolve_dependency(path, name)
            if target:
                queue.append(target)
            elif name not in optional:
                raise ValueError(f"Missing required dependency: {data['name']} -> {name}")
    return records


def nuget_packages(root, assets=None, include_packages=()):
    records = []
    selected = None
    if assets:
        selected = set()
        for asset in assets:
            selected.update(name.lower() for name, item in json.loads(asset.read_text())["libraries"].items() if item["type"] == "package")
        selected.update(name.lower() for name in include_packages)
    # Includes all restored build/tool dependencies; intentionally a notice superset.
    for path in sorted(root.glob("*/*/*.nuspec")):
        raw = path.read_bytes()
        nodes = {e.tag.split("}")[-1]: e for e in ET.fromstring(raw).iter()}
        if "id" not in nodes:
            continue
        if selected is not None and f"{nodes['id'].text}/{nodes['version'].text}".lower() not in selected:
            continue
        repo = nodes.get("repository")
        records.append({"name": nodes["id"].text, "version": nodes["version"].text,
                        "ecosystem": "nuget", "license": nodes["license"].text if "license" in nodes else "UNKNOWN",
                        "repository": dict(repo.attrib) if repo is not None else None,
                        "copyright": nodes["copyright"].text if "copyright" in nodes else None,
                        "metadata_sha256": digest(raw), "texts": text_files(path.parent)})
    if not records:
        raise ValueError("No restored NuGet packages found")
    if selected is not None:
        found = {f"{record['name']}/{record['version']}".lower() for record in records}
        missing = selected - found
        if missing:
            raise ValueError("Missing restored NuGet packages: " + ", ".join(sorted(missing)))
    return records


def rust_packages(root, built=None):
    import tomllib
    selected = None if built is None else {f"{p['name']}@{p['version']}" for p in built}
    records = []
    for path in sorted(root.glob("*/Cargo.toml")):
        raw = path.read_bytes()
        package = tomllib.loads(raw.decode())["package"]
        key = f"{package['name']}@{package['version']}"
        if selected is not None and key not in selected:
            continue
        records.append({"name": package["name"], "version": package["version"],
                        "ecosystem": "cargo", "license": package.get("license", "UNKNOWN"),
                        "repository": package.get("repository"), "authors": package.get("authors", []),
                        "metadata_sha256": digest(raw), "texts": text_files(path.parent)})
    if selected is not None:
        found = {f"{p['name']}@{p['version']}" for p in records}
        if selected - found:
            raise ValueError("Missing built Rust sources: " + ", ".join(sorted(selected - found)))
    if not records:
        raise ValueError("No vendored Rust package sources found")
    return records


def write_bundle(records, output, supplements=None):
    output.mkdir(parents=True, exist_ok=True)
    manifest = {"schema": 1, "scope": "installed package graph; final artifacts and native source obligations require separate verification",
                "packages": [], "missing": [], "notice_texts_complete": False}
    chunks = ["THIRD-PARTY SOFTWARE NOTICES\n\nRateFreak is MIT licensed. Dependencies retain their own licenses.\nThis bundle is not a grant of provider data or artwork rights.\n"]
    for record in sorted(records, key=lambda r: (r["ecosystem"], r["name"], r["version"])):
        key = f"{record['name']}@{record['version']}"
        texts = list(record["texts"])
        primary = any(re.match(r"^(license|licence|copying)($|[._-])", Path(name).parts[0], re.I) for name, _ in texts)
        supplement_record = None
        if supplements:
            supplement_index = supplements / "sources.json"
            entries = json.loads(supplement_index.read_text()) if supplement_index.exists() else []
            for entry in entries:
                if key in entry["packages"]:
                    raw = (supplements / entry["file"]).read_bytes()
                    if digest(raw) != entry["sha256"]:
                        raise ValueError(f"Supplement hash mismatch: {entry['file']}")
                    texts.append((f"supplement/{entry['file']}", raw))
                    primary = primary or entry.get("primary_license", False)
                    supplement_record = (supplement_record or []) + [entry]
        public = {k: v for k, v in record.items() if k != "texts"}
        public["texts"] = [{"file": name, "sha256": digest(data)} for name, data in texts]
        if supplement_record:
            public["supplement_sources"] = supplement_record
        manifest["packages"].append(public)
        if not primary:
            manifest["missing"].append(key)
        chunks.append(f"\n{'=' * 72}\n{key} ({record['ecosystem']})\nDeclared license: {record['license']}\n")
        for name, raw in texts:
            chunks.append(f"\n--- {name} ---\n{raw.decode('utf-8-sig', errors='replace')}\n")
        if not texts:
            chunks.append("\nMISSING LICENSE TEXT: publication requires follow-up.\n")
    manifest["notice_texts_complete"] = not manifest["missing"]
    bundle = "\n".join(chunks).encode()
    manifest["bundle_sha256"] = digest(bundle)
    (output / "THIRD-PARTY-NOTICES.txt").write_bytes(bundle)
    (output / "manifest.json").write_text(json.dumps(manifest, sort_keys=True, indent=2) + "\n")
    return manifest


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("kind", choices=["web", "nuget", "rust"])
    parser.add_argument("root", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("--supplements", type=Path)
    parser.add_argument("--assets", type=Path, action="append", help="restrict NuGet collection to restored application assets")
    parser.add_argument("--include-package", action="append", default=[], help="require an additional restored NuGet package as name/version (e.g. the EF tool)")
    parser.add_argument("--built", type=Path, help="JSON name/version list of Rust packages observed in the native build")
    parser.add_argument("--check", action="store_true", help="fail if any package lacks a license/notice text")
    parser.add_argument("--project-license", type=Path)
    parser.add_argument("--evidence", type=Path, help="include reviewed native source/build provenance")
    args = parser.parse_args()
    if args.kind == "web":
        records = web_packages(args.root)
    elif args.kind == "rust":
        records = rust_packages(args.root, json.loads(args.built.read_text()) if args.built else None)
    else:
        records = nuget_packages(args.root, args.assets, args.include_package)
    manifest = write_bundle(records, args.output, args.supplements)
    if args.project_license:
        (args.output / "LICENSE.txt").write_bytes(args.project_license.read_bytes())
    if args.evidence:
        shutil.copytree(args.evidence, args.output / "native", dirs_exist_ok=True)
    print(f"{len(records)} packages; {len(manifest['missing'])} missing license texts")
    if manifest["missing"]:
        print("Missing: " + ", ".join(manifest["missing"]))
    return 1 if args.check and manifest["missing"] else 0


if __name__ == "__main__":
    sys.exit(main())
