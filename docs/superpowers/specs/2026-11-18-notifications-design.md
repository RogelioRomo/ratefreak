# In-app notifications

Date: 2026-11-18
Status: Approved by the user on 2026-11-18, including individual deletion and clearing; implementation authorized.

## Intent and agreed scope

Give signed-in members a reliable way to discover new followers and comments on their reviews. The user approved a saved inbox, unread badge, periodic refresh, read/unread controls, links to profiles/reviews, current-access checks, and account export/deletion integration. The user also approved individual notification deletion and a confirmed clear-inbox action. Build on the existing .NET/PostgreSQL API, Next.js browser proxy and TanStack Query client.

Success means a committed follow or comment produces the appropriate recipient's inbox entry, repeated requests do not create duplicate follow entries, and the inbox never reveals content the recipient cannot currently access.

## Approaches

1. **Saved inbox with periodic refresh — selected.** Durable database records with normal authenticated API reads. Reuses the existing deployment and session/query infrastructure. Updates can take up to the polling interval to appear.
2. **Saved inbox with live delivery.** The same durable records plus live connections and reconnect handling. Faster delivery, but adds server and proxy responsibilities without improving durability. Deferred.

Email, push, live connections, notification preferences, collection discussions and activity feeds are outside this version. Do not backfill past follows or comments. Do not change provider integrations or their retention policies.

## Product behavior

The following details are implementation recommendations rather than additional user requirements:

- Add a Notifications navigation link with an unread count for authenticated members, available in desktop and mobile navigation. Use the existing layout and theme styles. Cap the displayed badge at `99+`; provide a meaningful accessible name including unread status.
- `/notifications` shows newest entries first with stable timestamp/ID ordering, bounded page navigation, and a total of currently available entries. Use 20 entries per page. The badge counts all available unread entries, not just the current page.
- Each entry names the actor using their current public identity, describes the event, shows the event time, and links to their profile or the review discussion. Comment notifications say that the actor commented on the recipient's review; they contain no comment/review body or copied media title. Existing discussion presentation continues to handle spoilers.
- Opening the inbox does not mark entries read. Provide per-entry “Mark as read” / “Mark as unread” buttons and a “Mark all as read” action. Following an entry's link attempts to mark it read, but read-state failure must not prevent navigation. Expose failures in the inbox when the member remains there.
- Provide a per-entry “Delete notification” action that permanently removes that notification, with no effect on its follow relationship, comment or review. Provide “Clear inbox” with an accessible confirmation dialog explaining that it permanently removes all existing notifications, including read entries and temporarily hidden entries. Cancellation makes no changes and returns focus to the trigger. A successful clear removes entries across all pages; notifications committed after the clear statement's snapshot remain. These actions do not offer undo.
- After deletion or clearing, refresh the list and unread badge and return to a valid page if the current page becomes empty. Announce success accessibly. Keep recoverable failures visible and preserve the current inbox on failure; disable duplicate submissions while pending. Clearing remains available when the visible inbox is empty because temporarily inaccessible entries may still exist.
- Show loading, empty, recoverable error and mutation-pending states. Keep existing data visible during routine refresh; session uncertainty or an identity change must conceal private data according to the existing session rules. Offer login with a safe return path to signed-out visitors.
- Poll unread counts every 60 seconds while the document is visible and the session is verified. Refresh on focus and after relevant mutations. Refresh inbox data while its page is open. Desktop/mobile navigation must share query keys and avoid duplicate independent polling loops. Do not poll in the background or while signed out.

## Event rules and atomic writes

### New follower

Create one notification for the followee when a new follower edge is successfully inserted. Existing self-follow rejection remains. A replayed or concurrent follow PUT cannot create another entry or reset its read state or timestamp.

Treat the notification as belonging to the active follow relationship: unfollow removes its notification in the same transaction; a later new follow can produce a fresh notification. This avoids links claiming a relationship that no longer exists. Enforce uniqueness in PostgreSQL, not only application checks. Account deletion removes both sides' notifications.

