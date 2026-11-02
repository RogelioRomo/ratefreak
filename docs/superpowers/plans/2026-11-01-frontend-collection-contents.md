# Collection Fields and Entries Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [x]`) syntax for tracking. Preserve the established native execution method; use a fresh whole-change reviewer after implementation.

Status: implemented and verified 2026-11-01. Actual test/review/runtime evidence is in `docs/frontend-checklist.md`. Work remains uncommitted on `feat/collection-metadata`.

**Goal:** Complete collection entry browsing/management, typed custom fields and revision-bound destructive confirmation.

**Architecture:** Extend the existing collections feature and retained session boundary. Add a feature-owned exact JSON/decimal layer through opt-in shared transport callbacks, stable collection/item read bundles, and focused native form components. Keep backend contracts and business rules unchanged.

**Tech Stack:** Installed Next.js 16.4.3, React 19.3, TypeScript, CSS Modules, TanStack Query 5, Vitest/Testing Library, .NET 10/PostgreSQL integration tests, Node 24 and pnpm 10.

**Spec:** `docs/superpowers/specs/2026-11-01-frontend-collection-contents-design.md` (approved by the user).

## Global Constraints

- Preserve all existing uncommitted work on `feat/collection-metadata`.
- No automatic commits, merge, push or deployment.
- No migration or new backend business rules are expected.
- Native/code-first execution, current editorial visual system, separate interface files and TanStack Query remain established choices.
- Fixed pageSize 20; page bounds 1–10000. Positions: 0–2147483647; ties sorted by ID.
- At most 30 fields; key 1–40 ASCII characters, label 1–80 UTF-16 units; custom title 1–200 UTF-16 units after trimming.
- Text maxLength 1–4000, default 1000; select 1–50 exact distinct options, each nonblank and at most 80 UTF-16 units.
- Exact .NET decimal coefficient limit 79228162514264337593543950335, scale up to 28; never round dynamic values through Number.
- Retain existing Origin/CSRF/JSON/body-size/cookie/attribution/no-store guards; no mutation retries or optimistic success.
- Preserve same-account nondestructive drafts during concealed session checks; invalidate deletion consent during any identity uncertainty.
- Real backend/browser verification is required, not replaced by fixture-only checks.
- Clean up only exact verification-owned records; preserve real catalog/personal data and local secrets.

## Review Focus

- Literal strings resembling codec tags or JSON escapes must remain strings and cannot become executable numeric JSON (Tasks 1–2).
- Item data and field definitions read across different revisions must never initialize a replacement editor together (Task 3).
- A completed write followed by unavailable refresh must show success plus Refresh needed and never replay the mutation (Task 3).
- A session check or remote mutation after impact preview must invalidate deletion consent, including a zero-value field (Task 6).
- Exact numeric zero, false, empty text and unset must survive schema changes, editor reopen and title-only replacement distinctly (Tasks 4–5).

## File map and execution setup

Read the spec and `web/AGENTS.md`, then installed Next guides
`web/node_modules/next/dist/docs/01-app/03-api-reference/03-file-conventions/page.md`
and `01-getting-started/05-server-and-client-components.md` before route work.
Use the worktree skill to check current isolation. If retaining this checkout,
stay on the feature branch and preserve all prior files; never silently restart
from main. Baseline tests and a plan-specific execution ledger precede Task 1.
Do not modify or delete the earlier metadata plan's workspace.

Paths below under `F/` mean `web/features/collections/`, not a literal directory.
All props/contracts go in `F/interfaces/`; union/tuple aliases may remain aliases.
Tests live beside their source; DOM tests declare jsdom. Existing
`F/components/test-support.tsx` supplies real QueryClient/SessionContext helpers;
extend only the necessary fixtures for complete backend response shapes.

Create exact-number, field/item and read-bundle contracts; feature codec/query
helpers; field, entry and confirmation components. Extend existing collection
detail, styles, keys, APIs and metadata editor only where coordination requires it.
One new thin route: `web/app/collections/[id]/items/[itemId]/page.tsx`.
No new global state, UI framework, drag system or provider integration.

### Task 1: Exact decimal/JSON transport without changing existing consumers

**Files:** Create `F/interfaces/exact-json.ts`, `F/query/exact-decimal.ts`,
`F/api/exact-json.ts`, their adjacent tests; modify
`web/shared/interfaces/api-request-options.ts`, `web/shared/api/request.ts`,
`web/shared/api/request.test.ts`.

