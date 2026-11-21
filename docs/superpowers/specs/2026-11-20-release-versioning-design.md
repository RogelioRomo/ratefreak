# Release versioning and administrator update awareness

Date: 2026-11-20. Status: approved by the owner on 2026-11-20; implementation pending.

## Intent and agreed scope

Prepare a repeatable self-hosted RateFreak release and let instance administrators
learn when a newer stable version is published. The owner selected GitHub release
announcements plus an administrator update notice on 2026-11-20. Updates remain
manual. The release/versioning task does not clear the independent provider,
redistribution or operational release gates recorded in `docs/STATUS.md`.

The agent proposed the defaults below; the owner approved the design on
2026-11-20. Execution is tracked in the
[implementation plan](../plans/2026-11-20-release-versioning.md).

## Current implementation and constraints

- There is no `.github/` workflow directory. `web/package.json` declares `0.1.0`;
  the backend has no shared explicit application version.
- `deploy/self-host/ratefreak.py build` builds API/web/tools images tagged `:local`.
  Compose uses these names. Migration acceptance records all three Docker image
  IDs and an override fingerprint; start rejects changes until explicit acceptance.
- Production Dockerfiles collect notices and runtime evidence. Native-source
  packaging has separate verification and outstanding final-artifact obligations.
- The API owns authorization and integration behavior. Existing `admin` policy
  checks current database roles; `/api/v1/admin` responses are already no-store.
- The web application has an authenticated moderation area and admin-only staff
  management. Account transitions already clear private cached state.
- The supported reference topology is single-host Linux amd64. Other architectures
  and multiple replicas must not acquire a supported claim from this work.

## Approaches

The owner chose GitHub releases plus in-app administrator update awareness over
GitHub subscriptions alone. This reaches operators inside their instances while
preserving GitHub subscriptions as a separate announcement channel. A user-facing
post-upgrade “What's new” experience was deferred.

Use GitHub's published latest stable release as the update source. A separately
hosted update manifest would add another publication and hosting dependency;
direct checks from each browser would multiply requests and disclose browser
network addresses. A shared API-side check fits the existing architecture.

For initial distribution, propose downloadable Linux amd64 image archives and an
installation bundle on GitHub Releases. This extends the existing local-image
workflow without introducing registry credentials or mutable registry tags.
Publishing GHCR images can be added separately; it is not required for this slice.

## Version contract

1. Add a root `VERSION` as the authoritative version. Use SemVer 2.0.0 and Git tags
   `v<version>`. Begin development at `0.1.0-dev.1`; `0.1.0` is the proposed first
   public stable-channel release, subject to the existing release gates. Here
   stable channel means a published release without a prerelease suffix, not a
   promise of a stable 1.x API.
2. Document the compatibility surface: HTTP contracts, documented instance
   configuration, operator commands and supported upgrade paths. Before 1.0,
   incompatible changes increment the minor version; fixes increment patch.
   Every release states whether database/configuration changes are required.
3. Embed version and source revision into API/web/tools at build time. Do not let
   runtime environment variables claim a different installed version. Generate
   the web build metadata from the same source instead of independently bumping
   `package.json`; add consistency validation for its base version.
4. Official candidates require tag, VERSION and source revision agreement and a
   clean tracked source tree. Developer builds are visibly development builds and
   never claim an official release. Preserve enough identity to distinguish
   untagged or modified builds from published artifacts.
5. Label images with version/revision/source and include those values in a release
   manifest with the image identities. Do not replace an already published version
   with different artifacts. All services must use one candidate manifest.

## Verification and release preparation

Add a read-only GitHub Actions CI workflow for pull requests and default-branch
changes. Run backend suites with real disposable PostgreSQL (no silent skips),
frontend tests/lint/typecheck/build, operator/compliance tests and version checks.
Pin third-party actions to reviewed commit SHAs. Fork pull requests get no secrets
or write token. Consult version-matched Next.js documentation before UI changes.

Add a manually dispatched candidate-preparation workflow accepting an existing
tag. Resolve the tag once to a commit and perform all work against that commit.
Reject invalid tags and mismatched versions. Run verification and build the three
Linux amd64 images. Exercise disposable HTTPS installation and update-awareness
acceptance; retain sanitized reports with the candidate identity. Only the
supported topology is represented as tested.

