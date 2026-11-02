# Collection metadata and visibility Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [x]`) syntax for tracking. Native execution is preserved from the approved design; do not switch to task subagents.

Status: implemented and verified 2026-11-01. Actual evidence and live-integration limits are in `docs/frontend-checklist.md`. Native execution; uncommitted on `feat/collection-metadata`.

**Goal:** Deliver accessible collection browsing and owner-only creation/editing of metadata and visibility.

**Architecture:** Add a feature-owned collections module with viewer-scoped TanStack Query reads and guarded, cancellable writes. Thin App Router pages compose focused screens using the incumbent editorial styles. Extend only exact collection proxy routes and canonical login continuations; use existing backend contracts unchanged.

**Tech Stack:** Installed Next.js 16.4.3, React 19.3, TypeScript, CSS Modules, TanStack Query 5, Vitest/Testing Library, Node 24 and pnpm 10.

**Spec:** `docs/superpowers/specs/2026-11-01-frontend-collections-design.md` (approved 2026-11-01).

## Global Constraints

- No backend changes, migrations or dependencies are expected.
- Object contracts and props belong in separate interfaces files.
- No automatic commit, push or deploy.
- Fields, entries and schema-change confirmation remain the next slice.
- Every collection read is viewer-sensitive, including the default list and detail.
- Page size is 20; page bounds are 1–10000.
- Name: trimmed 1–120 UTF-16 units; optional description: at most 2000 UTF-16 units; visibility: private/public, default private.
- Keep existing exact Origin, CSRF, cookie, attribution, cancellation and no-store protections; no mutation retry or optimistic success.
- Preserve Fraunces/DM Sans, dark blue/light oxblood themes, native labeled controls and focused components.
- No real-account writes for verification; preserve running local services and ignored secrets.

## Review Focus

- An authenticated All accessible list includes private collections: never label it public-only (Task 3).
- Background detail refresh while typing must not replace a dirty draft or its expected revision (Task 5).
- A create may succeed upstream before cancellation: show an uncertain outcome without replay (Task 4).
- A public-to-private save must remove obsolete local detail snapshots, including anonymous variants (Tasks 4–5).
- Session checks and identity changes during in-flight operations must conceal data and suppress old outcomes (Tasks 3–5).

## File structure and existing references

Create `web/features/collections/` with `interfaces/collections.ts` for wire contracts,
`interfaces/component-props.ts` for UI contracts, `query/filters.ts` for URL state,
`query/keys.ts` for cache identity, `api/collections.ts` for transport,
`hooks/use-collections.ts` for reads and `hooks/use-collection-write.ts` for writes.
Components: `collection-session.tsx` for the retained identity boundary,
`collections-screen.tsx` for list/scope/paging, `collection-list.tsx` for rows,
`create-collection-screen.tsx`, `collection-detail-screen.tsx`,
`collection-editor.tsx`, `metadata-fields.tsx`, and `collections.module.css`.
Do not introduce generic form/state frameworks.

Routes: create `web/app/collections/page.tsx`, `new/page.tsx`, `[id]/page.tsx`.
Policy: create `web/server/api/collection-policy.ts`; modify `route-policy.ts`
and `proxy.ts`. Modify auth `query/return-to.ts` and `components/auth-navigation.tsx`.

Use existing `personal/components/personal-controls.tsx` as the retained-draft
reference, `personal/hooks/use-personal-write.ts` as a write lifecycle reference,
and `auth/session/transition.ts` for cache clearing. Do not put collections into
its public-root startup allowlist. Existing write helper suppresses aborted errors:
creation needs its own explicit uncertain-outcome state, not blind reuse.

All tests named below live beside their source, with `@vitest-environment jsdom`
on DOM tests. Existing proxy tests provide configuration and fetch mocks.

### Task 1: Contracts, URL state and restricted transport

**Files:** Create `interfaces/collections.ts`, `query/filters.ts`,
`api/collections.ts` and adjacent `filters.test.ts`, `collections.test.ts` under
the feature root. Create `web/server/api/collection-policy.ts` and
`collection-policy.test.ts`; modify `route-policy.ts`, `proxy.ts`, `proxy.test.ts`.

