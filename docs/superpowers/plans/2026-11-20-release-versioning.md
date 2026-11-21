# Release Versioning and Update Awareness Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans (recommended here) or superpowers:subagent-driven-development to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Prepare verifiable versioned self-hosted release bundles and notify administrators of newer published stable releases without automatic upgrades.

**Architecture:** One root version and build revision identify API/web/tools together. Python tooling builds and validates immutable candidate bundles while preserving existing migration acceptance. The .NET API owns optional cached GitHub checks; the existing authenticated moderation area displays administrator-only update status.

**Tech Stack:** .NET 10, PostgreSQL 17, Next.js 16.4.6, React 19, TanStack Query, Python 3, Docker Compose, GitHub Actions.

**Spec:** [Approved design](../specs/2026-11-20-release-versioning-design.md), approved by the owner on 2026-11-20.

## Global Constraints

- Root `VERSION` starts at `0.1.0-dev.1`; first public stable-channel target is `0.1.0`; official tags use `v<version>` and strict SemVer 2.0.0.
- API/web/tools share a version, revision and release manifest; runtime environment variables cannot override embedded installed identity.
- Supported packaging is single-host Linux amd64. Source builds retain `:local` behavior. Normal start never downloads or upgrades implicitly.
- Preserve current image-ID/override-bound migration acceptance, stopped-writer guards and retained recovery artifacts.
- `Updates:Enabled=false` by default. Enabled status reads lazily refresh a 24-hour process-wide cache; manual checks share a five-minute minimum interval.
- Fixed upstream: `https://api.github.com/repos/RogelioRomo/ratefreak/releases/latest`; no credentials, redirects, arbitrary URLs or browser requests to GitHub.
- Transport bounds: ten-second timeout, 256 KiB body cap, five-minute failure cooldown and longer upstream retry/rate-limit delays.
- Current database `admin` authorization, no-store responses, CSRF-protected manual refresh, account-transition concealment and sanitized errors are required.
- No schema migration, persistent developer-instance change, remote push, tag/release publication, registry push or automatic upgrade.
- Missing compliance evidence blocks a release-ready candidate. Workflow preparation and verification do not close independent release gates.
- Read version-matched Next.js documentation under `web/node_modules/next/dist/docs/` before frontend edits; preserve existing visual conventions.

## Review Focus

- Retrying a partial bundle load must never select a mixed release or make an old migration receipt valid (Task 4).
- Deleted/prerelease-only upstream releases, cached ETags and upstream failures must never produce a false “up to date” result (Task 2).
- Admin revocation/account changes during a slow update check must conceal private UI and prevent cached results crossing identities (Tasks 3 and 5).
- Untrusted candidate tags, tracked secret files and archive members must not inject commands, expose local data or escape extraction paths (Tasks 1, 4 and 6).
- Published artifacts and verification evidence must remain tied to the exact candidate bytes, including when workflow retries occur (Tasks 6 and 7).

---

## File and interface map

- `VERSION`, `Directory.Build.props`, `Directory.Build.targets`: authoritative version and embedded .NET identity.
- `deploy/release/versioning.py`, `prepare.py`, `bundle.py`: source/build identity, candidate preparation and artifact validation, respectively.
- `deploy/self-host/release_bundle.py`: operator-side validated image loading/selection; keep independent of the build checkout.
- `deploy/self-host/ratefreak.py`, Compose and Dockerfiles: consume the shared identity/selection without weakening lifecycle guards.
- `src/RateFreak.Application/Updates/Updates.cs`: status DTO and `IUpdateService` contract.
- `src/RateFreak.Infrastructure/Updates/`: embedded identity adapter, SemVer comparison, bounded GitHub transport and shared cache.
- `src/RateFreak.Api/Endpoints/UpdateEndpoints.cs`: authorized HTTP boundary.
- `web/features/updates/`: typed API access, update status and administrator notice; reuse moderation session/access/query helpers.
- `.github/workflows/ci.yml`, `release-candidate.yml`: verification and manually dispatched candidate preparation with read-only repository permissions.
- `docs/releasing.md`, `docs/release-versioning-verification.md`: maintainer procedure and actual evidence/limits.

### Task 1: Embed trustworthy shared build identity

