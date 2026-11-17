# Native source packaging research

Research date: 2026-11-17. Scope: the existing amd64 review image and the
sharp-libvips v1.3.4 source recipe. This is a practical packaging plan, not a
release clearance decision. No replacement build was performed during research.

## Observed target

Offline inspection of `ratefreak-web:credits-review` identified image
`sha256:06b94da5647bf9c55e2d2e702aa6c9123329632f5e5e0b1117e89ee5876ef55b`,
Node `v24.22.2`, sharp `0.35.5`, libvips `8.18.7`, and librsvg `2.63.2`.
Requiring Sharp mapped this exact library into the process:

```text
/app/node_modules/.pnpm/@img+sharp-libvips-linux-x64@1.3.4/node_modules/@img/sharp-libvips-linux-x64/lib/libvips-cpp.so.8.18.7
```

The addon is under
`/app/node_modules/.pnpm/@img+sharp-linux-x64@0.35.5/node_modules/@img/sharp-linux-x64/lib/sharp-linux-x64-0.35.5.node`.
A fresh offline process generated a 4×4 red PNG successfully (95 bytes).
These are baseline observations, not evidence of modified-library operation.

## Required inputs beyond the 33 acquired files

1. **Full packaging source and platform configuration.** Preserve the complete
   v1.3.4 tree, resolved commit, archive hash, license and modifications. The parent
   investigation resolved the tag to `ebb95f8add54eee8bed840e3fb587e4cbec857d7`.
   The [build launcher](https://raw.githubusercontent.com/lovell/sharp-libvips/v1.3.4/build.sh)
   uses platform Dockerfiles, not just `build/posix.sh`.
   For this image retain `platforms/linux-x64/` including
   [Dockerfile](https://raw.githubusercontent.com/lovell/sharp-libvips/v1.3.4/platforms/linux-x64/Dockerfile),
   [Toolchain.cmake](https://raw.githubusercontent.com/lovell/sharp-libvips/v1.3.4/platforms/linux-x64/Toolchain.cmake),
   [meson.ini](https://raw.githubusercontent.com/lovell/sharp-libvips/v1.3.4/platforms/linux-x64/meson.ini)
   and the [pkgconf wrapper](https://raw.githubusercontent.com/lovell/sharp-libvips/v1.3.4/platforms/linux-x64/x86_64-linux-gnu-pkgconf).
   Capture the actual build environment and compiler/tool versions. The upstream
   Dockerfile uses Rocky Linux 8, GCC toolset 15, floating nightly Rust,
   `cargo-c`, Meson and Ninja; it supplies the `PLATFORM`, `FLAGS`, `RUSTFLAGS`,
   `MESON` and `PKG_CONFIG` settings. Pin a working environment for our instructions;
   do not claim those pins reconstruct upstream's original environment.

2. **Rust dependency sources and the effective lockfile.** The acquired librsvg
   archive contains a Cargo.lock with 366 package records and no git-source
   records. The [preserved recipe](../deploy/compliance/native/sharp-libvips-build.sh)
   removes selected image/cairo features, runs `cargo update --workspace`, then
   applies its librsvg patch. Preserve original and edited manifests/lockfiles,
   their diff, dependency checksums, vendor directory and Cargo configuration.
   Cargo documents that [workspace update](https://doc.rust-lang.org/cargo/commands/cargo-update.html)
   leaves non-workspace packages locked unless absent from the lockfile. Therefore
   compare `(name, version, source, checksum)` sets: if the edited set is a subset
   of the original, the original source set is a conservative superset, without
   needing byte-identical regenerated lockfiles. Verify this condition rather than
   assume it. [cargo vendor](https://doc.rust-lang.org/cargo/commands/cargo-vendor.html)
   supports additional manifests with `--sync`; vendor the original and edited
   workspaces and retain its printed source-replacement configuration. Verify the
   build with locked/offline dependencies. Audit any other build-time downloads
   and retain separately required source inputs rather than merely their URLs.

3. **Local-source build adaptation.** The recipe streams HTTP downloads, applies
   patches and performs inline `sed` edits; preserve every edit and replace network
   fetches with hash-checked local inputs. Its final notice download points at
   upstream `main`; use the reviewed pinned notice text instead. Keep the original
   recipe alongside the adaptation and document changed paths. Offline execution
   is a useful completeness check, not itself a legal requirement.

4. **Separate OS source set.** Use each final image's `os-packages.tsv`, deduplicate
   by source package/version, and acquire complete distro source sets (`.dsc`,
   upstream tarballs and Debian patch/archive files). Use the recorded source
   version, not the binary `+bN` revision. For example this web image records
   bash `5.2.15-2`, glibc `2.36-9+deb12u14`, coreutils `9.1-1`,
   util-linux `2.38.1-5+deb12u3`, and readline `8.2-1.3` as source versions.
   Conservatively acquiring all source-package rows avoids unreliable license-name
   filtering. Use matching `deb-src` repositories or historical snapshots when
   current mirrors no longer carry a version; fail visibly when unavailable.
   [APT source retrieval](https://manpages.debian.org/bookworm/apt/apt-get.8.en.html)
   supports explicit versions and download-only operation. Also inventory payloads
   outside dpkg, including Node and its bundled dependencies; dpkg cannot establish
   their source coverage. Toolchain acquisition records aid rebuilding, but do not
   assume all compiler/tool source is automatically required corresponding source.

## Smallest meaningful replacement verification

1. Build the aggregate library from the retained inputs using the documented
   platform/configuration and a compatible toolchain. Preserve build logs and
   make a harmless, documented source change producing an observable version or
   exported marker. Retain generated headers, archives and build configuration
   needed to recombine internally embedded covered libraries.
2. Start a fresh container from the image ID above with that rebuilt library
   mounted read-only over the exact library path. Use the normal non-root user;
   no application credentials or network are needed for direct Sharp tests.
3. Require Sharp using its installed absolute module directory, confirm the
   replacement hash, inspect `/proc/self/maps`, and verify the changed marker.
   Run raster resize/re-encode plus SVG input (exercises librsvg), checking output
   metadata and success. A fresh process matters because the recipe links the
   library with `NODELETE`. Same-file copying or merely loading the stock library
   under another pathname is only a mount/loader check.
4. Add a representative Next image-optimization request if practical. Record the
   exact container command, outputs, image ID and library hashes; this establishes
   tested behavior on amd64 only. If the existing addon cannot load an otherwise
   compatible rebuilt library, investigate its SONAME/export/ABI dependencies;
   Sharp also documents a [source build against custom libvips](https://sharp.pixelplumbing.com/install/).
   That fallback requires preserving Sharp addon source and its build inputs.

## Material limits and release decisions

The aggregate `.so` statically embeds libvips and other dependencies according to
the recipe. Replacing it demonstrates an external replacement path; it does not
alone prove that recipients can modify/relink each embedded covered component.
Review the applicable version-specific terms and retain the necessary component
sources, patches, build scripts and any application/object material that the
chosen compliance route requires. The locally preserved
[LGPLv3](../deploy/compliance/supplements/LGPL-3.0-LICENSE.txt) sections 4–5 distinguish
shared-library operation, relinking and combined libraries; the accompanying
[GPLv3](../deploy/compliance/supplements/GPL-3.0-LICENSE.txt) defines corresponding
source and delivery routes. GNU's live endpoints timed out during this research;
these are the repository's hash-recorded license texts.

Bit-for-bit reproduction is not the acceptance criterion here. Complete covered
source, workable modification/build/relink instructions, actual delivery and the
applicable license conditions are the relevant questions. A verified original-lock
superset can close the nested-crate acquisition gap; floating upstream tool
versions alone do not establish a legal blocker. A failed offline rebuild,
unavailable covered source, incompatible replacement, or missing distribution
mechanism remains concrete unfinished work. Publishing only third-party links or
local `.local/` paths does not create that delivery mechanism.