**Interfaces:** `DecimalValue` is an opaque/branded in-memory exact decimal token;
ordinary JSON objects/strings cannot impersonate it. Produce
`decimalValue(text: string): DecimalValue`, `decimalText(value: DecimalValue): string`,
`compareDecimals(a: string, b: string): -1 | 0 | 1`,
`parseExactJson(text: string): unknown`, `stringifyExactJson(value: unknown): string`.
Parser numeric tokens retain their original lexeme internally, including envelope
integers; domain decoders in Task 2 decide which tokens are decimal vs integer.
Extend `ApiRequestOptions<T = unknown>` with optional `decode: (text: string) => T`
and `encodeBody: (body: unknown) => string`; `apiRequest<T>` consumes the generic
options while retaining default response.json/JSON.stringify behavior.

- [x] Write failing tests that round-trip 0, false, empty strings, null, escaped quotes/newlines, arrays/objects and literal strings/objects resembling the internal token tag. Assert exact bytes/value for 79228162514264337593543950335 and 0.1234567890123456789012345678; preserve 9007199254740993 as a numeric token rather than a rounded Number. Reject malformed JSON, invalid raw-number grammar, nondecimal values, unsupported coefficient/scale and nonfinite input. Normalize signed zero, redundant trailing zeros and bounded exponent notation without expanding huge exponents; compare inclusive bounds exactly.
- [x] Add transport tests: opt-in decoder receives untouched successful response text, encoder emits exact JSON under the existing CSRF guard, failed responses still use Problem Details, 204 bypasses decoding and body-read/decoder cancellation stays cancellation. Existing callers without callbacks remain unchanged.
- [x] Run `pnpm --dir web test features/collections/query/exact-decimal.test.ts features/collections/api/exact-json.test.ts shared/api/request.test.ts`; observe expected missing-feature/assertion failures.
- [x] Implement a bounded token-aware JSON parser/serializer (no eval, regex replacement, unsafe string insertion or browser-only rawJSON APIs). Use JSON string escaping for every ordinary key/string; only validated branded number tokens emit unquoted numeric JSON. Normalize/check decimal coefficient/scale with string/BigInt arithmetic. Cap nesting at 16 for dynamic values and reject malformed Unicode/control input according to backend rules. Keep optional shared callbacks in the same error/cancellation/204 pipeline, not a second transport implementation.
- [x] Rerun focused tests; expected all pass. Do not add a dependency unless primary-source review and a recorded justification show it improves this exact scope.

### Task 2: Typed field/item APIs and exact proxy expansion

**Files:** Modify `F/interfaces/collections.ts`, `F/api/collections.ts` and tests;
create `F/interfaces/contents.ts`, `F/api/contents.ts`, `F/api/contents.test.ts`,
`F/api/decode-contents.ts`, `F/api/decode-contents.test.ts`.
Modify `web/server/api/collection-policy.ts`, `collection-policy.test.ts`,
`proxy.ts`, `proxy.test.ts`.

**Interfaces:** `CollectionItem {id, mediaItemId: string|null, title,
data: Record<string, string|boolean|null|DecimalValue>, position, createdAt, updatedAt}`;
`CollectionItemPage {items: CollectionItem[], page, pageSize, totalCount}`;
`DeletionImpact {fieldId, key, populatedItemCount, revision}`. Field configuration
uses typed unions for text/number/boolean/date/select; numeric min/max are
DecimalValue, text maxLength and positions are safe integers. Produce
`getItems(id, page, signal)`, `getItem(id, itemId, signal)`,
`createItem(id, input, signal)`, `updateItem(id, itemId, input, signal)`,
`deleteItem(id, itemId, revision, signal): Promise<void>`,
`createField(id, input, signal)`, `updateField(id, fieldId, input, signal)`,
`getDeletionImpact(id, fieldId, signal)` and
`deleteField(id, fieldId, previewRevision, signal): Promise<void>`.
Produce `FieldDefinitionInput`, `FieldCreateInput`, `FieldUpdateInput`,
`ItemCreateInput` and `ItemUpdateInput`, mirroring the matching C# records exactly;
all writes carry expectedRevision and use existing JSON/CSRF transport.