**Files:** Create `VERSION`, `Directory.Build.targets`, `deploy/release/versioning.py`, `deploy/release/test_versioning.py`, `web/scripts/build-identity.mjs`; modify `Directory.Build.props`, `web/package.json`, `web/next.config.ts`, the three `deploy/self-host/*.Dockerfile` files and `deploy/self-host/ratefreak.py`.

**Interfaces:** Python `read_identity(repository: Path, tag: str | None = None) -> dict` returns `{version, revision, official, dirty}`. `validate_version(value: str) -> str` rejects noncanonical or oversized SemVer. API assembly metadata and web-generated metadata expose the same version/revision/official fields. Official candidate preparation supplies identity from a validated clean tag; developer builds remain visibly nonofficial.

- [ ] Write failing `VersionIdentityTests`: VERSION/package base mismatch rejected; `v0.1.0` requires `0.1.0`; dirty/untagged builds nonofficial; nonmatching/invalid tags and shell metacharacters rejected; no runtime variable overrides assembly metadata. Assert full source SHA identity.
- [ ] Run `python3 -m unittest discover -s deploy/release -p test_versioning.py -v`; expect the new behavior to fail before implementation.
- [ ] Implement identity reading and strict version validation, root version and MSBuild embedding. Make local .NET builds work without Python/network dependencies; Docker source contexts receive generated identity because `.git` is excluded. Any official build must receive validated preparation metadata, not infer official status from a release-looking version string alone.
- [ ] Add web generation before dev/build; derive identity from root metadata, check package base version, and provide metadata in standalone output. Use server-side generated identity rather than `NEXT_PUBLIC_*` runtime settings. Docker/installation bundles must include every build input explicitly.
- [ ] Add matching OCI labels to API/web/tools; preserve source-build local image tags. Add a unit test reading compiled API identity via assembly metadata and a frontend test reading generated metadata.
- [ ] Run Python identity tests, `dotnet test tests/RateFreak.UnitTests`, `pnpm --dir web test` and `pnpm --dir web build`; expect all relevant tests/builds to pass. Check Dockerfile copy paths and package-lock consistency.
- [ ] Commit the coherent identity change on the implementation branch after verification.

### Task 2: Implement bounded stable-release checks and cache

**Files:** Create `src/RateFreak.Application/Updates/Updates.cs`, `src/RateFreak.Infrastructure/Updates/{BuildIdentity,SemanticVersion,UpdateOptions,GitHubReleaseClient,UpdateService}.cs`, `tests/RateFreak.UnitTests/{SemanticVersionTests,UpdateServiceTests}.cs`; modify `src/RateFreak.Infrastructure/DependencyInjection.cs`.

**Interfaces:** `IUpdateService.GetAsync(bool refresh, CancellationToken cancellationToken) -> Task<UpdateStatus>`. `UpdateStatus` includes `Installed` (`Version`, `Revision`, `Official`), `Enabled`, `State`, nullable `LatestVersion`, `ReleaseUrl`, `LastAttemptAt`, `LastSuccessAt`, `NextCheckAt`, `Stale` and `CanCheck`. Lowercase wire states: `disabled`, `unchecked`, `checking`, `current`, `available`, `unavailable`, `no-release`, `development`. `SemanticVersion.TryParse(string, out SemanticVersion)` and `CompareTo` implement precedence. Inject `TimeProvider` and a replaceable HTTP handler for deterministic tests.

- [ ] Write failing tests asserting `1.10.0 > 1.9.0`, `1.0.0-rc.10 > rc.2`, `1.0.0 > rc.10`, build metadata does not affect precedence, and invalid/leading-zero/oversized tags are rejected.
- [ ] Write failing transport/cache tests: disabled zero requests, untagged development verdict, 24-hour reuse, manual five-minute cooldown, coalesced concurrent calls, ETag/304, 404 clears obsolete latest result, malformed/prerelease/draft/unpublished data rejected, redirect refused, 256 KiB cap, timeout and caller cancellation, sanitized failure with stale last success, and upstream retry delay beyond five minutes. Assert 304 cannot manufacture success without cached content and failures never report current.
- [ ] Run `dotnet test tests/RateFreak.UnitTests --filter 'FullyQualifiedName~SemanticVersionTests|FullyQualifiedName~UpdateServiceTests'`; expect failures for missing behavior.
- [ ] Implement named fixed-origin client and shared singleton cache with bounded request coalescing. Cancel an individual wait without corrupting a shared refresh; bound upstream work independently. Validate headers/timestamps/tag before updating cache. Construct links locally and avoid logging payloads or credentials.
- [ ] Register options/client/service with disabled defaults. Read official installed identity from embedded metadata; no build or GitHub availability dependency at API startup.
- [ ] Run the focused command again, then `dotnet test tests/RateFreak.UnitTests`; expect all tests to pass. Commit the update-service change.