**Interfaces:** Produce `CollectionAuthor`, `CollectionSummary`, `CollectionField`,
`CollectionDetails`, `CollectionPage`, `CollectionCreate`, `CollectionUpdate` matching
the C# records (camelCase JSON); `CollectionFilters { scope: "all" | "mine";
page: number; pageSize: 20 }`. `collectionFilters(URLSearchParams): CollectionFilters | null`,
`collectionHref(CollectionFilters): string`; `getCollections(filters, signal?): Promise<CollectionPage>`,
`getCollection(id, signal?): Promise<CollectionDetails>`,
`createCollection(input, signal): Promise<CollectionDetails>`,
`updateCollection(id, input, signal): Promise<CollectionDetails>`.
`collectionRoute(method: string, path: readonly string[]): boolean` and
`validateCollectionQuery(params: URLSearchParams): URLSearchParams` are server-only.

- [x] Write failing tests: UI accepts omitted/all scope and mine, rejects duplicate/unknown/invalid scope/page; canonical href omits defaults; API maps mine to ownOnly=true, sends pageSize=20, propagates signals, POST fields=[] and PATCH expectedRevision/all metadata. Policy accepts only GET/POST root and GET/PATCH GUID; rejects items/fields/DELETE/malformed IDs; query accepts exact true/false and page/pageSize bounds 1–10000/1–100, rejects duplicate/unknown/invalid values.
- [x] Run `pnpm --dir web test features/collections/query/filters.test.ts features/collections/api/collections.test.ts server/api/collection-policy.test.ts` and observe the intended missing-module/assertion failures.
- [x] Implement the named contracts/functions using `apiRequest<T>` and existing GUID validation. Preserve all field response properties without displaying configuration. Wire list query validation and route policy into the proxy; POST root with query remains rejected.
- [x] Add proxy-level tests proving validated forwarding, no-store, disallowed queries/methods, and unchanged Origin/JSON/CSRF/attribution/body protections on collection POST/PATCH.
- [x] Run the focused tests plus `server/api/proxy.test.ts`, `server/api/mutations.test.ts` and `server/api/request-body.test.ts`; expect all pass. Review diff for accidental wildcard forwarding.

### Task 2: Viewer-scoped query and retained-session boundary

**Files:** Create `query/keys.ts`, `hooks/use-collections.ts`,
`components/collection-session.tsx`, `interfaces/component-props.ts`,
`components/collection-session.test.tsx`; extend `auth/session/transition.test.ts`.

**Interfaces:** `CollectionViewer = "anonymous" | string` (only verified account
IDs or the literal anonymous marker). `collectionKeys.viewer(viewer)`,
`.list(viewer, filters)`, `.detail(viewer, id)` all begin with `["collections", viewer]`.
`useCollections(viewer, filters: CollectionFilters | null, active: boolean)` and
`useCollection(viewer, id: string, active: boolean)` wrap useQuery with signal forwarding
and retry=false. `CollectionSession({children, requireAccount?, returnTo})` calls
its render child with `{viewer, accountId: string | null, active: boolean}` inside
a hidden/inert/disabled retained subtree while rechecking; initial unverified state
does not mount data readers. Props go in the interface file.

- [x] Write real QueryClient tests: no fetch before initial verification; verified anonymous can read accessible lists/details; mine/private pages offer login; network session failure offers Retry session, not anonymous content. Same-account background check conceals/inerts mounted draft, disables reads and restores the same DOM/input after verification. A confirmed different account/logout discards that subtree and cached collection data.
- [x] Add transition regression assertions that initial discovery removes collections while preserving existing catalog/community/provider roots; confirmed identity changes still clear everything.
- [x] Run `pnpm --dir web test features/collections/components/collection-session.test.tsx features/auth/session/transition.test.ts`; observe new tests fail.
- [x] Implement keys/hooks/boundary using existing useSession, explicit protected-entry refresh and remembered verified identity. Keep initial/check/error distinct; never use a fallback anonymous key during uncertainty. Stop/cancel active reads when concealed; use AbortSignal and ignore stale reader results. Reuse existing identity reset policy without relaxing it.
- [x] Rerun those tests until green; include anonymous→account and account→anonymous deferred-response assertions proving old results never appear.

### Task 3: Collection browsing, routes and login/navigation

**Files:** Create `components/collections-screen.tsx`, `collection-list.tsx`,
`collections.module.css`, `collections-screen.test.tsx`, `web/app/collections/page.tsx`.
Modify `auth/query/return-to.ts`, `return-to.test.ts`,
`auth/components/auth-navigation.tsx`; extend its existing screen/navigation test coverage.

**Interfaces:** `CollectionsScreen(): React.JSX.Element` composes Task 2's boundary
and Task 1's filter/href functions. `CollectionList({items: CollectionSummary[]}): React.JSX.Element`.
Add exact canonical collection paths to existing `safePreviewReturnTo` without
renaming it or broadening existing provider/catalog/library continuations.

- [x] Write failing DOM tests for explicit Apply scope (page reset), 20-item paging links, Back/Forward URL restoration, invalid filters with no request, loading/429/retry, empty and out-of-range recovery, wrapping/literal text, anonymous mine login, and authenticated all-accessible copy including private visibility. Test collection navigation active on root/new/GUID and strict returnTo acceptance/rejection including uppercase, encoding, query/fragment, external paths and duplicate parameters at the login parser.
- [x] Run `pnpm --dir web test features/collections/components/collections-screen.test.tsx features/auth/query/return-to.test.ts features/auth/components/screens.test.tsx`; confirm failing assertions.
- [x] Implement thin Suspense-wrapped list route following the installed Next guides and existing library pattern. Add Collections link for all visitors; show New collection entry action with protected login behavior. Keep owner handles plain text and do not invent totals beyond actual collection totalCount.
- [x] Implement flat responsive list/styles with existing tokens; explicitly describe All accessible semantics. Add no field/item UI, fake counts or dead links. Use Task 2's boundary so viewer-sensitive list content is concealed while checking.
- [x] Rerun focused tests; expect green. Verify unrelated auth navigation/continuations remain green.

### Task 4: Private-by-default creation and guarded write lifecycle

**Files:** Create `hooks/use-collection-write.ts`, `components/metadata-fields.tsx`,
`create-collection-screen.tsx`, `create-collection-screen.test.tsx`,
`web/app/collections/new/page.tsx`; extend feature interfaces/styles.

**Interfaces:** `CollectionMetadata { name: string; description: string; visibility:
"private" | "public" }`; `MetadataFields({value, onChange, errors, disabled})`
renders labeled controlled inputs. `useCollectionWrite()` produces `run<T>(operation:
(signal: AbortSignal) => Promise<T>): Promise<{value:T} | null>`, pending/error,
clearError and `outcomeUnknown`; account-bound operations abort on session loss,
route unmount and identity change. `CreateCollectionScreen(): React.JSX.Element`.

- [x] Write failing DOM tests: anonymous login continuation, private default, trimmed name, description null when empty, boundaries 120/2000, backend field errors, duplicate-submit suppression, explicit public exposure explanation, POST fields=[], 201 navigation to returned GUID, no automatic replay. Deferred create during session check must abort, conceal draft and show uncertain-outcome/My collections recovery after the same identity returns; late success after account/route change must not navigate or update caches.
- [x] Run `pnpm --dir web test features/collections/components/create-collection-screen.test.tsx`; confirm failure.
- [x] Implement native fields and account-only route using Task 2. Keep submitted metadata out of storage. Use refs/controller identity to suppress late results and synchronously guard repeated submissions. Preserve drafts on errors and during same-account checks. Mark transport/cancellation outcomes honestly when success cannot be ruled out; no retries.
- [x] On successful create invalidate collection lists and seed returned detail for the verified viewer, then navigate only if still on the same operation/account/route. Guard invalidation completion as well as response completion.
- [x] Rerun tests, including 401 refresh, 429 preservation and uncertain network failure; expect green.

### Task 5: Accessible details and revision-safe owner editing

**Files:** Create `components/collection-detail-screen.tsx`, `collection-editor.tsx`,
`collection-detail-screen.test.tsx`, `web/app/collections/[id]/page.tsx`;
extend component interfaces/styles and write-hook tests as needed.

**Interfaces:** `CollectionDetailScreen({id: string}): React.JSX.Element`;
`CollectionEditor({saved: CollectionDetails, accountId: string, active: boolean,
onCancel: () => void, onSaved: (details: CollectionDetails) => void})`.
`CollectionPageProps { params: Promise<{id: string}> }` lives in interfaces.

- [x] Write failing tests: public metadata and owner label; unavailable private/missing unified 404; nonowner cannot edit; owner opens fully populated editor; literal multiline descriptions and no invented empty-entry state. Dirty revision=1 draft survives background revision=2 refetch and PATCH still sends expectedRevision=1 with all three metadata fields. 409 keeps draft and blocks save; explicit Reload latest warns of replacement, loads latest snapshot/revision, then permits save. Failed reload preserves the old draft and conflict block.
- [x] Run `pnpm --dir web test features/collections/components/collection-detail-screen.test.tsx`; observe failures.
- [x] Implement async-param detail route within Suspense using installed Next guidance. Validate GUID before request. Render entry/field follow-up note. Edit only after verified owner/detail load. Hold editor snapshot/revision separately from query data; no silent rebasing. Cancel editing discards draft without mutation. On 404 conceal editor; 401 refreshes session; failures preserve inputs.
- [x] Implement PATCH using Task 4 lifecycle. Success removes obsolete cached detail variants for this collection (including anonymous), seeds current viewer's returned details and invalidates collection lists; avoid unmounting the editor while replacing its own cache. Do not invalidate unrelated catalog/personal data. Add cache test proving a public→private save cannot resurrect anonymous snapshots. A later external visibility change is discovered by normal refetch; no claim of server push synchronization.
- [x] Extend deferred-operation tests: session check restores same-account draft; account change clears it; route change/cancelled response does not show saved feedback on another collection. Verify metadata save leaves returned fields intact and sends no field/item properties.
- [x] Rerun detail/create/session tests together; expect green.

### Task 6: Whole-slice verification, review and delivery notes

**Files:** Update `docs/frontend-checklist.md`, `web/README.md`, `DESIGN.md`;
create `.impeccable/surfaces/collections.md` using existing surface documentation
patterns. Browser fixtures/captures remain ignored, not shipped.

**Interfaces:** No new product interfaces. Consume the completed routes and
document actual behavior/results; mark Phase 5 metadata/visibility complete only
after its required checks pass, leaving fields/items follow-up unchecked.

- [x] Run `pnpm --dir web test`, `pnpm --dir web lint`, `pnpm --dir web typecheck`,
  `pnpm --dir web build` and `git diff --check`; expect zero failures. Respect
  sandbox escalation when subprocesses require it. Run checks sequentially when
  Next-generated files/build state could collide. No backend suite if unchanged.
- [x] Use a temporary production preview and intercepted synthetic API responses
  for both themes at 320/768/1024/1440. Inspect list/create/detail/edit, long names,
  multiline/HTML-like descriptions, public/private labels, narrow header wrapping,
  invalid/empty/error/409/uncertain-create states, loading and session concealment.
  Verify keyboard focus/skip navigation, labels, history/filter restoration, login
  return, duplicate submission blocking and no overflow/runtime exceptions.
- [x] Perform live read-only list/detail checks if the existing API is available;
  report availability and fixture/live limits. No real collection/account writes.
  Stop only temporary verification processes; retain pre-existing services.
- [x] Obtain a fresh focused code/UI review per requesting-code-review skill;
  resolve material findings with failing regressions before fixes, then rerun
  only checks justified by the changes. No automatic commit/merge/push/deploy.
- [x] Record actual tests, visual evidence, remaining findings, local process
  state and next slice in the checklist; update current visual/API documentation.
  Final report links changed files and accurately distinguishes live and fixtures.

## Self-review

Spec sections map to Tasks 1–5 for behavior and Task 6 for release evidence.
Review Focus conditions have explicit tests in their owning tasks. API names,
viewer identity and revision snapshot ownership are consistent across tasks.
Execution setup must preserve the approved planning files and existing checkout
state; use the worktree skill before implementation to determine isolation.
No commits are part of this plan without a separate user request.
