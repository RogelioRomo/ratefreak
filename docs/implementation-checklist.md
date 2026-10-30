# Implementation checklist and changelog

Updated 2026-10-30. Current scope: Phase 0/1, Phase 2 accounts/catalog reads,
Development Swagger, Phase 3 TMDB (enabled locally), Phase 4 personal media,
and Phase 5 dynamic collections. Earlier entries record historical verification.

## Resume here — Phase 5 complete (2026-10-30)

Implemented collections with private/public visibility (default private), mixed
canonical/custom entries, owner-only editing, five custom field types and bounded
JSONB validation. User approved duplicate canonical-media prevention per collection
and permanent field deletion after impact preview/explicit confirmation, then
approved the full design and native execution plan. See ADR 0006 and README.

Current verification: **281 tests pass** (64 unit, 28 provider contract, 189 integration),
zero failed/skipped. Release build: zero warnings/errors. NuGet transitive audit:
no known vulnerable packages. EF: no pending model changes. Applied/reapplied
`20261030160901_AddCollections` to persistent local PostgreSQL; five migrations
recorded once each. Live Phase 5 lifecycle/privacy/validation/deletion checks pass.
Independent review completed: two important input-validation findings reproduced
in failing tests and fixed; the full suite and live regressions pass after fixes.
One minor test-hardening suggestion remains documented below.

Development API running at http://localhost:5080/swagger; PostgreSQL healthy on
127.0.0.1:55432. Recheck processes on resume. Removed only the temporary Phase 5
verification account and cascaded collections/fields/items; existing two catalog
records/mappings preserved. Local counts after cleanup: users 0, media 2,
collections/fields/items 0. No public deployment.

Work is on `main`. At the user’s request, Phase 5 was merged locally by
fast-forward from `dd454da` to `82a2ddb`; all feature commits are retained.
Post-merge verification on `main`: 281 tests pass, zero failed/skipped.
No remote is configured and no push was performed. Phase 6a design is now ready
for review: MusicBrainz full-length albums, one record per release group (user
approved this scope on 2026-10-30). See
`docs/superpowers/specs/2026-10-30-musicbrainz-design.md`; builder check found no
important gaps. No Phase 6 product code or migration has been written yet.
Email verification/recovery and deployment hardening remain required before launch.

### TMDB activation and live verification

- [x] User supplied the API Read Access Token through the owner-only local handoff file.
- [x] Transferred token through stdin to API .NET user-secrets without displaying it
  or placing it in shell arguments; removed the handoff file.
- [x] Enabled `Providers:Tmdb:Enabled` in user-secrets and started the Development API.
  PostgreSQL healthy; API liveness/readiness return 200; capability reports enabled.
- [x] Live movie search (Alien) and TV search (Breaking Bad) return results; previews
  return Star Wars (movie 11) and Breaking Bad (TV 1396), both HTTP 200.
- [x] Unauthenticated import returns 401; authenticated import without CSRF returns
  400. Registration/login and CSRF-protected imports succeed. New imports return
  201; repeats return 200 with the same canonical ID; catalog detail returns 200.
- [x] Logged out and removed only the newly created temporary verification account.
  Retained local catalog records: Star Wars `dd72a73f-2e04-436e-93a4-c955116ac3e4`
  and Breaking Bad `f7e35ab6-edbe-49b3-9bd7-20c21e3a3b7c`. No pre-existing data deleted.
- [x] Recorded live results here. API left running at http://localhost:5080/swagger;
  recheck service state on a future resume rather than assuming it survived.

### Following work, when authorized

- [x] Phase 4: private library/tracking, 0–5 half-step ratings, public owned reviews,
  live aggregates, ownership/CSRF tests and additive migration. See evidence below.
- [x] Phase 5: dynamic collections and schema validation.
- [ ] Phase 6: additional providers and social features.
- [ ] Phase 7: production hardening and deployment verification.
- [ ] Deferred account work: profile editing and public profiles.
- [ ] Deferred catalog work: authorized corrections/refresh and provenance policy.
- [ ] Frontend/Next.js proxy remains out of scope until explicitly requested.