### Task 3: Add administrator HTTP endpoints and deployment configuration

**Files:** Create `src/RateFreak.Api/Endpoints/UpdateEndpoints.cs`, `tests/RateFreak.IntegrationTests/UpdateEndpointTests.cs`; modify `src/RateFreak.Api/Program.cs`, `src/RateFreak.Api/Security/ApiSecurity.cs`, `src/RateFreak.Api/appsettings.json`, `deploy/self-host/compose.yaml`, `deploy/self-host/instance.env.example`, `.env.example`, `deploy/self-host/test_operator.py`.

**Interfaces:** GET `/api/v1/admin/updates` calls `GetAsync(false, ct)`; POST `/api/v1/admin/updates/check` calls `GetAsync(true, ct)`. Both return `UpdateStatus` JSON with no-store, require existing current-role `admin` policy, and use read/manual-check rate policies. POST requires existing CSRF filter. Deployment exposes `Updates__Enabled` explicitly, default false; existing entrypoint preserves this setting without introducing a new secret or rewriting instance configuration.

- [ ] Write failing API tests for 401 anonymous, 403 ordinary/moderator, granted/revoked administrator, no outbound call before authorization, disabled/manual result, no-store, CSRF/foreign-Origin rejection and throttled manual checks. Verify real PostgreSQL fixtures and role changes rather than only mocked claims.
- [ ] Write configuration tests proving Compose passes false by default and operator-enabled true survives container environment loading; neither state needs a database migration.
- [ ] Run `dotnet test tests/RateFreak.IntegrationTests --filter FullyQualifiedName~UpdateEndpointTests` and `python3 -m unittest discover -s deploy/self-host -p test_operator.py`; expect new assertions to fail.
- [ ] Implement endpoint mapping, dedicated manual-check rate limit and configuration/documentation examples. Keep upstream cooldown distinct from per-account HTTP abuse limits.
- [ ] Run those commands again and `dotnet build RateFreak.sln`; expect success, no skipped PostgreSQL tests. Commit the API/configuration change.

### Task 4: Validate, load and select one complete release bundle

**Files:** Create `deploy/self-host/release_bundle.py`, `deploy/self-host/test_release_bundle.py`; modify `deploy/self-host/ratefreak.py`, `deploy/self-host/compose.yaml`, `deploy/self-host/test_operator.py`, `docs/self-hosting.md`.

**Interfaces:** `validate_bundle(directory: Path) -> dict` validates manifest/checksums with no Docker mutation. `load_bundle(directory: Path, inspect_image, load_image) -> dict` validates, imports, verifies all three loaded identities and returns selection only after every check passes. Add `load-release --bundle <directory>` to the existing instance CLI. Persist selection as private `release-selection.json` only after stopped-writer and load verification. Existing source-build selection remains the default; `release_images()` and Compose must use the same selected image set for all lifecycle commands.

- [ ] Write failing tests for missing/hash-mismatched files, wrong platform/version/revision/labels, mixed image IDs, symlinks/traversal, duplicate JSON keys, malicious Docker archive members and unsupported manifest formats. Assert invalid input is refused before import where detectable and always before selection.
- [ ] Write failing lifecycle tests for active writers, partial import failure/retry, retained previous images, interrupted selection writes, new selection invalidating old migration acceptance, and identical-source-build behavior. A failed load never changes selection or an existing acceptance receipt.
- [ ] Run `python3 -m unittest discover -s deploy/self-host -p 'test_*.py'`; expect new tests to fail.
- [ ] Implement strict bundle validation, bounded manifest reading and archive-member checks. Load only manifest-declared files; verify immutable image IDs/labels after Docker import. Select versioned tags as one unit using atomic private-file writes. Do not extract untrusted archives with an unrestricted extract-all operation.
- [ ] Extend Compose selection and image-ID inspection together. Preserve explicit migration/replay/restore receipts and override fingerprints. Do not delete old images or alter persistent developer data.
- [ ] Run operator/bundle suites; expect all pass. Document downloaded-bundle and source-build upgrade paths, provenance verification and matching-version recovery. Commit the installation change.