- [x] Write failing route/query/API tests accepting only GET/POST collection/GUID/items, GET/PATCH/DELETE items/GUID, POST fields, PATCH/DELETE fields/GUID and GET fields/GUID/deletion-impact. Preserve root/detail behavior. Reject fields-list/detail GET, malformed segments, wrong methods and path/URL mismatch.
- [x] Test method-specific query validation: root list unchanged; item GET list accepts only page/pageSize; item DELETE only positive safe-integer expectedRevision; field DELETE requires it plus exact confirm=true. Unknown/duplicate keys, unsafe integers, missing confirmation, detail queries and query-bearing POST/PATCH are locally rejected. Ensure DELETE queries reach validation without broadly opening arbitrary query strings.
- [x] Test successful decoders against real DTO shapes: exact dynamic number tokens become DecimalValue; all envelope revisions/counts/positions/schema versions reject unsafe integers. Literal strings remain literal; malformed or unsupported value/configuration objects fail closed. Test complete write JSON, null canonical title, full replacement data, revision/query values, cancellation and 204.
- [x] Run `pnpm --dir web test features/collections/api/contents.test.ts features/collections/api/decode-contents.test.ts server/api/collection-policy.test.ts server/api/proxy.test.ts`; verify new checks fail before implementation.
- [x] Implement exact codecs/DTO normalization on collection detail and field/item responses using Task 1 callbacks. Preserve metadata create/update output and existing consumers. Canonicalize all GUIDs before URLs/keys. Extend the proxy allowlist and query dispatch by method/path; keep its shared mutation guards intact and no-store on every outcome. Do not depend on X-Collection-Revision or add generic header forwarding.
- [x] Add proxy integration assertions for same-origin JSON/CSRF/attribution and body-size guards on new POST/PATCH/DELETE, including empty guarded DELETE bodies. Run focused tests plus `server/api/mutations.test.ts` and `server/api/request-body.test.ts`; expected all pass.

### Task 3: Stable read bundles and mutation/refresh coordination

**Files:** Create `F/interfaces/snapshots.ts`, `F/api/snapshots.ts`,
`F/api/snapshots.test.ts`, `F/hooks/use-collection-contents.ts`,
`F/hooks/use-contents-actions.ts`, `F/hooks/use-contents-actions.test.tsx`;
modify `F/query/keys.ts`, `F/hooks/use-collections.ts` and component interfaces.

**Interfaces:** `PageSnapshot {details: CollectionDetails, page: CollectionItemPage}`,
`EntrySnapshot {details: CollectionDetails, item: CollectionItem}`;
`loadPageSnapshot(id, page, signal): Promise<PageSnapshot>`,
`loadEntrySnapshot(id, itemId, signal): Promise<EntrySnapshot>`.
Both read details(R1) → item/page → details(R2), accept equal revisions, retry
the entire read sequence once on mismatch, then expose a retryable changing-data
error. Expose `useCollectionContents(viewer,id,page,active)` and
`useEntryContents(viewer,id,itemId,active)`. Locator queries are viewer/route scoped;
accepted bundles populate revision/schema-scoped item/page keys from the bundle's
actual R, never a stale outer detail R. Editors consume a single accepted bundle.
`useContentsActions(viewer,id,active)` provides synchronous duplicate exclusion,
`execute<T>(mutation: (signal: AbortSignal) => Promise<T>,
refresh: (signal: AbortSignal) => Promise<void>): Promise<ConfirmedWrite<T> | null>`,
`needsRefresh` and `retryRefresh()`. `ConfirmedWrite<T>` contains confirmed value
and `needsRefresh`; a failed post-write read does not become a failed mutation.

- [x] Test R1=R2 accepted, changing R retries once, repeated changes stop, item404 fails without showing stale contents and abort between each step prevents further reads. Test a field deleted/required/type-config changed between detail and item reads cannot initialize a mixed-schema editor.
- [x] Test viewer/GUID/revision/schema/page identity, initial session blocking, paused/cancelled reads, remembered-account draft restoration and logout/account replacement clearing. Test old request completion cannot overwrite a newer route/page/account.
- [x] Test mutation success followed by failed refresh: one write, Saved/Removed plus needsRefresh, further writes blocked, retryRefresh performs only reads. A known success cannot be relabeled unknown because refresh failed. Cancellation before confirmed outcome remains unknown; cancellation/account change after success cannot seed or navigate an unrelated view.
- [x] Run `pnpm --dir web test features/collections/api/snapshots.test.ts features/collections/hooks/use-contents-actions.test.tsx features/collections/components/collection-session.test.tsx`; observe new failures.
- [x] Implement the read bundles, keys and coordinator with retry=false on Query mutations/queries outside the explicit bounded bracket read. Reuse useCollectionWrite for mutation-only cancellation; perform refresh as its own read phase. Clear obsolete contents after success and block all local editors until refreshed. Keep active/identity checks across both phases. Preserve the existing session startup clearing policy.
- [x] Rerun focused tests and existing collection metadata/session tests; expected all pass. Fresh metadata/editor initialization must read accepted current detail rather than a stale independently cached field snapshot.

