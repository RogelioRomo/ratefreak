# Current project status and release work

Updated: 2026-11-27.

RateFreak's first-release product scope is implemented and merged locally into
`main`. **Public release is not yet cleared.** The remaining work is provider
requirements, final artifact/security qualification, operational acceptance and
publication.

The release target is open-source software that others self-host, using the
Next.js → .NET → PostgreSQL reference installation. A project-owned production
service is not required ([ADR 0020](adr/0020-self-hosted-open-source-distribution.md)).
This page records current scope and state; dated plans and verification documents
retain their original evidence scope and do not override this backlog.

## Current implementation and local state

- Accounts, first-run Admin setup, saved-code recovery, optional linked
  Google/GitHub sign-in, account export/deletion and erasure journal/replay.
- Public catalog and provider search/import for TMDB movies/TV, IGDB games,
  MusicBrainz albums and Open Library work-based books; custom catalog items,
  protected uploads and ordered collections of existing items.
- Private libraries with search, filters, sorting, status/date tracking and
  completion history; ratings, reviews, comments, profiles, follows and avatars.
- Following/Community feeds, explicit per-rating sharing and notifications.
- Reporting/moderation, staff management and Admin removal with last-Admin
  protection. Admin appointment remains an operator action.
- Production API/web/tools packaging, reference Compose/HTTPS ingress,
  installation/upgrade/recovery instructions, provider credits, package notices,
  source-delivery tooling and release/versioning/update-awareness automation.
- MIT license, contributor guidance and security policy. GitHub private
  vulnerability reporting is enabled; maintainer notification delivery still
  needs verification.

