# Stable candidate freeze — 2026-11-27

The first stable-channel candidate selects version `0.1.0` for Linux amd64.
Qualification and publication remain separate gates; this record does not clear
public release.

## Frozen source

- Source revision: `f54048c233e405843160bc16d857f8c32082599f`.
- Application baseline: `de41315` (all implementation changes on local main).
- Candidate changes from that baseline are `VERSION` (`0.1.0`), maintainer-written
  `docs/releases/0.1.0.md`, the evidence filename correction described below, and a test-only feed assertion fix.
- Source/tag selection: clean isolated clone at
  `.local/stable-candidate-2026-11-27/source-qualified/`, with local-only annotated `v0.1.0`.
  The main checkout remains on its development version; no remote tag is created.
- Final schema migration: `20261127222703_AddSongs`, including the earlier TMDB
  and IGDB lifecycle migrations.
- A verified private `frozen-source-qualified.git.bundle` preserves the candidate commit/tag
  and history. This is private preservation material, not the public source archive.

The private freeze record under `.local/stable-candidate-2026-11-27/` records
source/configuration hashes and pinned Dockerfile base references. The tracked
installation source archive produced by the release tooling is the distribution
source snapshot; local instance files and Git history are excluded from it.

## Source-distribution preflight

The first source selection (`f33356a0291d5022173918f18001487d086e14ec`)
was superseded before image building: four tracked security evidence `.log` files
violated the existing source-distribution path policy. The corrected source uses
`.txt` filenames and updates their verification-index keys. All historical evidence
bytes and indexed hashes remain unchanged and were checked. The historical exporter
script retains its original output names. Source-archive preflight now passes;
the release safety policy was not weakened. The original clone/tag and private
record remain retained separately; no prior public tag/artifact was replaced.

The intermediate source `b329e038aa2ac59edf4bdb21899afcd371a27099` exposed
an order-dependent song artwork test: its shared database can contain another
test's community review. The assertion now selects the imported media ID before
checking its artwork, matching existing feed tests. The observed two-item failure
is retained privately. No production behavior changed.

## Frozen artifacts and verification

The existing `prepare.py` workflow built and retained the exact API/web/tools
images, collected notices and component inventories, and generated the tracked
installation archive and release notes under
`.local/stable-candidate-2026-11-27/candidate-qualified/` (about 679 MiB).
The [candidate manifest](release-evidence/2026-11-27-stable-freeze/candidate-manifest.json)
records all **11 file hashes** and the three exact image identities.

Fresh verification on the frozen revision passed all **15 release checks**:
**821 backend tests** (62 unit, 235 provider contracts, 524 database integration;
zero skips), **1,103 frontend tests**, and **146 operator/release/compliance tests**
(100/26/20), totaling **2,070**. Lint, TypeScript, frontend production build, API
Release publish and EF model consistency passed.
See the [verification summary](release-evidence/2026-11-27-stable-freeze/verification.json).

Read-back validation checked every manifest file checksum, all image inventories,
and matching Linux amd64 platform/version/revision labels. The installation source
archive exactly matches the independently generated frozen tracked-source archive;
its embedded version and source revision also match. The private Git bundle was
verified. See the [freeze receipt](release-evidence/2026-11-27-stable-freeze/freeze-record.json),
[image identities](release-evidence/2026-11-27-stable-freeze/image-identities.json)
and [component inventory](release-evidence/2026-11-27-stable-freeze/source-inventory.json).

Preparation stopped with the expected **incomplete candidate** result because
image-bound source-material review is still outstanding. The manifest stage is
`review`; no installable `release-manifest.json` or finalized `release/` exists.
This is a completed artifact freeze, not final qualification. Build cache use was
permitted; no refreshed vulnerability scan or compatible-fix review is claimed.
All disposable test containers were removed; only the existing development
PostgreSQL container remained. Raw verifier/build logs remain private.

The evidence/test corrections, release notes and documentation are also present
as uncommitted changes in the main working tree. Its `VERSION` and tags remain
unchanged. The candidate commits/tag are preserved in the isolated clone and
private Git bundle; nothing was pushed.

## Qualification boundary

Final image-bound source/notice coverage, layer/privacy inspection, current
security qualification and rebinding of exclusions/accepted risks are required.
Final installation/upgrade/browser/restore acceptance must use the exact retained
candidate images and configuration, including the single-arena allocator policy,
decoder restrictions, disabled PostgreSQL JIT, and current erasure/TMDB/IGDB replay.
Representative capacity evidence and security-report notification delivery remain
outstanding. See [STATUS](STATUS.md#remaining-release-gates).

Resume existing frozen bytes with the documented `prepare.py --resume` procedure
once their reviewed source materials are ready. Do not rebuild during finalization
or publication. If qualification requires source/runtime changes, preserve this
record and create a separately identified successor candidate.

No persistent installation migration, GitHub synchronization, tag publication,
release publication or external message is part of this freeze.

## Qualification follow-up

The [subsequent qualification](stable-candidate-qualification-2026-11-27.md)
completed source/license and all-layer/privacy checks on this original candidate,
but fresh security review found available compatible distro fixes. This original
freeze is preserved as historical evidence; a successor must apply the fixes and
repeat affected qualification. No release clearance follows from this record.
