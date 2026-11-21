# Self-hosted open-source release plan

Draft updated 2026-11-20 with agreed release announcements and administrator update
awareness scope and a proposed implementation design. Product direction is
defined in [ADR 0020](adr/0020-self-hosted-open-source-distribution.md). This is a
proposed work sequence, not an approved implementation specification.

The goal is for another person to install and operate RateFreak on infrastructure
they control. No Vercel account or centrally operated RateFreak service should be
required. Keep the existing Next.js → .NET → PostgreSQL architecture.

## 1. Design the reference installation

The [reference-installation design](superpowers/specs/2026-11-10-self-hosted-reference-installation-design.md)
was approved by the project owner. It specifies single-host Compose with optional Caddy,
authenticated ingress attribution, persistent storage and explicit maintenance
tools. The [implementation plan](superpowers/plans/2026-11-10-self-hosted-reference-installation.md)
is complete for the documented reference topology; see
[verification evidence](self-hosted-reference-verification.md).

- [x] Specify the supported initial topology and runtime/platform requirements.
  Implemented reference: a single-host Docker Compose installation containing
  frontend, API and PostgreSQL behind an HTTPS reverse proxy. Other hosting uses
  the same documented configuration/network/storage contracts.
- [x] Define how ingress supplies a trustworthy client address and how Next.js
  validates it. Production now supports authenticated trusted-proxy attribution;
  Development loopback mode remains local-only. Do not blindly trust
  client-supplied forwarding headers. Define which services may be exposed.
- [x] Define configuration and secret generation, fixed public origin, first-run
  Admin setup, optional OAuth/provider credentials, health checks and permissions.
- [x] Define persistent database, artwork and Data Protection storage plus the
  independently protected erasure journal. Document single-replica limits and
  requirements for other topologies.

The design was approved and implemented. Acceptance used disposable instances;
other topologies and each operator's external obligations retain their own checks.

## 2. Implement installation and maintenance packaging

- [x] Build production API/frontend images and a complete reference Compose file;
  the current `deploy/compose.yaml` starts only a development database.
- [x] Supply safe example configuration and first-run instructions without
  embedded credentials or requirements for developer user-secrets.
- [x] Provide explicit migration and durable journal provisioning procedures.
  Preserve existing data and refuse accidental resets/truncation.
- [x] Document HTTPS ingress, service networking, persistent-volume ownership,
  optional external database use and configuration for other hosts.
- [x] Document upgrades, matching schema/application versions, backup expiration,
  isolated restore with the current journal, key recovery and operator commands.

## 3. Verify the reference release

- [x] Install from production images with fresh volumes and no developer
  configuration. Complete first Admin setup and ordinary account registration.
- [x] Verify real HTTPS cookies, CSRF/Origin rejection, trusted client attribution,
  spoofed-header rejection, session revocation and private-response caching. Core
  recovery retains automated coverage; optional OAuth needs instance acceptance.
- [x] Verify restart/recreation persistence for database, artwork, sessions and
  journal; verify secrets and private storage cannot be downloaded directly.
- [x] Verify upgrade of an earlier supported installation without data loss.
- [x] Run coordinated database/artwork backup and isolated restore/replay using
  the documented installation. Check private erasure, public detachment, moderation,
  cleanup and repeated replay. See the [packaged reference evidence](self-hosted-reference-verification.md);
  earlier [local evidence](account-restore-verification.md) retains its original scope.
- [x] Verify ordinary/staff journeys and named operator appointment/removal/recovery
  on that installation. Disconnect/deadline checks passed; representative load
  capacity and pre-publication dependency/provider review remain release work.
