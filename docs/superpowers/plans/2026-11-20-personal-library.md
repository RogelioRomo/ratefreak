# Personal Library Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox syntax for tracking.

**Goal:** Add private-library title search, ordering, own-rating display and safe inline status updates.

**Architecture:** Extend the database-backed library query and additive response fields. Add a status-only transactional operation with expected-status and expected-account checks. Extend the existing Next.js proxy and list UI using account-scoped query invalidation.

**Tech Stack:** .NET 10, EF Core/Npgsql/PostgreSQL, Next.js 16, TypeScript, React, TanStack Query, CSS Modules, xUnit and Vitest.

**Spec:** `docs/superpowers/specs/2026-11-20-personal-library-design.md` (approved 2026-11-20).

## Global Constraints

- Existing callers retain Recently added ordering when new parameters are absent.
- Library page size remains 20; title search is a literal case-insensitive substring, trimmed, at most 200 characters.
- Sort choices: Recently added, Recently completed, Title A–Z, Highest rated, Lowest rated.
- Undated/unrated entries sort last for their respective sorts; media ID is the final deterministic tie breaker.
- Quick status updates preserve stored dates, never recreate deleted entries and never alter ratings or sharing.
- Preserve the existing visual language, both themes, responsive layout and account-transition concealment.
- Interface definitions remain in separate files. No new dependencies or persistent migrations expected.
- Do acceptance writes against disposable data; preserve the running local inspection services.

## Review Focus

- Literal `%`, `_` and backslashes must not broaden title searches (Task 1).
- Zero ratings must remain visible and sort ahead of unrated entries (Tasks 1 and 4).
- Concurrent date-only edits must survive an inline status save; concurrent status changes must conflict (Task 2).
- Account switching must never write to the new account from an old rendered row (Tasks 2–4).
- Removing the last filtered row on a later page must restore useful pagination and keyboard focus (Task 4).

### Task 1: Searchable, sortable library query and own ratings

**Files:** Modify `src/RateFreak.Application/PersonalMedia/PersonalMedia.cs`, `src/RateFreak.Infrastructure/PersonalMedia/LibraryService.cs`, `src/RateFreak.Api/Endpoints/PersonalMediaEndpoints.cs`; create `tests/RateFreak.IntegrationTests/LibraryWorkspaceTests.cs`.

**Interfaces:** Extend `LibraryQuery` with optional `string? Search = null, string? Sort = null` after its existing fields. Extend `LibraryItem` with `decimal? OwnRating = null`. GET `/api/v1/users/me/library` accepts `q` and `sort`; sort values are `recently_added`, `recently_completed`, `title`, `highest_rated`, `lowest_rated`.

- [ ] Write integration tests asserting combined filters and case-insensitive literal search for `%`, `_` and backslash; trimmed/blank/200-character queries; invalid and duplicate parameters; counts and page boundaries.
- [ ] Add fixtures with different completion dates, equal titles/dates/scores, zero scores, missing scores, another user's ratings and unreadable custom items. Assert every ordering, null placement, stable page ties and own-rating privacy.
- [ ] Run `dotnet test tests/RateFreak.IntegrationTests --filter FullyQualifiedName~LibraryWorkspaceTests`; confirm the new behavior fails before implementation.
- [ ] Implement endpoint validation and a database projection/query using own-rating joins or correlated subqueries. Preserve repeatable-read count/page consistency and apply ordering before Skip/Take. Recently completed ties use creation descending then media ID; title ties use creation descending then media ID; score ties use creation descending then media ID.
- [ ] Run the focused integration tests; require all passing with no skips. Inspect generated query shape for bounded execution and absence of per-row database requests.
- [ ] Commit the tested query change.

### Task 2: Status-only operation with conflict and identity protection

**Files:** Modify the same application/service/endpoint files and `LibraryWorkspaceTests.cs`.

**Interfaces:** Add `LibraryStatusRequest(string? Status, string? ExpectedStatus)` rejecting unmapped members; `LibraryStatusWriteResult` with Success/NotFound/Conflict and optional `LibraryItem`; `ILibraryService.UpdateStatusAsync(Guid userId, Guid mediaId, LibraryStatusRequest request, CancellationToken ct)`. PATCH `/api/v1/users/me/library/{mediaId}/status` requires `X-RateFreak-Account-Id`, CSRF and the existing personal write budget.

- [ ] Write API tests for successful status-only edits, saved-date preservation after an intervening date edit, changed-status conflicts, concurrent updates, missing/deleted/unreadable entries, malformed status/body, missing/wrong/duplicate account headers, anonymous access and missing CSRF.
- [ ] Run the focused integration suite and confirm new status behavior fails before implementation.
- [ ] Implement the operation under the same moderation/entry serialization as existing tracking writes. Compare the expected status and update only status/updated time; return 404 for missing/inaccessible entries and 409 for changed status/account mismatch. Same-target idempotent retries may succeed only if they cannot overwrite a third status. Do not change existing full tracking PUT semantics.
- [ ] Run focused integration tests including existing `PersonalMediaTests` and `DeletedAccountWriteTests`; require passing results without skips.
- [ ] Commit the tested write contract.

