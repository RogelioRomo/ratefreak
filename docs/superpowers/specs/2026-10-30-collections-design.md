# Phase 5 — dynamic collections and schema validation

Status: approved by the user on 2026-10-30, including the full written design.

## Intent and scope

Users organize canonical media and entirely custom entries into collections with
their own fields. Collections support mixed media, private/public visibility
(default private), and owner-only editing. Reuse the existing .NET layers, EF Core,
PostgreSQL, cookie authentication and CSRF. No frontend, collaboration, provider
changes, dynamic query language, archive/restore system or bulk data migration.

## Approaches and approved choices

- Prevent duplicate canonical media within each collection, enforced by a partial
  unique database index. The same media can appear in different collections.
  Custom entries have no identity-based deduplication. Allowing repeats was
  considered for viewing diaries; the user chose prevention for this version.
- Field deletion removes its values permanently, following an impact preview and
  explicit confirmation. Blocking deletion until values are manually cleared is
  cumbersome; archiving adds restoration complexity. The user chose confirmation.
- Proposed storage: relational collections/fields/items with JSONB item data, as
  specified in the architecture plan. Per-field value tables complicate typed
  validation and reads; arbitrary JSON schemas introduce an unnecessary language.
  Use a bounded set of field types with application validation and DB constraints.

## Collection and item behavior

- Name: trimmed 1–120 characters; description: optional, at most 2000 characters.
  Treat all content as plain text. Reject malformed Unicode and disallowed controls;
  allow tabs/newlines only in descriptions and text values.
- Canonical item references must exist. Custom items have no media reference and
  must supply a trimmed title (1–200 characters). Canonical items use the catalog
  title and reject a custom title. Item kind/media reference is immutable.
- Each item stores a JSON object keyed by immutable field keys. Reject unknown
  keys, invalid types and invalid values; optional fields may be omitted or null.
  Required fields must be present and non-null; required text cannot be blank.
- Item edits replace the complete data object and custom title, preserving the
  media reference. Item deletion is permanent and owner-only. No collection delete
  route in this slice; account deletion cascades collection records at DB level.
- Collection metadata edits replace name, description and visibility together.
  UUID identity, UTC timestamps, explicit nonnegative integer positions on fields
  and items. Position ties are allowed, ordered by UUID. No automatic reordering.

## Fields and validation

- At most 30 fields per collection. Keys: 1–40 lowercase ASCII letters/digits or
  underscores, starting with a letter; unique per collection. Reserve id, title,
  media_item_id, created_at and updated_at. Labels: trimmed 1–80 characters.
- Supported types: text, number, boolean, date and select. No implicit coercion.
  Text has a configured maximum length of 1–4000 (default 1000). Number uses finite
  decimal values supported by .NET decimal, with optional inclusive min/max.
  Dates are exact valid YYYY-MM-DD strings. Select uses 1–50 distinct nonblank
  string options of at most 80 characters, matched exactly. Configuration rejects
  unknown or irrelevant properties and inconsistent bounds.
- Key and type are immutable. Label and position edits preserve values.
  Changes to required/configuration must leave every existing item valid; otherwise
  return 409 without changing anything. Adding a required field to a nonempty
  collection is rejected; defaults and bulk backfills are deferred.
- Field deletion preview returns field identity, populated-item count and collection
  revision. Null/absent values do not count as populated. Delete requires explicit
  confirmation and that revision. In one transaction, remove the key from all item
  objects, delete the definition, and increment schema version and revision.
  A stale revision returns 409 and requires a new preview; never silently delete
  newly edited values based on an old preview. Item timestamps update when changed.

## Concurrency and persistence

- Three new tables: collections, collection_fields and collection_items; no
  rewrites of existing tables/data. FK ownership cascades; media deletion restricted.
  JSONB data/configuration constrained to objects. Unique collection/key and partial
  collection/media indexes; ordering indexes. No speculative JSONB indexes.
- Serialize every collection mutation with a database row lock on the collection
  inside its transaction, then check ownership, revision and current schema.
  All writes to existing collections require the last observed revision; stale
  edits return 409. Every successful mutation increments revision. Schema changes
  additionally increment schema_version. This prevents concurrent schema/item
  changes bypassing validation and makes deletion impact previews dependable.
- Bound input to existing 64 KiB request limit and JSON depth 16 for custom data.
  Bound collection/item list pages using existing 1–10000 / 1–100 rules.
  Paginated totals and rows use a consistent DB snapshot.

## API and access

Base: /api/v1/collections.

- POST /: create metadata and initial field definitions atomically; 201 + Location.
- GET /: list public collections plus the caller's private collections; optional
  own-only filter requires authentication. Order newest first then UUID.
- GET /{id}: metadata, fields and revision; public or owner only.
- PATCH /{id}: replace metadata with expected revision.
- POST /{id}/fields: add a field with expected revision.
- PATCH /{id}/fields/{fieldId}: replace mutable field properties with expected revision.
- GET /{id}/fields/{fieldId}/deletion-impact: owner-only impact preview, no-store.
- DELETE /{id}/fields/{fieldId}: confirmation and expected revision in query parameters.
- GET /{id}/items: accessible items ordered by position then UUID, paginated.
- POST /{id}/items: create item with expected revision; 201 + item Location.
- GET /{id}/items/{itemId}: accessible item details.
- PATCH /{id}/items/{itemId}: replace mutable item properties with expected revision.
- DELETE /{id}/items/{itemId}: expected revision in query parameters; 204.

Mutation responses include the current collection revision (response body or header
for 204). Unknown resources, private resources viewed by others and foreign writes
return 404. Unauthenticated mutations return 401; missing CSRF returns 400.
Malformed input returns 400; duplicate media, incompatible schema changes and stale
revisions return 409. Reject unexpected body properties. Author summaries expose
only public ID/handle/display name, never email. All collection reads use no-store
because visibility can change. Reuse public catalog and personal read/write rate
budgets, preserving existing sanitized Problem Details behavior.

## Verification and completion

Test validation before implementation, then PostgreSQL HTTP integration coverage
for public/private access, ownership, CSRF, duplicates, custom entries, each field
type, incompatible schema edits, deletion preview/confirmation/staleness, pagination,
concurrent writes, DB constraints and rollback. Generate/review additive migration,
verify repeat application and model consistency, run full suite and Release build.
Update README, ADR and checklist with actual evidence. Apply locally and smoke-test
with temporary records only, retaining existing accounts/catalog/personal data.
