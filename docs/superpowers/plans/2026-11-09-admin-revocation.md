# Offline Admin Revocation

Implements the user's request and ADR 0019's Admin-removal requirement using the
existing appointment/audit pattern. The user subsequently requested Admin removal
in Staff management too. Execute inline; no schema change. Existing Moderator
removal stays in place; do not add a new offline Moderator command.

## Behavior

- `--revoke-admin <existing-account-uuid> --reason <reason>` exits without HTTP or migrations.
- Validate UUID and existing 1–1000-character plain-text reason rules.
- Remove only Admin, preserving Moderator, account contents and security stamp.
- Under the existing global moderation lock and target-row lock, refuse removal
  of the last Admin; concurrent removals must leave at least one Admin.
- Record a real removal as operator-attributed `revoke_admin`, before true/after false.
- Repeated removal is idempotent with no additional event. Exit codes remain
  0 success, 2 invalid/missing/last-admin refusal, 1 operational failure.
- Current-role authorization denies subsequent admin requests, including existing
  sessions. Account deletion/retention and named external attribution stay unchanged.
- Add admin-only, CSRF-protected DELETE `/api/v1/admin/administrators/{id}` with
  required reason and existing role-write budget. Record the authenticated actor.
  No HTTP Admin-grant or baseline User-revocation operation is added.
- Existing Admin rows gain Revoke Admin with reason/confirmation. Last-admin409
  explains another Admin must remain; refresh roles before retries. Self-removal
  when another Admin exists must immediately conceal admin-only content. Preserve
  any existing Moderator membership. Granting Admin remains operator-managed.

## Tasks

- [x] Add failing CLI/service tests for actual removal, audit/idempotency, existing
  session denial, retained Moderator/security state, last-admin concurrency and
  strict command arguments.
- [x] Add `RevokeAdminAsync`, the protected API and UI, and extend offline command dispatch.
- [x] Run relevant backend tests/Release publish and independent review.
- [x] Update staff runbook, policy, README and release gates with supported command
  and remaining deployed verification. Verify no persistent role changes.

Leave changes uncommitted for review unless the user requests a commit.

## Evidence and decisions

Eight added backend cases cover offline/API revocation, argument validation,
idempotency/audit, preserved account/security state, retained Moderator access,
current-session denial, last-Admin refusal and concurrent removals. The API test
first failed at its missing route; service tests first failed at the missing
interface method. Tests now pass in disposable databases.

The UI tests first failed at missing Admin controls/proxy permission. Five added
UI cases now cover confirmation/refresh, cancellation/focus, 409 handling,
self-removal concealment and clearing old success feedback before a new decision.
Existing Moderator removal stays in place. There is no offline Moderator command
or baseline User-revocation route.

A recovery fixture was corrected to use a fresh service scope after an HTTP role
change, matching real requests/operator invocations rather than retaining stale
tracked EF memberships. Final backend suites: 419 integration, 27 unit and 154
provider contracts, zero skipped. Final frontend suite: 687 tests in 95 files.

Lint, typecheck, frontend production build and API Release publish passed. An
overlapping build/typecheck run initially raced on generated `.next/types` files;
parallel CPU load also caused three existing frontend tests to time out. Checks
passed after completion and sequential rerun, without changing those tests.

Independent review found no important source issues; its stale operational-doc
finding was fixed, then reviewed again. Real Chromium acceptance passed on
disposable services/accounts at 1440/768/390/320px, including last-Admin refusal,
retained Moderator/User access, self-removal concealment, audit and offline refusal.
Evidence: `/tmp/ratefreak-admin-revoke-browser/results.json`. No browser runtime errors.

Local API restarted with the final code; health, Staff management and anonymous
rejection at the new DELETE route verified. No migration or real role changes
were needed. Deployment attribution/retention and deployed removal/recovery remain
release gates.

The user subsequently requested committing these changes and merging them into
local `main`, superseding the earlier instruction to leave them uncommitted.
