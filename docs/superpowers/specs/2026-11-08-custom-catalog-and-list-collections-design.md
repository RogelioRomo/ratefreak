# Custom catalog items and list-only collections

Date: 2026-11-08.

Status: Approved direction; user authorized proceeding on 2026-11-08 after confirming
filesystem image storage. Executable plan is linked below. Database clearing and
deployment remain separately authorized operational steps.

Source: [preliminary plan](../plans/2026-11-07-custom-catalog-and-list-collections-preliminary.md).

Execution: [implementation plan](../plans/2026-11-08-custom-catalog-and-list-collections.md).

## Intent and scope

Let people create and rate anything using the existing catalog, ratings, reviews
and review comments. Collections organize references to those same items. A flower
in two collections has one catalog identity and one personal rating per person.

Keep ASP.NET Core/Identity, EF Core/PostgreSQL and the Next frontend. Preserve
existing catalog UUIDs, provider mappings, accounts and contributions. Remove the
collection field builder and free-standing entries at the list cutover. Do not add
category-specific schemas, a second review system, galleries, item-level comment
threads, automatic external image fetching or a create-and-add shortcut.

## Decisions and authorship

The user confirmed:

- Public-to-private changes preserve contributions and list memberships in storage;
  the item and those contributions become visible only to its creator.
- Custom items support one optional image, with upload, replacement and removal.
  Image access follows item visibility.
- Uploaded image files live on the hosting system's filesystem, not in the database.
- Public categories become available immediately. Duplicate checks normalize case
  and whitespace. Admins may rename, merge or hide public categories. Private
  categories remain visible only to their owner.
- Proceed with drafting the design following the existing phased approach.

The preliminary plan establishes private-default creation, explicit publication,
shared catalog identities, list-only collections and loss of test collection entry
and field data only. It does not identify or authorize clearing a database now.

The policies below for archival, edit limits, category details, upload limits,
storage mechanics and library labels are implementer proposals for approval with
this design; filesystem storage itself is a user requirement.
They must not be described as independently agreed user decisions.

## Approaches

Use the phased, additive approach recommended in the conversation: extend catalog
identity, close privacy paths, add custom item/category creation and artwork, then
cut collections over to relational lists. It reuses existing ownership, revision
and transaction controls while keeping unfinished private content inaccessible.

A single simultaneous rewrite couples privacy, provider compatibility, uploads
and destructive schema changes; it is harder to verify and roll back. A separate
custom-item database duplicates ratings and reviews and breaks shared item identity.
Those alternatives are rejected by the existing constraints. The precise execution
tasks follow only after approval of this design.

## Catalog identity and concurrency

Extend MediaItem rather than renaming every media-related symbol. Retain the four
legacy media types and introduce `custom`. Dynamic categories are UUID references,
never provider types. Add creator UUID (nullable for system rows), provenance
(`system` or `user`), visibility (`private` or `public`), category UUID, archived
state, moderation-hidden state and a monotonic revision. Keep those states separate.

Backfill all existing items as public/system-owned and seed their four public
categories. No provider mapping does not imply user ownership. Provider import
continues to accept only its supported legacy types and trusted metadata mappings.
User item creation cannot supply provider mappings, metadata JSON or ownership IDs.

Existing title/description bounds remain applicable: title 1–300 printable
characters, description at most 20,000 characters. Custom release date is optional
and initially omitted from the create/edit form. Artwork is optional.

Creator edits require the current revision. Visibility, archive, category and image
changes also advance it. Lock/recheck the current item inside write transactions.
Serialize contribution creation and visibility changes using the same item lock;
checking access before acquiring it is insufficient. Staff commands follow the
existing moderation lock order, followed by category, item and collection locks
in deterministic UUID order where more than one is involved. Specify and test the
complete lock graph in the implementation plan before adding write paths.

## Item lifecycle and contributions

Only the creator edits a user-created item. Provider/system items retain existing
trusted import behavior. Staff roles do not become creators or receive ordinary
owner editing permissions.

Publish is explicit and requires an active public category. Unpublishing preserves
everyone's ratings, reviews, comments, library rows and collection memberships.
Other contributors cannot read or edit those records while the item is private.
Republishing exposes eligible stored contributions again, preserving individual
moderation holds. The creator retains access to previously public reviews, comments
and their public author summaries while the item is private, consistent with the
confirmed decision above. Aggregates use retained ratings for either visibility;
only authorized item viewers can read them. Never expose individual rating records
or author details beyond the existing public contribution contracts.

