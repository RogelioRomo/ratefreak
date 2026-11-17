#!/usr/bin/env python3
"""Run pinned Linux-x64 libvips source recipe offline inside its prepared builder."""
import argparse
import json
from pathlib import Path
import shutil
import subprocess
import tarfile


def extract(archive, output):
    output.mkdir(parents=True, exist_ok=True)
    with tarfile.open(archive) as tar:
        tar.extractall(output, filter="data")
    return next(p for p in output.iterdir() if p.is_dir())


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--sources", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--proof-marker", action="store_true")
    args = parser.parse_args()
    root = args.sources.resolve()
    profile = json.loads((root / "PROFILE.json").read_text())
    args.output.mkdir(parents=True, exist_ok=True)
    package = extract(root / "packaging/sharp-libvips-packaging.tar.gz", Path("/work/packaging"))
    Path("/packaging").symlink_to(package)
    rsvg = extract(root / profile["librsvg_archive"], Path("/work/rust-original"))
    subprocess.run(["python3.12", str(root / "instructions/rust_sources.py"),
        "--lockfile", str(rsvg / "Cargo.lock"), "--archives", str(root / "rust/crates"),
        "--vendor", "/work/vendor", "--config", "/usr/local/cargo/config.toml"], check=True)
    recipe = package / "build/posix.sh"
    text = recipe.read_text()
    changes = {
        'CURL="curl --silent --location --retry 3 --retry-max-time 30"':
            f'CURL="python3.12 {root}/instructions/offline_curl.py --manifest {root}/PROFILE.json --root {root}"',
        'cargo update --workspace': 'cargo update --workspace --offline',
        'cd ${DEPS}/glib\n': 'cd ${DEPS}/glib\nmkdir -p subprojects/gvdb\ntar -xzf '+str(root / 'meson/gvdb.tar.gz')+' --strip-components=1 -C subprojects/gvdb\n',
        '# Pack only the relevant libraries':
            'cp -a ${TARGET}/lib '+str(args.output / 'relink-lib')+'\n# Pack only the relevant libraries',
    }
    if args.proof_marker:
        changes['# Link libvips.so statically into libvips-cpp.so'] = (
            'sed -i \'s/return VIPS_VERSION_STRING;/return VIPS_VERSION_STRING "-ratefreak-source-proof";/\' libvips/iofuncs/init.c\n'
            '# Link libvips.so statically into libvips-cpp.so')
    for before, after in changes.items():
        if text.count(before) != 1:
            raise ValueError(f"Upstream recipe change needs review: {before}")
        text = text.replace(before, after)
    text = text.replace('#!/usr/bin/env bash', '#!/usr/bin/env bash\n# Modified for verified offline sources/relink material and optional proof marker, 2026-11-17.')
    recipe.write_text(text)
    shutil.copyfile(recipe, args.output / "executed-posix.sh")
    # Bound parallel compilation independently of host CPU count.
    tools = Path("/work/bin")
    tools.mkdir()
    (tools / "nproc").write_text('#!/bin/sh\necho 4\n')
    (tools / "nproc").chmod(0o755)
    ninja = shutil.which("ninja")
    if not ninja:
        raise ValueError("Prepared toolchain lacks ninja")
    (tools / "ninja").write_text('#!/bin/sh\nexec '+ninja+' -j4 "$@"\n')
    (tools / "ninja").chmod(0o755)
    import os
    env = {**os.environ, "PATH": str(tools)+":"+os.environ["PATH"],
           "MESON": os.environ.get("MESON", "")+" --wrap-mode=nodownload", "CARGO_NET_OFFLINE": "true"}
    subprocess.run(["bash", str(recipe)], cwd=package, env=env, check=True)
    shutil.copyfile(package / "sharp-libvips-linux-x64.tar.gz", args.output / "sharp-libvips-linux-x64.tar.gz")
    shutil.copyfile(Path("/deps/rsvg/Cargo.lock"), args.output / "Cargo.lock.built")
    shutil.copyfile(Path("/target/versions.json"), args.output / "versions.json")


if __name__ == "__main__":
    main()