- [x] Record supported/tested topology and remaining operator-specific acceptance
  duties in the [release gates](frontend-release-verification.md#outstanding-release-gates).

## 4. Prepare the public project

- [x] Choose an open-source license with the project owner and add its license file:
  [MIT](../LICENSE), selected 2026-11-11.
- [ ] Review dependency/image redistribution and metadata/artwork provider terms,
  required attribution and separately supplied provider credentials.
  Initial review recorded 2026-11-13: [dependency/image findings](dependency-redistribution-review.md),
  [package inventory](dependency-inventory.md) and [provider findings](provider-terms-review.md).
  Provider credits for all four providers and image package notice collection are
  implemented; [candidate image/native evidence](release-compliance-verification.md)
  records verification and remaining source-delivery/provenance requirements.
  [Applicable provider agreements](provider-agreement-evidence.md) and each operator's
  eligibility/acceptance remain separate requirements; this checkbox stays open.
  Follow-up 2026-11-18: TMDB API/site terms were retrieved; their retention and
  termination clauses require clarification for permanent database imports. The
  user chose to retain TMDB and contact support; this is not a confirmed violation.
  [Current disposition](provider-agreement-evidence.md#current-disposition-2026-11-18)
  and [unsent provider questions](provider-agreement-questions.md) record lifecycle,
  optimizer, IGDB scope and operator decisions still needed.
  Follow-up 2026-11-17: [scroll-bar provenance](scroll-bar-license-provenance.md)
  is documented through exact artifact/source equivalence, and an image-bound
  application-native source sidecar passed offline rebuild/modified replacement
  verification. [Source-package evidence](native-source-package-verification.md)
  retains remaining OS/other-image source, final audit and publication scope.
- [x] Add a self-hosting quickstart, configuration reference, upgrade/recovery
  instructions, supported versions/limitations and troubleshooting.
- [x] Add [contributor guidance](../CONTRIBUTING.md) and a
  [security policy](../SECURITY.md): setup and applicable checks, private reporting,
  report scope, data protection and self-hosted responsibilities (2026-11-18).
- [x] Enable and verify GitHub private vulnerability reporting. The owner enabled
  it on 2026-11-18, and the subsequent public API check confirmed `enabled=true`.
  No test report was submitted; maintainer notification delivery is unverified.
- [x] Define the release/versioning process and automate verification for the
  supported installation/artifacts.
- [x] Implement release announcement preparation and administrator update awareness as part
  of the first-release versioning work; see the agreed scope below.
- [ ] Review tracked files and release artifacts for secrets, local user data and
  machine-specific configuration before publishing.

### Release announcements and update awareness

The owner agreed on 2026-11-20 to include GitHub release announcements and an
administrator update notice in the release/versioning work. This records scope;
the implementation design was approved by the owner on 2026-11-20.

The approved [implementation design](superpowers/specs/2026-11-20-release-versioning-design.md)
now records version identity, candidate preparation/distribution, manual upgrades,
administrator access and bounded update checks. The
[implementation plan](superpowers/plans/2026-11-20-release-versioning.md) records
the concrete delivery and verification steps. Implementation and local acceptance
are complete on `feat/release-versioning`, pending integration; see the
[verification record](release-versioning-verification.md).

- [x] Embed the installed application version in builds and keep API/web/tools
  artifacts tied to the same release version.
- [ ] Publish versioned GitHub releases with matching artifacts, a readable change
  summary, upgrade instructions and explicit migration requirements. Document how
  operators can subscribe through GitHub's Watch → Custom → Releases controls.
- [x] Add an administrator-only installed/latest stable version display and an
  update-available notice linking to release notes and upgrade instructions.
- [x] Provide an administrator-controlled update check, cached server-side, plus
  a manual “Check for updates” action. Specify the enablement default, check
  interval, external request behavior and failure handling in the implementation
  design. Compare release versions correctly and exclude drafts/prereleases from
  stable update notices; unavailable checks must not block normal application use.
- [x] Verify administrator access, version comparison, cached/manual checks,
  disabled/offline behavior and links to the matching release instructions.

An available upstream release does not mean an instance has installed it. Updates
remain manual through the documented backup/migration/upgrade procedure; this
scope does not include automatic installation. A user-facing “What's new” notice
after an instance upgrades is deferred until after the first release, alongside
the additional product features discussed on 2026-11-20.

Publishing releases, images or repository changes remains a separate authorized
action. Operators choose where to run their instances and must verify their own
backup expiration, journal protection, staff records and ingress configuration.