Use reversible archival instead of hard item deletion in the first release,
including items with no contributions. Archived items are excluded from discovery,
collections and social surfaces; their creator can view them in an owner management
screen and restore them. Restoration returns an item to private status so public
reappearance requires an explicit publish. No contribution cascades occur.

Creators may correct a public item's title/description/artwork. The UI explains
that replacing the subject requires a new item. Do not pretend to detect semantic
replacement automatically: reports and moderation handle disputes. Every edit
advances revision; edits never clear moderation holds. History/audit records contain
IDs and action metadata, not copied descriptions or image bytes.

Account deletion must not cascade shared catalog items or other users' contributions.
There is no apparent account-deletion endpoint in the current implementation. Do
not add one in this release. Use restrictive owner FKs on custom items and private
categories so direct account deletion fails rather than silently cascading or
orphaning them. Account retirement, private category custody and a supported erasure
workflow require a separate design. Existing account FKs on personal contributions
remain unchanged; this release makes no new erasure promise (ADR 0011).

## Categories

Store UUID, display name, normalized name, scope, owner UUID, hidden state and
revision. Trim and collapse whitespace, apply Unicode normalization and invariant
case normalization consistently for writes and search. Proposed name bound: 1–100
printable characters. Database uniqueness is authoritative under concurrent creates:
one normalized name in the public scope, or per owner in private scope. A public
name and a private name may coexist. Never reveal another owner's private matches.

Public categories are readable immediately without approval. The creator is recorded
for attribution but cannot privately rename or hide a shared public category.
Private category owners may rename their own categories. Admins rename, merge or
hide public categories with expected-revision checks, a reason and an audit event.
Do not allow ordinary moderators to administer categories.

Merge is public-to-public, rejects self/hidden targets, reparents affected items
atomically and keeps a source-to-target alias for old category IDs. Reject cycles.
Uniqueness conflicts require choosing a merge rather than silently renaming another
category. The four seeded categories cannot be merged or hidden in this release;
provider mappings rely on their stable identities.

Hiding a category excludes its public items from ordinary discovery and direct
access, including their contributions and images. Owners retain management access
to their own items and can select another active category; staff use privileged
routes. Restoring a category restores otherwise-public items subject to their other
states. No private item becomes public as a side effect of any category operation.

A private item may use an active public category or its creator's private category.
Publishing from a private category requires explicitly choosing an active public
category or creating one. Creating a public category from a private name does not
convert the private category or move its other items. No private category counts
or conflicts are exposed. Category hard deletion is outside this release.

## Common access policy

Centralize queryable predicates and command authorization, including parent item
checks for indirect IDs. Ordinary reads allow an active private item only to its
creator, and an active public item only if neither it nor its category is hidden.
Creator management reads separately allow archived/held owned items. Privileged
moderation reads require existing staff authorization and explicit staff routes.
Unauthorized direct IDs consistently return 404, including image and child IDs.

Apply policy before counting, aggregating, ordering and paging, not after materializing
a page. Review/comment moderation filters still apply. Cover catalog search/detail,
category browsing, ratings, reviews, review comments, reports, personal library,
public profiles/review histories, activity/feed surfaces where present, collection
pickers/list rows/direct membership IDs, and all write paths. An accessible collection
does not authorize access to a member item. Audit every existing reader before
enabling custom creation; do not assume changing catalog queries is sufficient.

Maintain a test matrix for anonymous, creator, another signed-in user, Moderator and
Admin across private/public/archived/item-held/category-held states. Verify bodies,
counts, direct IDs and images. Ordinary admin browsing does not bypass item privacy.
Staff may inspect held public content/report evidence through staff routes; unrelated
private items are not added to a general staff browsing API.

Use no-store responses on access-sensitive endpoints. Clear scoped frontend queries
on login/logout and invalidate item, category, library, profile and collection queries
after lifecycle changes. Viewer identity participates in cache keys. Publicly shared
image caches are prohibited because formerly-public images can become private.
Previously downloaded content cannot be revoked from a person's device.

## One image per item

Store uploaded image files in a configurable persistent directory on the hosting
system, outside the web root and source checkout. PostgreSQL stores only artwork
metadata: unique item FK, opaque asset UUID, server-generated relative storage key,
content type, dimensions and byte length. Never store image bytes in PostgreSQL.
Keep filesystem operations behind an application storage interface.

