# Native source package and license provenance follow-up

Verified: 2026-11-17. This completes the selected application-native source
packaging work for the Linux amd64 web candidate. It does not close the separate
OS-source, provider agreement or whole-release acceptance gates.

Follow-up, 2026-11-21: the [OS source audit](os-source-delivery-verification.md)
collected complete source-package inputs for all installed OS packages in the
retained development candidate and prepared a native sidecar bound to its exact
web image. Copied runtimes/tools, native component decisions and final-candidate
review remain open; neither audit closes whole-image redistribution clearance.

The subsequent [runtime component review](runtime-redistribution-verification.md)
records the copied-tool/framework/Skia decisions and supplemental notice delivery.
The original libvips rebuild/replacement scope below is unchanged; final
image/source reconciliation and publication remain outstanding.

## Result

- Retained `react-remove-scroll-bar` 2.3.8 and its authentic upstream MIT notice.
  Exact npm artifacts and the maintainer's license-only source ancestry establish
  the [documented code-equivalence chain](scroll-bar-license-provenance.md).
  No dependency/runtime change or invented copyright year was needed. The notice
  is a later upstream grant; the original tarball still lacks a license file and
  its published gitHead remains unavailable. Those historical facts are preserved.
- Prepared an image-bound, checksum-verified source sidecar containing **406 input
  files**: 33 native source archive/patch files, 357 original locked crate archives,
  the full packaging source tree, GVDB fallback source, build/verification helpers,
  lockfiles and notice/provenance evidence.
- Rebuilt the aggregate with a harmless source marker and **networking disabled**.
  Loaded that rebuilt library into the normal non-root, read-only web image and
  verified the exact mapped library path/hash, version marker, PNG resize and SVG
  rendering to WebP.
- Added complete license-text notices for all **162 registry packages observed
  compiling** in this Linux rebuild. Existing package notice checks report no
  missing texts. This is a conservative compiled-dependency notice set, not a
  claim that every compiled crate contributes code to the final binary.

## Source artifact and delivery

The prepared local artifact is:

`.local/release-compliance/artifacts/ratefreak-libvips-8.18.7-linux-amd64-sources.tar.gz`

The committed [artifact metadata](release-evidence/2026-11-17-native-sources/source-artifact.json)
records its size, SHA-256 and matching image ID. A second independent archive
creation produced the same SHA-256. This proves reproducible **archive bytes**;
bit-for-bit reproduction of the upstream native binary is not claimed.

The archive is a source sidecar to accompany the binary release. Publish it and
its trusted checksum/image mapping alongside the matching image artifacts when
publication is authorized and the remaining release gates are satisfied. Local
files or third-party links alone are not a completed public source-delivery event.
No repository, image or archive was published by this work.

## Reproduce collection and verification

Use Python 3.11+ for these standalone source tools and a matching checkout.
The `fetch` step needs network access for uncached public archives; existing files
are checksum-checked and a changed cache entry is rejected rather than overwritten.
Choose a fresh output filename; creation refuses to replace an existing artifact.

```bash
python3 deploy/compliance/source_bundle.py fetch --manifest deploy/compliance/native/source-bundle.lock.json --source-dir .local/release-compliance/source-set
python3 deploy/compliance/source_bundle.py create --manifest deploy/compliance/native/source-bundle.lock.json --source-dir .local/release-compliance/source-set --image docs/release-evidence/2026-11-17-native-sources/web-image.json --output /tmp/ratefreak-native-sources-new.tar.gz
python3 deploy/compliance/source_bundle.py verify /tmp/ratefreak-native-sources-new.tar.gz
```

The builder checks the separate required-file inventory, production build inputs,
declared native/crate coverage, source hashes, image package/component versions and
native fingerprint. Archive verification repeats inventory/coverage and payload
hash checks. It rejects missing inputs, altered sources and incompatible image
evidence. This does not authenticate an untrusted manifest or replace license review.

The [included instructions](../deploy/compliance/NATIVE-SOURCE-README.md) explain
building the toolchain from the pinned packaging tree, unpacking sources and
running the offline build. Toolchain preparation accesses public package servers;
the actual native compilation runs with `--network none`, four CPUs and 8 GiB.
The [tool versions](release-evidence/2026-11-17-native-sources/toolchain-versions.txt)
and [builder/image evidence](release-evidence/2026-11-17-native-sources/native-build.json)
identify what was tested. Upstream tool selectors are floating; these records do
not promise that future tool downloads reproduce the same binary.

## Rust and Meson coverage

The original librsvg lockfile has 357 registry packages. Reproducing the feature
edits removes four and adds no registry name/version/checksum. Offline Cargo
metadata resolution and the actual native build both passed. All 357 archives
remain available as a conservative source superset. The actual build's lockfile
is retained as well as the original/edited evidence.

The separate GVDB archive uses the same exact commit referenced by GLib's wrap
file. Meson runs with downloads disabled. All recipe downloads are served by a
hash-checking local reader; unknown URLs fail rather than reaching the network.

Some source-superset crates are for other targets or development and have qualified
notice provenance. Their original files/declarations are retained and the
[provenance note](librsvg-rust-notice-provenance.md) preserves those limits.
They are excluded from the observed Linux notice inventory. The two required
supplements in that inventory are mutants' exact-source MIT license and selectors'
canonical MPL text identified by its source headers. No license grant was invented.

## Rebuild and replacement proof

The only intentional library source change appends `-ratefreak-source-proof` to
`vips_version_string`. The original configured sources, patches and offline
feature handling are otherwise retained. Static archives for relinking and the
executed recipe are retained locally under
`.local/release-compliance/rebuild-proof/`.

The permanent check can be repeated with Docker access:

```bash
python3 deploy/compliance/verify_native.py --image docs/release-evidence/2026-11-17-native-sources/web-image.json --library .local/release-compliance/replacement/lib/libvips-cpp.so.8.18.7 --output /tmp/ratefreak-replacement-check.json
```

[Recorded results](release-evidence/2026-11-17-native-sources/replacement.json):
source marker present; replacement fingerprint differs from the stock library;
Node maps the exact replacement path and sees the same hash; raster PNG output
is 8×8; SVG-to-WebP output is 6×6. The container uses UID/GID 10001, read-only
storage and no network. No running deployment's library was overwritten.

## Verification and remaining scope

- 18 compliance tests and 30 operator tests passed. Independent review findings
  on required coverage, symlink containment and partial-output cleanup were
  reproduced, regression-tested, fixed and rechecked.
- The fresh web production image built and its packaged notice hash/source
  profile were checked. Frontend UI behavior and dependency versions did not change.
- The full offline native rebuild and modified-library replacement passed on
  Linux amd64. This does not add ARM support or replace a deployed end-to-end test.
- The corresponding-source delivery for base OS packages and any remaining
  API/tools or whole-image redistribution requirements remain separate work.
  This sidecar deliberately does not claim those sources are included.
- Provider agreement access/scope, operator-specific permissions, final security
  review and eventual artifact publication remain open under [STATUS](STATUS.md).
