# Library History Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans to implement this plan task by task, with test-first changes and one independent final code review.

**Goal:** Private completion history and twelve monthly totals, with year/month filters.

**Architecture:** Add one snapshot-consistent aggregate/page query over existing library records. Expose a private GET through the Next.js proxy and cache it under the account's library prefix. Build a read-only History section using the existing UI patterns.

**Tech Stack:** .NET 10, EF Core/PostgreSQL, Next.js, TypeScript, TanStack Query, CSS Modules, xUnit/Vitest.

**Spec:** `docs/superpowers/specs/2026-11-20-library-history-design.md` (approved 2026-11-20).

## Global Constraints

- Only currently readable, owned Completed entries with completion dates count.
- One current library entry counts once; edits/status changes/removals affect the recap.
- Date-only grouping; explicit years 1–9999, months 1–12; month requires an explicit year.
- Initial year is latest eligible year, otherwise server UTC current year.
- Twelve annual month totals include zeros; selected-period total describes the full matching list.
- Browser pages contain 20 entries; API page size 1–100 and page 1–10000.
- No new dependencies, migration, permanent diary, public sharing or persistent acceptance writes.

## Review Focus

- December 9999 and leap-day bounds cannot overflow or shift timezone (Task 1).
- Aggregates, available years and page must share eligibility and one DB snapshot (Task 1).
- History keys must invalidate on existing tracking/rating writes (Task 2).
- Private totals must disappear on failed refresh/account change (Task 3).
- Empty requested years and month-spanning pages need clear filters/headings (Task 3).

### Task 1: Private history query and endpoint

**Files:** Create `src/RateFreak.Application/PersonalMedia/LibraryHistory.cs`, `src/RateFreak.Infrastructure/PersonalMedia/LibraryHistoryService.cs`, `tests/RateFreak.IntegrationTests/LibraryHistoryTests.cs`; modify `src/RateFreak.Infrastructure/DependencyInjection.cs`, `src/RateFreak.Api/Endpoints/PersonalMediaEndpoints.cs`.

**Interfaces:** `LibraryHistoryQuery(int? Year, int? Month, int Page, int PageSize)`; `LibraryMonthTotal(int Month, int Count)`; `LibraryHistoryPage(int Year, int? Month, IReadOnlyList<int> AvailableYears, IReadOnlyList<LibraryMonthTotal> Months, int AnnualTotal, int TotalCount, int MissingDateCount, IReadOnlyList<LibraryItem> Items, int Page, int PageSize)`; `ILibraryHistoryService.GetAsync(Guid userId, LibraryHistoryQuery query, CancellationToken ct)`.

- [ ] Write integration tests for eligible/undated/wrong-status/other-user/hidden/custom records, month/year grouping, zeros, latest/fallback year, explicit empty year, full counts across pages, equal-date UUID ordering, own score zero, edits/removals, leap day and December 9999. Add strict query/auth/no-store cases.
- [ ] Run `dotnet test tests/RateFreak.IntegrationTests --filter FullyQualifiedName~LibraryHistoryTests`; observe failures before implementation.
- [ ] Implement `GET /api/v1/users/me/library/history` with year/month/page/pageSize validation and personal-read budget. Derive actor from session; reject unknown/duplicate keys.
- [ ] Implement bounded SQL aggregates and page queries under repeatable-read using existing visibility rules. Use inclusive DateOnly bounds so year 9999 requires no year-10000 arithmetic. Project own scores in SQL. Register the service.
- [ ] Run focused integration tests, require all passing without skips, then commit.

### Task 2: Browser contracts and invalidation

**Files:** Create `web/features/personal/interfaces/history.ts`, `query/history.ts`, `query/history.test.ts`, `api/history.ts`, `api/history.test.ts`, `hooks/use-history.ts`, `web/server/api/history-policy.ts`, `history-policy.test.ts`; modify `web/server/api/route-policy.ts`, `proxy.ts`.

**Interfaces:** History query `year?: number, month?: number, page: number, pageSize: 20`; matching response types; `parseHistoryFilters`, `historyParams`, `historyHref`, `getHistory`; account query key `[...personalKeys.library(accountId), "history", filters]`.

- [ ] Add failing tests for default/year/month URLs, duplicate/unknown/malformed/year-limit/month-without-year inputs, exact GET-only proxy allowance, query forwarding, auth cancellation and cache prefix invalidation by existing tracking/rating mutations.
- [ ] Run the focused Vitest files and confirm intended failures.
- [ ] Implement normalization and GET API/proxy/hook using the Task 1 contract, existing request cancellation and no-store guards. Forward only allowed query parameters and reject writes.
- [ ] Run focused tests and commit the passing contract.

### Task 3: Library History section

**Files:** Create `web/app/library/history/page.tsx`, `web/features/personal/components/history-screen.tsx`, `history-content.tsx`, `history-timeline.tsx`, `history.module.css`, `library-navigation.tsx`, `history-screen.test.tsx`; extend `interfaces/history.ts`; modify `library-screen.tsx`, `web/features/auth/components/auth-navigation.tsx` and related navigation tests.

- [ ] Read local Next.js search-parameter/navigation documentation. Add failing UI tests for private session gate, resolved year/empty filters, monthly links/annual totals, timeline grouping, rating zero, missing-date guidance, URL Back/Forward, page repair/focus, errors and identity transitions. Test Library remains globally selected on nested history.
- [ ] Run focused UI tests and observe failures.
- [ ] Implement Library/History links and read-only responsive history UI. Use labelled filters and twelve compact month links with counts. Group only the bounded page, render date-only values without timezone conversion, and label annual vs selected-period totals distinctly.
- [ ] Conceal all history content on session/read errors; cancel private reads on unmount/transitions. Repair out-of-range pages using the resolved year and selected month; focus the results summary after repair.
- [ ] Run focused UI/contract tests, lint/typecheck and commit.

### Task 4: Verification and handoff

**Files:** Update `docs/STATUS.md`, `README.md`, `web/README.md`, spec status; add `docs/library-history-verification.md`.

- [ ] Run full backend/frontend suites, lint/typecheck, frontend production build, API Release publish, model consistency and diff checks; record exact results.
- [ ] On isolated real API/proxy/database services, use an ordinary browser account with multi-month/year, missing-date and >20-row fixtures. Verify filters, links, paging, keyboard, edits reflected in history, logout, both themes and 320/390/768/1440px widths.
- [ ] Request one independent final diff review against the design. Reproduce material findings with tests, fix and rerun affected checks; record remaining limits honestly.
- [ ] Update documentation and record evidence; commit a clean feature branch. Preserve the running main application and provide an inspectable local preview. External publication and integration require their own authorization.
