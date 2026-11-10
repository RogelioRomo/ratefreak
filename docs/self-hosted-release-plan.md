# Self-hosted open-source release plan

Draft updated 2026-11-10 following the user's clarification. Product direction is
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

- [ ] Choose an open-source license with the project owner and add its license file.
- [ ] Review dependency/image redistribution and metadata/artwork provider terms,
  required attribution and separately supplied provider credentials.
- [x] Add a self-hosting quickstart, configuration reference, upgrade/recovery
  instructions, supported versions/limitations and troubleshooting.
- [ ] Add contribution and security-reporting guidance, release/versioning process
  and automated verification for the supported installation/artifacts.
- [ ] Review tracked files and release artifacts for secrets, local user data and
  machine-specific configuration before publishing.

Publishing releases, images or repository changes remains a separate authorized
action. Operators choose where to run their instances and must verify their own
backup expiration, journal protection, staff records and ingress configuration.