### Task 5: Add private administrator Updates UI

**Files:** Create `web/features/updates/interfaces/update-status.ts`, `api/updates.ts`, `api/updates.test.ts`, `components/updates-screen.tsx`, `components/update-notice.tsx`, `components/updates-screen.test.tsx`, `components/update-notice.test.tsx`, and `web/app/moderation/updates/page.tsx`; modify `web/features/moderation/components/{staff-nav,staff-boundary}.tsx` and relevant navigation tests.

**Interfaces:** `getUpdates(signal?: AbortSignal): Promise<UpdateStatus>` and `checkUpdates(signal?: AbortSignal): Promise<UpdateStatus>` use existing same-origin `apiRequest`. Reuse account-scoped `useStaffQuery` with admin scope and existing access-store invalidation. The page/notice share one updates query; manual results update that query without creating social notifications.

- [ ] Read applicable `web/AGENTS.md` and version-matched Next.js routing/data-fetching documentation. Read the relevant UI skill before adding components; follow the current moderation layout and controls.
- [ ] Write failing tests for installed/latest identity, disabled instructions, manual loading/cooldown, current/available/no-release/development/unavailable/stale states, sanitized errors and fixed release links. Assert keyboard-operable controls, announced feedback and readable timestamps without assuming a fixed locale.
- [ ] Write failing tests for ordinary/moderator concealment, revoked admin on GET/POST, account change/logout/session error during a delayed check, stale query concealment and no browser GitHub request. Update navigation tests to verify Updates is available only after admin access is established; do not add general-user navigation.
- [ ] Run `pnpm --dir web test features/updates features/moderation`; expect new tests to fail.
- [ ] Implement the page, shared notice and API adapter using existing staff/session helpers. Add “Check for updates,” installed/latest versions and notes/upgrade links; explain operator-managed installation and deployment-controlled enablement. Preserve focus during checks and use existing responsive styles.
- [ ] Run focused tests, `pnpm --dir web lint`, `pnpm --dir web typecheck`, then the full frontend tests/build; expect all pass. Commit the UI change.

### Task 6: Prepare exact versioned candidate artifacts

**Files:** Create `deploy/release/{bundle,prepare}.py`, `deploy/release/{test_bundle,test_prepare}.py`, `docs/releases/README.md`, `docs/releases/TEMPLATE.md`, `docs/releasing.md`; modify Dockerfiles/build metadata interfaces only where Task 1 requires candidate integration.

**Interfaces:** `prepare.py --tag v<version> --output <new-directory>` resolves one clean committed tag, runs verification and builds images from that source. Output `release-manifest.json` format 1 describes version, full revision, Linux/amd64, image IDs/tags/labels/archive filenames, migration identity and file SHA-256/size inventory. Output includes image archives, tracked-source installation archive, notes, notices, matching source materials and sanitized verification reports. `bundle.py verify <directory>` is read-only and uses Task 4's validation contract. Never overwrite an existing candidate directory/version.

- [ ] Write failing fixture-based tests for immutable source selection, dirty/tag mismatch, hostile tag input, missing/unsafe notes, tracked secret/user-data paths, untracked exclusions, archive metadata, checksum tampering, revision/image mismatch and reruns refusing replacement. Assert Git refs enter subprocess argument arrays, never shell interpolation.
- [ ] Write failing tests that absent OS/API/tools/native coverage yields a nonzero incomplete-candidate result, even if image builds pass. Verify compliance manifests bind to each exact image ID and every required source artifact is inventoried. Do not add a boolean “compliant” flag that substitutes for image-bound evidence.
- [ ] Run `python3 -m unittest discover -s deploy/release -p 'test_*.py'`; expect new tests to fail.
- [ ] Implement candidate preparation with existing compliance tooling and a tracked-source archive allowlist plus forbidden-file checks. Existing unresolved source-delivery work remains an explicit blocker. Preserve incomplete diagnostic artifacts separately from installable release bundles.
- [ ] Implement reusable manifest verification/notes checks and document the manual publication procedure, GitHub release subscription instructions, pre-1.0 compatibility rules and manual upgrade/recovery limits. A successful preparation does not itself authorize publication.
- [ ] Run Python release/operator/compliance suites; expect all pass. Inspect generated synthetic artifacts for secret/untracked leakage. Commit candidate tooling/docs.

