# Public Profiles Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Safe public profile reads and owner-only display-name/bio partial editing.

**Architecture:** Extend AccountUser with nullable Bio, expose PublicProfile DTO through IUserProfileService, and keep AccountProfile private. Explicit optional-field tracking prevents omitted values clearing stored data; one atomic field-selective EF update and response projection share a transaction.

**Tech Stack:** .NET10, ASP.NET Core Minimal APIs/Identity, EF Core/PostgreSQL, xUnit HTTP tests.

**Spec:** docs/superpowers/specs/2026-10-30-public-profiles-design.md (approved by user).

## Global Constraints

- Fixed handles; only displayName and bio editable; owner derived from session.
- DisplayName trimmed1..80 valid Unicode without controls; Bio optional/null or trimmed literal text<=500 UTF-16 units, only tab/CR/LF controls.
- Public DTO id/handle/displayName/bio/createdAt, never email/security/private state.
- CSRF, strict JSON/duplicates, no-store including case/trailing slashes.
- profile-read60/IP/min, profile-write10/account/min, no queues/429 Retry-After.
- Omitted fields preserved; concurrent disjoint updates merge, same-field last-write-wins.
- No Identity stamp/credential/mapping mutation; no frontend, real accounts, merge or push.
- Guard Down while any bio is nonnull.

## Review Focus

- Case variants and trailing slash must retain no-store and authorization (Task1).
- Nullable field tracking must distinguish omission and explicit null (Task1).
- Authenticated account deleted during write must not produce orphan/500 (Task2).
- External sign-in and local login must return the same owner bio contract (Task2).
- Concurrent profile updates must not overwrite security state or each other (Task2).

### Task 1: Profile HTTP contracts and persistence

**Files:** Application/Identity/UserProfiles.cs; Infrastructure/Authentication/UserProfileService.cs; AccountUser.cs; Persistence/RateFreakDbContext.cs and migration; Api/Endpoints/UserProfileEndpoints.cs; Api/Security/ApiSecurity.cs; Api/Program.cs; tests/RateFreak.IntegrationTests/UserProfileTests.cs.

**Interfaces:** IUserProfileService.FindAsync(Guid,CancellationToken), FindByHandleAsync(string,CancellationToken) -> PublicProfile?; UpdateAsync(Guid,UpdateProfileRequest,CancellationToken) -> ProfileUpdateResult. AccountProfile adds nullable Bio as final field.

- [x] Write failing HTTP tests for public ID/handle reads, safe exact DTO, owner PATCH and omitted/null semantics, unauthorized/CSRF, invalid input and missing users. Run filtered UserProfileTests; expect missing-route RED.
- [x] Implement presence-aware request and validation, explicit public/private projections, service transaction, profile routes/rate policies/metadata; update local/external AccountProfile construction.
- [x] Generate/inspect additive users.Bio migration plus CHECK and guarded Down.
- [x] Run focused tests GREEN; commit.

### Task 2: Identity preservation, concurrency and migration proof

**Files:** tests/RateFreak.IntegrationTests/UserProfileSecurityTests.cs, UserProfileConcurrencyTests.cs, UserProfilesMigrationTests.cs; relevant service/schema files as needed.

**Interfaces:** consumes Task1 profile request/response contracts; no new public operations.

- [x] Test exact Identity security/credentials/login mappings preserved and sessions usable after edits; updated author names in reviews/comments/follows.
- [x] Test simultaneous disjoint updates, deletion before update401 and blocked deletion between update/projection; use real PostgreSQL/interceptors with bounded gates.
- [x] Test per-account write/IP read budgets, Unicode/UTF-16 boundaries, safe no-store errors, strict JSON, Development OpenAPI and external AccountProfile bio.
- [x] Test Down refusal with existing bio and safe downgrade/reapply preserving Identity/catalog/social/moderation data.
- [x] Run focused tests and relevant account/social/migration regressions GREEN; commit.

### Task 3: Review and release evidence

**Files:** README.md; docs/adr/0012-public-profiles.md; docs/implementation-checklist.md; approved design and this plan.

- [x] Run complete solution tests, Release build, EF model check and transitive vulnerability audit. Expected: no failures/skips/warnings/model changes/vulnerable packages.
- [x] Independent static review of whole branch; fix required findings with failing regression tests first.
- [x] Apply/reapply local migration, read-only temporary API smoke, stop temporary API. No persistent verification accounts.
- [x] Record evidence, mark completed steps and commit; leave feature branch unmerged.

## Execution ledger

Existing isolated feature branch feat/public-profiles retained in current permitted
workspace. User approved design and continuation; native execution continues with
one final independent reviewer. Pre-flight: Task2 consumes Task1 DTOs/service/schema;
Task3 consumes verified routes and migration. No interface conflicts found.

Ruling: tests compare profile timestamps against a persisted login/read response,
not the pre-persistence registration clock (PostgreSQL truncates to microseconds).
Existing security-stamp validation renews cookies on authenticated requests; the
profile service must not explicitly refresh/revoke sessions. Security-field
snapshots and a subsequent authenticated request prove preservation.

Ruling: independent review identified stale Identity full-row writes overwriting
profile edits. Reproduced name reversion. Added DisplayName/Bio/UpdatedAt EF
concurrency tokens, preserving Identity stamps and column schema. New logout-race
test then reproduced500; logout now locks/reloads user before stamp revocation.
These safeguards preserve approved profile/security contracts.

Ruling: full suite exposed historical external-sign-in migration tests seeding/loading
current AccountUser (now includes Bio) against older schema without Bio. Seed only
historical columns and project only fields being preserved; keep duplicate-pair
refusal and account/security preservation assertions unchanged. Observed both
historical cases RED before correction.

Completion evidence: initial17 profile contracts RED on missing routes; migration
rollback guard RED→GREEN. Focused30 profile/OAuth cases GREEN plus cache-boundary
case; stale Identity overwrite and logout race regressions RED→GREEN. Final full
suite582 GREEN (64 unit/154 provider/364 integration), zero failed/skipped. Release
zero warnings/errors; EF model matches migration; seven-project transitive audit
no known vulnerable packages. Reviewer verified fixes and historical-test repair;
no remaining required findings. Applied/reapplied AddUserBios locally; users0,bios0,
media2,reports0,audit0,assigned roles0; migration history once. Temporary5081 health,
Swagger/OpenAPI schemas and profile404/400/401/no-store checks pass; server stopped.
