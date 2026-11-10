# Staff Management Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans to implement task-by-task.

**Goal:** Let administrators manage moderators inside the existing moderation UI.
**Architecture:** Extend the moderation service with an admin-only account directory; reuse audited moderator writes. Keep admin denial separate from general moderation denial in the browser.
**Tech Stack:** .NET, PostgreSQL/EF Core, Next.js, React Query, Vitest, xUnit.
**Spec:** `docs/superpowers/specs/2026-11-09-staff-management-design.md`

## Global Constraints

- No schema migration, deployment, persistent test-account mutations, or new dependencies.
- Expose only public identity and staff role flags; require existing admin authorization and CSRF for writes.
- Keep Admin roles read-only in this UI and require reasons of 1–1000 characters.
- Default to staff-only accounts; submitted search by handle/display name with deterministic pagination.

## Review Focus

- Moderator opening an admin page retains report-queue access.
- Account changes and lost admin privileges conceal cached admin data.
- Literal searches containing percent/underscore never become wildcard searches.
- Failed/uncertain writes require refreshing before another attempt.
- Read-only Admin rows and cancellation cannot invoke role mutations.

### Task 1: Admin account directory

Files: `Moderation.cs`, new `ModerationAccounts.cs`, `ModerationEndpoints.cs`, new `StaffManagementTests.cs`.
Interface: GET `/api/v1/admin/accounts?q=&staffOnly=true&page=1&pageSize=20` returns `ModerationPage<StaffAccount>` with id, handle, displayName, isAdmin, isModerator. q trimmed, maximum 100 characters; paging existing bounds; reject unknown/duplicate queries.
- [x] Write/run failing integration tests for permissions, no-store/privacy, literal search, staff filter, pagination, validation and current roles.
- [x] Implement bounded directory query and exact endpoint; preserve existing write behavior.
- [x] Run relevant integration tests until green.

### Task 2: Browser access and proxy

Files: new `staff-management` filters/API/access modules and tests; `moderation-policy.ts`, return-to filters and tests.
Interface: `getStaffAccounts`, `setModerator`, independent admin request boundary using staff account/session cancellation.
- [x] Write/run failing tests for exact proxy routes/filters, safe return URLs and admin denial isolation.
- [x] Allow GET admin/accounts and PUT/DELETE admin/moderators/{UUID} only; canonical validated directory queries.
- [x] Implement separate admin access scope, sharing 401 global staff denial while containing 403 to admin scope.
- [x] Run focused frontend tests until green.

### Task 3: Staff-management screen

Files: new staff screen/editor/hooks/interfaces, `/moderation/staff/page.tsx`, existing staff navigation/CSS; new component tests.
- [x] Write/run failing UI tests covering list, search/filter controls, role indicators, read-only Admin rows, reason/confirmation/cancel, success refresh, failure lock and session isolation.
- [x] Implement accessible components using existing styles and account-scoped queries; disable writes during refetches and after failures until refresh.
- [x] Run UI tests, then frontend suite/lint/typecheck/build and backend tests/Release publish.
- [x] Independent final review; no important findings.
- [x] Restart the local candidate, inspect desktop/mobile without changing persistent roles, and update STATUS with actual evidence.

## Execution record

Ruling: Work in the existing checkout on `feat/staff-management`, preserving the user-requested running application, ignored local configuration, installed dependencies and database access. No second checkout or commits to main.
Ruling: Execute inline following the user's approval; no additional implementation-choice approval is needed. Leave changes reviewable and uncommitted unless requested.

Task 1 complete: seven directory tests failed before implementation. Investigation
of initial 500 responses found EF could not filter on a constructed record;
filtering the anonymous database projection before mapping to StaffAccount fixed
the translation. All 36 moderation tests then passed; the complete integration
suite passed 404 tests.

Task 2 complete: scoped-denial regression failed before the access change; new
filter/proxy tests failed before their implementation. Focused checks passed.

Task 3 complete: seven UI tests failed before the new screen existed, then passed.
Final frontend suite: 675/675, 92 files. Lint, typecheck, production build and API
Release publish passed. Unit suite 27/27 and provider contracts 154/154 passed.
Initial no-restore failures came from Windows paths in pre-existing assets files;
restoring existing dependencies corrected those paths. No dependencies added.
Independent reviewer reported no important findings. Disposable real-browser
verification passed, including 1440/768/390/320px layouts. Temporary evidence is in
`/tmp/ratefreak-staff-browser`; test services/database were removed after checking.
