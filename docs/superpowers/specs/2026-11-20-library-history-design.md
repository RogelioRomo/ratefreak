# Private library completion history

Status: approved by the user on 2026-11-20. The user selected the library-recap approach.

## Intent and agreed approach

Help a person look back at titles they have completed, grouped by month, with
monthly totals and year/month filters. This is a private recap of their current
library. It uses existing tracking dates rather than adding a permanent activity log.

Approaches considered:

1. Current-library recap: selected by the user. One completion per existing library
   entry, reflecting subsequent edits and removals. Requires no new event model.
2. Permanent diary: stores each completion, including rewatches/rereads, independently
   of the current tracking state. Deferred because it requires a new logging workflow,
   persistence and lifecycle policies beyond this first version.

The product scope is completion history and monthly totals. Rating-distribution
charts, separate media-breakdown dashboards and repeated-completion logging remain
future features. Displaying the existing own rating beside a title is included as
context, not as a new statistics feature.

## Eligibility and privacy

- Include only the signed-in user's current tracking entries with status `completed`
  and a non-null completion date, whose catalog items are currently readable under
  the existing catalog policy. Include supported provider types and custom items.
- Count each eligible library entry once. A later date edit moves it to the new
  month. Changing its status away from Completed or removing the entry removes it
  from the recap. This behavior is explained on the page.
- Completed titles with no completion date are excluded from the timeline and dated
  totals. Show a separate account-wide count of readable completed titles missing
  dates, with a link to the Completed library view where they can open and edit titles.
  Do not assign dates automatically or infer them from rating/update timestamps.
- Treat completion dates as date-only values; filtering and display must not shift
  dates across months through timezone conversion. Honor future dates already allowed
  by tracking; do not introduce a new tracking-write restriction.
- Access is private and no-store. Existing session, visibility and personal-read
  budgets apply. No public profile/feed/export or account-erasure format changes are
  needed because all information is derived from already managed library records.

## Proposed navigation and presentation

Add `/library/history`, reached from a small Library/History section navigation on
the library and history pages. Keep the global Library navigation selected for both.
Reuse the existing artwork, typography, themes, filters and pagination patterns.

The history page shows a short explanation, year/month filters, the selected
period's completion total, a compact twelve-month summary for the selected year,
and a newest-first list grouped by month. Each title shows artwork, title, media
type, completion date and current own rating if present (including zero).
Titles link to catalog details; this page is read-only. Missing-rating text remains
distinct from a zero rating. Provide links back to the library for editing.

The twelve-month summary uses text counts with month links and includes zero months.
It always summarizes the selected year, even when a single month filters the list;
label this distinction clearly and mark the selected month. This gives useful
orientation without adding a charting dependency or twelve large statistic cards.

## Proposed filter and count rules

- Select one year, optionally one month. Month can be All months or 1–12.
  An All years aggregation is outside this first version.
- The initial year is the most recent year with an eligible dated completion.
  If there are none, use the server's current UTC year as the empty initial view.
  Year choices come from eligible completions; include an explicitly requested
  valid year even if it has no entries, so an empty filter remains understandable.
- Valid explicit years are 1–9999. Reject a month without an explicit year, unknown
  or duplicate query keys and invalid values consistently in the UI parser, proxy
  and API. Date bounds must handle December 9999 without overflowing.
- Year/month/page live in the URL. Apply filters resets page to 1; Back/Forward,
  pagination and reload preserve the chosen period. Month links submit the selected
  year and month and reset pagination.
- Page size is 20 in the browser; retain the existing API paging limits of 1–100
  entries and pages 1–10000. Sort by completion date descending, then media UUID.
- The selected period's total and pagination count describe all matching entries,
  not just the displayed page. The annual total equals the twelve monthly totals.
  Page boundaries may split a month; repeat its heading on the next page without
  suggesting a page-local count is its full monthly total.
- On a successful refresh, repair a later page beyond the last valid page and
  preserve the selected period. Keep keyboard focus useful after navigation.

## API and state boundaries

Add a dedicated read-only history query under the existing private-library API.
Return the resolved year/month, available years, twelve monthly counts, annual and
selected-period totals, missing-date count and the paginated title entries in one
response. Compute eligibility, aggregates, sorting and paging in PostgreSQL within
one repeatable-read snapshot. Do not download the full library into the browser,
make per-row rating requests or group unbounded title records in application memory.

Cache history beneath the account's existing library query-key prefix so tracking
updates/removals and rating changes invalidate both views. Validate queries at the
Next proxy and forward only the explicitly allowed GET route. Abort in-flight work
and conceal private data during account transitions and session errors. A failed
refresh must show an error rather than presenting stale totals as current.

No database migration is expected. Keep existing local inspection services and
persistent data intact; use isolated disposable data for acceptance writes. Any
index migration found necessary from measured query behavior is separate reviewed work.

## Verification

Integration tests cover eligibility/ownership/visibility, missing dates, multiple
years and months, leap days and year boundaries, date-only handling, December 9999,
zero months, counts beyond one page, deterministic equal-date ordering, edits/removal,
own-rating privacy, strict input validation and consistent aggregate/page snapshots.
Frontend tests cover URL/query/proxy contracts, default/resolved year, month links,
empty states, missing-date guidance, counts, grouping across pages, rating zero,
cache invalidation, failed refresh and logout/account transitions.

Run applicable backend/frontend suites, lint/typecheck and production builds.
Check real ordinary-account browser filtering, pagination, detail links and keyboard
interaction, both themes and narrow/wide layouts against disposable fixtures.
Do not infer production-scale performance or production deployment acceptance from
local fixture tests. Request independent code review before completion.

## Left to the builder

Exact DTO/service/component names, query parameter normalization, SQL query shape,
CSS details and accessible focus handling may be chosen within these requirements.
Use existing Next.js, TypeScript, TanStack Query, CSS Modules and .NET patterns;
keep object interfaces in separate files and avoid new dependencies.
