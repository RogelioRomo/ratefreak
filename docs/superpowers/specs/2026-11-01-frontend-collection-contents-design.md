# Collection fields, entries and schema-change confirmation

Status: approved and implemented, 2026-11-01. Completion and real-backend
verification evidence are recorded in `docs/frontend-checklist.md`. Next slice after the completed and live
verified metadata/visibility UI. Preserve all existing uncommitted work on
`feat/collection-metadata`. Native/code-first execution, current editorial visual
system, separate interface files and TanStack Query remain established choices.
No automatic commits, merge, push or deployment.
Independent builder check completed: “nothing important left.”

## Intent and approach

Let owners define their collection's fields, add catalog titles or custom entries,
edit values and remove entries. Visitors can read entries in accessible collections.
Field deletion requires an impact preview and explicit confirmation of permanent
value loss. The existing backend implements these operations; no migration or new
backend business rules are expected.

Approved approach: extend collection detail with paginated entries, an inline
add-entry form and owner field management; give each entry a focused detail/edit
page. This keeps collection context and the existing metadata/visibility workflow.
A separate administration builder would add navigation and duplicate context for
the same small set of native forms. A raw JSON editor is shorter to implement but
does not provide the typed, labeled controls this frontend needs. Preserve the
existing visual direction; this is not a generic builder.

## Collection and entry pages

Replace the collection detail's future-interface note with Entries and Fields
sections, keeping existing metadata, owner and visibility. Entries use a flat
semantic list with title, Custom entry or Catalog title, and an entry-detail link.
Canonical entries also link to `/catalog/{mediaId}`; do not invent artwork or a
media type absent from the entry DTO. Public visitors get the same literal values
as owners, without management controls. Collection visibility governs all entries.

Entry paging uses `itemsPage`, default 1, fixed pageSize 20, bounds 1–10000.
Reject unknown/duplicate/invalid detail-page query parameters before entry reads;
offer a canonical reset link. Back/Forward restores paging. Page changes must
not remount metadata, field or add-entry drafts. Keep loading, empty, out-of-range,
retry/429, missing and inaccessible states distinct; a collection/entry 404 hides
cached contents and never implies that the collection is merely empty.

`/collections/{collectionId}/items/{itemId}` displays entry title, a parent link,
canonical catalog link when present and all current fields as labeled values in
field position/ID order. Preserve line breaks, wrap long content, render strings
literally and display booleans as Yes/No, zero as 0, dates without timezone shifts,
and absent/null as No value. An unavailable entry has neutral missing/inaccessible
copy. Only the verified owner sees Edit entry and Remove entry.

Within collection detail, allow only one owner editor at a time: metadata, one
field editor, field removal, or add entry. Finish or cancel it before opening
another. Entry detail has one edit/removal flow. Do not silently discard a dirty
editor when a different action is selected. This is a local interaction guard;
other tabs/devices still use server revision conflict protection.

## Defining and editing fields

Owners see ordered field definitions and Add field, Edit and Remove actions.
The field form has Key, Label, Type, Required and Position, plus configuration
appropriate to the type. Defaults: optional text field, maxLength 1000, position 0.
Display positions as explicit nonnegative integers, bounded to the backend int
range 0–2147483647. Ties are allowed and sorted by ID; no drag-and-drop or automatic
renumbering. A maximum of 30 fields applies.

Key is immutable after creation: 1–40 lowercase ASCII letters/digits/underscores,
starts with a letter, unique within the collection. Reserved keys: id, title,
media_item_id, created_at, updated_at. Label is trimmed 1–80 UTF-16 units. Type is
also immutable after creation. Editing replaces Label, Required, Configuration and
Position together; display the saved Key and Type as read-only text.

Configuration uses native labeled controls, no JSON textarea:

- Text: maximum length integer 1–4000; the existing unspecified default is 1000.
- Number: optional inclusive minimum and maximum, represented as decimal text;
  validate minimum <= maximum without binary floating-point rounding.
- Boolean/date: no configuration properties; send an empty object.
- Select: 1–50 options, each nonblank and at most 80 UTF-16 units, exact distinct
  strings. Use individual labeled option inputs with Add option/Remove option;
  preserve intentional whitespace/case and do not silently rename stored values.

Explain before saving that Required or configuration changes must keep all saved
entries valid. Adding a required field to a nonempty collection is prohibited;
disable that choice when a verified entry count is nonzero, with an explanation,
while retaining server validation for concurrent changes. No defaults, automatic
backfill, field-key/type conversion, archive or bulk repair.