### Task 4: Field management forms and exact value controls

**Files:** Create `F/query/field-input.ts`, `F/query/field-input.test.ts`,
`F/query/entry-input.ts`, `F/query/entry-input.test.ts`,
`F/components/field-manager.tsx`, `field-editor.tsx`, `field-configuration.tsx`,
`entry-value-controls.tsx`, `field-editor.test.tsx`, `entry-value-controls.test.tsx`;
extend `F/interfaces/contents.ts`, component props and `collections.module.css`.

**Interfaces:** `FieldDraft` holds editable key/label/type/required/position and
type-specific configuration strings; `EntryValueDraft` distinguishes set/unset
from its string/boolean/decimal value. Produce validation/encoding functions
`fieldInput(draft: FieldDraft, existingFields: CollectionField[], editingFieldId?: string): InputResult<FieldDefinitionInput>` and
`entryData(drafts: Record<string, EntryValueDraft>, fields: CollectionField[]): InputResult<CollectionItem["data"]>`.
`InputResult<T>` is `{ok: true; value: T} | {ok: false; issues: FormIssues}`;
`FormIssues` has `fields: Record<string,string[]>` and `summary: string[]`.
Successful entryData
contains every set field, not only dirty keys. `FieldManager`, `FieldEditor`,
`FieldConfiguration` and `EntryValueControls` consume Task 2 DTOs, accepted detail
snapshots, active/locked state and coordinator callbacks. Keep props in interfaces.

- [x] Write failing tests for reserved/duplicate/invalid keys, immutable key/type on edit, trimmed label boundaries, 30-field cap, int positions, default optional text/maxLength1000, text maxLength1–4000 and exact number min<=max. Select options preserve case/intentional whitespace; enforce 1–50 distinct nonblank options <=80 units, including control/Unicode errors and labels with punctuation.
- [x] Test every native value control: zero and false set values, unset/null display, optional empty string versus unset, required cannot unset, required whitespace rejected, leap dates/calendar validity without timezone changes, exact option identity, unsupported/malformed saved data blocks replacement. Title-only entry replacement preserves a 28-digit fractional decimal exactly.
- [x] Test field create/edit full payloads and backend error mapping: `fields[index].label`, configuration errors, unknown/global errors remain visible in an accessible summary. Nonempty verified count disables adding Required; concurrent incompatible changes still surface generic409. Dirty drafts retain original revision through refetch; deliberate reload warns/replaces snapshot; a compatibility409 with unchanged revision allows editing a corrected proposal after explicit reload.
- [x] Run `pnpm --dir web test features/collections/query/field-input.test.ts features/collections/query/entry-input.test.ts features/collections/components/field-editor.test.tsx features/collections/components/entry-value-controls.test.tsx`; observe failures.
- [x] Implement focused native forms, typed configuration controls and field/value validators using exact-decimal helpers. Render read-only keys/types on edit; boolean/date config is {}; do not silently trim options or coerce values. Preserve unmatched server errors. Use Task 3 coordinator for writes and explicit reload. Keep field deletion controls delegated to Task 6.
- [x] Rerun focused tests; expected all pass. Verify no raw JSON textarea, new design tokens or animations appear.

### Task 5: Entry browsing, catalog/custom forms and local editor coordination

**Files:** Create `F/query/items-page.ts`, `items-page.test.ts`,
`F/components/collection-workspace.tsx`, `entry-list.tsx`, `entry-editor.tsx`,
`catalog-picker.tsx`, `entry-values.tsx`, `entry-detail-screen.tsx`,
`collection-workspace.test.tsx`, `entry-editor.test.tsx`, `entry-detail-screen.test.tsx`;
modify collection detail/metadata editor composition, shared feature props/styles;
create `web/app/collections/[id]/items/[itemId]/page.tsx`.