### Required before public launch

- [ ] Email ownership verification.
- [ ] Password recovery/reset.
- [ ] Review accepted TMDB terms, commercial licensing if applicable, metadata
  storage/image rights, and frontend approved-logo/notice attribution.
- [ ] HTTPS/trusted-proxy setup, secure durable Data Protection keys, secret
  storage, browser/proxy CSRF and cookie forwarding, and deployment abuse limits.
- [ ] Account deletion/retention policy, backups/restore verification and the
  remaining production checklist in `ratefreak-backend-plan.md`.

Local defaults: API `http://localhost:5080`, Swagger `/swagger`, OpenAPI
`/openapi/v1.json`, PostgreSQL `127.0.0.1:55432` (host port 5432 is used by an
unrelated PostgreSQL). Read `README.md`, this checklist and the architecture plan
when resuming. Prefer executing authorized commands for the user.

## Dependency audit follow-up (2026-10-29)

User-side restore reached NuGet but failed NU1903 for transitive Microsoft.OpenApi
2.0.0 and SSH.NET 2025.1.0. Added explicit patched-version references:
Microsoft.OpenApi 2.7.5 in Api, and SSH.NET 2026.0.0 in IntegrationTests.
References retain their existing parent packages; no audit warnings are suppressed.
Advisories: GHSA-v5pm-xwqc-g5wc, GHSA-mggc-4xg6-vcxf, GHSA-q939-rpr3-3284.
User-side restore, build and the full test suite now pass after this dependency change.
The resolved asset graphs now contain Microsoft.OpenApi 2.7.5 in Api and
IntegrationTests, and SSH.NET 2026.0.0 in IntegrationTests. Corrected the
Testcontainers builder to use its supported image-taking constructor after
the newly available test compilation exposed CS0618.

Earlier sandbox verification: all six projects build with zero warnings/errors using
cached dependencies and `--no-restore`. Executing the xUnit assemblies directly
passes all 7 unit cases and 5 HTTP integration cases. The sole database case
fails before container startup because the sandbox denies the Docker socket.
The user subsequently supplied successful normal restore/build/test output:
13 tests passed, 0 failed, 0 skipped. This includes the real PostgreSQL migration
test. The successful restore reported no vulnerability warnings; this does not
replace a dependency audit before production deployment.

## Local port selection (2026-10-29)

User-side `ss` output showed 127.0.0.1:5432 occupied, with no listeners on
55432 or 5080 at the time of the check. Set the Compose host default, `.env.example`,
the existing local `.env`, and README connection example to 55432. PostgreSQL
inside the container remains on 5432; the API launch profile remains on 5080.
Existing user-secrets must also use Port=55432; secret storage was not modified.

## Phase 0

- [x] Record approved independent .NET deployment with future Next.js proxy on Vercel.
- [x] Record ASP.NET Core Identity/cookie direction and proxy security work for Phase 2.
- [x] Record provisional defaults for taxonomy, ratings, reviews, collections,
  imports, artwork, hosting and first provider.
- [ ] Confirm each provisional product rule before its implementation phase.

## Phase 1 implementation

- [x] .NET 10 solution and four production projects with explicit references.
- [x] Dependency injection, validated connection options and scoped DbContext.
- [x] Empty baseline migration/snapshot; no startup migrations or business schema.
- [x] PostgreSQL 17 Compose, persistent volume, localhost binding, health check.
- [x] Development OpenAPI, JSON logging, trace IDs and sanitized Problem Details.
- [x] Independent liveness and database/migration readiness.
- [x] xUnit v3 configuration tests, HTTP integration tests, Testcontainers migration test.
- [x] README with secrets, migration, startup, health and test commands.
- [x] Independent read-only code review: no concrete correctness findings.

## Verification evidence

