# Staff moderation implementation plan

> **For agentic workers:** Use superpowers:executing-plans. Preserve native execution and this checkout. User approved proceeding and temporary local staff provisioning on2026-11-05.

**Goal:** Deliver guarded staff report handling, visibility and audit workflows.
**Architecture:** Feature-owned typed transport, account-scoped query keys and an external per-account denial latch. A shared staff boundary conceals private data during session/read uncertainty; version-bound editors never replay unknown POST outcomes.
**Tech stack:** Existing Next16/React19/TanStack Query5/TypeScript/CSS Modules/Vitest.
**Spec:** `docs/superpowers/specs/2026-11-05-frontend-moderation-design.md`.

## Global constraints

No backend/schema/dependency changes, real-account promotion, automatic commit,
merge/push/deployment. Exact guarded staff API routes only. Inputs1..1000 trimmed
characters, canonical UUIDs, page1..10000/pageSize20. Preserve raw timestamp strings.
No sensitive details/reasons in storage, URLs, mutation variables/history or logs.
No automatic mutation retries. Account changes discard staff data/editors.

## Review focus

-401/403 on any staff request cancels all staff activity and clears private cache;
explicit retry alone reopens authorization. Test with concurrent reads/writes.
-Same-account checks retain reason drafts hidden/inert/disabled; consent expires.
-Unknown writes cannot be repeated after reads/refocus/cancel/reopen.
-Confirmed204 feedback survives failed follow-up reads, while another write waits.
-Deleted-target dismissal uses null timestamp; exact microseconds survive requests.

## Files and responsibilities

Create `web/features/moderation/interfaces/moderation.ts` for DTOs/props/context.
Create `api/moderation.ts`, `query/filters.ts`, `query/keys.ts`,
`query/access.ts`, `hooks/use-staff-query.ts`, `hooks/use-staff-write.ts`.
Create focused boundary, navigation, feedback, paging, target-content, action-editor,
queue-screen, report-screen, target-screen, audit-screen components and CSS Module.
Thin `/moderation`, `/moderation/reports/[id]`,
`/moderation/{reviews,comments}/[id]`, `/moderation/audit` pages.
Create `web/server/api/moderation-policy.ts`; integrate into route/proxy query
validation. Extend only validated moderation continuation routes. Add Account link.

## Task 1: Contracts and exact guarded transport

- [x] Write failing policy/filter/API tests for exact GET lists/details/targets/audit,
POST resolve/visibility, forbidden admin routes, unknown/duplicate query params,
UUID validation, page limits, and unchanged raw timestamp/null request payloads.
- [x] Observe RED with `pnpm --dir web test features/moderation server/api/moderation-policy.test.ts`.
- [x] Implement typed DTOs/API and reusable URL filter codec with strict canonical
validation; wire route allowlist and query encoding into existing guarded proxy.
- [x] Verify focused tests GREEN, including Origin/CSRF/no-store proxy integration.

## Task 2: Staff boundary, reads and writes

- [x] Write failing tests for account-scoped reads, denial latching/cache removal,
session concealment/abort, fresh explicit authorization retry, cancellation and
late completions. Include concurrent403 and pending write cases.
- [x] Implement denial store outside Query cache, session/account boundary and
abort-aware useMutation hook (`retry:false`, `gcTime:0`, transient operation ref,
no sensitive variables/results). Queries run only under verified matching identity.
Explicit access retry performs reports GET before lifting denial; failure retains it.
- [x] Verify tests GREEN; no public namespace exception for staff queries.

## Task 3: Staff screens and version-bound action editor

- [x] Write failing behavior tests for queue/status/paging, literal/spoiler content,
null reporters/targets, own-author exclusion, exact timestamp consent and reset,
409 reload, terminal404, validation429, unknown outcome blocking and independent204.
- [x] Implement screens and thin routes. Add Account navigation and narrowly
validated login continuations. Preserve private editors across transient reads
under hidden/inert/disabled wrappers; remove on401/403. Explicit inline confirmation
binds target version/report status/action/reason and expires on session uncertainty.
- [x] After204 retain confirmed status outside concealed read wrapper, invalidate
staff/public/personal namespaces, and require successful fresh inspection before
another action. No automatic write replay. Keep report resolved state read-only.
- [x] Verify focused moderation/auth tests and typecheck/lint.

## Task 4: Full verification and live evidence

- [x] Run full frontend suite, lint/typecheck/build and whitespace checks.
- [x] Run existing PostgreSQL ModerationTests with zero skips.
- [x] Run real Chromium→Next→API→PostgreSQL: dedicated local staff membership
(user-authorized), ordinary-user403, queue/detail, resolve/hide/dismiss/restore,
raw-version409, public reads/audit persistence, role revocation and cache concealment,
CSRF/Origin, keyboard and both themes at320/768/1024/1440. No intercepted responses.
- [x] Guard cleanup by exact account/target/report/audit identities; remove temporary
staff membership/accounts and all test-owned content, preserving existing catalog.
- [x] Fresh whole-change reviewer; reproduce material findings RED→GREEN and rerun
relevant/full proof. Record evidence and remaining release work in frontend checklist.
Leave implementation uncommitted unless separately requested.

## Self-review

All design sections map to tasks. Revocation needs an external latch because
removing Query cache alone would cause refetch loops. Transient read failures must
conceal private data while retaining drafts; confirmed feedback is outside it.
Timestamp tokens remain raw strings. Live staff provisioning is user-authorized;
existing real accounts never change roles. Native execution and proceeding are
already authorized; no further execution-method choice is needed.


## Execution evidence

Complete native implementation; no automatic commit. Final588/588 frontend tests
across84 files, lint/typecheck/build/whitespace pass. PostgreSQL ModerationTests28/28,
zero skips. Fresh whole-change reviewer: no material findings or deferred minors.
Mechanical design scan[]. Live running API/browser verifies ordinary/revoked403,
explicit access retry, concurrent-edit409, review/comment hide/restore public404→200,
live/deleted dismissal, six audit events and CSRF400/Origin403 on all3 staff POST
routes. Eight responsive/theme captures plus revoked mobile inspected; no overflow
or runtime exceptions. Evidence `/tmp/moderation-*`.

Dedicated author/reporter/staff accounts plus two reviews, comments, four reports,
six audit events and a temporary Moderator role were guarded-cleaned; all matching
record counts0, two existing media rows preserved. Membership revoked before cleanup.
Local Development API restarted with unchanged settings after accumulated registration
budget blocked third account; exact first two accounts reused. No real account role
changes. Development frontend restarted after build; no production ingress claim.