**Interfaces:** `itemsPage(params: URLSearchParams): number | null`,
`itemsHref(collectionId: string,page: number): string`. `CollectionWorkspace`
owns one `OwnerEditor` discriminated union: none, metadata, add-field, edit-field,
remove-field or add-entry; selecting another action cannot discard a current draft.
`EntryEditor` consumes an accepted snapshot, immutable kind/media reference on
edit, typed value drafts, position and coordinator. `CatalogPicker` calls existing
`getCatalog(CatalogFilters, signal)`, returns selected catalog ID/title and holds
explicit search/type/page locally. `EntryValues` renders all ordered field values.
`EntryDetailScreen({collectionId,itemId})` consumes stable entry snapshots.

- [x] Write failing DOM tests for real QueryClient list/paging states and strict itemsPage validation; Back/Forward and page changes preserve metadata/field/add-entry draft DOM. Only one local owner editor opens; nonowners never see management controls. List entries have genuine custom/catalog labels and canonical catalog links without invented artwork/type/counts.
- [x] Test explicit catalog search, 20-row picker paging, cancellation and stale results, selected-title retention and title:null payload. Custom title1–200 trimmed, position and full typed data replace correctly; kind/media identity stay immutable. Duplicate409 retains draft and offers reload/reselection without scanning a single page to assert uniqueness. A failed catalog POST404 rechecks parent access before offering reselection.
- [x] Test entry detail labels and literal multiline/HTML-like text, false/0/No value/exact decimals, field order by position/ID, unavailable privacy, field-read failure blocking edits and 413/429/backend error summaries retaining input. Anonymous login recovery uses existing canonical parent collection paths; do not broaden continuation allowlist.
- [x] Run `pnpm --dir web test features/collections/query/items-page.test.ts features/collections/components/collection-workspace.test.tsx features/collections/components/entry-editor.test.tsx features/collections/components/entry-detail-screen.test.tsx`; observe missing-feature failures.
- [x] Implement workspace/forms/list/detail and thin Suspense route with Promise params per installed Next guidance. Use revision-bracket snapshots for display and fresh editor initialization, not a bare list row. Keep editor snapshots outside page-query keys; reject invalid URL queries before entry fetches. Coordinate existing metadata editing with other owner editors and post-success refresh blocking. Replace future-interface copy with working entries/fields sections.
- [x] Rerun new tests plus existing `collection-detail-screen.test.tsx` and metadata creation/session tests; expected all pass. Include same-account checks and confirmed logout/account changes with pending entry writes.

### Task 6: Revision-bound field and entry deletion confirmations

**Files:** Create `F/components/field-removal.tsx`, `entry-removal.tsx`,
`field-removal.test.tsx`, `entry-removal.test.tsx`; wire workspace/entry detail,
extend component interfaces and existing styles.

**Interfaces:** `FieldRemoval` takes verified collection/field identity, active
viewer state and Task 3 coordinator. Its consent is bound to the exact
`DeletionImpact` object/revision; `EntryRemoval` takes a freshly bracketed entry
snapshot and its revision. Both expose explicit Cancel, no automatic confirmation.

- [x] Write failing tests for preview0 and preview>0: show label/key/populated count and permanent loss before Confirm; DELETE uses the preview revision and confirm=true even if another detail query refetches. A later observed revision invalidates Confirm. On409 discard preview, fetch only after explicit new preview, and require another click; never silently replace consent or replay.
- [x] Test session-checking/error and route/account changes invalidate consent, including same-account return, while nondestructive drafts elsewhere remain intact. Pending deletion cancellation/unknown outcomes require server-state checking and no automatic repeat. Preview401/404/429 and transport failures never enable Confirm.
- [x] Test entry confirmation names the entry, distinguishes its data from catalog/personal records, sends current snapshot revision, and on conflict requires a fresh snapshot plus new confirmation. Successful204 followed by refresh failure remains Removed/Refresh needed; later recovery returns to the collection and recovers an empty last page.
- [x] Run `pnpm --dir web test features/collections/components/field-removal.test.tsx features/collections/components/entry-removal.test.tsx`; observe failing assertions.
- [x] Implement inline native Confirm/Cancel flows using Task 2 API functions and Task 3 lifecycle. Invalidate consent synchronously when active/identity/revision/context changes so an already queued click cannot delete with renewed implicit consent. Handle deletion204 without response body/header assumptions.
- [x] Rerun focused tests plus workspace and entry detail tests; expected all pass. Exercise changed revision between preview and network submission and before/after session transitions.

