# In-app notifications

Implemented locally on 2026-11-18. This is feature acceptance, not deployment
approval. See the [approved design](superpowers/specs/2026-11-18-notifications-design.md)
and [implementation plan](superpowers/plans/2026-11-18-notifications.md).

## Behavior

Signed-in members have a private `/notifications` inbox and navigation unread
badge. New followers and comments by others on their reviews create entries.
There is no historical backfill, self-notification, email, push or live socket.
The count polls every 60 seconds while visible and refreshes on focus; the open
inbox refreshes on the same schedule. Desktop/mobile navigation share one count
poller. The displayed badge caps at `99+`.

Entries support read/unread, mark-all-read, individual deletion and confirmed
clear-inbox across all pages. Opening the inbox does not mark it read; following
an entry attempts marking it read without delaying navigation. The request can
survive route navigation but is canceled on account changes.

Clear includes temporarily hidden entries; notifications committed after the
clear statement's database snapshot survive. No copied comment/review text or
media titles are stored or returned. Clearing/deleting affects notification rows
only. Replaying an existing follow, editing a comment or restoring visibility
cannot recreate a deleted entry. An unfollow/refollow or a distinct new comment
is a new event and may notify again.

Listing, totals, unread counts and individual mutations use current recipient
access. Hidden comments/reviews and unreadable/archived media disappear from the
inbox/count. Undeleted entries can reappear after access is restored. Hidden
profile text falls back to the actor's handle. Source, actor and recipient
foreign keys cascade cleanup; account erasure and durable replay remove both
received and actor-linked entries while detaching retained public catalog data.

Notifications have no fixed expiry. They remain until deleted/cleared or their
source/account is removed. Export adds `notifications.json` containing only
currently accessible, undeleted entries. Individual deletion and clearing do not
create erasure-journal records; backup copies follow the existing maximum
30-day lifecycle and can restore earlier inbox state. Account deletion retains
its independent erasure replay guarantee.

## Database and API

`20261118195946_AddNotifications` adds only the notification table, indexes,
constraints and cascading source/account foreign keys. Existing follows/comments
are unchanged and not backfilled. Apply it explicitly with the matching release
using the documented backup/upgrade workflow. Startup does not migrate. The
persistent local database received this migration on 2026-11-18 after explicit
user authorization, a private mode-0600 backup and an isolated restore check.
All 27 original application tables retained identical row counts and content
fingerprints; the new notification table was empty. Evidence:
`.local/localhost/notifications-migration/verification.json`. The local API was
restarted with the existing environment settings: readiness returned 200 and both
notification read endpoints returned 401 anonymously, confirming authentication
without writing persistent test data. Downgrade removes
notification data; take a backup before any intentional downgrade.

All endpoints require authentication and return no-store responses. Writes
require CSRF and use the social-write budget; reads use social-read. The browser
proxy permits only the listed routes and bounded paging. Commands accept an
absent body or `{}`; other JSON, malformed JSON and unexpected fields are rejected.
No request can choose another recipient.

| Method | `/api/v1` route | Result |
| --- | --- | --- |
| GET | `/notifications?page=1&pageSize=20` | Available items, page/pageSize, totalCount, unreadCount; page 1–10000, size 1–100 |
| GET | `/notifications/unread-count` | `{ unreadCount }` |
| PUT | `/notifications/{id}/read` | Mark an eligible owned entry read; 204 |
| DELETE | `/notifications/{id}/read` | Mark an eligible owned entry unread; 204 |
| POST | `/notifications/read-all` | Mark currently eligible entries read; 204 |
| DELETE | `/notifications/{id}` | Delete an eligible owned entry; 204 |
| DELETE | `/notifications` | Clear all owned entries, including inaccessible ones; 204 |

Unknown, inaccessible or another account's individual IDs all return 404. Repeated
read-state changes preserve state; repeating a deleted ID returns 404. Clearing an
empty inbox returns 204 without disclosing hidden-entry counts.

## Verification

PostgreSQL/API tests cover concurrent/replayed follows, self-comment exclusion,
comment editing/deletion, live visibility/counts, profile fallback, pagination,
ownership, auth/CSRF, invalid command payloads, deletion/clear and statement-snapshot
concurrency. Injected database triggers verify source/notification rollback and
all-or-nothing clearing. Exports exclude hidden and individually deleted entries.
Actual lifecycle deletion and failed-erasure journal replay remove notifications
in both directions while preserving detached public contributions.

Frontend tests cover inbox actions/errors, confirmation cancellation/focus,
pagination after deleting the last entry, account transition concealment,
cross-tab identity signals with delayed session resolution, mark-read surviving
navigation, shared count polling/background pause and fresh-result focus refresh.
Proxy tests reject unexpected methods, paths, paging and command bodies.

The real Chromium/API/Next.js/disposable PostgreSQL journey exercised two ordinary
accounts, follower and spoiler-safe comment events, read/unread, clear/cancel,
replayed-follow suppression, new events after clear, individual deletion with
comments intact, logout concealment and no browser runtime errors. Both themes at
1440, 768, 390 and 320 pixels had no horizontal overflow; desktop/mobile screenshots
were inspected. Additional real-browser regressions verified delayed mark-read
through navigation, rejected clear payloads, and immediate cross-tab concealment
while session resolution was delayed. Temporary evidence lives under `.local/localhost/notifications/`.

Browser acceptance used the development Webpack server: the default development
Turbopack server repeatedly reloaded before hydration in this workspace. Production
Turbopack compilation is checked separately. This does not qualify production
HTTPS/cookies, multiple replicas, or a release image.

Final tests: 27 unit, 446 PostgreSQL/API integration and 196 provider contracts
passed with zero skips; 808 frontend tests across 105 files passed. ESLint and
TypeScript checks and the final production Turbopack build passed.
Temporary acceptance servers stopped and the disposable
database was removed. Subsequent authorized migration of the persistent local
database added only notification storage and its migration-history entry;
existing application data was preserved.
