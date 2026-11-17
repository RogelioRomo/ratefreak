# RateFreak Linux-x64 libvips source sidecar

This source set accompanies the web image/native fingerprint in
SOURCE-MANIFEST.json. It contains source archives/patches, the full upstream
packaging tree, original locked Rust crate sources and native build helpers.
It is separate from RateFreak account data and contains no provider credentials.

The included Rust notice bundle covers the 162 registry packages observed compiling
in the tested Linux build. The 357-crate source set also contains unbuilt target/dev
dependencies; their original declarations are retained. Known notice provenance
limits for those extra targets are recorded in the included provenance note.

This sidecar covers the application libvips aggregate. It does not contain the
base operating-system sources, .NET/SkiaSharp sources or all general-purpose
compiler/toolchain sources, and is not a blanket image-redistribution clearance.
Provider metadata/artwork is outside its scope.

## Verify and unpack

Verify the published archive SHA-256 against trusted release metadata before
opening it. `python3 source_bundle.py verify ARCHIVE.tar.gz` checks internal file
hashes and inventory; it does not authenticate an untrusted publisher. Extract
into a fresh directory. Python build helpers require Python 3.12+ in the upstream
builder (Python 3.11+ for the standalone tools).

Copy SOURCE-MANIFEST.json to PROFILE.json inside the extracted directory. The
upstream packaging tarball is pinned by commit and SHA-256. Its Linux-x64
Dockerfile supplies a compatible build environment; the upstream compiler/tool
selectors are floating, so retain the builder image ID and tool versions used.
Do not distribute that builder image without its own source/license review.

## Rebuild without network access to library sources

Build the toolchain image using `platforms/linux-x64/Dockerfile` from the unpacked
packaging archive. This preparation requires access to compiler/tool repositories.
Then run a fresh container with the extracted sidecar mounted read-only at
`/sources`, an empty output directory at `/output`, four CPUs and an appropriate
memory limit, using:

    python3.12 /sources/instructions/rebuild_native.py --sources /sources --output /output --proof-marker

Use `--network none` on the container. The helper restores the complete packaging
source, prepares checksum-verified Cargo directory sources, injects the pinned
GVDB fallback and replaces the recipe downloader with a hash-checking local
reader. Unknown URLs and Meson downloads fail. The upstream recipe's feature
edits are retained; Cargo workspace resolution is offline. `--proof-marker` adds
`-ratefreak-source-proof` to libvips's version string as an observable source
change. Omit that flag for an unmarked build. The executed recipe and component
archive/static relink material are retained in the output directory.

The original librsvg lockfile has 357 registry crates. Its feature-edited lockfile
has 353, all identical name/version/checksum entries from the original set.
All 357 archives are included as a conservative source superset. `rust_sources.py`
can reconstruct the vendor directory from the original source archive's lockfile
without a registry index. No upstream source archive is silently rewritten.

## Replace the aggregate in a disposable runtime

Unpack the resulting `sharp-libvips-linux-x64.tar.gz`. In a fresh container from
the recorded web image ID, mount the rebuilt `lib/libvips-cpp.so.8.18.7` read-only
over the original path recorded in SOURCE-MANIFEST.json/native evidence. Use the
image's normal UID/GID, no network and no deployment secrets. Use a fresh process
because the aggregate is linked NODELETE. Confirm the loaded path/hash and the
proof version via `vips_version_string`, then exercise Sharp raster resize,
re-encoding and SVG rendering. Do not overwrite a running installation's library.

The included `instructions/verify_native.py` performs those checks using a recorded
image evidence JSON, the rebuilt library path and an output JSON path. It requires
Docker access and uses a disposable read-only, non-root, network-disabled container.

Replacing the aggregate and rebuilding its covered components are distinct checks.
Keep all sources, patches, build instructions, lockfiles, notices and any required
relink material available together with the binary release. Publication, supported
architectures and OS corresponding-source delivery need separate acceptance.