The API service account needs read/write access to this directory. A containerized
API must mount persistent host storage rather than use its disposable container
layer. Database and artwork files need coordinated backups/restoration. Initial
deployment assumes one hosting filesystem; multiple API instances must use the
same persistent mounted filesystem before uploads are enabled on all instances.
The implementation plan must document configuration, permissions and provisioning;
the current Compose file provisions PostgreSQL only.

An authenticated creator uploads to an existing owned item; creating the item first
avoids unattached uploads. Failure leaves the item without a new image and the UI
allows retry. Accept JPEG, PNG and WebP inputs, at most 5 MiB and 20 megapixels with
bounded dimensions. Decode and re-encode to a single static image, maximum 2048
pixels on the longest edge, stripping metadata. Reject invalid/animated inputs and
SVG. Decoder/package selection, licensing and exact resource limits must be checked
in the implementation plan. Bound decoding work and concurrent uploads.

Use multipart PUT and DELETE operations with expected item revision, existing CSRF
protection and shared write abuse budgets. Enforce limits at proxy and API boundaries.
Validate/decode before the short write transaction, then recheck ownership and
revision under lock. Write validated bytes to a unique staging file, then atomically
rename it to its final immutable path on the same filesystem before committing its
database reference. Never overwrite the currently referenced file in place. The
database transaction switches the current asset metadata and advances item revision;
removal clears the reference. Concurrent stale writes return conflict without changing
the current artwork. File writes and database commits are not one atomic transaction.

After successful replacement/removal, delete the old unreferenced file. Failed or
interrupted requests may leave staging or unreferenced files; use bounded cleanup
with a grace period and reference rechecks so it cannot delete an in-flight upload.
Retain the old file if the database update fails. Filesystem write failures leave
the current reference unchanged. A missing referenced file returns unavailable
artwork and is logged without exposing filesystem paths; the UI uses its fallback.
Test crash/failure boundaries and disk-full behavior. Use server-generated keys
resolved strictly beneath the configured directory; never accept client paths or
original filenames as storage paths, and prevent traversal/symlink escapes.

Serve artwork through a same-origin API route that rechecks current item access on
every request. Only the current asset UUID resolves; replaced asset URLs return 404.
Use no-store, correct content type and nosniff. Private bytes must not be exposed by
static-file middleware, an image optimizer cache, public storage or logs. Existing
provider image allowlists remain intact. Custom artwork uses only the app's exact
artwork route; arbitrary user URLs are not supported. Supply a neutral custom-item
fallback, alt text and keyboard-accessible upload/removal controls.

## Public reporting and moderation

Extend existing report target FKs/discriminators for items and categories. Signed-in
users report accessible public items/categories; private IDs yield 404. Recheck
visibility under transaction locks. Existing Moderator/Admin content moderation may
hide/restore public custom items; category administration remains Admin-only.
Preserve ADR 0011's prohibition on staff moderating their own content, database role
rechecks, stale-write checks, shared locks, CSRF, abuse budgets and transactional
credential-free audit events. Report resolution does not implicitly restore content.
Staff queues may retain access to a reported formerly-public target after it becomes
private, limited to that report and audited staff workflow, without copying content
into general public projections or audit events.

## Contracts and frontend

Proposed routes under `/api/v1`:

- `GET /categories`, `POST /categories`, and owner/admin category operations.
- `POST /media` for custom creation; owner update, publish/unpublish and
  archive/restore operations use item revision. Retain existing catalog read URLs.
- `PUT /media/{id}/artwork`, `DELETE /media/{id}/artwork`, and access-checked
  `GET /media/{id}/artwork/{assetId}`.

Finalize exact verb/DTO names and OpenAPI/proxy entries in the implementation plan.
All mutation DTOs reject unmapped members and derive actor/creator from the session.
Use existing validation/problem conventions: 400 invalid, 404 inaccessible,
409 stale revision/duplicate, 413 upload size and 429 budget exhaustion.

Creation collects title, description, category and private-default visibility, then
offers optional artwork upload. Keep draft form values if publication/category or
upload fails. Common item detail reuses rating/review/comment controls. Add owner
management and category picker/create flows. Category selection is independent of
legacy format/provider filters. Generic tracking uses existing stored status values
with neutral labels Planned, In progress, Completed, Paused and Dropped; tracking
remains optional. No status migration or new rating scale is required.

## Collections become lists

Keep collection identity, owner, name, description, visibility, revision and timestamps.
Require every membership to reference an existing item. Keep unique
`(collection_id, media_item_id)`, position and membership identity/timestamps. Remove
field definitions, collection schema version, copied custom titles and JSON values.

