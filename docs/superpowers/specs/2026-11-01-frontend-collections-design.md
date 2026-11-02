# Collection metadata and visibility

Status: approved and implemented, 2026-11-01. Completion evidence is recorded in
`docs/frontend-checklist.md`. The user selected the recommended public browsing
plus owner-management slice and approved native plan execution. Preserve the editorial
design system and existing session protections. No automatic commit, push or deploy.
Independent builder check completed: “nothing important left.”

## Intent and approaches

Let visitors discover accessible collections and let signed-in owners create and
edit their name, description and visibility. This is Phase 5's first slice;
fields, entries and schema-change confirmation remain the next slice.

Chosen approach: public/access-aware collection browsing with My collections,
creation and owner-only metadata editing, built on existing APIs. It delivers a
shareable collection address and its visibility rules together. Alternative:
owner-only management first; smaller but postpones the public side of visibility.
Full fields/items delivery would exceed the agreed focused slice.

Use the installed Next.js App Router, CSS Modules, feature-owned APIs/hooks and
TanStack Query. Object contracts and props belong in separate interfaces files.
No backend changes, migrations or dependencies are expected. Read installed Next
guides before implementation. No new visual direction interview is needed.

## Pages and navigation

Add Collections to the shared navigation, active on all collection routes.
`/collections` lists accessible summaries in a flat semantic list: wrapping title
link, owner display name/handle, description when present and visibility label.
Do not invent artwork, counts, profile links or entry previews. Explain that the
default list includes public collections and, when signed in, one's own private
collections. The API cannot return a public-only list for an authenticated viewer.

Use explicit All accessible / My collections scope selection and Apply, with
`scope=mine` and `page` in the URL; omitted scope means all. Page size is 20.
Scope changes reset page 1. Back/Forward restores submitted state. Reject unknown,
duplicate or invalid parameters without fetching; page bounds are 1–10000.
Provide reset, loading, retry/429, empty, and out-of-range recovery states.
Anonymous My collections offers login rather than calling an authenticated list.

`/collections/new` is protected and contains labeled Name, Description and
Visibility controls, defaulting to Private. Explain that Public exposes the
collection and its existing entries to everyone. Explicit Create collection sends
`fields: []`; no field builder. Successful creation navigates to its detail page.
Anonymous entry offers login continuation; no automatic creation after login.

`/collections/{guid}` shows accessible metadata, owner and visibility. Use one
neutral unavailable state for inaccessible private and missing records. For the
verified owner, an Edit metadata action reveals an inline editor populated from
the successfully loaded record. Nonowners have no editing controls. Include a
clear note that entry and custom-field interfaces are coming in the next slice;
do not claim an existing collection is empty or display an invented entry count.
No collection deletion control: the backend has no collection-delete endpoint.

Reuse Fraunces/DM Sans, theme tokens, native controls and shared feedback. Desktop
may use the established heading/form split; mobile stacks in reading order.
Plain text descriptions preserve newlines, wrap long content and never render HTML.

## Editing, visibility and conflicts

Name is required, trimmed before sending, 1–120 UTF-16 units after trimming.
Description is optional, at most 2000 UTF-16 units; send null for an empty input.
Visibility is exactly private/public. Show server field errors and preserve drafts
on validation, rate-limit and transport failures. Backend remains authoritative
for prohibited controls and malformed Unicode. No uniqueness claim for names.

PATCH replaces all metadata: send name, description, visibility and the revision
from the snapshot loaded into the editor. Never advance that revision silently
when background data refetches while the draft is dirty. Preserve existing fields
and items by sending no schema/item properties. Explain that changing visibility
also changes access to existing collection contents; the native visibility choice
and explicit Save metadata are the confirmation, with no extra modal.

