# OS source acquisition and remaining image audit

Verified: 2026-11-21. Scope: the retained Linux amd64 development candidate at
`11f404984dd4bec1eb985aad472a0471742e57cf`, not final release clearance or a
qualification of current `main`. No images, releases or sources were published;
no persistent instance, database or provider setting was changed.

The later [runtime coverage fix](#runtime-coverage-fix) below resolves the collector
and inventory gaps in code and disposable derived images. The original retained
candidate and the initial audit evidence remain historical snapshots.

The subsequent [runtime redistribution review](runtime-redistribution-verification.md)
records the completed selected component decisions and supplemental notice
delivery. Its final-candidate limits supersede the pending review instructions
in this earlier audit snapshot.

## Prepared source materials

The audit collected **562 checksum-verified files for 184 unique OS source
package/version pairs**, covering the installed OS inventory of all three
retained images:

| Image | Binary packages | Unique source package/versions |
| --- | ---: | ---: |
| API, Ubuntu noble | 125 | 86 |
| Web, Debian bookworm | 112 | 75 |
| Tools, Debian bookworm plus PGDG | 156 | 98 |

Source packages shared between images account for the lower aggregate total.
The collection includes every selected `.dsc` and all original archives,
distribution patches and packaging files referenced by its SHA-256 list. Every
descriptor's source name/version and complete input list matched the selected
source metadata; file sizes and hashes passed, with zero download failures.

APT successfully authenticated current source indexes with the images' archive
keys. Four Ubuntu sources required the signed `20261101T000000Z` snapshot:
`apt` 2.8.3, `glibc` 2.39-0ubuntu8.9, `pcre2` 10.42-4ubuntu2.1 and `perl`
5.38.2-3.2ubuntu0.6. PGDG's historical archive supplied
`postgresql-common` 293.pgdg12+1. The index archives retain signed InRelease
files, source indexes, repository selections and APT update results. HTTPS
verification remained enabled. The tools collector used a read-only host CA
store because that image lacks a system CA certificate store.

The local OS artifact is:

`.local/release-compliance/os-audit-2026-11-21/ratefreak-candidate-os-sources.tar`

Its size is **1,044,961,280 bytes** and SHA-256 is
`6e78705e84204511caac65a9c1778461b3d78f210380e475d01d5fd9d4c07736`.
The archive contains the source inputs, image/source mappings, five authenticated
metadata snapshots and instructions for staging the original filenames before
`dpkg-source -x`. All source payload hashes were checked again from the finished
archive. Building every OS package and reproducing its binary were not performed.
Sources use a deduplicated, hash-prefixed storage layout; the included manifest
records each original filename. The archive deliberately excludes non-OS
runtime and application components.

The existing 406-input native source set was separately validated and packaged
for the retained web image ID, after checking its package version, component
versions and native binary hash. The candidate-bound local sidecar is
`web.native-sources.tar.gz` in the same audit directory, SHA-256
`355fadebf2aa96e78b82e6bc26299d76a6afd78b847c1a30866bd8105b9c6e3c`.
This changes the image binding, not the native sources or binary. A full native
rebuild/replacement was not repeated; its earlier scoped proof remains in the
[native verification](native-source-package-verification.md).

The retained candidate's original inventory predates the current full native
profile validation. Fresh inventory evidence was collected from the same exact
images and validated against the candidate-bound sidecar. The old candidate
inventory and release artifacts were left untouched.

## Artifact checks and concrete gaps

The three packaged application notice manifests cover 134 package records.
Their notice-bundle hashes, project MIT license copies and OS inventory hashes
passed. All **594 archived runtime license-file hashes** matched their evidence.
These checks establish integrity and text presence for the recorded scope, not
complete license applicability or source clearance.

The saved API/web/tools image archives contain 13/13/23 layers. Inspecting their
package-status records found no installed binary package/version pairs missing
from the merged inventory. This does not prove that all lower-layer files are
package-managed or that every removed file has been classified.

The remaining findings are actionable:

1. **Inventory copied runtime tools and their bundled dependencies.** The web
   image includes npm 11.19.1, Corepack 0.36.0 and Yarn 1.22.22 outside the
   application package inventory. There are 185 global npm/Corepack package
   manifests under `/usr/local`, plus Yarn under `/opt`. Review this shipped
   content before recording source exceptions; top-level runtime labels do not
   describe every dependency.
2. **Collect Yarn's notice.** `/opt/yarn-v1.22.22/LICENSE` exists in the image
   but is absent from its runtime notice archive. The collector scans
   `/usr/local` and `/usr/share/dotnet`, excluding `/opt`. Its exact SHA-256 is
   `ae09761b5d27e237571245437653b483bbb85de3cc64b802e8ef47d03aabec1e`.
   Fix the collection scope and verify the rebuilt web artifact.
3. **Review tools-image additions.** `gosu` 1.19, built with Go 1.24.6, and
   upstream PostgreSQL entrypoint scripts need explicit provenance, notices and
   source decisions. They are not represented by the OS source mappings alone.
4. **Finish native and bundled-content decisions.** Preserve and review the
   exact SkiaSharp embedded-component notices and EF bundle closure before
   asserting no-source-required exceptions. The API ships 20 Skia runtime files,
   including Linux variants, macOS, Windows DLLs and PDBs; an amd64 runtime check
   does not remove those files from distribution scope. The conservative web
   inventory names SWC, but no SWC native payload was observed under `/app` in
   this candidate; distinguish inventory supersets from actual shipped files.
   Copied Node/.NET notices, browser bundles and fonts retain final review scope.
5. **Reconcile the final candidate.** Build from the chosen final revision,
   refresh changed inventories/sources and create reviewed per-component source
   mappings. Complete all-layer artifact and secrets/local-data review. Then
   validate the complete source coverage and run final candidate acceptance.

No blanket permissive exception or complete `coverage.json` was generated.
The OS-source acquisition portion is prepared for these exact retained images;
the whole-image redistribution gate remains open. The recommended next slice is
to fix notice/inventory coverage for the copied runtime tools, then complete
their component-specific source decisions. The
[primary-source research](os-source-delivery-research.md) explains the acquisition
approach and the distinction between engineering policy and license terms.

## Durable evidence and repeatability

- [Audit summary](release-evidence/2026-11-21-os-sources/audit.json): exact image
  IDs, candidate artifact checksums, source artifact metadata and open findings.
- [Source file mapping](release-evidence/2026-11-21-os-sources/source-files.json):
  every original source filename, URL, SHA-256, size and per-image package mapping.
- Local authenticated index archives, detailed layer/runtime inventories,
  acquisition results, refreshed native profile and acquisition/verification
  scripts are retained under `.local/release-compliance/os-audit-2026-11-21/`.
  Large source archives are intentionally outside Git. Retention of these local
  files is not a public source-delivery event.

Relevant existing checks were rerun: 18 compliance tests and three source-inventory
tests passed. No application code changed, so application suites and production
builds were not repeated. Provider clearance, final operational acceptance,
vulnerability review, final artifact publication and authorization remain separate.

## Runtime coverage fix

Follow-up, 2026-11-21. The working tree now collects runtime notice texts under
`/opt`, captures versioned global package manifests and hashes copied
`/usr/local/bin` files. The release source inventory includes these in a new
`runtime` scope. Package manifests are grouped by name/version with all observed
paths retained; copied files are identified by absolute path and SHA-256. No
source exception is inferred from package license declarations.

Runtime evidence schema 2 and source inventory format 2 are required. The
collector rejects old runtime evidence, and both candidate finalization and
bundle validation reject old saved source inventories. A source decision set
omitting the new runtime components is rejected. Fresh candidates must be
prepared from the corrected source; relabeling an old inventory is not an upgrade.
Review and final bundle validation also read the actual archived runtime evidence
and compare its runtime component set with the saved inventory. An old notice
archive or omitted runtime component is rejected even when artifact checksums
match and the saved inventory declares the new schema.

Three local images tagged `runtime-compliance-proof` were derived from the exact
retained API/web/tools images with networking disabled during collection. They
copy only the corrected collector and regenerate its runtime notice evidence;
their audit label marks them as uncommitted proof images. They are not official
release candidates or full production rebuilds.

| Derived image | Runtime package manifests | Runtime component decisions required | Collected runtime license files |
| --- | ---: | ---: | ---: |
| API | 0 | 0 | 144 |
| Web | 146 | 154 | 276 |
| Tools | 0 | 4 | 175 |

The web archive contains Yarn's exact license bytes/hash recorded above; its
inventory contains npm 11.19.1, Corepack 0.36.0 and Yarn 1.22.22 plus their
versioned bundled dependency manifests. Configuration/fixture manifests without
both a package name and version are not treated as separate versioned packages.
The tools inventory contains `gosu` and the upstream entrypoint scripts, including
the alias paths shipped in `/usr/local/bin`. OS inventories and application-native
path/hash pairs match their original retained images. All 595 archived runtime
license-file hashes passed verification.

Verification passed: **19 compliance, 26 release and 52 operator tests**. The
baseline release suite initially hit sandbox `EPERM` while Node spawned Git;
rerunning outside the sandbox passed. Regression coverage exercises real notice
archive contents, nested runtime packages, copied executable hashes, source
coverage rejection and old bundle rejection even with matching artifact
checksums. Full backend/frontend suites were not repeated because this change
only affects release tooling.

[Runtime fix evidence](release-evidence/2026-11-21-os-sources/runtime-fix.json)
records the derived image IDs, collector hash and verification results. Detailed
archives and inventories are retained under
`.local/release-compliance/runtime-fix-2026-11-21/`.

This resolves the missing Yarn notice and copied-runtime inventory implementation
gaps. Individual runtime, native and bundled-content source/notice decisions,
whole-image artifact review and final source-matched candidate qualification
remain open. Capturing `gosu` or a package name is not a review of its dependency
licenses. The prior candidate-bound OS/native source artifacts must be reconciled
with the final images before release.