Creation accepts name/description/visibility only. Entry add accepts mediaItemId,
position and expectedRevision; entry update accepts position and expectedRevision
only. Reads return a live visible item summary plus membership metadata. Remove
field/impact routes, contracts, proxy/OpenAPI allowances and UI. Legacy fields,
title and data payloads are rejected rather than ignored.

Private items may be added only by their creator to that creator's private lists.
Public items may be added to any list the caller owns. On publishing a collection,
reject if it contains a non-public, archived or held item; return an owner-safe
validation result. If a member later becomes inaccessible, preserve its row but
filter it from results and totals. Never silently publish its item or category.
The creator may still see a now-private item in their own public list because they
can access it, but ordinary visitors cannot; publishing an already-private-containing
list is blocked. UI explains that a public list's visible entries vary by viewer.

Preserve parent row locks, expectedRevision, uniqueness, deterministic position/UUID
ordering and removal behavior. Removing membership never deletes the item or its
contributions. Recheck item state while adding and while changing list visibility.
Lock ordering must coordinate these operations with item/category lifecycle writes.

## Transition and rollback

Inventory known consumers before contract removal: frontend, API tests, proxy,
OpenAPI and deployment documentation. Repository evidence cannot establish absence
of external/deployed clients; confirm that operational fact before cutover. If any
depend on old contracts, version/deprecate before removal.

1. Add/backfill catalog and category storage without changing existing identities.
2. Implement and verify all access policy paths before allowing private custom data.
3. Add category/custom item/artwork/moderation functionality, then matching frontend.
4. Prepare list schema/API/frontend and an explicit offline test-data clearing tool.
5. Before executing it, identify the intended disposable database without printing
   credentials, stop incompatible writers and record schema version plus counts.
6. Clear only collection memberships and field definitions/values; retain collection
   metadata and advance every retained collection revision. The reset tool requires
   an explicit database target and destructive acknowledgement; this design is not
   that acknowledgement for any particular database.
7. Apply schema removal only when no incompatible rows remain. Normal migrations
   must fail clearly rather than delete data. Roll out matching list API/frontend,
   invalidate old collection caches and verify counts and user journeys.

Preserve users/roles, provider IDs, catalog IDs, ratings, reviews, comments, libraries,
reports, audit events and collection records. Do not drop/reset the database.
Additive migrations may roll back only before unrepresentable custom/private data
exists. Old public-only readers must never run against new private content. Old
collection schema can be recreated, but deleted entry/field data has no promised
recovery. Prefer forward corrections once new data is active.

## Acceptance and delivery

After design approval, write an executable plan with dependent tasks, actual file
paths, migrations, verification commands, lock order and consumer inventory. Update
ADR 0006 only then to link the approved replacement while preserving its history.
The preliminary Phase 1 checkbox stays incomplete until the design is approved and
the implementation plan exists.

Acceptance requires fresh PostgreSQL migration verification; preservation of legacy
UUIDs/provider behavior; privacy tests over all direct/indirect surfaces; race tests
for visibility, membership and contributions; duplicate category races; upload size,
decode, replacement, access and cache tests; filesystem failure/cleanup and persistence
tests; list revision/order/removal tests; strict
API/proxy contracts; and frontend tests/build plus keyboard, responsive Light/Dark
journeys. Run provider regressions and independent review before merge. A successful
catalog detail test alone is not evidence that privacy is complete.

## Implementation choices delegated to the builder

Choose internal class names, migration names, decoder after dependency review,
precise SQL indexes, error field names and component composition. Keep existing
application/infrastructure separation and separate TypeScript interface files.
Any discovered conflict that would change the agreed product behavior returns for
design review rather than silently relaxing privacy or deleting shared data.


## Implementation review refinements (2026-11-08)

Unavailable memberships remain filtered and preserved until the owner acts. An
explicit, revision-protected collection-level removal operation lets the owner
clear unavailable references without exposing their IDs or counts. It never runs
automatically and does not delete catalog items or contributions. Direct hidden
membership IDs still return 404. Publication eligibility is checked only when
changing a private list to public; an already-public list can still be renamed
when a member becomes unavailable. Item-editor conflicts offer an explicit reload
of the saved revision while preserving draft fields.

The independent review found these recovery gaps and verified the fixes; the
regressions were observed failing before the fixes and passing afterward. Real
cutover and external consumer confirmation remain operator gates, not completed
implementation actions.