On 409 keep the draft and block resubmission with the stale revision. Offer Reload
latest metadata, explicitly warning it replaces the draft; only that deliberate
action loads the latest snapshot and revision. Do not automatically merge, retry
or overwrite. Cancel editing discards local changes and returns to current details.
404 during editing conceals the editor and shows unavailable; 401 triggers session
revalidation/login recovery. Error messages never claim a failed write succeeded.

Disable duplicate submissions; no optimistic success or automatic mutation retry.
After success, use returned details, reset the editor snapshot and invalidate
collection lists and detail variants belonging to the current viewer. Changing
public to private must not leave old public snapshots visible in the local cache.
Aborted/uncertain creates are not replayed: explain the outcome may be unknown and
offer My collections to check before creating again.

## Session-aware reads and writes

Every collection read is viewer-sensitive, including the default list and detail:
the same URL can contain an owner's private data. Keys include an explicit verified
anonymous marker or account ID, scope/page or collection ID. Never classify the
whole collections namespace as public in session startup cache preservation.
Wait for initial session verification before these requests. Anonymous public
browsing works after verification; session failures have explicit retry guidance.

Conceal cached collection content during session checks and stop viewer-sensitive
reads. Keep mounted same-account drafts hidden, inert and disabled through a
background check; resume only for the same verified identity. Confirmed logout or
account change discards drafts/results and uses the existing cache-clearing policy.
On session errors keep drafts concealed until identity is verified again. Cancel
pending writes on session/route changes and ignore stale outcomes; never replay.
Respect query cancellation and verify account/route identity before applying results.

Extend the existing strict login continuation allowlist only with canonical
`/collections`, `/collections/new`, `/collections/{lowercase-guid}`. Login from My
collections may return to `/collections`, then the person selects My collections;
no query-bearing continuations. Existing external/encoded/ambiguous/query/fragment
and duplicate-returnTo rejection stays intact. Returning never performs a write.

## Transport boundary and contracts

Allow only GET collections, GET collections/GUID, POST collections and PATCH
collections/GUID through the proxy. List query keys are ownOnly, page, pageSize:
exact true/false, page 1–10000 and pageSize 1–100; the UI sends 20. Reject duplicate
and unknown keys. Reject query strings on the remaining new routes. Keep all
fields/items routes and DELETE closed in this slice.

Retain exact Origin, fresh CSRF, JSON/body limit, cookie allowlist, authenticated
attribution, timeout/cancellation and no-store protections on the relevant paths.
Use existing CollectionSummary, CollectionDetails and CollectionPage contracts;
details include fields but this UI does not edit or render field configuration.
Creation returns 201/details and PATCH returns 200/details; revision is available
in the body, so no new response-header transport is required. Preserve numeric
revision semantics; do not fabricate an increment or expose it as product copy.

## Verification and delivery limits

Test first: exact proxy methods/query rejection/guards; API contracts and signals;
canonical login paths; URL scopes/paging/history; loading/error/empty/unavailable;
private defaults; owner-only controls; replacement metadata; revision 409 with
draft retention and deliberate reload; visibility updates/cache invalidation;
same-account checking/draft restoration; logout/account changes; stale responses,
pending-write cancellation and uncertain create recovery. Use real QueryClient
component tests and synthetic network responses rather than real-account writes.

Run full frontend tests, lint, typecheck, production build and diff checks. No
backend test rerun is required if backend code stays unchanged. Inspect both themes
at 320/768/1024/1440, keyboard/focus, long names/descriptions, visibility/conflict
states, and browser history; check overflow and runtime errors. Read-only live
checks may verify anonymous list/detail access. Clearly distinguish fixture and
live evidence. Obtain a fresh focused code/UI review, then update the checklist
with actual results and runtime state. Preserve existing local services and secrets.

Builder-owned: component/file splits, precise copy/spacing within the current
design, test helpers, safe query-key layout and invalidation mechanics. No new
animation, stats, search API, sorting, profile/social links, external sign-in,
entry/schema controls or unrelated refactors. Next slice remains fields/items and
schema-change confirmation.
