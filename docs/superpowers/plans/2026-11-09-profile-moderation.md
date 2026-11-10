# Profile Moderation Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans to implement task-by-task.

**Goal:** Report, inspect, hide and restore profile text in the existing moderation UI.
**Architecture:** Add account-level hidden state and a dedicated version, extend existing report targets, and enforce handle fallbacks in every public identity projection. Preserve owner's current text and lifecycle erasure behavior.
**Tech Stack:** .NET/EF Core/PostgreSQL, Next.js/React Query, xUnit/Vitest/Chromium.
**Spec:** `docs/superpowers/specs/2026-11-09-profile-moderation-design.md`

## Global Constraints

- No suspensions, handle changes, historical text snapshots, new dependencies or production deployment.
- Moderator/Admin current-role authorization, self-action denial, required reasons, CSRF and existing budgets.
- Edits stay hidden until explicit staff restore; profile versions change only with actual text/visibility changes.
- Additive local migration with backup and preservation verification; disposable databases for tests.
- Lifetime report deduplication, no-store responses, existing retention and erasure replay.

## Review Focus

- Hidden text never leaks through public author summaries or admin account directory.
- Concurrent account/Identity writes cannot undo hidden state or overwrite newer text.
- Erasure pseudonymizes profile targets without violating report constraints; replay remains idempotent.
- Cache/session transitions conceal staff and reporter data and invalidate public names after decisions.
- Down migration refuses existing profile reports and preserves their data on failure.

### Task 1: Backend profile targets and public suppression

Files: `AccountUser.cs`, profile/account DTOs and mappings, `UserProfileService.cs`, moderation domain/service/endpoints/configuration, public author projections, new integration tests and EF migration.
Interfaces: POST users/{id}/reports; GET moderation/profiles/{id}; POST moderation/profiles/{id}/visibility. Existing target timestamp contract. Optional default-false `profileTextHidden` on account/public profile.
- [x] Write/run failing integration tests for reporting permissions/duplicates, visibility, public identities, owner edits/no-op versions, stale decisions and own-profile denial.
- [x] Implement flags/version/target references, query suppression and monotonic row-locked profile edits; generate and inspect migration.
- [x] Run new and existing profile/moderation tests; fix evidence-based failures.

### Task 2: Lifecycle and migration proof

Files: `AccountErasure.cs`, `AccountDataExport.cs`, migration and integration tests.
- [x] Write/run failing tests for erased profile references, pseudonymized targets, retained deleted-target dismissal, export flag, Identity concurrency and guarded downgrade.
- [x] Implement lifecycle extensions and guarded Down; test migration defaults, constraints, no-data-loss downgrade refusal and replay.
- [x] Run backend regression suites and Release publish/model consistency.

### Task 3: Existing profile and moderation UI

Files: reporting interfaces/API/proxy policy, profile screen/editor, moderation routes/types/filters/views/cache keys and frontend tests.
- [x] Write/run failing tests for profile reporting routes, staff navigation/filtering, owner/public notices, suppression, invalidation and session safety.
- [x] Implement Report profile, profile inspection route, field labels, existing hide/restore confirmations and owner notice using existing styles.
- [x] Run frontend tests/lint/typecheck/build, independent review and real disposable Chromium acceptance at desktop/mobile sizes.

### Task 4: Local delivery and evidence

- [x] Make private local backup and fingerprint existing content; apply only verified migration; verify existing data unchanged excluding new columns/migration metadata.
- [x] Restart local API and verify frontend/API health/new endpoints; update STATUS/API/operator docs with evidence and pending production gates.

## Execution ledger

Ruling: Use existing checkout on `feat/profile-moderation` to preserve the running local setup and ignored dependencies/configuration. Execute inline following design approval, with one independent review at the end; leave implementation changes uncommitted unless requested.
Preflight: Tasks 1/2 share the profile report live reference and target type; use `profile`/`ProfileId`. Tasks 1/3 share `profileTextHidden` and the existing timestamp action contract. Task 4 consumes the reviewed migration from Tasks 1/2.

Task 1 complete: four API tests first failed at missing profile routes. Added
`IsProfileHidden`, concurrency-token `ProfileUpdatedAt`, profile-report references,
public query fallbacks and current-role/versioned actions. Owner edits lock the row
and advance the dedicated version only when text actually changes.

Task 2 complete: export test first failed at the missing flag; downgrade test first
failed because hidden-state rollback was allowed. Export, erasure/replay and guarded
downgrade now pass. Seven added integration cases bring the suite from 404 to 411.
Ruling: guard hidden profiles and profile audit history as well as reports, because
downgrade must not republish hidden text or discard the meaning of retained history.

Ruling: historical fixtures must seed their target schema's columns via parameterized
SQL rather than use the current AccountUser EF INSERT. Current-model reads after a
partially refused historical downgrade first reapply later migrations. Three failing
historical tests now pass with their preservation assertions intact. The profile
deletion-race test now races before the initial row lock rather than before UPDATE,
where the row is already protected. The directory test searches its own handle to
avoid assuming a shared fixture's staff population fits on page one.

Task 3 complete: frontend red tests showed missing routes/notices/invalidation and
field labels, then passed after integration. Final suite 682/682 (95 files); lint,
typecheck, production build, API Release publish and EF model consistency passed.
Unit 27/27, provider contracts 154/154 and final integration 411/411, zero skipped.
Independent implementation reviewer reported no important findings. Real Chromium
acceptance passed on disposable services/database with no runtime errors, including
report/hide/correction/restore, audit, cancellation, logout concealment and layouts
at 1440/768/390/320px. Browser harness logout assertion was corrected to expect the
existing in-place login prompt rather than an unsupported redirect.

Task 4 complete: reviewed migration `20261110014234_AddProfileModeration` applied to
the persistent local test database after a mode-0600 pg_dump backup. Original row
counts/content fingerprints across 27 application tables were identical before and
after; new flags default visible. Metadata/backup hash in
`.local/localhost/profile-moderation-migration-verification.json`. API restarted;
health, public profile contract, frontend pages and anonymous rejection at the
protected profile-inspection proxy verified. Production gates remain open.

Integration instruction: the user subsequently requested committing this feature,
merging both feature branches into local `main`, and deleting the merged branches.
That request supersedes the original instruction to leave changes uncommitted.
