# Moderation and Reporting Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox syntax for tracking.

**Goal:** Add private reporting, reversible moderation and audited role administration.

**Architecture:** Existing Identity roles authorize staff through current database membership. Explicit report targets and transactional audit events accompany content visibility, report resolution and role changes. A shared PostgreSQL advisory transaction lock serializes privileged mutations and relevant content writes; public reads filter hidden targets.

**Tech Stack:** .NET 10, ASP.NET Core Identity, EF Core, PostgreSQL, xUnit HTTP integration tests.

**Spec:** docs/superpowers/specs/2026-10-30-moderation-design.md

## Global Constraints

- User requires no stored role; Moderator and Admin are explicitly assigned.
- Never grant Admin through HTTP or registration; operator command only.
- CSRF for all HTTP mutations; strict JSON, no-store, safe identity projections.
- Reports5/account/hour; moderation reads60/min, writes30/min; role writes10/min.
- Immutable report targets and audit metadata survive deletion; no copied bodies/emails.
- Guard rollback against hidden content, reports or audit events.
- No suspension, frontend, real privileged account assignment, merge or push.

## Review Focus

- Role revocation racing a decision must serialize with the permission check (Task2/4).
- Hidden parent threads must stay unavailable through direct comment reads (Task3).
- Deleted targets must preserve reports without violating CHECK constraints (Task1/4).
- Edited targets must reject decisions against old timestamps (Task2/4).
- Failed audit insertion must roll back content and role mutations (Task4).

### Task 1: Reporting persistence and HTTP boundary

**Files:** Domain/Moderation entities; Application/Moderation contracts; Infrastructure/Moderation services and Persistence configuration/migration; Api/Endpoints/ModerationEndpoints.cs; tests/RateFreak.IntegrationTests/ModerationTests.cs.

**Interfaces:** IModerationService accepts explicit actor/target IDs, produces status results and safe DTOs; persistent ContentReport and ModerationEvent use UUIDs/UTC.

- [x] Write HTTP tests for report creation, duplicate409, own400, missing404, validation and auth/CSRF; run filtered test command and observe missing-route RED.
- [x] Implement typed report targets, immutable metadata, nullable live references, unique constraints; strict requests and rate policies; additive migration with guarded Down.
- [x] Run focused reporting tests GREEN; inspect migration/model and commit.

### Task 2: Staff workflow and role permissions

**Files:** Infrastructure/Moderation/*; Api/Security/ModerationAuthorization.cs; Api/Endpoints/ModerationEndpoints.cs; tests/RateFreak.IntegrationTests/ModerationTests.cs.

**Interfaces:** service read/detail/resolve/visibility/role operations return success/notfound/conflict/forbidden; permission lookup uses persisted Identity memberships.

- [x] Add failing tests for role boundaries, staff private reads, hide/restore, stale409, dismiss and resolution409, private audit events, role grant/revoke and immediate revocation.
- [x] Implement database authorization policies, global transaction advisory lock, atomic audit append, safe private projections and paginated queries. Admin implicitly moderates; deny own-content actions.
- [x] Run focused tests GREEN and commit.

### Task 3: Public visibility and author writes

**Files:** Domain/PersonalMedia/Review.cs; Domain/Social/ReviewComment.cs; Infrastructure/PersonalMedia/ReviewService.cs; Infrastructure/Social/ReviewCommentService.cs; tests/RateFreak.IntegrationTests/ModerationVisibilityTests.cs.

**Interfaces:** public read filters differ from author mutation projections; content writes share moderation lock.

- [x] Add failing tests for every list/detail/count/parent-comment-create path, hidden author edits/deletes and independent child restoration.
- [x] Filter public reads and parent checks; preserve hidden flags on edits, serialize writes, project author-only mutation results without public filter.
- [x] Run focused moderation/comment/review tests GREEN and commit.

### Task 4: Operator command, races, deletion and release evidence

**Files:** Api/Program.cs; Infrastructure/Moderation/*; integration moderation persistence/concurrency/migration tests; README.md; docs/adr/0011-moderation.md; docs/implementation-checklist.md.

**Interfaces:** offline --appoint-admin UUID --reason text command runs without HTTP and records an operator event.

- [x] Add failing tests for operator idempotence, deletion-surviving reports/audits, concurrent duplicate reports/decisions/revocation, failed-audit rollback, migration refusal and safe downgrade/reapply.
- [x] Implement operator command and remaining transactional safeguards. Document retention, bootstrap, exact routes and rollback limits.
- [x] Run full solution tests, Release build, EF pending-model check and dependency audit. Inspect outputs, obtain independent whole-branch review, fix required findings with regression tests.
- [x] Record actual verification in checklist; commit all changes, leave feature branch unmerged.

## Execution ledger

Design approved by user. Native execution chosen to carry out the instruction to continue; no implementer delegation. Feature branch protects main. Advisory lock is intentionally coarse for this first low-volume slice and must precede all account/content locks; staff reads use snapshot transactions. No worktree created: current permitted workspace used on isolated feature branch.


Execution evidence: baseline522 GREEN; initial three HTTP tests RED on missing routes;
visibility, operator/rollback, SQL NULL and review regressions RED→GREEN. Final full
suite550 GREEN (64/154/332), Release clean, EF model unchanged, seven-project NuGet
audit no vulnerable packages. Independent review fixes verified with no required
findings left. Local additive migration applied/reapplied; no privileged real accounts.
Ruling: removed parent FOR UPDATE from comment creation after deterministic legacy
before-insert deletion reproduced a timeout500; advisory serialization plus existing
FK404 handling preserves moderation and deletion semantics. Ruling: use an offline
maintenance host (not a second web service provider) for Admin appointment.