`FollowService.SetAsync` currently performs an idempotent parameterized insert with `ON CONFLICT DO NOTHING`. Extend that operation transactionally, using whether the edge was actually inserted to decide notification creation. Handle account disappearance using the existing missing-result behavior. A notification failure rolls back the follow operation.

Deleting or clearing its notification leaves the follow edge intact. Replayed follow PUTs must not recreate a deleted entry: notification insertion occurs only for a newly inserted edge, never by repairing a missing notification on an existing edge. Unfollowing and following again is a new event and can create a new notification.

### Comment on a review

Create one notification for the review owner when another account successfully creates a comment on that review. No self-notifications. One entry per distinct comment; separate comment POSTs remain separate comments/events. Editing a comment creates no new notification and does not reset read state.

Add the notification inside `ReviewCommentService.CreateAsync`'s existing transaction and moderation lock. Both records commit together or neither does. Deleting the comment or its review removes the corresponding notification through referential cleanup. Deleting its author or recipient also removes it.

Deleting or clearing a comment notification leaves the comment and review intact. Editing or restoring visibility of the source must not recreate a deleted notification. A genuinely new comment can still create a new entry.

## Storage and visibility

Use a dedicated notification entity/table with a server-generated UUID, kind, recipient account, actor account, event timestamp and nullable read timestamp. Comment events reference the source comment; its review/media relationship is derived rather than copied. Follow events are uniquely associated with the current actor/recipient follow edge. Add indexes for recipient/newest ordering, unread queries and event uniqueness, and database constraints for valid kind/source combinations. Exact entity/configuration organization is left to the builder.

Store identifiers and event metadata only. Do not store actor names, review text, comment text, provider metadata or arbitrary URLs. Generate internal links from validated typed identifiers.

One shared recipient-scoped eligibility query must govern listing, totals, unread counts and read mutations:

- Recipient is always the authenticated account; requests cannot select another recipient.
- Actor must still exist. Hidden profile text uses the existing handle fallback, never the hidden display name or bio.
- A follow notification requires the current follow edge.
- A comment notification requires a non-hidden comment, a non-hidden parent review and a media item readable by the recipient under `CatalogAccess.Readable`. Use the same archival/visibility semantics as existing review/comment endpoints; do not invent an alternative access rule.

Hidden or temporarily inaccessible entries disappear from the inbox and unread count without being rewritten or marked read. If access is restored, the original entry and read state can reappear. Source deletion is permanent cleanup. Check eligibility when serving the request, even if it was valid when the notification was created. Use a consistent database snapshot for a response containing both list and totals. A subsequent target change may still make a clicked link unavailable; existing target-page unavailable states handle this normally.

Individual deletion and clearing physically delete notification rows; no dismissal tombstones or copied content are retained. Individual deletion uses the same recipient/eligibility checks as listing and read mutations. Clear-inbox deletion is instead scoped to all rows owned by the authenticated recipient, including currently inaccessible entries, without returning their IDs, counts or content. This ensures clearing prevents old hidden entries returning after visibility restoration. Event creation is synchronous and atomic with its source, so no asynchronous delivery or backfill can resurrect deleted events.

## API and browser proxy

Proposed API contracts, using existing auth, problem responses, rate limits and CSRF conventions:

- `GET /api/v1/notifications?page=1&pageSize=20`: available entries, page/pageSize, totalCount and unreadCount. Validate page 1–10000 and pageSize 1–100.
- `GET /api/v1/notifications/unread-count`: available unreadCount.
- `PUT /api/v1/notifications/{id}/read`: mark an eligible owned entry read.
- `DELETE /api/v1/notifications/{id}/read`: mark it unread.
- `POST /api/v1/notifications/read-all`: mark currently eligible owned unread entries read.
- `DELETE /api/v1/notifications/{id}`: permanently delete an eligible owned entry.
- `DELETE /api/v1/notifications`: permanently clear the authenticated recipient's inbox across all pages, including temporarily inaccessible entries.

Writes return 204; an unknown, inaccessible or other member's per-entry ID returns the same 404. Read-state operations are idempotent. Read-all acts on a bounded-in-time database statement snapshot, so events arriving afterward remain unread; it must not load an unbounded inbox into application memory. No caller-supplied timestamps or recipient IDs. Require authentication for every route, CSRF for every mutation, private no-store responses and the existing social read/write rate policies.

