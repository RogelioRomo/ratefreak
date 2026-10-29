# Phase 4 — personal library, ratings and reviews

User authorized implementation with ratings 0–5 inclusive in increments of 0.5,
one editable public review per user/media, private libraries and shared tracking statuses.
The user delegated remaining choices and asked to proceed without another design gate.

## Approach and scope

Keep the existing Domain/Application/Infrastructure/API layers, PostgreSQL and EF Core.
Three independent tables/services: user_media, ratings and reviews. Adding/removing a
library entry never changes ratings/reviews; rating/review does not add to a library.
No frontend, collections, provider changes, moderation system or account expansion.
Alternatives: one combined user-media table couples independent lifecycles; stored
aggregate counters require synchronization. Chosen: separate records and live SQL
aggregates for correctness and a small coherent change. No new packages.

## Product behavior (remaining details delegated to implementer)

- Private library: one entry per user/media. Status is planned, in_progress,
  completed, paused or dropped for both media types. Optional date-only startedAt
  and completedAt; completion cannot precede start. Dates explicit, no automatic
  status transitions or timestamps inferred from tracking dates. PUT replaces all
  tracking fields; DELETE idempotent. Stable pagination by creation time then media ID,
  optional status/type filter. No public user-library route.
- Ratings: nullable input score is required, decimal 0..5 inclusive and multiples of
  0.5. One rating per user/media; PUT atomically upserts, DELETE idempotent. GET own
  rating returns 404 when unrated. Public GET ratings returns count and average
  (null for no votes), calculated from local ratings only, without provider scores.
- Reviews: one public plain-text review per user/media. Body trimmed, 1..10000 UTF-16
  code units; reject NUL, unpaired surrogates and controls except tab/newlines.
  isSpoiler defaults false. POST creates (201), duplicate 409. PATCH replaces body
  and spoiler flag, requires body; DELETE is owner-only. Foreign and missing review
  IDs return 404. Public paginated list exposes user ID/handle/displayName, never
  email. Individual GET review supports created Location. Text stored literally;
  clients must render as text, never unsanitized HTML.
- All mutations require cookie authentication and CSRF. Reject unexpected JSON fields;
  nullable score prevents a missing score becoming a zero rating. Private reads
  no-store. Public reads 60/IP/minute (existing catalog budget), private reads
  60/account/minute and writes 30/account/minute, no queue. Existing 64 KiB body cap.
- Pagination page 1..10000 and pageSize 1..100, defaults 1/20. Reviews ordered newest
  first then ID. Consistent list count/page within a repeatable-read transaction.
- UUID and UTC audit timestamps. Unique user/media pairs, DB score/status/date/body
  constraints, FK users cascading deletion and catalog restricting deletion.
  Concurrent upserts preserve identity; concurrent duplicate reviews return 201/409.

## API

GET /api/v1/users/me/library; PUT/DELETE /api/v1/users/me/library/{mediaId}
GET/PUT/DELETE /api/v1/media/{mediaId}/rating (current user)
GET /api/v1/media/{mediaId}/ratings (public aggregate)
GET/POST /api/v1/media/{mediaId}/reviews; GET/PATCH/DELETE /api/v1/reviews/{id}
Unknown media yields 404 for writes/public queries. Deletes of absent own library
entries/ratings yield 204. PUT library/rating returns 200 with persisted record.

## Verification

HTTP tests against Testcontainers PostgreSQL for authorization, CSRF, private reads,
independent lifecycles, all score increments/boundaries, reviews ownership/spoilers,
pagination/filtering, rate limits, concurrent upserts/duplicates, schema constraints
and sanitized input failures. Generated migration additive, inspect Down, full suite,
Release build, EF consistency and live local flow. Retain existing user/catalog data.
No Git metadata: no repository initialization, worktree, commit or merge.
