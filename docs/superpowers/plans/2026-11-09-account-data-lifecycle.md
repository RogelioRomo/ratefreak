# Account data lifecycle implementation plan

> **For agentic workers:** Use superpowers:executing-plans or superpowers:subagent-driven-development. Track the steps below.

**Goal:** Deliver user-controlled export and permanent deletion with accurate retention disclosures.

**Architecture:** Keep lifecycle operations in a dedicated service behind authenticated, CSRF-protected endpoints. Coordinate with the existing PostgreSQL moderation lock. The browser uses operation-specific confirmation grants and the existing account identity transition.

**Tech stack:** .NET/Identity/EF Core/PostgreSQL, Next.js/React/TanStack Query, ZIP streams and protected local files.

**Spec:** `docs/superpowers/specs/2026-11-09-account-data-lifecycle-design.md`

## Global constraints

- Preserve public custom catalog IDs and moderation flags without creator attribution.
- Recovery events: 90 days while active, removed on deletion. Resolved moderation records: 365 days after resolution. Backups: 30 days, enforced operationally.
- Grant lifetime five minutes; operation/account/security-stamp bound, single-use.
- Export archive limit 256 MiB; proxy export timeout five minutes; no persistent export/credential browser storage.
- No real account erasures, database resets, push or deployment during implementation.

## API contract shared by backend and frontend

All routes have prefix `/api/v1/users/me/data`.

- `GET /`: `{ hasPassword, providers: string[], recoveryEventDays:90, resolvedModerationDays:365, backupDays:30, confirmedOperation: "export"|"delete"|null }`.
- `POST /confirm`: `{ operation:"export"|"delete", password:string }` -> 204; issues HttpOnly grant cookie.
- `POST /external/{google|github}/start`: `{ operation }` -> upstream OAuth challenge; proxy returns `{authorizationUrl}`.
- `POST /export`: `{}` -> ZIP attachment.
- `POST /delete`: `{ handle:string }` -> 204; requires grant and exact handle.
- Cookie `ratefreak.lifecycle` (development) / `__Secure-ratefreak.lifecycle` (production), path `/api/v1/users/me/data`, Strict/HttpOnly, Secure in production.
- OAuth completion `{ reauthenticated:true, operation, provider }` navigates through the proxy to `/account?dataAction=export|delete`. This marker is not authorization.

## Review focus

- Detached catalog authors remain reportable; no inner-join disappearance or null dereference.
- Deletion races with stale authenticated writers cannot recreate data or produce unhandled FK failures.
- Deletion dialogs distinguish immediate erasure, open reports, retained public entries and backup deadlines.
- Operation grants cannot be replayed, used across accounts/operations or survive stamp changes.
- ZIP downloads preserve headers, bound resources, cancel upstream work and clean temporary files.

### Task 1: Backend lifecycle and persistence

**Files:** new `Application/Identity/AccountData.cs`, infrastructure `Authentication/AccountData*.cs`, `AccountErasure*.cs`, API `Endpoints/AccountDataEndpoints.cs`; modify Identity/catalog/moderation persistence, OAuth endpoints, DI/security and a generated migration. Tests: `tests/RateFreak.IntegrationTests/AccountDataTests.cs`.

**Interfaces:** Produces the API contract above; exports return a disposable seekable stream. Erasure journal/replay and retention sweep expose callable operations for tests/maintenance.

- [x] Write and run failing PostgreSQL tests for secret-free isolated ZIP export, confirmation protection and erasure preserving public references.
- [x] Implement confirmation/grants, bounded ZIP, transactional deletion, pseudonymization, safe artwork cleanup and journal/replay.
- [x] Extend existing OAuth purpose validation for same-account lifecycle confirmation.
- [x] Add migration and regression tests for null authors, last-admin checks, retained reportability, concurrent stale writers, rollback, replay, cleanup and retention boundaries.
- [x] Run backend tests and model consistency checks; do not apply migration to the persistent database as part of tests.

### Task 2: Browser proxy and account controls

**Files:** new `web/features/auth/api/account-data.ts`, account data settings/dialog components and tests; modify account tabs, route/cookie/OAuth policies and proxy tests; add bounded export stream helper.

**Interfaces:** Consumes the contract above. Use `session.changeIdentity` for deletion and `session.runExclusive` where needed; never cache secrets or automatically retry actions.

- [x] Write and run failing tests for ZIP routing/stream headers and the final deletion dialog's retention text and cancel behavior.
- [x] Implement bounded export proxy, grant-cookie forwarding and OAuth completion navigation.
- [x] Implement Data and privacy tab, password/provider confirmation, ZIP download and accessible modal with typed handle and final destructive confirmation.
- [x] Test download/credential failures, provider callback marker without authorization, expiry/retry, identity/cache clearing and dialog keyboard behavior.
- [x] Run full frontend tests, TypeScript and ESLint.

### Task 3: Integration, operations and final verification

**Files:** `docs/adr/0017-account-data-lifecycle.md`, `docs/account-data-operations.md`, README, web README, STATUS, release gates and design surface documentation.

- [x] Document API/configuration, 30-day backup expiration, independent erasure journal and restore replay.
- [x] Review integrated code against all five review-focus cases and fix material findings with regression tests.
- [x] Run full backend/frontend suites, TypeScript/ESLint and relevant builds; record browser or infrastructure limits.
- [x] Update this plan with actual results and report changes without claiming deployment or enforced infrastructure backup expiry.

Implementation is already authorized by the user's approval of the design and dialog addition. Continue without another approval checkpoint; no commit/push is implied.


## Execution record

Implemented against the shared contract in separate backend/browser work areas.
During implementation verification, no real account was erased, no persistent
database migration/reset was performed, and no commit/push/deployment was made.
Subsequently, the user explicitly requested applying the migration: on 2026-11-09
it was applied to local `ratefreak`, with a private backup and identical row counts/
content fingerprints across all 25 existing tables. Journal setup remains separate. Disposable PostgreSQL integration fixtures
and a separate localhost API/frontend/browser database supplied runtime evidence.

Implementation rulings: journal files must be preprovisioned with durable directory
entries and private permissions; complete records survive torn trailing writes,
while malformed complete records fail closed. Export acquires the session advisory
lock before beginning its repeatable-read snapshot. Downgrade refuses already
detached ownership instead of fabricating accounts. The confirmation dialog is
explicitly modal and includes both active-account and post-deletion retention.

Review fixes: added the 90-day recovery disclosure inside the final dialog; covered
torn journal tails; a real browser caught an account-screen redirect race after
successful deletion, fixed with a synchronous parent-owned navigation guard and
success/lost-response/failure integration tests. No timers or persistent client
storage are used for this guard.

Final verification counts and infrastructure limits are recorded in
`docs/STATUS.md` and `docs/frontend-release-verification.md`.