- User terminal: `dotnet restore RateFreak.sln` succeeded with auditing enabled.
- User terminal: `dotnet build RateFreak.sln --no-restore` built all six projects.
- User terminal: `dotnet test RateFreak.sln --no-build --no-restore` passed all
  13 cases (7 unit, 5 HTTP integration, 1 PostgreSQL integration), with no failures
  or skipped tests. The PostgreSQL test verifies explicit migration application,
  repeat application, migration history, snapshot consistency and readiness.
- User terminal: local dotnet-ef 10.0.2 tool restore succeeded (earlier output).
- Production API build succeeded: 0 warnings, 0 errors, including all four projects.
  Used cached packages, single-process MSBuild and disabled NuGet audit for this
  offline verification command only; repository configuration does not disable audit.
- Compose `config --quiet` passed with an ephemeral dummy password.
- Starting the API without its required connection string failed as intended
  with a sanitized options validation message.
- A temporary dependency-free console probe using real Infrastructure code passed
  six invalid-configuration cases, valid scoped DbContext resolution, migration
  discovery, empty domain model, snapshot consistency and idempotent SQL generation.
- A temporary in-memory IServer probe booted the actual compiled API entry point
  in Production and Development without opening sockets. In each environment:
  liveness 200, unavailable-database readiness 503, missing route 404, sanitized
  injected failure 500. OpenAPI returned Development 200 / Production 404.
  Error responses included trace IDs and did not disclose the injected exception.
  These probes supplement but do not replace the checked-in xUnit suite.

## Verification status and remaining deployment work

- [x] Complete audited restore and run the full suite with Docker access.
  Verified by the user-provided terminal output; sandbox restrictions remain local
  to the assistant environment.
- [x] Apply/reapply migration against real PostgreSQL in Testcontainers.
- [x] Restore the local dotnet-ef tool.
- [x] Complete first local Compose setup, run the documented EF CLI database
  update against that persistent database, and smoke-test the running API.
  After enabling CLI access on 2026-10-29, verified Docker and NuGet connectivity;
  PostgreSQL is healthy at 127.0.0.1:55432. Configured API development user secrets
  from the existing Compose credentials without printing them. Restored/built
  the solution and ran the full suite directly: 13 passed, 0 failed, 0 skipped.
  Ran `dotnet ef database update` twice and queried PostgreSQL migration history:
  `20261028000100_InitialFoundation` is recorded once. Started the API on
  http://localhost:5080; live/ready/OpenAPI returned 200 and an unknown API route
  returned 404 Problem Details with a trace ID.
- [x] Audit all six projects including transitive dependencies with NuGet
  connectivity: `dotnet list RateFreak.sln package --vulnerable
  --include-transitive --no-restore` reported no vulnerable packages on 2026-10-29.
  Recheck dependency updates and advisories before production deployment;
  this result is not a security certification.
- [x] Initialize Git and commit the completed work through Phase 4 at the user’s
  explicit request. Earlier phases were implemented before Git metadata existed.

Phase 1 foundation acceptance is verified: build, full automated suite,
persistent local Compose database, explicit EF CLI migration and running-server
smoke checks all pass. Production deployment hardening and confirmation of
future product rules remain outside this phase.

## Historical Phase 1 scope decisions

- Baseline migration is hand-authored and empty; no Phase 2 tables were invented.
- Provider-contract test project is deferred until there is a provider to test.
- Dockerfile/API container, ingress hardening and production operational setup
  remain future deployment work; this phase supplies local PostgreSQL Compose.
- At the end of Phase 1, product defaults remained proposals and only the proxy
  deployment direction was confirmed. Identity was subsequently authorized and
  implemented in the Phase 2 slice below; frontend and providers remain absent.
- Initial test-first verification was blocked by package restore rather than
  observed red/green behavior. Later user-side execution passed all 13 tests.

The user subsequently authorized Phase 2's email/password accounts slice; its
current status is recorded below. Catalog and provider implementations remain deferred.


## Phase 2 — accounts slice (2026-10-29)

