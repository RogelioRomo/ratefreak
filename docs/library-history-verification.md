# Private library history verification — 2026-11-20

Implemented and verified on `feat/library-history`, pending local integration.
The [approved design](superpowers/specs/2026-11-20-library-history-design.md) defines
a private recap of current Completed entries with dates, not a permanent diary.
The [implementation plan](superpowers/plans/2026-11-20-library-history.md) records scope.

## Implemented behavior

- Library/History navigation and GET `users/me/library/history` use existing account
  authentication, visibility, personal-read budgets and no-store responses.
- Eligible records belong to the caller, remain readable, have status Completed
  and a completion date. Custom entries follow existing owner/public visibility.
  Account-wide completed entries without dates get separate guidance.
- Latest eligible year is selected initially; empty history uses the UTC current
  year. Explicit years 1–9999 and optional months 1–12 are strictly validated;
  month requires year. Requested empty years stay selected.
- Twelve zero-filled monthly totals and annual count describe the whole year;
  selected-period count and paginated records reflect the year/month filter.
  All queries share one PostgreSQL repeatable-read snapshot. Sorting/grouping and
  paging occur before materialization; own ratings do not create per-row requests.
- Current dates/statuses determine inclusion. Existing writes invalidate history
  beneath the account library cache prefix. Date-only formatting avoids timezone
  conversion and preserves leap days and the full supported date range.
- Route re-entry resets the session gate through the installed Next Activity
  lifecycle before displaying preserved private history. Filter/month/page
  navigation restores keyboard focus; failed reads conceal counts and rows, and
  out-of-range page repair shows progress instead of a false empty-period message.

## Automated checks

| Check | Result |
| --- | --- |
| `dotnet test RateFreak.sln` | 32 unit, 494 PostgreSQL/API integration, 196 provider contracts; 722 total, zero skipped |
| `pnpm --dir web test` after final review fixes | 928 tests across 116 files |
| `pnpm --dir web lint` | Passed |
| `pnpm --dir web typecheck` | Passed |
| `pnpm --dir web build` after final review fixes | Passed |
| `dotnet publish src/RateFreak.Api -c Release -o .local/history-release` | Passed |
| `dotnet ef migrations has-pending-model-changes --project src/RateFreak.Infrastructure --startup-project src/RateFreak.Api` | No changes since last migration, using disposable configuration |
| `git diff --check` | Passed |

All 21 new integration cases passed. They cover ownership/visibility/custom items,
missing dates, multiple years, empty/default years, all monthly counts, zero own
ratings and other-user rating exclusion, deterministic equal-date paging, leap day,
years 1/9999, edits/status changes/removal, invalid input, auth and no-store.
A database interceptor deletes a record on a separate connection after the first
history read; the response retains consistent counts/rows from its original
snapshot and the next request sees the removal.

Initial API tests failed 21/21 before implementation. Extreme-date diagnostics
then reproduced two PostgreSQL integer-overflow failures: Npgsql represents
DateOnly.MinValue/MaxValue as infinities. Explicit year/month CASE extraction
handles those stored values without schema, data or global driver changes.

Frontend contracts, UI, safe login continuations, nested navigation and actual
rating save/removal invalidation are covered. Independent review found two
important issues: preserved-route session gating and focus loss on ordinary
navigation. Five reproducing tests failed before the fixes (Activity re-entry,
filter/month/page focus, and misleading page-repair emptiness); all 19 history UI
tests passed afterward. A repeated-month second-page test was also added. The
reviewer's concern about rating invalidation already had an actual mutation test
in `personal.test.tsx`; it passes in the final suite. No finding is deferred.

## Real browser acceptance

Chromium used the actual Development Next.js proxy and API with a separately
migrated disposable PostgreSQL database. An ordinary account had 31 synthetic
records: dated completions across multiple media types/years/months, more than
20 in one month, a private custom entry, two undated completions, and excluded
paused/hidden future-year entries. No real user or provider records were copied.

The final browser run passed default year and aggregate counts, zero months,
missing-date guidance, two pages within January and its repeated heading, zero
rating, browser Back, year/month filters, February 29, an explicitly empty year,
detail links and a real tracking-date edit updating cached history after navigation.
Keyboard activation of month/page/filter controls restored focus to History
results. Logout concealed private content. Both themes at 1440/768/390/320px had
no horizontal overflow or page runtime errors; desktop/mobile screenshots were
visually inspected.

A separate real Next Back-navigation check silently cleared session cookies,
delayed `/users/me`, and returned to the preserved History route. Private counts
and rows stayed concealed before the delayed response and after it confirmed
signed-out status. This complements the React Activity lifecycle regression.

## Local runtime and limitations

The isolated preview is localhost:3200 with API localhost:5280 and disposable
synthetic data. Existing main and earlier preview services remain separate.
Ignored evidence lives in this feature worktree's `.local/history-checks/` and
`.local/history-acceptance/`: check logs, execution ledger, fixture metadata,
`browser-verification.json`, `privacy-verification.json` and eight screenshots.
These are local evidence paths, not durable published artifacts.

No persistent database migration or user-data write was performed. Production web
build and Release publish were verified separately from Development HTTP browser
acceptance. This does not verify deployed HTTPS/OAuth/provider agreements or
production-scale load. No new staff browser workflow is claimed.

Implementation rulings: continued the previously chosen native workflow after
approval; added a canonical history login continuation to preserve the chosen
period; handled existing Npgsql extrema locally in the query; used the installed
Next Activity guidance to gate re-entry. These preserve the approved product scope.
