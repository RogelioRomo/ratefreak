# Personal media implementation plan

> Execute inline with superpowers:executing-plans and test-driven-development.

Goal: private tracking library, 0–5 half-step ratings and public owned review CRUD.
Architecture: separate Domain entities and Application contracts; scoped EF services;
API security filters; additive PostgreSQL migration. No new production packages.
Spec: ../specs/2026-10-29-personal-media-design.md

## Global constraints

Preserve existing records and routes; no frontend or provider changes. No token in
logs. Authentication and CSRF on every mutation; no user ID accepted in write DTOs.
0–5 inclusive in increments of 0.5; one editable public review per user/media.
User delegated remaining implementation choices. Work inline in the provided
workspace because no Git metadata exists; no worktree/commit/ledger scripts needing Git.

## Review focus

- Concurrent library/rating PUTs preserve a single identity; duplicate reviews 201/409.
- Foreign review mutation cannot change contents or reveal private account fields.
- Missing/null scores and malformed review strings cannot become 0 ratings or 500s.
- Deleting library entries cannot delete ratings/reviews or corrupt aggregates.
- Tied timestamps, paging and filtering must not leak another user's library.

## Tasks

- [x] 1. Write PersonalMediaTests HTTP lifecycle, input/security, ownership, paging,
  concurrency and SQL constraint tests in tests/RateFreak.IntegrationTests; run the
  focused test to observe missing-route RED before adding production code.
- [x] 2. Add Domain/PersonalMedia entities; Application/PersonalMedia contracts/input
  rules. Add Infrastructure/PersonalMedia services and persistence configuration,
  wire DbContext/DI. Atomic PostgreSQL upserts for tracking and ratings; unique
  review conflict maps to 409. Implement endpoints in PersonalMediaEndpoints.cs,
  wire Program and rate limits/no-store. Run focused tests (build before migration).
- [x] 3. Generate AddPersonalMedia migration and inspect additive Up/dependent-first
  Down. Run focused tests GREEN then full dotnet test RateFreak.sln --no-restore.
- [x] 4. Release build, EF model consistency, NuGet audit, focused independent review;
  fix concrete review findings with regression tests. Apply/reapply local migration,
  restart Development API and live verify ownership/CSRF/tracking/rating/reviews.
- [x] 5. Document contracts and limitations in README/checklist/ADR, record proof and
  all deviations here. No commit because workspace has no Git metadata.

## Evidence / rulings

- User approved 0–5/0.5 ratings, other recommendations, and delegated remaining choices.

- Baseline: 113/113 tests pass. RED: all 35 Phase 4 cases fail with missing endpoints.

- Initial GREEN run exposed unsupported EF ordering/filtering after record construction.
  Direct PostgreSQL regression reproduced the translation error; moved predicates and
  ordering before DTO projections (same pattern as existing CatalogQueries).

- GREEN: full suite 162/162 (7 unit, 28 provider, 127 integration); zero skips.
- Generated migration 20261029193432_AddPersonalMedia; inspected additive Up and
  Down that removes only the new tables. Fresh NuGet audit: no known vulnerable packages.

- Independent read-only review: one P2 finding, write response SELECT could race
  concurrent DELETE. Four deterministic HTTP regressions use a second real DB
  connection between write and response: observed library/review-create 500 and
  rating/review-update 404. Fix holds write transactions through response loading.
- Final: Ruling: production performance remains unmeasured — this phase adds
  correctness tests and indexes, not load tests; Phase 7 owns measurement. Cost if
  wrong: operational query/connection bottlenecks need tuning before production.
- Review runtime race-frequency concern is resolved by deterministic reproduction,
  not an estimate of frequency. No minor findings were reported.

- Final: fixed write/delete response race — four deterministic tests RED→GREEN;
  whole suite 166/166 (7 unit, 28 provider, 131 integration), zero skips.
- Final Release build 0 warnings/errors; EF snapshot matches; dependency audit clean.
- Local migration applied/reapplied, four migration history entries once each.
  Live security/lifecycle/OpenAPI/TMDB checks passed. Two temporary verification
  accounts removed; final counts 0 accounts, 2 existing media/mappings, 0 personal rows.
- Documentation complete. All authorized Phase 4 work complete. API left on port 5080.