On 400 show field/configuration errors beside the inputs and preserve the draft.
Backend field error keys can be `fields[index].property` rather than `field.property`;
map the target field when identifiable and retain an accessible error summary for
all unmatched errors, never silently drop them. A generic 409 can mean stale data,
duplicate key or incompatible existing values: do not label every conflict as
concurrent editing. Preserve input and offer explicit Reload latest collection,
warning that it replaces this editor draft. If a refreshed unchanged schema still
rejects the change, explain the compatibility/uniqueness constraints and allow the
person to revise their proposal. Never automatically repeat the failed mutation.

## Adding and editing entries

Add entry offers Catalog title or Custom entry. Custom entries require a trimmed
title of 1–200 UTF-16 units. Catalog selection uses an explicit title/type search
against the existing local catalog API, with cancelable requests and 20-row paging
inside the picker. Selection shows the exact chosen title and media ID internally;
send only mediaItemId, with title null. Do not accept a free-text catalog identity,
automatically import providers, or add titles to the user's personal library.
Offer a separate catalog/discovery link if the desired title has not been imported.

Entry kind/media identity cannot change on edit. Canonical titles stay read-only
and use title null on replacement; custom titles remain editable. Both create/edit
include Position and a native control for every field in the loaded schema:

- Text: textarea, configured maximum length; required values cannot be whitespace.
- Number: text input with decimal keyboard hint, preserving its exact decimal text.
- Boolean: explicit Yes/No select; false is never treated as missing.
- Date: native date input storing exact calendar YYYY-MM-DD.
- Select: native select containing the saved exact option strings.

Optional values have an explicit Set a value toggle so unset, empty text, false and
zero cannot be confused. Unset omits that key; absent and null read as No value.
Required values cannot be unset. Submit the complete data object for replacement,
not only changed keys. Retain all unchanged values loaded into the editor; never
save while the record/schema read has failed or discard an unrecognized/malformed
value silently. A schema mismatch requires a fresh stable snapshot and deliberate
reload. Field labels identify controls; immutable keys identify data/error mapping.

Prevent duplicate canonical titles within a collection. Other collections may use
the same title; custom titles need not be unique. Show the backend's generic 409 as
a potential changed collection, duplicate title or schema incompatibility, with
draft retention and explicit reload/reselection. Do not infer duplicates by scanning
one list page. A catalog selection that has disappeared can cause 404; refresh the
parent collection to distinguish a missing title from loss of collection access,
then offer reselection only if access remains verified.

## Revisions, stable reads and decimal fidelity

Every collection mutation increments its single revision, including metadata,
fields and entries. Editors keep the revision of their original stable snapshot;
background refreshes never rebase a draft silently. All IDs and cache keys remain
canonical lowercase and viewer-scoped. New item list/detail keys include collection
identity, item/page and the revision/schema context used to interpret their values.
No collection content enters the session startup public-query allowlist.

The current item list/detail responses have no collection revision. Obtain a
stable logical snapshot by reading collection detail (R1), the needed item/page,
then collection detail (R2); use it only when R1 == R2. Reuse one verified read
bundle for display/editor initialization. On a changing revision, retry this
read-only sequence once, then show explicit Retry rather than loop. Opening a field
or add-entry editor uses freshly verified collection fields/revision; existing-entry
edit additionally verifies its item in that same bracket. Server changes after the
last read still produce an ordinary write conflict, never a silent overwrite.

Field/item write responses return only a field/item or 204; their updated revision
is not in those bodies. After confirmed mutation success, close/reset only that
editor and refresh the collection/read bundle before enabling another write. If
refresh fails, report Saved/Removed with Refresh needed; retry the read alone.
Do not turn a successful mutation followed by a failed read into a retryable write
failure. This avoids new response-header coupling or speculative revision increments.
Invalidate affected collection lists/detail/items across viewer variants and clear
obsolete alternate-viewer data when visibility changes. Preserve unrelated drafts.

The backend deliberately supports exact .NET decimal values; standard JSON.parse
and Number can round valid stored numbers. This slice must not narrow that contract
or round values on a description/title-only edit. Add a small feature-owned lossless
JSON codec for dynamic data/configuration numeric tokens, exposing decimal text to
forms and emitting validated raw JSON numbers on writes. The shared request helper
may gain narrowly opt-in success decoding/body encoding callbacks; default behavior,
path/CSRF/error/204/cancellation handling remain unchanged. The codec owns JSON
escaping and validates numeric tokens; no string interpolation of untrusted values
into JSON and no eval, whole-response regex substitution or browser-specific raw-JSON
API dependency. Envelope counts/positions/revisions must be safe integers; reject
an unrepresentable envelope rather than rounding. Keep interfaces explicit.

