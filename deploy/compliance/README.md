# Release notice collection

Python 3 (standard library only) collects actual license/notice texts from restored
packages. It never grants rights or declares an image legally cleared.

From the repository root, after frozen frontend install and .NET restore:

```bash
pnpm --dir web notices
python3 deploy/compliance/notices.py nuget "$HOME/.nuget/packages" /tmp/ratefreak-api-notices --assets src/RateFreak.Api/obj/project.assets.json --supplements deploy/compliance/supplements --project-license LICENSE --evidence deploy/compliance/native --check
python3 -m unittest discover -s deploy/compliance -p 'test_*.py'
```

`pnpm dev` and `pnpm build` generate web notices automatically. The web production
dependency closure includes installed optional dependencies; unavailable optional
platform binaries are omitted. Required unresolved dependencies stop collection.
NuGet collection with `--assets` uses the restored API graph and explicitly requested additional
packages (`--include-package dotnet-ef/10.0.2` in the tools image); build-only dependencies and other-platform native assets are
included as a conservative notice superset. Test dependencies are excluded unless
their project assets are explicitly supplied. The tool package's bundled content
is covered by recursively collected texts; this does not audit its binary closure.

`manifest.json` records versions, declared licenses, metadata/text hashes and
supplement provenance. `--check` fails for a missing primary license text or a
supplement hash mismatch. **Text presence is not license applicability or complete
compliance.** In particular, the scroll-bar supplement records its version
provenance through exact artifact/source equivalence; see the
[follow-up evidence](../../docs/scroll-bar-license-provenance.md). Native and OS
source-delivery obligations retain their own scope and checks.
The generated concatenated text is `THIRD-PARTY-NOTICES.txt`; the project license
is `LICENSE.txt`. Generated web output is ignored by Git and must not be edited.

All three production Dockerfiles collect notices in their build stages, preserve
them in runtime images and capture runtime evidence after OS package installation:

| Image | Notice directory |
| --- | --- |
| API/tools | `/app/notices/` |
| Web | `/app/public/notices/` (served under `/notices/`) |

`runtime_evidence.py` records OS package/source-package versions, native binary
hashes and host-architecture `ldd` results, embedded libvips component versions,
and existing runtime/OS copyright files in `runtime-license-texts.tar.gz`.
This archive supplements package notices; preserve both. It does not download OS
corresponding sources or prove a binary can be rebuilt/relinked.

Runtime evidence schema 2 also records versioned package manifests under
`/usr/local`, `/opt` and `/usr/share/dotnet`, including bundled global package
dependencies, and hashes every copied file in `/usr/local/bin`. Notice scanning
includes `/opt`, so the Node image's Yarn license is retained. The release
inventory adds these packages and files to the `runtime` scope: package rows use
`npm:<name>` and their declared version; file rows use `file:<absolute-path>` and
their SHA-256. Duplicate package/version rows retain their manifest paths in one
component. These records identify review scope, not licensing clearance.

Source inventory format 2 requires schema-2 runtime evidence for every image.
Both finalization and bundle validation reject saved inventories predating this
coverage, and cross-check the archived runtime evidence against the saved runtime
component inventory. Prepare a fresh candidate from the corrected source; do not add a
schema marker to old evidence. Each newly inventoried runtime component still
needs matching source material or an individually reviewed source exception.

`runtime-supplements/sources.json` records reviewed additional notices for copied
tools, global package managers, frameworks and native aggregates. The production
Dockerfiles copy those files and pass `--image-family api`, `web` or `tools`.
Applicable rules require every pinned target to exist and match its SHA-256,
then verify the supplement hash before packaging it with provenance. Missing or
changed files require re-review; unrelated image families do not select the rule.
The [runtime delivery evidence](../../docs/runtime-redistribution-verification.md)
records decisions, source materials and exact proof-image scope. Full source
coverage, final builds, provider clearance and publication retain separate gates.

`supplements/sources.json` pins retrieved license evidence by hash and source URL.
`native/sources.json` pins the selected libvips packaging recipe/version file;
`native/source-acquisition.json` records source archive/patch hashes. Acquired
archives are retained locally in `.local/release-compliance/native-sources/`,
outside Git. They are not published automatically. Missing Rust crate sources,
platform toolchains, source-delivery obligations and reproducibility are described
in [the release evidence](../../docs/release-compliance-verification.md).

On dependency updates, recollect notices, review changed provenance/licenses,
refresh native source evidence, build images and verify their actual payloads.
Do not copy an old manifest onto a new image or interpret a passed text-presence
check as resolving the remaining release gates.

## Native source sidecar

The [source-package verification](../../docs/native-source-package-verification.md)
records the 406-input sidecar, full offline native rebuild, source-modified
replacement test and remaining OS/publication scope. The reviewed source manifest
is `native/source-bundle.lock.json`. `source_bundle.py fetch/create/verify` provides
portable collection, image binding and reproducible source-archive generation.
Source archives stay outside Git; the manifest, hashes, helpers and evidence are
versioned here. Do not substitute a different image/component set without review.

`rust_sources.py` reconstructs a Cargo directory source from locked `.crate`
archives. `rebuild_native.py` runs the pinned recipe inside its prepared builder
without network. `verify_native.py` checks the rebuilt source marker and exact
library loaded by Sharp in a disposable runtime.

For Rust notices, after preparing the vendor sources:

```bash
python3 deploy/compliance/notices.py rust .local/release-compliance/rust/verified-vendor /tmp/ratefreak-rust-notices --built deploy/compliance/native/rust-built-packages.json --supplements deploy/compliance/rust-supplements --check
```

The observed Linux build's 162 registry packages have complete collected texts;
the resulting notice bundle is included with the npm libvips package notices.
The original 357-crate source set includes unbuilt target/dev dependencies; its
separately recorded provenance limits are not silently promoted to runtime claims.

## Bundled reference services

PostgreSQL/Caddy reference images are now distributed alongside the application
images. `deploy/release/reference_inventory.py` collects their actual installed
OS source identities, original available copyrights/notices, ELF fingerprints,
Go module/toolchain identities and configuration scripts without starting the
containers. Those facts are not source/license clearance. Supplemental original
licenses and matching source/build materials are reviewed separately and bound
through `reference-coverage.json`; final preparation and bundle validation reject
missing or mismatched coverage. Include inherited-layer sources and preserve
all documented provenance limits. See `docs/releasing.md` and ADR 0023.