The TMDB lifecycle is complete and merged through `8568481`: metadata and posters
refresh from five UTC calendar months and expire at six, preserving user content,
internal identities and TMDB IDs behind an unavailable placeholder. Independent
removal/termination policy also protects restores. The staff clarification and
owner's image-processing decision are recorded; no repeat TMDB storage/image
permission request is pending. See [TMDB verification](tmdb-lifecycle-verification.md)
and [provider disposition](provider-agreement-evidence.md#current-disposition-2026-11-24).

The merged baseline passed **1,909 tests**: 778 backend (zero skips), 1,013
frontend and 118 operator/release/compliance, plus model consistency. Runtime
inputs match the recorded TMDB image proof. This is local verification, not
qualification of a final stable release.

Migration `20261124233146_AddTmdbContentLifecycle` is applied to the persistent
local `ratefreak` database after a private database/artwork backup, matching
isolated restore and upgrade/rollback/reapply rehearsal. All 29 original
application tables' columns/data and existing artwork were preserved;
reapplication was idempotent. See [integration/migration evidence](release-evidence/2026-11-24-tmdb-lifecycle/main-integration.json).
Private backup and apply records are in
`.local/localhost/tmdb-content-lifecycle-migration/`.

Following that TMDB migration, API/frontend were left stopped and persistent-instance
provider/runtime settings were unchanged. Nothing was pushed or published. Upstream synchronization failed because GitHub SSH
authentication was unavailable. Other installations need their own reviewed
migration and configuration; this local migration does not upgrade them.

On 2026-11-27, the owner requested application startup and explicitly authorized
the IGDB migration. `20261127171719_AddIgdbContentLifecycle` was applied to the
local `ratefreak` database after a private database/artwork backup and isolated
restore, upgrade, rollback and reapplication rehearsal. All 32 existing application
tables and artwork were preserved; repeat application was idempotent. Private
records are under `.local/localhost/igdb-content-lifecycle-migration/`.
The Development API and web app are running at `http://localhost:5080` and
`http://localhost:3000` for owner review, with existing provider settings and
credentials retained and local lifecycle policy/cache paths provisioned.
Startup checks/logs are under `.local/localhost/provider-startup/`. This is local
manual review, not final release qualification or publication.

MusicBrainz album artwork, full song catalog support with associated-release
artwork, and the signed-in Recently rated section were subsequently implemented.
Album/song discovery defaults to results with loaded covers and permits showing
all results. The signed-in home introduction was removed and the footer now
displays "Made with RateFreak", linking to the repository in a new tab. The MIT
license is unchanged; no mandatory visible-attribution condition was added.
`20261127222703_AddSongs` is applied to the local database;
the EF database update command was rerun successfully with no pending migration.
See [ADR 0022](adr/0022-musicbrainz-songs-and-artwork.md). These local changes do
not establish migration or release acceptance for other installations.
The final frontend verification passed all 1,103 tests across 129 test files.
All implementation commits are on local `main`; no local feature branches remain.
GitHub synchronization remains blocked by unavailable SSH authentication, so the
remote branch state has not been verified and these commits have not been pushed.

The [2026-11-25 security follow-up](security-followup-2026-11-25.md) implements: Debian 13 web/tools runtimes, stable SkiaSharp 4.157.0
with refreshed native notices, PostgreSQL 17.12 Trixie with collation safeguards,
HEIF/AVIF and Node zstd decoder restrictions, and PostgreSQL JIT disabled.
Verification passed 778 backend, 1,026 frontend and 123 tooling tests, plus lint,
TypeScript, model consistency, compiled decoder checks, fresh/upgrade HTTPS,
browser and 33-table coordinated restore acceptance. Source coverage and all 43
image layers were inspected. JIT disabling was separately tested on those
qualified application images; final installation-package qualification must
include that later configuration delta. No persistent installation was upgraded.
A fresh verification before local main integration reran all **1,927 tests**
and passed lint, TypeScript and model consistency; see the
[main integration record](release-evidence/2026-11-25-security-followup/main-integration.json).

The [2026-11-26 security closure](security-closure-2026-11-26.md) applies a
single-arena malloc policy to all six reference service roles and completes the
missing OS C++ caller review, including 26 matching Build-ID debug companions.
Six of the previous eight IDs now have conditional exclusions, including the
additional Node stream-output candidates. All six real allocator probes,
fresh HTTPS acceptance, a 40-request concurrency smoke, 33-table coordinated
restore/replay and all 123 operator/release/compliance tests passed. This changes
installation configuration on the previous exact images; the frozen installation
archive must be regenerated at final qualification. Representative capacity
evidence remains outstanding. No persistent installation was changed.

**The 2026-11-26 security review has no unaccepted findings for its recorded
images/configuration.** The later stable-candidate review below requires available
vendor fixes before release. The owner authorized
a [risk-based CVE policy](security-risk-policy.md) for all future findings:
apply available compatible fixes; otherwise findings assessed as low practical
risk may be accepted with documented reasons and reassessment conditions.
CVE-2019-1010022 (thread-stack guard) and CVE-2019-1010024 (stack-address reuse)
are accepted low risks, covering the six prior open image/source records and
16 package associations. Their native behavior is unchanged; they are not
claimed patched or unreachable. See the
[acceptance decision](release-evidence/2026-11-26-security-risk-acceptance/decision.json)
and [current dispositions](release-evidence/2026-11-26-security-risk-acceptance/dispositions.json).
Earlier blocked receipts retain their historical scope. This closes those two
blockers on the reviewed images/configuration; final stable artifacts still need
fresh security qualification and any available compatible fixes.

The **0.1.0 stable candidate is frozen locally**, at source revision
`f54048c233e405843160bc16d857f8c32082599f`, with exact Linux amd64 API/web/tools
image archives, collected notices, component inventory, installation source and
release notes. The frozen revision passed all 15 release checks and **2,070 tests**
(821 backend with zero skips, 1,103 frontend, 146 operator/release/compliance).
All 11 artifact checksums and image platform/version/revision identities were
validated. See the [freeze record](stable-candidate-freeze-2026-11-27.md).
The candidate remains at `review`. Its [source/license reconciliation](release-evidence/2026-11-27-stable-sources/review.md)
now covers all 502 component entries, with 849 OS source-input entries and the
406-input native sidecar freshly verified. Its [all-layer/privacy review](release-evidence/2026-11-27-stable-layers/review.md)
checked all 43 layers and exact tracked source without finding a new payload or
configured-secret blocker. The [fresh security scan](release-evidence/2026-11-27-stable-security/summary.json)
requires compatible vendor fixes: API krb5, Debian shadow, PostgreSQL zstd and
Caddy OpenSSL/zlib packages. Conditional exclusions and the two existing accepted
low risks remain explicit; available fixes still require a successor candidate.
The owner approved supplying patched PostgreSQL/Caddy alongside the existing
application images. The five-image successor is frozen locally at
`9eea2c76550fe03920dd999f1677565e4f43d12b`; all 15 release checks and **2,128 tests**
passed (821 backend with zero skips, 1,103 frontend, 204 tooling). The finalized local bundle has21indexed artifacts at `verified`, with exact five
image archives, matching sources/notices and fresh/upgrade acceptance receipts.
Independent readback verified all artifact bytes,404source archive members,
880component entries and identical final image IDs. Compatible OS fixes are
applied; source, security and operational qualification passed. See the
[final package review](release-evidence/2026-11-27-successor-freeze/final-bundle-review.md).
The local bundle is under
`.local/stable-qualification-2026-11-27/successor-candidate/release/`.
Caddy uses a Debian final runtime with the exact upstream static server/assets;
the historical Alpine trial's unresolved grant question does not qualify that
trial for delivery. Original candidate artifacts remain unchanged.
The [successor security ledger](release-evidence/2026-11-27-successor-security/README.md)
assesses all 1,433 associations across 633 image/source records, with zero unaccepted
blockers. Compatible OS fixes are applied. Two previously accepted glibc risks
and specific caller/configuration exclusions remain explicit, including 68
fix-listed associations in unchanged static vendor Go/module copies; those
copies are not claimed patched. All seven allocator roles, decoder restrictions,
JIT-off and current configuration probes passed. The inherited public PostgreSQL
snakeoil key is documented as unused with ssl=off and requires replacement if
TLS is enabled.

Successor recovery, 36-table nonempty restore/replay and representative
720-operation capacity acceptance passed on all five exact final images. See the [qualification record](stable-candidate-qualification-2026-11-27.md)
and [resumption handoff](release-handoff-2026-11-27.md) for current task state.
The successor source/tag lives in an isolated clone; the main checkout retains
its development version. No persistent installation was changed and nothing was
pushed or published.

## Remaining release gates

The local successor package and its provider/source/security/operational proof
are complete. Providers remain optional/default-disabled; preserve eligibility,
operator registration/contact, aggregate pacing, attribution and lifecycle/removal
conditions. The described use remains free/noncommercial, with no commercial IGDB
partnership pending. Existing cover/Archive interpretation and provenance limits
remain in [provider agreement evidence](provider-agreement-evidence.md).

1. **Verify maintainer security-report notification delivery.** The owner confirms
   private reporting is enabled but has not received notifications. This does not
   establish a delivery failure or prove receipt. A clearly labeled private test
   draft is prepared locally; no report/message was sent. Confirm receipt through
   the configured maintainer notification channel before closing this gate.
2. **Restore GitHub authentication and synchronize the reviewed source.** Prior
   SSH authentication was unavailable. The 23 reviewed successor implementation
   files and qualification evidence are committed and merged into local `main`.
   The prior 204 tooling tests passed; the owner requested no additional test
   runs for this commit/merge. The isolated frozen source commit
   and local-only tag preserve the exact reviewed release. Preserve all current
   main changes/evidence and the original candidate when integrating/synchronizing.
3. **Publish only after authorization.** The local verified bundle is ready for
   review. Obtain explicit approval for remote source/tag/artifact publication;
   deliver its full matching sources/notices and the release announcement using
   the existing [release plan](self-hosted-release-plan.md) and
   [versioning process](release-versioning-verification.md). Nothing is published
   and the persistent developer installation remains unchanged.

## Per-installation operator duties

These must be verified for each installation; local/reference evidence does not
establish them for every self-hosted instance:

- Correct HTTPS origin/proxy attribution, secure session handling and optional
  Google/GitHub callback configuration.
- Protected secrets, durable Data Protection keys and persistent PostgreSQL/artwork
  storage, including restart/recovery and replica behavior where applicable.
- Coordinated database/artwork backups expiring within 30 days, an independently
  protected erasure journal, current TMDB/IGDB policies and a successful isolated restore.
- Current staff permissions, named operator attribution, protected external
  records with 365-day expiration and Admin removal/recovery acceptance.
- Monitoring, sanitized logging, secret rotation and perimeter/coordinated abuse
  budgets; current application rate limiters are process-local. Verify
  disconnect propagation and capacity for the chosen topology.
- Provider credentials, eligibility/contact/registration and applicable agreements;
  external-client compatibility and reviewed target-database upgrades.

Use the [self-hosting guide](self-hosting.md), [account operations](account-data-operations.md),
[staff operations](staff-access-operations.md) and [release-gate table](frontend-release-verification.md#outstanding-release-gates).

## Deferred product scope and retained decisions

No additional product features are required for the first release. Collection
discussions, catalog correction UI and broader provider refresh/provenance work,
account suspensions/appeals and richer private statistics are deferred. The
required TMDB lifecycle is already implemented. First-use/onboarding work was
explicitly declined.

Recovery uses saved codes and linked providers; local email verification and
email-delivered recovery are excluded ([ADR 0014](adr/0014-account-recovery.md)).
Collections reference catalog items; retired dynamic fields/free-standing entries
are not planned ([ADR 0016](adr/0016-custom-catalog-and-list-collections.md)).
Profile hiding covers avatar/name/bio together, with explicit staff restoration
([ADR 0021](adr/0021-profile-avatars.md)). Staff access and operator attribution
follow [ADR 0019](adr/0019-staff-access-and-operator-attribution.md).
Upgrades remain manual; user-facing “What's new” notices are deferred.

## Documentation and evidence

- [Root README](../README.md), [web README](../web/README.md),
  [contributor guide](../CONTRIBUTING.md) and [security policy](../SECURITY.md).
- [Implementation history](implementation-checklist.md),
  [frontend milestone history](frontend-checklist.md) and [architecture decisions](adr/).
- [Release plan](self-hosted-release-plan.md),
  [release verification](frontend-release-verification.md) and [self-hosting guide](self-hosting.md).

Detailed feature, migration, source-delivery and browser evidence remains in its
linked verification documents and Git history. Dated plans, old branch/port names
and temporary paths describe their original runs; they do not establish current
runtime state or final release clearance.