Clear uses one recipient-scoped set-based DELETE with PostgreSQL statement-snapshot semantics, without loading entries into application memory. It returns 204 even for an empty inbox. Notifications committed after that snapshot survive; the confirmation text must not promise preservation of notifications received while the dialog was open before confirmation. A repeated per-entry delete returns the same non-disclosing 404 as an unknown entry. A failed deletion must not partially clear the inbox.

Extend the browser proxy's exact method/path allowlist and query/body validation for these routes. Preserve credential forwarding, cancellation, CSRF, no-store and response bounds. Add typed frontend API helpers and account-scoped query keys under a private namespace. Invalidate count/list after mutations. Existing identity reconciliation must cancel and clear these queries on logout/account changes; never show the previous account's badge or inbox during a transition.

## Account lifecycle and retention

Notifications are account data. Include an explicit notifications JSON entry in the private export with owned event metadata and current eligible actor/target projection; exclude currently hidden/inaccessible events, deleted notifications and all copied private text. Preserve export snapshot, moderation locking and temporary ZIP protections. Document that the export reflects currently accessible inbox entries.

Received entries are removed when their recipient is deleted. Actor-linked entries are also removed when that actor is deleted, including durable erasure replay. Source FKs remove deleted comment/review entries. Add integration coverage for public-content ownership detachment to ensure it does not retain the erased member's notifications.

Do not introduce a new six-month or other fixed expiry. Inbox entries persist until the recipient deletes/clears them or their account, actor or source relationship is removed. Individual deletion and inbox clearing affect the live database; existing backups retain their documented lifecycle, and these inbox actions do not create account-erasure journal entries or promise restore-proof erasure. Existing account backup retention and account-erasure journal guarantees apply. Update account lifecycle/privacy documentation to describe this new data category; a future retention policy would be a separate decision.

## Delivery and validation

Create an additive EF migration and verify model consistency. Test migrations and workflows against a disposable PostgreSQL database. Do not apply the migration to the persistent local database during implementation; any later application must use the established backup/preservation workflow.

Required evidence:

- Actual PostgreSQL/API tests for atomic creation, no self-notification, concurrent/replayed follows, unfollow/refollow, separate comment events, edit stability, ownership isolation, auth/CSRF, pagination, read/unread/all-read and response no-store behavior.
- Deletion/clear tests for ownership isolation, missing/inaccessible IDs, all-page and read/unread removal, hidden-row cleanup without disclosure, empty/repeated operations, no recreation after replay/edit/visibility restoration, intact source relationships, statement-snapshot handling of concurrent new notifications, atomic failure and exclusion from later exports.
- Privacy tests for hidden comments/reviews/profiles, inaccessible catalog items, restored visibility, source/actor deletion, export and account erasure/replay. List totals and unread counts must obey the same eligibility checks.
- Frontend/proxy tests for exact permitted routes, invalid inputs, refresh and mutation behavior, failure recovery, account transitions, signed-out state, accessible unread labeling and safe links. Cover delete/clear count refresh, valid pagination after deletion, confirmation cancellation/focus restoration, duplicate-submission prevention and accessible outcome announcements.
- Relevant backend suites, frontend tests, lint, typecheck and production build. Real browser checks against a disposable database for two-account event creation, inbox/read behavior, logout concealment, desktop/mobile layouts in both themes, with no private fixture data left in the persistent database.
- Update `docs/STATUS.md` and API/account documentation with implemented behavior and evidence limits. No automatic push, release or deployment is part of this feature.

The builder may settle file/component names, query implementation, migration constraint names, exact spacing/icons and test fixture organization within existing conventions. Changes to event types, delivery approach, privacy behavior or retention require a scope decision.

## Authorized local integration follow-up

After implementation and acceptance, the user authorized applying the migration
to the persistent local database, then committing and merging into `main`. The
migration was applied on 2026-11-18 with a private backup, verified isolated restore
and unchanged original-table fingerprints. This supersedes the implementation-only
restriction above for that specific local application. No push/deployment authorized.