Generate a candidate directory containing:

- API/web/tools image archives, checksums and manifest (version, revision, image
  IDs, platform, schema/migration identity and verification references).
- A tracked-source installation bundle with the operator tools, Compose files,
  documentation and license; exclude local/untracked files, secrets and user data.
- Matching collected notices and the source-delivery materials required for
  these exact images. Reuse the compliance tooling; do not treat the existing
  web native-source sidecar as covering all images or OS packages.
- Maintainer-written release notes explaining changes, upgrade prerequisites,
  migrations/config changes, backup procedure, recovery limits and known issues.
  Automated commit lists may supplement this, not substitute for it.

Preparation uploads Actions artifacts for review under repository access rules;
these artifacts must contain no private instance data or secrets. It
does not push tags, publish a release or push images. Missing compliance material
is a blocking incomplete-candidate result, not a green release-ready claim. The
workflow may retain diagnostic build artifacts, clearly marked incomplete.

Document a separate maintainer publication procedure. It verifies checksums and
the exact candidate, checks the outstanding release gates and obtains the owner's
publication authorization before creating/publishing a GitHub release with that
tag and those bytes. Do not rebuild between acceptance and publication. GitHub
release publication is the announcement event; document Watch → Custom → Releases.
Prereleases must be marked accordingly. No automatic publication on merge or tag.

## Installation and upgrade integration

Keep source builds supported and retain their existing `:local` behavior. Add an
explicit operator command to load and validate a downloaded release bundle with
writers stopped. Validate hashes, manifest/image version and revision agreement,
image IDs and architecture before accepting the bundle. Use versioned local image
tags selected together by the operator tool, with no independent per-service
version overrides. Normal start never pulls or upgrades implicitly.

Extend image selection in Compose and the operator tool together. Preserve the
current migration acceptance guard for both source builds and downloaded images;
loading/selecting another candidate must not make an old acceptance record valid.
Retain previous image identities and artifacts for reviewed recovery. Follow the
existing stop → backup → prepare/migrate → start procedure. No persistent local
instance or database changes are authorized by preparing this implementation.

## Administrator update checks

### Configuration and network behavior

Propose `Updates:Enabled=false` by default, controlled by the operator's deployment
configuration, passed explicitly through the reference Compose/entrypoint path.
Administrators see whether checks are enabled and how the operator enables them;
there is no database-backed settings toggle or schema migration in this slice.
When disabled, neither automatic nor manual actions contact GitHub.

When enabled, lazily check on an administrator status request if the shared cache
is older than 24 hours. No browser contacts GitHub and no startup dependency is
introduced. Manual checks can bypass the 24-hour success cache but share a global
five-minute minimum interval, request coalescing and failure backoff. No background
polling is needed when nobody is using administration.

Use a named bounded HTTP client to the fixed URL
`https://api.github.com/repos/RogelioRomo/ratefreak/releases/latest`, without
credentials, redirects or caller-supplied URLs. Set GitHub's documented media/API
headers and a product/version User-Agent; use conditional ETags when available.
The request reveals the server's network address and product version, never user
identifiers, instance origin, account data or provider credentials. Document this
before enablement. Proposed bounds: ten-second timeout, 256 KiB response cap,
five-minute failure cooldown, honoring a longer upstream retry/rate-limit delay.

Validate published, non-draft, non-prerelease strict SemVer tags. Compare semantic
versions numerically, ignoring build metadata for precedence. Construct release
links from the fixed repository and validated tag; do not trust arbitrary URLs or
render upstream release HTML. Treat 404 as no published stable release, not proof
that the instance is current. Handle network errors, rate limits and malformed
responses without failing normal application use.

Keep the cache process-local for the supported single API replica. Cache loss
after restart is acceptable; no persistence or migration is required. A failed
refresh retains a last successful result with its timestamp and marks it stale.
Never replace a failed check with “up to date.” Development/custom builds display
their identity and link to releases but do not claim a trustworthy upgrade verdict.

### API and user interface

Add `GET /api/v1/admin/updates` for installed identity, configuration status,
cached latest stable version, check timestamps and bounded status/cooldown fields.
An enabled GET may perform the lazy cached check. Add
`POST /api/v1/admin/updates/check` for explicit refresh, protected by existing
CSRF/Origin handling and admin rate limiting. Both require the current `admin`
database-role policy and return no-store responses. Ordinary users and moderators
cannot read status or trigger an upstream request.