- [x] Record approved email/password sign-in and unique public handle direction.
- [x] Application contracts/input rules and Infrastructure ASP.NET Core Identity service.
- [x] UUID users, UTC timestamps and generated `AddIdentityAccounts` migration.
- [x] Database unique normalized email/handle indexes and explicit Identity relationships.
- [x] Register/login/logout/current-user endpoints; no automatic login on registration.
- [x] HttpOnly cookie sessions, eight-hour expiry, immediate all-session logout revocation.
- [x] Antiforgery cookie/header validation on every auth mutation and token issuance route.
- [x] Login lockout, bounded inputs/body size, separate IP rate limits and no-store responses.
- [x] Durable Development keys and validated explicit key path outside Development.
- [x] Test-first lifecycle: observed missing-route 404, then implemented the account flow.
- [x] Full suite: 37 cases pass (7 unit, 30 integration); no failures/skips.
- [x] Fresh dependency audit: no known vulnerable packages, including transitives.
- [x] EF reports no pending model changes after the generated migration.
- [x] Independent security/correctness review: no unresolved findings. Confirmed
  the review's Unicode-normalization overflow case with a failing HTTP test (500),
  then bounded the normalized email and verified 400 with the full suite green.
- [x] Added a Development/Production malformed-JSON regression test; observed the
  old handler returning 500 in Development, then preserved sanitized 4xx statuses
  for BadHttpRequestException. Unexpected failures still return sanitized 500.
- [x] Apply migration to the persistent local database and verify live HTTP account flow.
  Register 201, login 200, /users/me 200, logout 204, revoked/replayed session 401.
  Requests without CSRF return 400; malformed JSON returns sanitized 400; the
  real Kestrel body limit returns 413. Temporary smoke-test account was removed.
- [x] Restart the local API with this slice and verify health/OpenAPI.
  http://localhost:5080 is running; liveness/readiness/OpenAPI return 200 and the
  OpenAPI document includes all five account routes. No password/cookie printed.
- [x] Reapply migration without changes; model snapshot matches the current model.

Limits: no email verification/recovery, frontend/proxy, catalog, profile editing,
admin endpoints or media providers. Do not publicly launch local-account endpoints
until email ownership/recovery and deployment/abuse hardening are complete.
Logout deliberately revokes all sessions; per-device session management is deferred.


## Development Swagger UI (2026-10-29)

- [x] Added UI-only Swashbuckle.AspNetCore.SwaggerUI 10.3.1 using the existing
  /openapi/v1.json document; no second schema generator or database change.
- [x] /swagger page, index and embedded JavaScript are registered only in Development.
- [x] Automatic fresh antiforgery token/header for same-origin /api/v1 writes, so
  register/login/logout remain usable without weakening server-side CSRF checks.
- [x] Six new integration cases: Development returns 200 for page/index/bundle;
  Production returns 404. Existing OpenAPI environment tests remain green.
- [x] Full suite: 43/43 (7 unit, 36 integration), no failures/skips.
- [x] Focused independent review: no blocking findings.
- [x] Fresh dependency audit: no known vulnerable packages, including transitives.
- [x] Local /swagger, index, bundle, initializer and OpenAPI return 200; readiness 200.
- [x] Executed the actual served Swagger index.js initializer in a temporary Node
  harness. Its function parser rejected an async interceptor (SyntaxError); changed
  it to a promise-returning function, then verified the initializer executes,
  only same-origin API writes receive CSRF headers, and token failures abort writes.
- [x] Used that delivered interceptor against the real local API: registration 201,
  login 200, /users/me 200, logout 204 and /users/me 401 with refreshed CSRF tokens.
  Temporary verification account was removed. No new Node runtime/build dependency
  is required by the backend; this was a supplementary local verification harness.
- [x] Local API restarted and left running at http://localhost:5080/swagger.

Scope stays at development documentation; no catalog/provider/frontend work added.


## Phase 2 — canonical catalog slice (2026-10-29)

- [x] Domain movie/TV-series models with bounded input and UTC/UUID identity.
- [x] Application-specific catalog query interface and public summary/detail DTOs.
- [x] EF no-tracking queries, literal case-insensitive substring search, bounded
  pagination (page 1..10000, size 1..100) and title/UUID ordering.
