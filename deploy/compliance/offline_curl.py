#!/usr/bin/env python3
"""Serve a pinned upstream recipe URL from a verified local source set; no network."""
import argparse
import hashlib
from pathlib import Path
import shutil
import sys
import json


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--manifest", type=Path, required=True)
    parser.add_argument("--root", type=Path, required=True)
    args, original = parser.parse_known_args()
    urls = [a for a in original if a.startswith("https://")]
    if len(urls) != 1:
        raise ValueError("Offline recipe expects one known source URL")
    profile = json.loads(args.manifest.read_text())
    matches = [e for e in profile["files"] if urls[0] in [e.get("url"), *e.get("request_urls", [])]]
    if len(matches) != 1:
        raise ValueError(f"Unpinned recipe source URL: {urls[0]}")
    entry = matches[0]
    path = (args.root / entry["file"]).resolve()
    if not path.is_relative_to(args.root.resolve()):
        raise ValueError("Unsafe offline source path")
    with path.open("rb") as source:
        if hashlib.file_digest(source, "sha256").hexdigest() != entry["sha256"]:
            raise ValueError(f"Changed offline source: {entry['file']}")
        source.seek(0)
        if "-O" in original:
            with Path(urls[0].rsplit("/", 1)[-1]).open("wb") as target:
                shutil.copyfileobj(source, target)
        else:
            shutil.copyfileobj(source, sys.stdout.buffer)


if __name__ == "__main__":
    main()