Normalize decimal coefficient/scale with string/BigInt arithmetic. Validate the
.NET 96-bit coefficient limit (79228162514264337593543950335) and scale up to 28
after removing redundant trailing zeroes; handle bounded exponent notation, signed
zero, tiny/large values and min/max exactly. Backend validation remains authoritative.
Do not spread this codec into unrelated catalog/rating APIs. Dependency choice and
codec file boundaries are builder-owned, but any proposed new dependency needs
primary-source review and must not change these observable guarantees.

## Destructive actions

Entry removal uses an inline confirmation naming the entry and explaining that its
stored values are permanently removed while the catalog title and personal activity
remain. Confirm/Cancel, expectedRevision from a freshly loaded stable entry snapshot,
no automatic retries. A conflict preserves context and requires fresh snapshot plus
new confirmation. On success return to the collection and recover an empty last page.

Field removal first loads owner-only deletion-impact. Show field label/key and the
returned populatedItemCount with explicit permanent deletion language. Null/absent
values do not count. Even zero requires explicit Confirm remove field or Cancel.
Deletion sends confirm=true and expectedRevision from that exact impact preview,
never from a later background collection read. If any observed revision changes
after preview, invalidate the confirmation. On 409 discard the old preview and
require a new preview and a new click; never silently update its revision or replay
the delete. Loading/error/session checks disable confirmation. Session uncertainty,
identity changes and leaving the flow invalidate consent even if the same account
later returns. No typed magic words, modal or collection deletion.

## Proxy, session and failures

Add only exact collection GUID paths: GET/POST items, GET/PATCH/DELETE items/GUID,
POST fields, PATCH/DELETE fields/GUID and GET fields/GUID/deletion-impact. No fields
list/detail GET that the backend does not implement. Existing root/detail routes
remain exact. Only item list GET accepts page/pageSize (1–10000 / 1–100). Item DELETE
accepts only a positive safe-integer expectedRevision; field DELETE additionally
requires exactly confirm=true. Reject duplicates, unknown keys and all queries on
other new routes. Preserve JSON/body-size/Origin/CSRF/cookie/attribution/no-store
guards; DELETE retains the existing guarded empty-JSON request convention.

Reuse the retained session boundary: conceal/inert/disable contents during checks,
pause reads, abort pending writes, retain same-account nondestructive drafts and
discard them on confirmed identity changes. Clear deletion consent on uncertainty.
Late account/route results cannot navigate, show success or seed another account's
cache. On 401 revalidate session and offer existing canonical parent-collection
login continuation. Anonymous entry pages remain read-only; no new query-bearing
continuation rule or write replay. Distinguish 400/401/404/409/413/429 and transport
failures; retain input and render accessible error summaries. Unknown create/delete
outcomes require checking current server state before another action.

## Verification and delivery

Test first: exact proxy routes/method-specific queries/guards; lossless codecs with
high-precision round trips and unchanged-value replacement; each field type and
configuration bounds; zero/false/blank/unset; keys/labels/options/positions; stable
revision-bracket reads, stale/late responses and bounded retries; local editor
exclusion; generic conflicts/draft reload; duplicate catalog titles; unavailable
records; failed post-success refresh without mutation replay; deletion impact zero,
populated values, changed revision, new confirmation and consent invalidation;
same-account drafts and changed-account privacy; item paging/history and error maps.

Run full frontend tests/lint/typecheck/build/diff checks and the existing real
PostgreSQL collection integration suite. Real backend/browser verification is
required per the user's established preference, not replaced by fixture-only checks:
start/reuse local PostgreSQL, API5080 and frontend3100 with existing ignored secrets.
Use dedicated test accounts/collections, add/edit each field type, create custom and
canonical entries using existing catalog records, validate persistence across reload,
public/private and nonowner access, duplicate409, real stale-revision conflicts,
incompatible schema409 and stale/populated field deletion confirmation. Do not
import or change real catalog/personal data just to test. If no suitable canonical
catalog title exists, provision it only in a disposable test database and distinguish
that evidence. Clean up only exact verification-owned records after checks.

Inspect both themes at 320/768/1024/1440, keyboard/focus, long labels/options/text,
30 fields, missing values, false/zero, errors and confirmations. Record real vs
synthetic evidence honestly; unit fixtures remain appropriate for deterministic
faults and validation but do not establish live integration. Obtain focused fresh
code/UI review, update checklist, README and collection surface guidance, preserve
incumbent visual tokens and report services left running. No broad social/profile,
external sign-in, provider import, drag sorting, bulk schema repair or new design
system work. Implementation planning follows approval of this document.