- [x] Public GET /api/v1/media and GET /api/v1/media/{id}, documented in OpenAPI/Swagger.
- [x] Independent 60/IP/minute catalog limit with Retry-After and Problem Details.
- [x] JSONB object metadata, date-only releases and external artwork references.
- [x] Unique provider/namespace/external-ID mappings, FK and restricted deletion.
- [x] Generated 20261029170128_AddCanonicalCatalog migration; reviewed additive Up
  and dependent-first Down. Applied locally, reapplied without changes. EF reports
  no pending model changes; existing migration repeatability tests remain green.
- [x] Observed missing catalog routes fail 9 new endpoint cases before implementation.
- [x] Real PostgreSQL tests cover seeded reads/details, escaped percent/underscore/
  backslash searches, stable tied-title pages, type filtering, nullable fields,
  mapping namespaces, duplicate rejection, FK/deletion and DB check constraints.
- [x] Independent review found NUL search causing 500 and padded boundary searches
  rejected before trimming. Reproduced both in failing tests, fixed normalization/
  validation, then verified full suite: 62 passed (7 unit, 55 integration), 0 skipped.
- [x] Restarted Development API; live health, readiness, Swagger, empty catalog and
  OpenAPI return 200. Invalid type/NUL search return 400, unknown item 404. OpenAPI
  includes both catalog routes and q/type/page/pageSize parameters.
- [x] README and catalog design/implementation plan updated.

No catalog records were seeded into the local database. Provider imports remain
Phase 3 and require a separate implementation step; there are no provider calls,
public catalog writes, frontend or Next.js proxy in this slice. Profile editing,
public user profiles, email verification and password recovery remain pending.
Email ownership/recovery and deployment hardening are still required before launch.


## Phase 3 — TMDB search, preview and import (2026-10-29)

- [x] Checked official TMDB FAQ, application authentication, search/movie/TV detail,
  image and rate-limit documentation. Free developer use is non-commercial with
  attribution; commercial licensing and accepted account terms require review.
- [x] Application provider/import contracts and Infrastructure typed HttpClient
  adapter; no SDK or new production package. Separate provider-contract test project.
- [x] Validated disabled-by-default options, secret bearer header, fixed HTTPS API
  origin, redirects disabled and generated/validated artwork references.
- [x] Movie/title and TV/name normalization, nullable dates/artwork, type-specific
  metadata without community/provider-score mixing; adult content excluded.
- [x] Bounded overall timeout, four concurrent outbound operations/no queue,
  one transient retry, Retry-After handling, 1 MiB response and depth-32 JSON bounds.
- [x] Public capabilities/search/preview; separate 20/IP/minute external budget.
- [x] Authenticated CSRF-protected import, 10/account/minute; only provider/ID/type
  accepted. Extra properties rejected; server fetches authoritative details.
- [x] Explicit transaction creates canonical item and mapping atomically; concurrent
  unique conflicts roll back and return winner. Repeats skip HTTP/preserve corrections.
- [x] Existing Phase 2 schema sufficient; EF reports no pending model changes.
- [x] Missing-route RED: four endpoint cases failed before implementation.
- [x] Full suite: 113 passed (7 unit, 28 provider contract, 78 integration), zero
  failed/skipped. Includes real PostgreSQL concurrent imports, replay, namespaces,
  CSRF, unauthorized/invalid payloads, rate limits, provider failures and no-orphan checks.
- [x] Independent review found malformed UTF-16 JSON string decoding could escape
  as 500. Reproduced lone-high/lone-low surrogate failures, converted to sanitized
  invalid-provider response and verified 502/no rows in endpoint regression.
- [x] Release build: zero warnings/errors. Fresh NuGet transitive audit reports
  no known vulnerable packages in all seven projects.
- [x] Local API restarted at localhost:5080. Ready/Swagger/capabilities 200;
  disabled search/preview 503; invalid type 400; anonymous import 401. All four new
  routes appear in live OpenAPI. Existing local catalog data was not altered.