### Task 3: Proxy and browser API contracts

**Files:** Modify `web/server/api/personal-policy.ts`, `web/server/api/proxy.ts`, their relevant tests, `web/features/personal/interfaces/personal.ts`, `web/features/personal/query/filters.ts`, `web/features/personal/query/filters.test.ts`, `web/features/personal/api/personal.ts` and `web/features/personal/api/personal.test.ts`.

**Interfaces:** `LibraryFilters` gains optional `q?: string` and `sort?: LibrarySort` to preserve existing consumers; parsing returns normalized values. `LibraryItem` gains optional nullable `ownRating` for backward-compatible fixtures. Export `saveLibraryStatus(id: string, status: TrackingStatus, expectedStatus: TrackingStatus, accountId: string, signal?: AbortSignal): Promise<LibraryItem>`.

- [ ] Add failing tests for search/sort URL round trips, invalid/duplicate input, search length, custom media filters, status-route allowlisting, forwarded expected-account header, and exclusion of that header from unrelated routes.
- [ ] Run `pnpm --dir web test features/personal/query/filters.test.ts features/personal/api/personal.test.ts server/api/personal-policy.test.ts`; confirm expected failures.
- [ ] Implement query normalization with the Task 1 values and explicit status route allowlisting. Forward expected-account only on this operation (alongside the existing avatar exception). Use the existing JSON/CSRF/Origin/body-size protections.
- [ ] Run the same tests plus route/proxy tests; require passing results.
- [ ] Commit the tested browser contract.

### Task 4: Library search, sorting, ratings and row updates

**Files:** Modify `web/features/personal/components/library-screen.tsx`, `personal.module.css`, `web/features/personal/interfaces/component-props.ts`; create `web/features/personal/components/library-row.tsx` and `library-screen.test.tsx`; update existing `personal.test.tsx` only where the intentional UI changes require it.

**Interfaces:** `LibraryRowProps` in the separate props file contains `item: LibraryItem`, `accountId: string`, and `onSaved: (mediaId: string) => void`. Consume Task 3 parsing/API and existing `usePersonalWrite` plus account-scoped personal query keys.

- [ ] Read the applicable local Next.js documentation and frontend UI skill before changing components.
- [ ] Add failing component tests for submitted search/sort/filter URLs, Back/Forward state, own score zero/unrated display, status save body/header, pending duplicate prevention, conflict refresh/retry, request failure, account transition cancellation/concealment and no automatic date changes.
- [ ] Add a filtered later-page test where saving the last row navigates to the last valid page, announces the update and gives useful focus. Cover a surviving row's save announcement and preserved filter state.
- [ ] Run `pnpm --dir web test features/personal/components/library-screen.test.tsx`; confirm expected failures.
- [ ] Build the search/sort form and row controls in the existing design. Use explicit Save status, per-row pending/error feedback and a stable live region outside removable rows. Invalidate tracking/library queries after save; reset the row editor to refreshed server state after an explicit conflict refresh.
- [ ] Run focused library/personal/frontend contract tests, lint and typecheck; require passing results.
- [ ] Commit the tested UI.

### Task 5: Integrated acceptance, review and documentation

**Files:** Update `docs/STATUS.md`, API/browser contract documentation in `README.md` and `web/README.md`, and add `docs/personal-library-verification.md`.

- [ ] Run `dotnet test RateFreak.sln`, `pnpm --dir web test`, `pnpm --dir web lint`, `pnpm --dir web typecheck`, `pnpm --dir web build`, API Release publish and `git diff --check`. Record exact results; investigate failures before claiming completion.
- [ ] Use an isolated database/API/browser candidate to check search, all sorts, status-only writes/date preservation, conflicts, filtered pagination, logout/account transitions, keyboard focus and 320/390/768/1440px layouts in both themes. Keep real user data out of fixtures/evidence.
- [ ] Request one independent review of the final diff against the approved design; address material findings and rerun the checks they affect.
- [ ] Record implemented behavior, verification scope and limits in the evidence document and STATUS. Mark the design implemented only after acceptance passes. Preserve the running inspection environment and make any final runtime changes explicit.
- [ ] Commit verified changes and report outcome, checks, limitations and the local URL. Do not publish or deploy externally.

## Execution recommendation

Native execution: the query, proxy and UI share closely related contracts, and one
implementer can carry those interfaces through the change with one independent
review at the end. No additional product decisions are needed.
