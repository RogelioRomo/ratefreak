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

API/frontend remain stopped; persistent-instance provider/runtime settings are unchanged. Nothing
was pushed or published. Upstream synchronization failed because GitHub SSH
authentication was unavailable. Other installations need their own reviewed
migration and configuration; this local migration does not upgrade them.

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

**The current security review has no unaccepted findings.** The owner authorized
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

## Remaining release gates

The order below is a suggested sequence. Earlier candidate checks are completed
evidence for their exact sources/images; changed final artifacts need matching
qualification.

1. **Carry the implemented provider changes into final qualification.** The
   [implementation handoff](provider-implementation-handoff.md) now has IGDB
   five/six-calendar-month lifecycle, durable removal/restore protection and a
   managed cover cache, plus direct Open Library covers bypassing proxy/optimizer.
   See [implementation verification](provider-implementation-verification.md) for
   exact development-test/artifact scope. Final stable artifact qualification
   remains required; no persistent installation was migrated by this work.
   The described use is completely free/noncommercial; no commercial IGDB
   partnership is pending. Six months is our chosen policy, not IGDB approval;
   cover-processing and Archive terms interpretation limits remain documented
   in the [agreement evidence](provider-agreement-evidence.md). Keep providers
   optional/default-disabled and release artifacts free of imported content and
   shared credentials. Record operator registration/contact, eligibility,
   aggregate pacing and attribution. TMDB lifecycle implementation remains in
   place; final artwork/source/security checks belong to the candidate below.
2. **Freeze and qualify the stable candidate.** Select the stable version and
   source revision; build final API/web/tools images and assemble matching
   source materials, license/notice delivery and inventories. Repeat relevant
   source, all-layer artifact, secret/privacy and dependency/security checks.
   Apply available compatible security fixes and reassess accepted findings under
   the [current policy](security-risk-policy.md). Preserve the allocator, decoder,
   JIT, architecture and caller conditions of existing exclusions, and bind any
   retained risk acceptances to the final artifacts.
   Keep historical candidate identities unchanged and retain documented
   provenance limits. Earlier source-delivery and candidate proofs are in
   [runtime redistribution verification](runtime-redistribution-verification.md)
   and [candidate verification](release-candidate-verification-2026-11-23.md).
3. **Complete final reference-installation acceptance.** On the exact candidate,
   verify fresh installation, supported upgrade/data preservation, real HTTPS
   sessions/CSRF/logout/recovery, persistence, ordinary/staff browser journeys
   and coordinated backup/isolated restore with current erasure and TMDB policy
   replay. Earlier reference and TMDB image proofs already passed; repeat the
   checks affected by the final source/runtime changes. Finish representative
   load/capacity evidence and verify maintainer security-report notification
   delivery. See [reference verification](self-hosted-reference-verification.md)
   and the [detailed acceptance checklist](frontend-release-verification.md#outstanding-release-gates).
4. **Publish the approved release.** Restore GitHub authentication, synchronize
   the source, and obtain authorization for versioned artifact publication.
   Deliver the matching source/notices with binaries and publish the release
   announcement using the implemented release/versioning process. See
   [release plan](self-hosted-release-plan.md) and [versioning verification](release-versioning-verification.md).

## Per-installation operator duties

These must be verified for each installation; local/reference evidence does not
establish them for every self-hosted instance:

- Correct HTTPS origin/proxy attribution, secure session handling and optional
  Google/GitHub callback configuration.
- Protected secrets, durable Data Protection keys and persistent PostgreSQL/artwork
  storage, including restart/recovery and replica behavior where applicable.
- Coordinated database/artwork backups expiring within 30 days, an independently
  protected erasure journal, current TMDB policy and a successful isolated restore.
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