- [x] README records activation, attribution/logo requirement, limits, errors and
  commercialization distinction. Prepared empty ignored owner-only token handoff
  at .local/tmdb-token.txt so credentials need not be pasted in chat or shell arguments.
- [x] Configured the token in user-secrets and enabled/verified live TMDB after
  this initial implementation. See activation evidence at the top of this file.
- [ ] Review TMDB account API terms for intended deployment/commercial use and
  storage/image rights; full terms page was crawler-blocked. No search caching,
  image copying/proxying, background refresh or frontend was added.

Public launch still requires email verification, password recovery, frontend TMDB
approved-logo/notice attribution, proxy/ingress/secrets/abuse hardening and the other
previously recorded launch work. Refresh/moderation and additional providers remain
future slices; existing local corrections are preserved by repeat imports.


## Phase 4 — personal library, ratings and reviews (2026-10-29)

- [x] Approved scale replaces provisional 0–10 with **0–5 in 0.5 increments**.
  Private library; shared tracking statuses planned/in_progress/completed/paused/
  dropped; explicit optional date-only start/completion, chronological constraint.
- [x] Independent library/rating/review lifecycles; library removal preserves votes
  and reviews. One current rating and one editable public review per user/media.
- [x] Application contracts, Domain validation, scoped persistence services and
  API routes. Public local count/average excludes provider scores, null when unrated.
- [x] Plain-text reviews with bounded content, spoiler flag, public author without
  email; owner-only edits/deletes. Unknown/foreign review mutations return 404.
- [x] All mutations authenticated and CSRF protected; unexpected JSON fields rejected.
  Missing/null score cannot become zero. Private reads no-store. Separate per-account
  read/write budgets; public routes use existing catalog budget; bounded pagination.
- [x] Generated additive migration `20261029193432_AddPersonalMedia`; unique pairs,
  score step/status/date/body checks, account cascade/catalog restrict foreign keys,
  indexes. Reviewed Up/Down. No existing schema/data dropped or rewritten.
- [x] Atomic PostgreSQL library/rating upserts; concurrent review creation 201/409.
  Repeatable-read snapshots keep each page/count internally consistent.
- [x] Baseline 113/113. Observed 35 missing-endpoint test failures before implementation.
  Fixed a reproduced EF translation failure by applying predicates/order before DTO
  construction. HTTP tests run against real Testcontainers PostgreSQL.
- [x] Independent review found response reads racing concurrent DELETE. Reproduced
  library/review-create 500 and rating/review-update 404 in four deterministic tests;
  transaction now holds locks through response loading. All four regressions pass.
  No remaining review findings/minors; performance/load testing stays Phase 7.
- [x] Final full suite `dotnet test RateFreak.sln --no-restore --verbosity quiet`:
  **166 passed**, 0 failed/skipped (7 unit, 28 provider, 131 integration).
  Covers score boundaries/half steps, direct DB constraints, FKs/cascades/uniqueness,
  CSRF/auth, private scope/cache, review limits/Unicode/spoilers/ownership, aggregates,
  paging/filtering/rate limits and concurrent writes/deletes.
- [x] Final Release solution build: 0 warnings/errors. EF no pending model changes.
  Fresh NuGet all-project transitive audit: no known vulnerable packages.
- [x] Applied/reapplied migration to persistent DB; history contains all four IDs
  once each. Local OpenAPI includes all six Phase 4 route paths. Health/readiness,
  Swagger/OpenAPI and live TMDB search return 200 after restart.
- [x] Live two-account flow: private library isolation, valid/invalid 0–5 ratings,
  CSRF rejection, two-vote average 4.0, public review create/edit/delete, duplicate
  409, foreign mutation 404, library removal preserving rating/review, rating removal
  returning aggregate to null, logout/revoked sessions. Temporary accounts removed.
  Final database: 0 accounts, 2 pre-existing media, 2 mappings, 0 library/rating/review
  records. No pre-existing data deleted; no additional catalog records retained.
- [x] Updated README, ADRs, design/plan and this checklist. API left running on 5080.

