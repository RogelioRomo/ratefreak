#!/usr/bin/env python3
"""Check a source-rebuilt libvips replacement in a disposable image/container."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import re

CONTAINER_CHECK = r"""
from pathlib import Path
import ctypes,hashlib,json,subprocess,sys
path,marker=sys.argv[1:3]
lib=ctypes.CDLL(path);lib.vips_version_string.restype=ctypes.c_char_p
version=lib.vips_version_string().decode()
assert marker in version,version
sharp=[str(p.parent) for p in Path('/app/node_modules').rglob('package.json') if json.loads(p.read_text()).get('name')=='sharp']
assert len(sharp)==1,sharp
js="const expected="+json.dumps(path)+";const sharp=require("+json.dumps(sharp[0])+r''');const fs=require('node:fs');(async()=>{
const raster=await sharp({create:{width:16,height:16,channels:3,background:'#2060a0'}}).resize(8,8).png().toBuffer();
const svg=await sharp(Buffer.from('<svg xmlns="http://www.w3.org/2000/svg" width="12" height="12"><rect width="12" height="12" fill="red"/></svg>')).resize(6,6).webp().toBuffer();
const a=await sharp(raster).metadata(),b=await sharp(svg).metadata();if(a.format!=='png'||b.format!=='webp'||a.width!==8||a.height!==8||b.width!==6||b.height!==6)throw Error('image dimensions');
console.log(JSON.stringify({raster:{format:a.format,width:a.width,height:a.height},svg:{format:b.format,width:b.width,height:b.height},mapped:fs.readFileSync('/proc/self/maps','utf8').split('\n').some(line=>line.endsWith(' '+expected)),mapped_path:expected,node_library_sha256:require('node:crypto').createHash('sha256').update(fs.readFileSync(expected)).digest('hex')}));})();'''
r=json.loads(subprocess.check_output(['node','-e',js],text=True));assert r['mapped'];assert r['node_library_sha256']==hashlib.sha256(Path(path).read_bytes()).hexdigest()
r.update(version_string=version,library_sha256=hashlib.sha256(Path(path).read_bytes()).hexdigest());print(json.dumps(r))
"""


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--image", type=Path, required=True, help="recorded image evidence JSON")
    parser.add_argument("--library", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--marker", default="ratefreak-source-proof")
    args = parser.parse_args()
    image = json.loads(args.image.read_text())
    if not re.fullmatch(r"sha256:[0-9a-f]{64}", image.get("image_id", "")):
        raise ValueError("Expected immutable image ID")
    libraries = [x for x in image["runtime"]["native_binaries"] if Path(x["path"]).name.startswith("libvips-cpp.so.")]
    if len(libraries) != 1 or not libraries[0]["path"].startswith("/app/node_modules/"):
        raise ValueError("Expected one application libvips aggregate in the image")
    native = libraries[0]
    with args.library.open("rb") as source:
        fingerprint = hashlib.file_digest(source, "sha256").hexdigest()
    if fingerprint == native["sha256"]:
        raise ValueError("Replacement must differ from the original binary")
    raw = subprocess.check_output([
        "docker", "run", "--rm", "--network", "none", "--read-only", "--user", "10001:10001",
        "--mount", f"type=bind,source={args.library.resolve()},target={native['path']},readonly",
        "--entrypoint", "python3", image["image_id"], "-c", CONTAINER_CHECK, native["path"], args.marker
    ], text=True)
    result = json.loads(raw)
    if result["library_sha256"] != fingerprint:
        raise ValueError("Container did not load the supplied replacement")
    result.update(image_id=image["image_id"], original_library_sha256=native["sha256"], network="none", uid=10001,
                  scope="Source-rebuilt aggregate replacement; raster/SVG smoke checks on Linux amd64")
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(result, indent=2)+"\n")
    print(json.dumps(result, indent=2))


if __name__ == "__main__":
    main()
