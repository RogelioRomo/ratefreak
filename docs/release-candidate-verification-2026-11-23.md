# Local release candidate verification — 2026-11-23

The current application revision has a locally finalized development candidate
with matching source delivery and fresh-install/upgrade/browser acceptance.
This advances the source/artifact and exact-candidate work recorded in STATUS;
it is not public-release clearance or a production deployment.

Subsequent [vulnerability review](release-security-review-2026-11-23.md) found
runtime/tool/OS advisories requiring remediation and residual review. The current
candidate does not pass the security release gate; the acceptance/source evidence
below retains its engineering scope.

## Identity and retained artifacts

| Item | Value |
| --- | --- |
| Version/platform | `0.1.0-dev.1`, Linux amd64 |
| Source revision | `f19a90784abff9ea5b0d787d7fc3a4cb85ac2a4b` |
| Source selection | Clean isolated local clone with a local-only `v0.1.0-dev.1` tag; main checkout/tag state unchanged |
| Preparation | Existing release verifier and production Dockerfiles; one immutable source export |
| Finalization | Existing `prepare.py --resume`, reusing the reviewed image archives without rebuilding them |
| Final bundle | `.local/release-candidate-2026-11-23/candidate/release/` |
| Source artifact | `sources.tar.gz`, 1,255,738,039 bytes |
| Source artifact SHA-256 | `a17e690ac4b3b44aa2da8d19ac407e72e2570c24639ede8e311b037bc0e1bf37` |

The retained [release manifest](release-evidence/2026-11-23-candidate/release-manifest.json)
records all 15 delivered file hashes and exact API/web/tools image identities.
The workflow's `verified` stage records its engineering checks; it does not
establish provider eligibility, vulnerability clearance or publication permission.
The local tag exercises official-build identity rules without publishing a tag
or release remotely. Stable-release version selection remains separate.

Large image/source archives, screenshots and scratch audit scripts remain local.
Checked-in evidence is a durable record of their identities and results, not a
replacement for delivering the actual corresponding-source materials.

## Source and notice reconciliation

The [coverage record](release-evidence/2026-11-23-candidate/coverage.json) maps
**719 image/component entries**: API 162, tools 199 and web 358. These include
393 installed OS binary-package entries, application package notice supersets,
copied runtime tools, frameworks and web native components. Repeated components
in different images are separate entries, not 719 unique upstream projects.

- All current OS source package/version pairs match the prepared source archive.
  All **562 source inputs** were read back and hash-checked. Representative
  Ubuntu/Debian ACL and PGDG postgresql-common sources unpacked successfully with
  their distribution build rules. Existing authenticated APT acquisition evidence
  is retained; source signatures were not independently reverified during unpacking.
- Application package metadata and complete notice bundles match the prior
  inspected payloads. Runtime component keys and native path/hash/size fingerprints
  also match. ASLR addresses in `ldd` output are not binary fingerprints.
- The web native sidecar is newly bound to this exact image and validates all
  **406 locked source/build inputs**. The earlier offline native rebuild and
  modified-library replacement proof retains its recorded scope; it was not rerun.
- The migration executable's **104 embedded entries and 36 dependency-library
  records** contain no uninventoried NuGet version. Embedded Skia and the .NET
  apphost match the reviewed payload/framework commit.
- A separate sharp-addon review found its Node-addon-api collaborator notice was
  absent from the application notice bundle. The matching source-material set
  now supplies that original notice and conservative source/header inputs beside
  the binaries. Those delivery files must accompany any distribution; the image
  itself was not changed to include the additional notice.
- Delivery copies of the prepared .NET/Skia notice archives normalize local tar
  ownership metadata. Every original member's bytes are unchanged and verified.

Read the [source review and conditions](release-evidence/2026-11-23-candidate/release-source-review.md),
[source reconciliation](release-evidence/2026-11-23-candidate/source-reconciliation.json),
[source validator result](release-evidence/2026-11-23-candidate/source-validation.json)
and [OS extraction evidence](release-evidence/2026-11-23-candidate/os-source-validation.json).
Filenames in the coverage record refer to the actual source delivery bundle,
not to standalone downloadable archives in this documentation directory.