Limits: no frontend, collections, episode progress, private reviews, review moderation
workflow, public profiles or account deletion endpoint. Email verification/recovery,
TMDB attribution/terms and deployment hardening remain required before public launch.


## Initial Git repository (2026-10-29)

- User explicitly requested Git initialization and a commit of all work to date.
- Initial branch: `main`; existing configured Git author identity retained.
- Source, migrations, tests and documentation included. Local `.env`, user-secrets,
  Data Protection keys, temporary handoff files and build/test outputs excluded.
- Added ignore patterns for generated directories containing literal backslashes.
- No remote configured and no push performed.


## Phase 5 — dynamic collections (2026-10-30)

- [x] Approved full design and implementation plan; recorded ADR 0006.
- [x] Domain/Application/Infrastructure/API module; no new dependencies.
- [x] Private/public visibility, owner-only writes, canonical/custom entries,
  no public email disclosure, no-store reads and existing shared rate budgets.
- [x] Text/number/boolean/date/select validation, bounded definitions/options,
  unique immutable keys/types, required/nullable rules and complete data replacement.
- [x] Prevent duplicate canonical media per collection; allow independent custom rows.
- [x] Locked parent-row transactions and expected revisions for all existing-collection
  writes; schema edits revalidate current items; failures leave state unchanged.
- [x] Owner-only deletion preview counts populated entries; confirmed fresh revision
  deletes definitions and all keys atomically; stale confirmations return 409.
- [x] Generated additive `20261030160901_AddCollections`; inspected Up/Down.
  Repeatable migration/model checks pass. Five local migrations recorded once each.
- [x] Test-first missing-route/validation RED→GREEN; PostgreSQL ownership/CSRF,
  JSON/field/item validation, duplicate handling, schema compatibility, rollback,
  pagination, shared limits, direct DB constraints and cascade/restricted deletion.
- [x] Independent-connection concurrent item/schema/delete writes allow one winner
  and one 409; coherent read snapshot prevents visibility/data race disclosure.
- [x] Malformed escaped surrogate in JSON property name reproduced as a failing unit
  test; validation boundary now rejects it without a 500; HTTP regression passes.
- [x] Final full suite 281/281 (64 unit, 28 provider, 189 integration), no failures/skips. Release build zero warnings/errors;
  audited all seven projects/transitives, no known vulnerable packages.
- [x] Persistent-local migration apply/reapply, health/readiness/Swagger/OpenAPI,
  register/login, private creation, custom/canonical items, invalid-value 400,
  duplicate 409, stale-confirmation 409, confirmed deletion 204, visibility changes.
- [x] Logged out and removed only the exact temporary verification account;
  collections/fields/items cascaded away. Retained two pre-existing catalog records.
- [x] README contracts and ADR/checklist updated.
- [x] Independent whole-branch review: no critical findings. Reproduced and fixed
  rounded/underflowed decimal validation versus raw JSON storage (including bounds),
  and duplicate DTO properties masking earlier invalid data. Exact coefficient/
  exponent comparison rejects unrepresentable nonzero decimals; persisted numeric
  values/configuration are serialized from the validated decimal. Exactly-zero
  extreme-exponent input is canonicalized safely. API JSON deserialization rejects
  duplicate properties before binding. Unit/HTTP regressions observed RED→GREEN.
- [x] Final live run repeats lifecycle and verifies numeric precision/underflow and
  masked duplicate input return 400, while exact-zero exponent forms persist safely.
  Exact temporary user removed; final local counts again users 0, media 2,
  collections/fields/items 0. API restarted with fixes and left running on 5080.
- [ ] Deferred minor review suggestion: compare complete collection/item snapshots
  before and after each rejected field deletion to strengthen timestamp/value/schema
  preservation assertions. Current status/revision/deletion lifecycle tests pass;
  implementation rejects these requests before executing any mutation.

Limits: no frontend, collection deletion endpoint, archive/restore, bulk backfills,
key/type migrations or collaboration. Phase 6 providers/social and Phase 7 production
hardening remain future work. Public launch still requires email verification,
password recovery, TMDB terms/attribution and remaining deployment safeguards.
