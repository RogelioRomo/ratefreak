# Custom catalog operations and collection cutover

This is an operator runbook. Implementation and disposable-database verification
do not authorize resetting an existing installation or deploying a release.

## Artwork storage

Set `Artwork__Enabled=true` and `Artwork__RootPath` to an absolute persistent
host directory, for example `/var/lib/ratefreak/uploads`. Keep it outside `wwwroot`
and outside any static-file server mapping. The API service identity needs read,
write, rename and delete access; other untrusted identities must not write there.
Do not point it at a symlink or share it with unrelated uploads. Container installs
must mount persistent storage at that path; an ephemeral container layer loses images
on replacement. Multiple API replicas must use the same persistent filesystem.

The database stores asset IDs, references, dimensions, length and MIME metadata,
never image bytes. A custom item has at most one image. Uploads accept static JPEG,
PNG or WebP up to 5 MiB, 20 megapixels and 8192 pixels per axis. The API decodes at
most two concurrently and re-encodes WebP with orientation applied, metadata stripped
and longest edge at most 2048 pixels. SkiaSharp 4.155.0 and its Linux native assets
must be validated on the actual deployment architecture. Upload is disabled by
default; enabled but unavailable storage fails safely without publishing a reference.

Downloads always go through access checks, including public-to-private changes;
keep the proxy's `Cache-Control: no-store` and `X-Content-Type-Options: nosniff`.
Do not expose the directory through a CDN, static server or Next image optimizer.
Staff can inspect private reported artwork only through the scoped moderation route.

Monitor disk space, upload errors, missing-file diagnostics and cleanup failures.
Replacement promotes an immutable file before committing the new reference and only
deletes the old image after commit. A failed commit may leave an orphan while the
old reference remains valid. Hourly bounded cleanup progresses through files and
removes unreferenced files older than 24 hours, excluding active uploads. Database
reference-check failure prevents deletion. Allow capacity for staging and the orphan
grace period; do not use filesystem deletion as a way to change catalog visibility.

Back up the database and artwork directory together during a quiescent maintenance
window. Restore both to the same point, with permissions and mount paths restored.
Before reopening writes, verify referenced files exist, owner-only images return
404 to anonymous viewers, public images load through the API, and upload/replace/
remove works after an API restart. A database-only backup cannot restore images.

## Upgrade prerequisites

Repository consumers are updated together: API DTOs/OpenAPI, web proxy and frontend.
External consumers are **unconfirmed**. The operator must inventory integrations and
confirm they can adopt list-only contracts before real cutover. If legacy clients
exist, arrange versioning/transition first. An in-repository search is not evidence
that no deployed clients exist.

Collections retain metadata and become memberships of existing catalog UUIDs.
Fields, free-standing entry titles, custom values and schemaVersion are retired.
The normal `ConvertCollectionsToLists` migration refuses any legacy membership or
field rows and does not delete them. Fresh databases need no reset. Downgrade cannot
reconstruct discarded values and is guarded; restore a coordinated backup instead.

For a legacy **test-data** installation only, an independently approved offline reset
can clear memberships and fields while retaining collection metadata and advancing
revisions. It does not clear users, items/provider mappings, ratings, reviews,
comments, libraries, reports or audits. This reset is not an export or conversion
of valuable legacy data. Stop if existing values must be retained and arrange a
separate migration design.

## Approved test-data cutover sequence

1. Confirm external consumers, explicit permission to discard legacy entries/fields,
   and matching API/frontend release artifacts. Record the exact host and database
   identity without credentials. Take and verify database/filesystem backups.
2. Stop all API instances, jobs and incompatible writers. The command takes advisory
   and table locks, but those are not a substitute for stopping old applications.
3. Capture before counts for users, media_items, media_external_ids, ratings,
   reviews, review_comments, user_media, content_reports, moderation_events,
   collections, collection_items and collection_fields using the installation's
   schema/table names. Record collection revisions and representative retained rows.
4. Configure the maintenance process with the intended connection string through
   normal secure configuration. Run the new API executable from its release directory
   with `--reset-test-collection-data --database-name EXACT_DATABASE_NAME`. This is a
   dry run. It reports exact database name, applied schema version and before counts
   for collections, memberships and fields; it never prints connection credentials.
   A target-name mismatch or missing legacy schema refuses the operation.
5. Only after reviewing the dry run and authorization, run the same command with
   `--confirm-test-data-loss`. The transaction clears collection_items and
   collection_fields and advances every retained collection revision. The output
   counts are **before** counts; independently verify both tables are empty,
   collection count/metadata unchanged, each revision advanced once, and all other
   recorded counts/representative rows preserved. A command failure must leave the
   reset transaction rolled back. Do not blindly retry an uncertain result.
6. Apply the release's normal EF migrations offline. Confirm no pending model
   changes. Install the matching frontend/proxy before reopening traffic; retire old
   assets and force old tabs to reload. Old fields/data payloads and routes must fail.
7. Verify provider imports retain existing UUIDs; create a private custom item,
   confirm stranger/anonymous item and image 404, publish, add the same identity to
   two lists, rate/review/comment, then unpublish and confirm retained contributions
   are restricted. Verify list counts filter inaccessible memberships. Reorder/remove
   must not delete catalog items or personal contributions. Exercise image persistence
   across restart and explicit archive/restore to private.
8. If checks fail, keep writers stopped and restore the coordinated pre-cutover
   backups with the matching previous application version. Do not attempt to infer
   lost fields from the new list schema. Record verification and operator acceptance
   before reopening traffic.

List memberships remain when an item becomes unavailable. Publishing a private
list with private, archived or held members is refused. Its owner can explicitly
confirm **Remove unavailable memberships** to recover a blocked list without seeing
hidden identities or counts. This removes references only and never changes the
underlying item or contributions; it is never performed automatically on publication.
