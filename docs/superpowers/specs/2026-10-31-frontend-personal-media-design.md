# Personal library, ratings and reviews

Status: approved by the user and implemented, 2026-10-31. Completion evidence
is recorded in docs/frontend-checklist.md. Fresh independent builder check:
“nothing important left.” Next slice of the approved frontend
roadmap; provider discovery/import is complete. Preserve existing feature checkout,
native/code-first execution and no automatic commits, pushes or deployments.

## Intent and approach

Let signed-in people track catalog titles, rate them and publish/edit one review
per title. Add `/library` and personal controls to `/catalog/[id]`, retaining the
approved editorial Fraunces/DM Sans system, default Dark/blue, Light palette and
Tabler icons. Catalog records, library entries, ratings and reviews remain distinct.

Recommended builder choice: existing feature-owned API/Query/forms architecture,
with two small authenticated backend reads for current user/media state. Current
library and review lists cannot reliably tell whether a particular user's record
exists without scanning all pages. Add GET `/api/v1/users/me/library/{mediaId}`
and GET `/api/v1/media/{mediaId}/review`. Extend service interfaces and filtered
queries; return the existing LibraryItem/ReviewDetails DTOs,404 when absent,401
without authentication, no-store, and existing personal-read rate limits. Derive
ownership exclusively from the session. No schema migration or new write semantics.
The own-review lookup respects existing `!IsHidden` visibility; do not expose
moderated review content or introduce moderation behavior in this slice.

Alternatives: scan paginated lists (expensive and vulnerable to stale/partial
membership inference), or keep editing only on library/review list rows (prevents
a coherent title-detail workflow). The small read additions fit the existing
services and avoid these compromises.

## Private library

`/library` has one main heading, explanation, labeled Status and Media type filters,
explicit Apply filters and20-item pagination. URL parameters status/type/page
restore through Back/Forward. New filters reset page1; invalid/duplicate/unknown
filters have a reset action and do not query. Statuses: planned, in_progress,
completed, paused, dropped; readable labels Planned, In progress, Completed,
Paused, Dropped. Types reuse the four catalog types.

Use a responsive semantic list with artwork/fallback, title link, status and any
saved dates. Never show another account's cached results while checking session,
changing identity or handling401. Initial entry verifies the session using the
existing protected-account pattern; anonymous visitors get a login action with
canonical `/library` continuation. Network/429 session failures are explicit,
not treated as signed out. Include loading, retryable error, empty library,
no-match and out-of-range recovery with catalog/discovery entry points.

Add Library navigation for signed-in accounts with an active state. Anonymous
visitors can still open `/library` and receive the login flow. No dead controls.

## Personal controls on title details

Below public title information, present separate tracking, rating and review
sections. Public details remain visible without signing in; anonymous visitors
see Log in to track/rate/review with a canonical current-detail continuation.
Private reads run only after a verified session and use keys containing account
identity and media ID. Block each form until its own saved-state read succeeds;
404 means absent, transport failures never mean empty.

Tracking form: native Status select, optional Started on and Completed on date
inputs, explicit Save tracking; the absent state offers Add to library. Load all
existing dates before editing because PUT replaces them. Do not infer dates from
status, clear dates accidentally on status edits, or require rating/review to be
in the library. Validate completion >=start and show field errors. Removing
tracking uses an inline confirmation explaining that rating/review remain. Confirm
Remove from library or Cancel; reset its form to absent after successful204.

Rating form: native labeled score select with unrated placeholder and every0.5
step from0 through5. Zero is a real score, never a falsey empty value. Save rating
and separate Remove rating; load the saved score first. Show a public community
count/average from the existing aggregate, with honest No ratings yet for count0
and null average. Do not mix provider scores or invent star counts. Invalidate
own-rating and aggregate queries after successful saves/removals.

Review editor: plain textarea, required trimmed1–10000 UTF-16 code units, spoiler
checkbox and Publish review or Save review. State clearly that reviews are public.
Load the own-review endpoint first; edit uses the returned review ID. Render all
body text literally, never as HTML. Preserve drafts while requests fail, and show
server field errors. Delete review requires inline confirmation. Handle409 from
creation by refetching own review; never silently overwrite an existing review
with the attempted draft. If conflict persists but no visible own review exists,
explain that an existing review is unavailable and do not reveal hidden content.

Public reviews appear as paginated plain-text entries on title details, with
author display name/handle and date. Spoiler bodies start hidden behind a native
Show spoilers button, with an explicit Hide spoilers action. Never reveal spoiler
text through a collapsed summary/accessible label. URL parameter reviewsPage owns
this independent pagination, bounds1–10000 and fixed20 rows; changing it does not
remount personal forms or discard drafts. Show genuine review count, loading,
empty/error/429 states and recovery. No comments/profile/follow/moderation controls.

## Session, writes and navigation

Reuse session/CSRF client and current proxy protections. New PUT/PATCH/DELETE
methods must be enabled only on exact feature paths, with the same exact Origin,
JSON/body-size, cookie/attribution, no-store, timeout and cancellation requirements
as current POST writes. No generic method/path forwarding. Allow public GET
ratings/reviews, own GET rating/review and library GETs, existing PUT/DELETE tracking
and rating, POST review, PATCH/DELETE review byGUID. Enable public review-detail
GET only if the implementation actually uses it.

Only library list accepts status/type/page/pageSize and review list accepts
page/pageSize. Validate exact allowed values, duplicates, unknowns and numeric
bounds before forwarding. Reject all query strings on other new routes. Keep
object contracts and props in separate interfaces files. No dependencies needed.

Extend the existing safe login continuation allowlist to only canonical `/library`
and `/catalog/{guid}` in addition to provider previews. Reject external,
protocol-relative, encoded/ambiguous, query/fragment and duplicate returnTo values;
retain `/account` for invalid/absent continuation. Returning to title/library does
not automatically replay any pending write.

Disable duplicate submissions while pending and never retry mutations automatically
or claim optimistic success. Tie each pending operation and outcome to account,
media and form identity; cancel/ignore stale outcomes on identity/route changes.
Preserve unrelated form drafts when a successful write invalidates other queries.
On401 hide private state, refresh session, offer login recovery. Distinguish
400/404/409/429 and transport failures with recovery. Deletions and updates affect
only their own independent resource; successful mutations invalidate the relevant
account-owned state, library lists and/or public reviews/aggregate as appropriate.

## Verification and limits

Backend tests for the two read additions: ownership,401, absent404, existing DTO,
hidden-review visibility and unchanged write semantics, against repository patterns.
No migration. Frontend test-first proxy route/method/query protection, client
signals/contracts, login continuation, URL filters, real QueryClient state tests,
loading/recovery, zero-rating, replacement-date preservation, public literal text,
spoiler reveal, review409/drafts, confirmation, identity changes and late outcomes.
Run full frontend tests/lint/typecheck/build, relevant backend tests and diff check.

Inspect both themes at320/768/1024/1440, keyboard/focus, Back/Forward, dates/long
review text and console. Synthetic fixtures cover authenticated writes/deletions
and identity edge cases; no real-account writes or destructive verification.
Read-only local API checks may verify public aggregate/review contracts. Report
live vs fixture evidence and actual checks, fresh code/UI review, remaining work
and running processes in docs/frontend-checklist.md. Keep existing API, database
and development preview available; no credential printing or tracked secrets.

Builder-owned: focused component/file boundaries, field copy and spacing within
the incumbent design, test helpers and cache invalidation implementation. Do not
add charts, new styling primitives, animations, user stats or unrelated refactors.
Next roadmap slice is collection metadata/visibility; external sign-in is separate.