### Task 7: Real integration, visual review and delivery evidence

**Files:** Update `docs/frontend-checklist.md`, `web/README.md`, `PRODUCT.md`,
the collection section of `DESIGN.md` and `.impeccable/surfaces/collections.md`.
Keep browser reports/captures under ignored `.impeccable/review/collection-contents/`;
extend or create a locally runnable verification script in `/tmp/` without secrets
in output. No source-level integration-test additions unless a real uncovered
backend behavior is found; existing collection integration tests cover backend rules.

**Interfaces:** Consume finished routes and real API/database; report actual
checks and preserve native execution. Completion does not authorize integration.

- [x] Run `pnpm --dir web test`, `pnpm --dir web lint`, `pnpm --dir web typecheck`, `pnpm --dir web build` and `git diff --check`. Expected zero failures/warnings requiring fixes. Keep Next typegen/build sequential. Use approved escalation for required subprocesses rather than weakening checks.
- [x] Start/reuse PostgreSQL with `docker compose --env-file .env -f deploy/compose.yaml up -d --wait`, API with `dotnet run --project src/RateFreak.Api --launch-profile http`, frontend3100 using its existing ignored proxy settings. Check API readiness and real proxy200. Webpack development preview is an available fallback for the prior observed Turbopack reload loop; record which is used. Do not print/rewrite secrets or migrate existing data automatically.
- [x] Run `dotnet test tests/RateFreak.IntegrationTests --no-restore --filter FullyQualifiedName~CollectionsTests --verbosity quiet`. Expected all current collection tests pass, no failures/skips, using disposable real PostgreSQL. Record actual count rather than assuming the prior58.
- [x] Run real browser checks without interception: dedicated account/collection; create all five field types; custom/canonical entries from existing catalog; exact high-precision number persistence after title-only edit/reload; false/0/empty/unset; duplicate409; stale revision409; incompatible schema409; optional field add on populated collection; required-add prevention; populated/zero deletion impact; actual stale preview followed by new confirmation; field deletion removes only its key; entry removal preserves catalog/personal records; public/private/nonowner access; logout. Read assertions from real responses and rendered UI, not seeded browser mocks. Bound waits to actual session/read completion when switching browser contexts.
- [x] If existing catalog has no suitable title, verify canonical flows only against a disposable test database with temporary catalog data and explicitly identify that boundary. Never import/modify existing shared catalog or personal records for test convenience. Do not claim fixture tests establish live integration.
- [x] Inspect both themes at320/768/1024/1440, keyboard labels/focus/skip navigation, paging/history, wrapping names/options/30 fields and destructive/error states. Deterministic fault fixtures may supplement real checks; label them. No overflow/unexpected JavaScript exceptions. Run the mechanical design detector once after UI is finished and resolve relevant findings.
- [x] Obtain fresh focused whole-change code/UI review; fix material findings with reproducing RED→GREEN tests and rerun justified checks. Record decisions/deferred minors honestly; preserve current visual tokens and unrelated work. Update actual checklist/surface/API capabilities after review.
- [x] Clean up only exact verification-owned accounts/collections using guarded IDs/counts; preserve real data. Stop temporary Chrome/test services, leave requested API/frontend available and record final readiness/runtime state. Report real versus synthetic evidence and that changes remain uncommitted. Mark this follow-up complete only after required live and automated evidence passes.

## Self-review and handoff

Spec coverage: transport/precision → Tasks1–2; revision/session/post-success reads →
Task3; field configuration/typed values/errors → Task4; list/routes/catalog/custom
entries/editor exclusion → Task5; both destructive flows → Task6; required real
backend/browser checks, review and documents → Task7. Each Review Focus condition
has a named failing test in its owning task. Task1 token contracts feed Task2 DTOs;
Tasks4–6 consume Task3 accepted snapshots and mutation/read phases consistently.
No task consumes a response revision header that the current proxy drops.

Plan approved and executed natively. No dependency installations, backend changes,
migrations, commits, merges, pushes or deployment. Actual verification and guarded
cleanup evidence are recorded in the frontend checklist.
