# Phase 6b: follows

User approved follows next, then IGDB. Implement within the existing .NET API.

PUT/DELETE `/api/v1/users/{userId:guid}/follow` use the authenticated actor, CSRF,
and a separate 30/account/minute social-write budget. Both return 204, including
replays; unknown targets return 404 and self-follow/unfollow return 400.
GET `/api/v1/users/{userId:guid}/followers` and `/following` are public, with
page 1..10000 and pageSize 1..100 (defaults 1/20), returning items, totalCount,
page and pageSize. Unknown users return 404; existing empty lists return 200.
Read budget: separate 60/IP/minute social-read. Responses use no-store.

Items contain id, handle, displayName and followedAt only, ordered ascending by
follow timestamp then related user UUID. Relationships are directed: mutual follows
require two rows. They grant no access to private libraries or collections.

Use a typed domain row, application service contract and EF implementation.
Composite primary key enforces uniqueness, CHECK prohibits self-follow, two
cascading account FKs remove relationships when either account is deleted.
Indexes support both list directions and stable ordering. Conflict-safe INSERT
handles concurrent repeated follows without exceptions or timestamp changes.

Approaches: a standalone follows slice fits the approved increment; coupling it
to profiles/comments/feed would expand scope unnecessarily. A generic social target
framework has no present need. Builder choice: public graph and oldest-first order,
following the project's existing public review and pagination conventions.

No profiles, comments, blocking/private accounts, moderation, notifications, feeds,
provider work or deployment changes. IGDB follows this slice as requested.

Prove lifecycle, isolation, concurrent repeats, tied timestamp pagination, privacy,
CSRF/authentication, invalid paging, missing accounts, database constraints/cascade,
OpenAPI, migration repeatability and model consistency using real PostgreSQL.
