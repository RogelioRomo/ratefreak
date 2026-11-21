# Release versioning verification

Implemented and verified locally on 2026-11-20 on `feat/release-versioning`.
The user subsequently authorized committing and merging the feature into `main`
and explicitly requested no further tests. Integration reused the recorded
verification without rerunning tests/build hooks. No release, remote tag, registry
image or persistent-instance upgrade was published or performed.

## Implemented behavior

Root `VERSION` identifies API/web/tools together. Official preparation exports
one tagged commit into an isolated build context; ignored local files cannot
enter images. Source installation archives contain committed files plus explicit
`SOURCE_REVISION` provenance and can be rebuilt nonofficially without Git metadata.

Read-only CI and manual candidate preparation retain exact image archives,
checksums, source installation, notices and public-safe verification summaries.
Explicit finalization resumes reviewed bytes without rebuilding. Distinct
review/final schemas require image-bound inventories, every verification suite,
separate fresh/upgrade acceptance and corresponding-source decisions for every
actual shipped component/version. The native sidecar must match the shipped
package, binary fingerprint and complete locked source profile. These enforce
review inputs and byte identity; they are not automated legal clearance.

Bundle loading refuses conflicting version tags before importing, preserves old
selection/receipts on failed selection writes, selects immutable image IDs and
rejects independent service-image overrides across all Compose profiles. A source
build explicitly selects all local images only after the complete build succeeds.
Both transitions require fresh migration acceptance; old images remain available.

Administrator update checks default to disabled. Enabled checks use the fixed
public GitHub endpoint, shared 24-hour caching, conditional requests, coalescing,
five-minute manual/failure cooldowns and longer upstream retry delays. Invalid,
missing, interrupted or stale results never claim current software. Both endpoints
recheck the live Admin role after a slow check before disclosing status.
The UI shares account-scoped status, conceals denied/inactive sessions, enables
manual checks when their cooldown expires and preserves keyboard focus during
checks. Upgrades remain manual; no ordinary-user inbox event or automatic download
was added.

## Executed checks

| Check | Result and scope |
| --- | --- |
| Backend | 756 passed, zero skipped: 62 unit, 498 PostgreSQL/API integration, 196 provider contracts |
| Frontend | 942 tests across 117 files; lint, typecheck and production build passed after the browser focus fix |
| Python | 90 passed: 22 release, 50 operator/bundle, 18 existing compliance tests |
| Complete verifier | All 15 checks passed in the isolated tagged source clone at `11f4049`, including API Release publish and model consistency; its backend source is unchanged at the final app-image checkpoint `ce5e1ac` |
| Final app images | API/web/tools built from `ce5e1ac` with matching full revision labels and explicitly nonofficial build identity; subsequent changes concern host-side evidence packaging/profile validation and documentation |
| Fresh HTTPS installation | Final app images, fresh disposable volumes, Admin/ordinary accounts, real trusted TLS, secure cookies, CSRF/Origin denial, uploads/privacy/export, disabled update checks, ordinary-user denial and logout denial passed |
| Upgrade | Pinned `9cc808a` source baseline to new image set preserved all recorded table fingerprints, tracking and artwork; old migration acceptance was rejected. Final `ce5e1ac` image transition repeated preservation and explicit acceptance |
| Chromium | Real local CA trust, both themes at 1440/768/390/320px, no horizontal overflow or page exceptions, keyboard action/focus, ordinary-user denial and logout concealment passed |
| Simulated newer release | Deterministic service/API/UI tests and browser interception exercised availability and refresh without a real GitHub release or outbound browser GitHub request |
| Candidate preparation | A local-only `v0.1.0-dev.1` tag in an isolated clone produced an 11-artifact review inventory. A synthetic ignored `.pem` marker was absent from the official web image. Preparation correctly returned nonzero with no installable release manifest because approved corresponding-source materials were absent |
| Finalization | Synthetic image/source/acceptance fixtures exercised the full resume path, producing a separate verified directory while preserving reviewed candidate bytes |
| Recovery/cleanup | Docker export/load retained the inspected image ID. Disposable test services/database volumes were removed; the existing developer instance, local image tags and persistent database were not changed |

The complete verifier and candidate dry run used `11f4049`. Final frontend/browser
proof used `ce5e1ac`. Later host-side packaging, complete-profile and Compose-profile
checks passed their focused/full Python suites; no backend/frontend application
behavior changed after that checkpoint. This is component-scoped evidence, not a
claim that an unpublished final public candidate has completed every release gate.

Private logs, image identities, hashes, screenshots, acceptance and shutdown
receipts are under `.local/release-versioning/` in the main checkout and temporary
`/tmp/ratefreak-*` paths. They are local evidence, not durable published artifacts.
Candidate/Actions exports use allowlisted summaries; raw logs/TRX and local tar
owner metadata are excluded by the final implementation.

## Review and implementation choices

One independent whole-change review identified archived-source context leakage,
mutable-tag/receipt hazards, incomplete artifact/source/upgrade validation, delayed
role revocation, conditional-cache/interrupted-body failures, ambiguous artifact
selection, raw report leakage, cooldown expiry and source-selection issues.
Important findings were resolved with regression coverage. Chromium additionally
caught native disabled-button focus loss; the check control now retains focus
with guarded aria-disabled behavior. No review finding remains deferred.

Execution used the already approved native workflow and coherent verified commit
groups (coarser commit granularity). Candidate review/finalization adds temporary
artifact storage to preserve exact image identity. Bundle selection uses less
human-readable immutable image references to prevent cross-instance tag races.
Source obligations remain component-by-component maintainer decisions, adding
review effort instead of claiming automated license clearance.

## Remaining public-release gates

No hosted GitHub Actions run, real subscriber notification delivery or public
release is claimed. The current proof tag exists only in the isolated local clone.
The feature branch adds no application schema migration. First public release
still requires reviewed source delivery/provider clearance, final matching
artifacts and the other gates in STATUS, followed by publication authorization.
Source-material artifact production remains part of the separate outstanding
source-delivery work; the workflow validates its expected provenance when supplied.
A successor release must use the preceding published stable bundle as its upgrade
baseline. A user-facing post-upgrade “What's new” experience stays deferred.
