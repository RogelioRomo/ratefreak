# Personal library: search, sorting and quick status updates

Status: proposed design, awaiting user approval.

## Intent and agreed scope

Make the private library easier to use day to day: find a saved title, choose
an ordering, see your rating and update tracking status without opening the title.
The user selected option 2 on 2026-11-20. This document specifies that option;
details identified below as proposals are implementation recommendations.

## Approaches considered

1. Search and sorting only: smaller change, but status updates still require
   navigating to title details.
2. Search, sorting, rating display and quick status updates: selected by the user.
   Improves both finding and maintaining saved titles while retaining the current
   library structure.
3. Full library workspace with bulk edits, saved filters and multiple layouts:
   broader scope and more interaction complexity; deferred.

## Current behavior and constraints

- `/library` is private, paginated at 20 entries and filtered by status/media type.
- The API orders by creation time descending, then media ID. Only readable catalog
  items are returned. Ratings are separate from tracking entries.
- Existing tracking PUT replaces status and both optional dates. Reusing a stale
  copy of the dates for a quick status update could overwrite newer edits.
- The existing Next.js proxy validates routes and queries. The API owns validation,
  visibility, authentication and data changes.
- Preserve the existing visual language, both themes, responsive layout and
  account-transition concealment. Interface definitions remain in separate files.

## Proposed experience

Retain the current list with artwork, title, media type and optional tracking dates.
Add a labelled title-search field and sort selector to the existing filter form.
An explicit Apply button submits search, status, media type and sorting together,
resetting pagination to page 1. URL parameters preserve selections across reload,
pagination and browser Back/Forward. Provide Clear filters for narrowed results.

Each entry displays the signed-in user's rating as, for example, `Your rating:
4.5 / 5`; zero is a valid rating, and missing ratings display `Not rated`.
Rating editing stays on the title page.

Each entry has a labelled status selector and explicit Save status button.
Saving disables that row's controls, shows progress, and announces success or an
actionable error. Other rows remain usable. No optimistic success is shown.
After success, refresh library data: an entry no longer matching the active status
filter disappears. Preserve valid pagination or return to the preceding nonempty
page if the last row on a later page disappears. Keep keyboard focus meaningful
when the edited row disappears and announce the change.

Status-only saves preserve the stored start/completion dates, including when another
tab has edited dates. They never invent dates, change ratings, or share activity.

## Proposed search and ordering rules

- Search matches a literal, case-insensitive substring of the catalog title within
  the current user's readable library. Trim outer whitespace; blank means no search.
  Limit input to 200 characters and reject invalid/duplicate query values consistently
  at the API and proxy. Escape SQL wildcard characters so `%` and `_` are literal.
- Sort choices: Recently added (default), Recently completed, Title A–Z,
  Highest rated and Lowest rated.
- Recently completed uses the optional completion date descending, with undated
  entries last. It does not implicitly filter tracking status.
- Rating sorts use only the current user's score, with unrated entries last in
  either direction. Title order is case-insensitive using the existing database
  conventions; locale-aware natural sorting is outside scope.
- Every ordering has deterministic tie breakers, ending in media ID. Counts,
  filtering, ordering and pagination happen in the database before page materialization.
- Existing API callers that omit the new query parameters retain current defaults.
  Add a nullable own-rating value to library responses without exposing other users'
  private ratings. Do not issue one rating request per visible row.

## Proposed write contract and stale-state handling

Add a dedicated status-only library operation rather than sending the existing
full tracking PUT from the list. It updates an existing, currently readable entry
atomically; it must not recreate an entry deleted in another tab.

The request contains the new status and the status the row was displaying. Compare
the expected status under the same transaction/lock as the update. Return conflict
if another edit changed the status, then require the user to refresh and retry.
Date-only changes do not conflict and their stored values are preserved. Concurrent
same-status saves are harmless; detecting a status that changed away and back is
not required for this scoped operation. Existing full tracking PUT remains unchanged.

Bind the request to the account that rendered the row using the existing expected
account header pattern; the API compares it with the authenticated account and the
proxy forwards it only for this explicitly allowed operation. Require the existing
CSRF, origin and write-budget protections. Abort/hide stale UI on logout or account
changes. A successful write invalidates the account's library and relevant tracking
queries so title details and filtered/sorted pages reflect the saved result.

## Boundaries

No bulk editing, new layouts, saved filters, diary/stats, rating editing, collection
changes, onboarding, public-library mode or automatic sharing. No persistent database
migration is expected: use existing titles, ratings and tracking fields. If measured
query behavior requires an index migration, document it separately before applying
anything to the persistent database. Keep the currently running local services usable
for inspection; use disposable data for acceptance writes.

## Verification

Use focused backend integration coverage for own-rating privacy, readable-item
filtering, literal search, combined filters, all sorts/nulls/ties, pagination, invalid
queries, date preservation, deleted rows, status conflicts and account mismatch.
Cover the new proxy contract, URL state, rating display including zero, row saves,
errors/conflicts, refetch/pagination and account transitions in frontend tests.
Run the repository's applicable backend/frontend checks and production build.
Check ordinary-account browser behavior, keyboard interaction and narrow/wide
layouts in both themes. Verify query shape avoids per-row requests; do not claim
production-scale performance from fixture tests.

## Left to the builder

Exact component boundaries, route/parameter names, index-free query composition,
CSS adjustments, test fixtures and accessible focus implementation may be chosen
within these behavior requirements. Record any material deviation before building it.
