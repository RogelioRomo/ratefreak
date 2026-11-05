# Public content reporting implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Native execution is preserved from the frontend checklist.

**Goal:** Let readers explicitly report another person's public review or comment with safe session, duplicate and uncertain-outcome behavior.

**Architecture:** A feature-owned reporting control mounts an account-keyed editor on each public content surface. An abort-aware TanStack mutation calls the existing browser transport through two exact proxy routes. Reports do not mutate public content or invalidate public queries.

**Tech Stack:** Next.js 16, React 19, TypeScript, TanStack Query 5, CSS Modules, Vitest and Testing Library; existing shared Select and Button.

**Spec:** `docs/superpowers/specs/2026-11-05-frontend-reporting-design.md` (approved 2026-11-05).

## Global constraints

- Preserve this checkout and existing work; no automatic commits, merges, push or deployment.
- No backend/schema/dependency changes, real-account staff promotion or provider activation.
- Separate interfaces, API functions, hooks and focused components; reuse editorial tokens.
- Reasons: `spam`, `harassment`, `hate`, `sexual_content`, `violence`, `spoiler`, `other`.
- Trim details; maximum 1000 characters; Other requires nonempty details.
- Never retry POST automatically or put report details in URLs, storage or retained mutation history.
- Session uncertainty hides/inerts/disables retained drafts and aborts writes; confirmed identity changes discard drafts and feedback.
- 201 is confirmed submission; 409 is an already-reported terminal state; unknown outcomes block resubmission and warn that reload loses drafts.

## Review focus

- A public background-read failure must retain an open report draft; a confirmed 404 must conceal it and prevent writes (Task 3).
- A session check followed by the same account must retain input but an account switch must discard input and success feedback (Task 2).
- A late successful response after cancellation must not become success for a different account (Task 2).
- Opening/cancelling reports must preserve concealed spoilers and return keyboard focus sensibly (Tasks 2–3).
- Duplicate reporting remains terminal after report resolution; no invented persistent reporter history (Tasks 1 and 4).

## File map

- Create `web/features/reporting/interfaces/reporting.ts`: request/acknowledgement, target, props and write-result interfaces/types.
- Create `web/features/reporting/api/reports.ts` and `.test.ts`: typed report POST.
- Create `web/server/api/reporting-policy.ts`; modify `route-policy.ts`, `route-policy.test.ts`, `proxy.test.ts`: exact route allowlisting and inherited security.
- Create `web/features/reporting/hooks/use-report-write.ts` and `.test.tsx`: abort/identity-aware mutation without retained details.
- Create `web/features/reporting/components/{report-control,report-form,report-error}.tsx`, `reporting.module.css`, and component tests: account boundary, form and honest feedback.
- Modify `web/features/personal/components/public-reviews.tsx`: report action and retained background-error content.
- Modify `web/features/comments/components/{review-context,comment-card,discussion-screen,comment-list}.tsx` and `interfaces/comments.ts` only as needed to propagate availability.
- Extend existing personal/comment integration tests; add reporting coverage where separate tests give clearer boundaries.

### Task 1: Exact transport and reporting contract

**Interfaces:** `ReportTarget = { type: "review" | "comment"; id: string; authorId: string }`; `ReportInput = { reason: ReportReason; details?: string | null }`; `ReportAcknowledgement = { id: string; status: string; createdAt: string }`. Export `createReport(target: ReportTarget, input: ReportInput, signal?: AbortSignal): Promise<ReportAcknowledgement>`.

- [x] Write failing API tests asserting review/comment POST paths, exact reason/details bodies, signal forwarding and 201 acknowledgement; verify 409 remains an ApiError rather than success or a retry.
- [x] Write failing route tests: only POST `reviews/{guid}/reports` and `comments/{guid}/reports` pass; reject GET/PATCH/DELETE, malformed IDs, extra segments, staff routes and report history paths.
- [x] Run `pnpm --dir web test features/reporting/api/reports.test.ts server/api/route-policy.test.ts` and observe the missing feature failures.
- [x] Implement the interfaces, API and `reportingRoute(method: string, path: readonly string[]): boolean`; call it from the existing route policy without broadening other routes.
- [x] Add proxy tests for both routes covering foreign/missing Origin, CSRF forwarding, JSON/body limits, upstream status/Retry-After, no-store and cancellation using the existing proxy fixtures.
- [x] Run `pnpm --dir web test features/reporting/api/reports.test.ts server/api/route-policy.test.ts server/api/proxy.test.ts`; require all passing.

### Task 2: Session-safe report control and form

**Interfaces:** `ReportControl({ target, available = true }: ReportControlProps)` owns cached-account identity and anonymous login continuation. `ReportForm({ target, accountId, available, onCancel }: ReportFormProps)` owns draft and feedback. `useReportWrite()` returns `{ run, pending }`, where `run(operation: (signal: AbortSignal) => Promise<ReportAcknowledgement>)` returns saved/rejected/uncertain. Use TanStack `useMutation` with `retry: false`, `gcTime: 0`, and no report input in mutation variables or results retained by its cache; pass operation through a transient ref and clear it after settlement.