### Task 7: Automate verification and candidate preparation

**Files:** Create `.github/workflows/{ci,release-candidate}.yml`, `deploy/release/{verify,workflow_checks}.py`, `deploy/release/test_workflows.py`; modify `CONTRIBUTING.md`, `docs/releasing.md`.

**Interfaces:** `verify.py --report-directory <directory>` runs required backend/frontend/operator/compliance/version checks, fails on any failure/skip and records sanitized results. CI runs read-only on pull requests/default-branch pushes. Candidate workflow is `workflow_dispatch` with tag input; it resolves/validates one commit, verifies it and calls Task 6 preparation. Upload diagnostic reports on failure without labeling them ready.

- [ ] Write failing workflow checks for missing read-only permissions, unsafe `pull_request_target`, unpinned third-party actions, secrets/write scopes, untrusted tag expression embedded in shell and publication/push commands. Assert tag input is passed through a validated argument/environment boundary and never evaluated as shell text.
- [ ] Run `python3 -m unittest discover -s deploy/release -p test_workflows.py`; expect failure before workflows exist.
- [ ] Verify current primary GitHub Actions/API documentation and exact reviewed action commit SHAs. Add pinned setup/checkout/artifact actions, .NET/Node/pnpm/Python setup, Docker availability and bounded job timeouts. Separate verification from artifact preparation; do not grant packages/contents write permissions.
- [ ] Add candidate smoke/HTTPS acceptance integration using disposable instances only. Retained reports must identify the exact source/images; retries never silently replace previously accepted artifact bytes.
- [ ] Run workflow checks plus the same local verification entry point. Use a YAML/workflow validator if available. Report hosted Actions execution as unverified until a separately authorized remote run actually succeeds. Commit workflow automation.

### Task 8: Final acceptance, review and evidence

**Files:** Create `docs/release-versioning-verification.md`, extend `deploy/self-host/acceptance.py` or add `deploy/release/acceptance.py`; update `docs/STATUS.md`, `docs/self-hosted-release-plan.md`, `docs/self-hosting.md`, approved design status and plan checkboxes.

**Interfaces:** Disposable acceptance accepts explicit candidate/baseline identities, synthetic account/data seed and `--confirm-disposable`. First baseline is committed `9cc808a`; later releases use the preceding published stable bundle. Fake upstream transport tests simulate release availability; no real release publication or persistent instance is required.

- [ ] Add/run failing acceptance assertions for initial install, administrator update access, disabled network behavior, simulated available release, logout/revocation, bundle upgrade preserving synthetic database/artwork and old migration receipt rejection.
- [ ] Build fresh candidate images and baseline images with distinct retained names. Run HTTPS acceptance against disposable services/database, ordinary/admin accounts and both themes at desktop/mobile widths; verify keyboard focus and delayed-session concealment. Verify local-disabled operation remains usable. Retain sanitized logs/artifact fingerprints and stop disposable services afterward.
- [ ] Run `dotnet test RateFreak.sln`, frontend lint/typecheck/test/build, API Release publish, model consistency, Python operator/release/compliance tests and `git diff --check`; expect zero failures/skips. Compare manifest bytes to accepted images. Do not call incomplete compliance or unavailable hosted CI evidence passing.
- [ ] Request one independent whole-change review using the requesting-code-review skill, approved spec/plan and exact changed files. Resolve important findings with focused failing regression tests followed by applicable passing suites.
- [ ] Record actual checks/counts, publication blockers and any unavailable verification in the evidence document. Mark only completed work in STATUS/plan. Preserve the branch for user review; do not merge, publish, push or start the persistent developer instance.

## Self-review and execution

Version contract, update transport/API/UI, installation guards, candidate preparation,
CI, documentation and acceptance requirements map to Tasks 1–8. Shared interfaces
are identified above; acceptance relies on the Task 4 manifest and Task 2 DTO rather
than creating another version/status representation. Review Focus cases have named
test coverage in their owning tasks.

Recommended execution: native in this session, in an isolated implementation branch,
with one independent whole-change review. The tasks share build identity, manifest
and status interfaces, so maintaining one implementation context avoids repeatedly
reconstructing those contracts. Plan review and execution-method selection precede
implementation as required by the writing-plans workflow.
