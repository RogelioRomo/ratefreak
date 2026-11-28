# Stable candidate qualification — 2026-11-27

Qualification of the original `0.1.0` candidate at
`f54048c233e405843160bc16d857f8c32082599f` found available compatible OS security
fixes. The original candidate remains retained and cannot proceed to public
release under the current security policy. A successor must apply the fixes and
repeat affected qualification. Nothing has been published or applied to the
persistent development installation.

## Completed original-candidate evidence

- [Source/license coverage](release-evidence/2026-11-27-stable-sources/review.md):
  all 502 component entries mapped, all 47 prior material hashes checked, all 849
  OS source-input entries read back, all 406 native source inputs verified in an
  image-bound sidecar. Complete original notices and provenance limits retained.
- [All-layer/privacy inspection](release-evidence/2026-11-27-stable-layers/review.md):
  all 43 layers inspected, including hidden/overwritten files; all 1,721 committed
  source files and the generated installation revision match. No configured-secret
  match or new redistribution payload blocker. Native/runtime, migration apphost,
  Sharp/fonts and lower-layer source coverage retain exact evidence and limits.
- [Fresh security review](release-evidence/2026-11-27-stable-security/summary.json):
  new scanner database, exact artifact/config binding, application dependency scan,
  native-byte rebinding and six real allocator/decoder/configuration checks.
  Existing low-risk acceptances remain visible. Available vendor fixes still gate
  release even where supported-caller exclusions reduce current exposure.
- Disposable fresh HTTPS and browser checks passed. A synthetic pinned baseline
  upgrade preserved original columns/rows of 30 tables and checked expected new
  lifecycle tables and the Songs seed. Recovery, nonempty 36-table restore/replay and720-operation capacity proofs passed; receipts are retained in the original stable acceptance evidence directory.

## Fixes and packaging decision

Apply the recorded [available compatible distro fixes](release-evidence/2026-11-27-stable-security/available-fixes.json):
API krb5; Debian shadow packages; PostgreSQL zstd packages; and Caddy's Alpine
OpenSSL/zlib packages. Application Dockerfiles already upgrade OS packages, but
cached runtime build layers must be refreshed and verified.

The owner approved the complete five-image bundle. The implementation and
corrected upgrade harness are frozen at successor revision
`9eea2c76550fe03920dd999f1677565e4f43d12b`. All 15 verification checks and 2,128
tests passed. Five exact archives, original notices and actual component
inventories are preserved unchanged. Finalization completed successfully: the
local installable bundle has 21 indexed artifacts at `verified`, with the complete
404-file source archive and exact fresh/upgrade acceptance receipts. Independent
bundle validation/readback passed all artifact hashes, 880 component source
entries and all five IDs. See the
[final package review](release-evidence/2026-11-27-successor-freeze/final-bundle-review.md).
The Caddy final runtime uses Debian13 and the exact upstream static Caddy binary
and assets. An Alpine trial revealed an unresolved APK GPL2/OpenSSL3 grant issue;
that trial is retained as historical evidence and is excluded from delivery.
Current final-image scans show no fix-listed OS package rows; remaining Go
findings require the separately recorded supported-caller assessments.

The [handoff](release-handoff-2026-11-27.md) records exact final identities and
active proof paths. Source acquisition and exact-image binding passed for all 880 component entries.
The independent app review verified all 1,186 OS input references, 379 descriptor
closures,406 native inputs and 1,731 committed source blobs. Exact successor
[operational acceptance](release-evidence/2026-11-27-successor-acceptance/README.md)
passed fresh/upgrade/browser, recovery/persistence and nonempty 36-table restore
with erasure/TMDB/IGDB replay, preserving song artwork/keys. Capacity passed 720
requests with zero errors; all disposable projects were cleaned. The [final security review](release-evidence/2026-11-27-successor-security/README.md)
assesses all 1,433 associations/633 records with zero unaccepted blockers. Two
accepted glibc risks and supported caller/configuration exclusions remain visible;
68 fix-listed static vendor Go/module associations are not claimed patched.
Seven allocator roles and decoder/JIT/configuration probes passed. The inherited
PostgreSQL public snakeoil key is unused with ssl=off; enabled TLS requires an
operator-controlled replacement. Exact-byte bundle finalization and independent artifact readback passed.

## Application package refresh trial

A separately tagged [application-image trial](release-evidence/2026-11-27-application-update-trial/package-refresh.json)
rebuilt the exact original source with fresh runtime package layers. All three
builds passed. Installed package inventories confirm the API's four krb5
libraries moved to `1.20.1-6ubuntu2.12`; web/tools `login.defs` and `passwd` moved
to `1:4.17.4-2+deb13u1`. No other installed package versions changed. The original
frozen tags/archives and persistent instance were unchanged. These trial images
prove package refresh is available; they are not a selected successor candidate
and do not inherit completed final security/source/acceptance qualification.

## Acceptance harness findings

The current reference operator correctly provisions the new policy paths. The
pinned old baseline tools cannot provision the current Compose TMDB/IGDB mounts,
so baseline fixture preparation must use the current candidate's storage-init
before migrating/seeding with the historical baseline tools.

The old verifier compares complete database fingerprints across migrations. The
new lifecycle tables, added columns, EF migration history and Songs seed are
legitimate deltas. Correct acceptance must prove original columns/rows remain
unchanged while separately checking only the expected schema/seed additions.
Local proof adaptations exercise this distinction; the frozen source is unchanged.

## Remaining gates

The local bundle is finalized and independently validated. Source, security and
operational proofs passed. Preserve the reviewed bundle for authorized publication. Verify
maintainer security-report notification delivery; existing records prove private
reporting is enabled, not that a report notification was received. No external
report/message has been sent. Synchronization and explicit publication approval
remain separate later steps. No finalized installable release is claimed.