- [x] Write failing control/form tests for anonymous continuation without submission, known own-author exclusion, all seven reasons, trimmed details, Other whitespace rejection and 1000-character limit. Use unique IDs and native error associations.
- [x] Add failing tests for 201 feedback, terminal 409, unavailable 404, 400 validation, 401 refresh, forbidden 403, 429 Retry-After and preserved draft. Network/408/5xx/cancelled outcomes block resubmission and show reload/draft-loss warning.
- [x] Add failing hook/control tests for duplicate clicks, unavailable guards, abort on check/unmount, late completions, same-account draft retention, identity change reset, and absence of details in Query mutation state/history after settlement.
- [x] Run the focused reporting tests and observe failures before implementation.
- [x] Implement the hook using existing session lifecycle conventions, without importing the comment feature's write hook. Retain the account-keyed editor during checks using cached session identity; require a confirmed matching live account before writing.
- [x] Implement the compact inline form with shared Select/Button, explicit moderation-details copy, contextual hint/errors, disabled fieldset and hidden/inert guards. Do not render target body or reveal spoilers. Focus the reason control on open and restore focus to Report on cancel; announce confirmed/duplicate/uncertain feedback.
- [x] Add token-based CSS with wrapping actions, 16px input text and usable narrow-width controls. Keep confirmed feedback independent from read refreshes; do not invalidate public queries.
- [x] Run `pnpm --dir web test features/reporting`; require all passing.

### Task 3: Integrate all public content surfaces

**Consumes:** Task 2 `ReportControl`; target author IDs are already present on public DTOs. Preserve current public page path and query in login continuation through an explicitly validated return path. If extending `safePreviewReturnTo`, permit only canonical review `page` and catalog `reviewsPage` parameters with integer values 1..10000; reject foreign origins, malformed IDs, extra paths and unrelated query parameters.

- [x] Write failing tests for Report on catalog review cards, original discussion review and comment cards; verify own-content exclusion and anonymous login never posts.
- [x] Write failing regressions for retained report drafts after catalog review background errors, review/comment refresh errors and same-account checks; confirmed missing parent/list content hides/inerts controls and aborts pending report writes. Propagate explicit availability to the original review and comment report controls.
- [x] Add failing tests for page-aware safe login continuation, spoiler concealment during report editing and keyboard open/cancel behavior. Preserve existing review/comment edit and pagination behavior.
- [x] Run the owning personal/comment/return-to test files and observe the new assertions failing.
- [x] Mount the shared control on each surface, keyed by stable target and account identity. Update `PublicReviews` to retain existing data on background failure like `CommentList`; show independent recovery text and conceal/inert retained 404 content. Preserve the existing loading/empty/paging states.
- [x] Run `pnpm --dir web test features/reporting features/personal features/comments features/auth/query/return-to.test.ts`; require all passing.

### Task 4: Full verification and delivery record

- [x] Run `pnpm --dir web test`, `pnpm --dir web lint`, `pnpm --dir web typecheck`, `pnpm --dir web build` and `git diff --check`; require successful output. Escalate only when actual sandbox restrictions require it.
- [x] Run existing real PostgreSQL reporting integration tests with zero skips; find their exact names in `tests/RateFreak.IntegrationTests/Moderation*Tests.cs`. Include duplicate lifetime, author exclusion and hidden-parent behavior without promoting real accounts.
- [x] Verify real browser → Next → API → PostgreSQL review and comment reports with dedicated fixture accounts/content. Exercise 201, duplicate reload/409, Other validation/recovery, login continuation, keyboard focus, concealed spoilers and Origin/CSRF/anonymous-write guards. Record exact fixture IDs before writes and perform guarded exact-identity cleanup, verifying zero matching fixture records remain.
- [x] Capture and inspect both themes at 320/768/1024/1440px; check overflow, runtime errors and interaction during session checks. Use synthetic failures only for separately labeled recovery checks, not as evidence for real persisted reports.
- [x] Request one fresh whole-change review per executing-plans; review session isolation, unknown outcomes, target availability, mutation privacy and proxy restrictions. Fix material findings with failing regressions, then rerun the relevant proof and full suite.
- [x] Update `docs/frontend-checklist.md` with behavior, precise checks/results, artifacts, cleanup, remaining limits and next staff slice. Mark only public reporting complete; Phase 7 and external sign-in stay pending. Leave work uncommitted.

## Plan self-review

All approved design sections map to Tasks 1–4. The plan preserves native execution,
existing checkout, exact transport guards, feature boundaries and release limits.
Added explicit availability propagation and retained catalog background-error data
because reports introduce drafts on a surface that currently unmounts on errors.
Page-aware login continuation requires narrowly extending the existing validator;
its current form accepts only bare canonical review/catalog paths.


## Execution evidence — 2026-11-05

Native implementation complete. Baseline534 tests; final556/556 across77 files.
Focused missing-feature tests observed RED→GREEN. Lint/typecheck, production
build and whitespace checks pass. Existing PostgreSQL ModerationTests28/28,
zero skips. Fresh whole-change reviewer found no material findings; no deferred
minor findings. Mechanical design scan returned `[]`.

Live Chromium→Next3100→API5080→PostgreSQL verified reports201 for both target
kinds, persisted duplicates409, CSRF400, foreign-Origin403, anonymous401,
own-content exclusion, Other validation/recovery, real429 draft retention,
page-aware login continuation with no automatic report, focus restoration and
native keyboard skip navigation. No intercepted/synthetic API responses.
Eight captures cover both palettes at320/768/1024/1440; all inspected, no
horizontal overflow or runtime exceptions. Scripts/captures `/tmp/reporting-*`.

Restarting Development Next resolved a reload loop during initial automation.
A temporary production preview correctly rejected Development-only proxy/cookie
configuration; successful live verification uses Development, not production
hosting. No guard was weakened. Sandboxed Next TypeScript subprocess/MSBuild
IPC failures were rerun successfully outside the sandbox.

Two dedicated accounts, one review, one comment and two persisted reports were
removed in an exact-identity guarded transaction; all matching record counts
are zero. Existing Breaking Bad catalog row preserved. No product database staff
promotion. No backend/schema/dependency/configuration changes, commits or deploy.