An [independent read-only review](release-evidence/2026-11-23-candidate/independent-review.json)
found no issue blocking local candidate finalization. It checked the source
archive against tracked Git blob bytes, component/material mappings, OS/native
input hashes, migration fingerprint and portable notice contents. Its scope
does not include exhaustive legal sufficiency or proof that no secret exists.

## Artifact and privacy inspection

All **52 distributed image layers**, including lower-layer files hidden by later
changes, were inspected: **24,708 regular-file entries**, about 2.24 GB of file
content. OCI identity/configuration chains and source/version labels match.
No additional OS package/version occurs only in a lower layer.

The installed Next package's notices include MPL texts for optional OG/satori
components. Those implementations and yoga/resvg WASM are absent from every
distributed layer. Shipped MIT Next wrappers retain dynamic import references
to that absent optional implementation; the inspected browser bundles contain
none of those markers. This finding does not clear a future OG feature.

The 1,158 tracked source files pass the release path policy. Seven configured
private values were compared without exposing their contents; none was found
in tracked source or the distributed image layers/configuration. Token/key
heuristics found only unchanged inherited base-image cryptographic content and
PostgreSQL's public default snakeoil key. The tools entrypoint does not run that
PostgreSQL server or select its certificate. Upstream attribution emails,
synthetic fixtures and two historical workspace-path references were reviewed
separately from active private configuration.

See the [layer audit summary](release-evidence/2026-11-23-candidate/layer-audit-summary.json)
and [tracked-source audit](release-evidence/2026-11-23-candidate/tracked-source-audit.json).
These are scoped integrity/privacy checks, not a universal secret-detection claim.

## Exact-candidate acceptance

| Check | Executed result |
| --- | --- |
| Complete release verifier | All 15 checks passed: restores, backend suites, operator/release/compliance suites, frontend lint/typecheck/tests/build, API Release publish and model consistency |
| Backend execution | 756 passed, zero skipped: 62 unit, 196 provider contracts, 498 PostgreSQL/API integration |
| Fresh HTTPS installation | Exact candidate images; secure HttpOnly cookies, CSRF/foreign-Origin rejection, forged-header handling, ordinary registration, decoded artwork/privacy, account ZIP export and update-access/logout checks passed |
| Upgrade | Existing `9cc808a` synthetic baseline to exact candidate images; database fingerprints and artwork preserved; stale image-set migration acceptance rejected and explicit new migration acceptance passed |
| Chromium | Trusted local TLS with certificate-error bypass disabled; 25 page journeys at 1440/768/390/320px; admin routes and update controls in light/dark themes, ordinary routes in the default theme |
| Update browser behavior | Actual installed version/revision and disabled state, intercepted newer-release scenario, keyboard manual check/focus, ordinary denial and logout concealment passed; no browser GitHub requests |

The [machine-readable summary](release-evidence/2026-11-23-candidate/summary.json)
retains verifier and fresh/upgrade results. [Browser evidence](release-evidence/2026-11-23-candidate/browser-results.json)
lists the exact routes, themes and widths. Optional live Google/GitHub callbacks,
real provider imports and real upstream release/subscriber notifications were
not exercised by this candidate acceptance.

All disposable acceptance services and volumes were removed after checks.
The developer instance, persistent database, provider configuration and data were
not modified. Previous development-candidate image IDs remain retained under
dated backup tags; local `0.1.0-dev.1` image tags now select this candidate.

## Remaining before public release

Provider clarification and operator eligibility remain open, including TMDB's
pending retention/lifecycle clarification, IGDB scope and Open Library cover
optimization. No pending question is treated as a confirmed violation or clearance.

Vulnerability remediation/residual review, the stable version/final source freeze, remaining
reference/operator operational gates, maintainer security-notification delivery,
and authorized matching binary/source publication remain required. Stable identity
or source/payload changes require refreshed relevant candidate evidence.

The unchanged font binaries and original OFL notices are verified, but their
original download/subsetting history was not independently reconstructed. The
sharp header material is the declared minimum dependency version, not a claim of
exact upstream prebuild resolution or addon binary reproduction. These provenance
limits remain explicit for the final distribution review.