Use explicit states: disabled, unchecked, checking, current, update available,
unavailable, no published stable release and development/custom build. Include
last-success and last-attempt timestamps and manual-check availability so errors
and cooldown are clear. Failure responses remain sanitized.

Add an administrator-only Updates entry/page within the existing administration
area, reachable alongside Staff management. Show installed version, latest stable
version, last checked time and “Check for updates.” When newer, show the release
notes/upgrade links and explain that the operator installs the update. Include a
small update notice in the admin area using the same cached result. Reuse current
UI conventions, accessibility and session boundaries; conceal cached private
results on revoked access, logout/account changes or session-check failure.
No notification-inbox events, ordinary-user banners, automatic downloads or
installation buttons are included.

## Proof required

- Version parsing/ordering: 1.9 versus 1.10, prerelease ordering, build metadata,
  invalid/oversized values, development identity and tag/version mismatch.
- Transport: disabled makes zero requests, success/ETag/304, missing release,
  malformed/oversized/redirect responses, timeout/cancellation, rate limits,
  failure cooldown, stale results and concurrent request coalescing. Use fake
  transport/time; no test relies on a real release being available.
- API: anonymous/ordinary/moderator access denied before any outbound work,
  current-role revocation, no-store, CSRF and manual-check rate limiting.
- UI: admin-only navigation, all status/error states, manual-check feedback,
  keyboard/focus/mobile behavior and private-cache clearing across account changes.
- Packaging: clean source selection, no local data in bundles, matching metadata,
  checksums and image IDs; stopped-writer guard; mismatched/mixed candidates rejected;
  migration acceptance invalidated across candidates; previous artifacts retained.
- CI: inspect workflow permissions and trigger boundaries; run local verification
  commands and candidate preparation where available. Workflow files passing local
  checks is not evidence of a successful hosted Actions run.
- Disposable HTTPS acceptance for ordinary/admin accounts, fresh installation,
  upgrade without data loss, checks-disabled operation and simulated newer release.
  No real GitHub release or real instance upgrade is needed to prove the feature.

For the first candidate, use the existing committed pre-feature revision
`9cc808a` as a pinned source-build upgrade baseline, with synthetic ordinary/admin
accounts and catalog/library/artwork data in a disposable instance. This checks
transition from the current local-image packaging to release-bundle selection;
existing migration fixtures separately cover schema upgrades. Record exact
baseline/candidate image identities and data-preservation evidence. For later
releases, use the preceding published stable bundle as the upgrade baseline.
Never use the persistent developer instance as an acceptance fixture.

Implementation clarification, 2026-11-20: source review binds to built image IDs.
Preparation therefore retains exact incomplete candidate archives for review;
explicit finalization resumes those bytes without rebuilding and writes the final
release separately. This avoids invalidating source evidence with a fresh build. Review hardening
uses immutable image IDs for selected bundle services, exports official build
contexts from tagged source only, validates separate review/final schemas and
component-bound source evidence, and publishes only allowlisted verification
summaries. Raw diagnostic logs and test results remain local.

## Remaining builder choices and delivery boundary

The builder chooses file/module boundaries, workflow job splitting, test fixture
placement and accessible layout details consistent with repository conventions.
Source-delivery gaps must be recorded and kept blocking publication; this task
does not silently expand into resolving every legal or operator-specific gate.

Implementation follows an approved design and a concrete execution plan. Record
verification evidence and update STATUS when implemented. No public release,
remote push, tag publication, outbound support message or persistent instance
migration is authorized by this design.

## Primary references

- [Semantic Versioning 2.0.0](https://semver.org/)
- [GitHub Releases API](https://docs.github.com/en/rest/releases/releases#get-the-latest-release)
- [GitHub release subscriptions](https://docs.github.com/en/repositories/releasing-projects-on-github/about-releases)
- [GitHub Actions workflow permissions](https://docs.github.com/en/actions/reference/workflows-and-actions/workflow-syntax#permissions)

Consulted 2026-11-20; verify version-sensitive API and Actions details when
implementing.

Builder review, 2026-11-20: no important unanswered questions after specifying the
first-release upgrade baseline. The owner subsequently approved the design.
