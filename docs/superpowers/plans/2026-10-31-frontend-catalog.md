# Frontend Catalog Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans for native execution,
> preserving the previously selected execution method. Steps use checkbox syntax.

**Goal:** Browse, search and paginate the local catalog and open media details.

**Architecture:** Extend the restricted proxy and existing API client. Feature
APIs and TanStack Query hooks own server data; thin routes compose focused screens.
URL parameters own submitted filters and pagination.

**Tech Stack:** Existing Next.js App Router, React, TypeScript, CSS Modules,
TanStack Query, Vitest and Testing Library; no new dependencies.

**Spec:** `docs/superpowers/specs/2026-10-31-frontend-catalog-design.md`.

Approved by the user on 2026-10-31. They then requested the homepage redesign
first; catalog implementation resumed after the approved design refinements.
Implemented 2026-10-31; verification and integration limits are in the frontend checklist. Use the selected editorial
visual direction in `DESIGN.md` when continuing UI work.

## Global constraints

- Keep object contracts and component props in separate `interfaces/` files.
- Read the relevant installed Next.js guides before editing routes.
- Keep the fixed upstream origin, route allowlist, no-store, timeout,
  cancellation, attribution and cookie protections.
- Provider import/external sign-in/library work remains separate.
- Work on a new branch from main; preserve the existing checkout preference.
- No automatic commit, push or deployment.

## Review focus

- Duplicate/unknown query parameters must not widen the proxy boundary.
- Back/Forward must restore form values and matching results.
- A late search response must not replace the current filter's results.
- Empty and out-of-range pages must retain a route back to useful results.
- Long titles, missing artwork and failed images must remain readable at 320px.

### Task 1: restricted catalog transport

**Files:** Modify `web/server/api/route-policy.ts`, `proxy.ts`, `proxy.test.ts`;
create `web/server/api/catalog-query.ts` and `catalog-query.test.ts`.

**Interfaces:** Preserve `isAllowedRoute(method, path): boolean`; add
`validateCatalogQuery(params: URLSearchParams): URLSearchParams` for validated,
canonical forwarding. Invalid input produces a controlled 400 response.

- [x] Create the new branch and run the frontend baseline tests.
- [x] Add failing tests: GET media and GUID detail work; POST media, provider
  paths and malformed IDs remain closed; only list queries are accepted.
- [x] Add failing tests: allowed query forwarding, trimming and encoding,
  duplicate/unknown rejection, 200-character limit, null rejection, all four
  media types, numeric bounds and unchanged auth query rejection.
- [x] Run focused tests and confirm failures express missing catalog behavior.
- [x] Implement the policy/query validation and validated upstream URL.
- [x] Run focused proxy/query tests and existing transport tests.

### Task 2: catalog data and URL state

**Files:** Create `web/features/catalog/interfaces/{catalog-filters,media-summary,
catalog-page,media-details}.ts`, `api/catalog.ts`, `api/catalog.test.ts`,
`query/keys.ts`, `query/filters.ts`, `query/filters.test.ts`,
`hooks/use-catalog.ts`, `hooks/use-media.ts`.

**Interfaces:** `CatalogFilters` contains q, type, page and pageSize;
`getCatalog(filters, signal?)` returns `Promise<CatalogPage>`;
`getMedia(id, signal?)` returns `Promise<MediaDetails>`.
Contract fields match `src/RateFreak.Application/Catalog/Catalog.cs`.

- [x] Add failing tests for parsing absent/invalid/duplicate URL filters,
  canonical query serialization and resetting pagination for new filters.
- [x] Add failing API tests proving encoded search/type/page/pageSize,
  GUID detail paths and AbortSignal propagation through the real client.
- [x] Implement filters, separate contracts, APIs, complete query keys and hooks.
- [x] Verify API/filter tests; use screen tests in Task 3 to prove query behavior.

### Task 3: screens and navigation

**Files:** Create `web/app/catalog/page.tsx`, `web/app/catalog/[id]/page.tsx`;
feature-owned components `catalog-screen`, `catalog-filters`, `catalog-results`,
`media-artwork`, `media-detail-screen`, their CSS Modules and separate prop
interfaces; create `web/features/catalog/components/screens.test.tsx`.
Modify existing auth navigation, homepage and relevant shell tests.

**Interfaces:** CatalogScreen consumes URL state and useCatalog; filter form
submits normalized URL parameters; CatalogResults consumes CatalogPage and
filters; MediaDetailScreen consumes the route ID and useMedia.

- [x] Inspect imageReference formats in provider adapters and read installed
  Next.js route/search-parameter documentation.
- [x] Add failing DOM tests with a real QueryClient and mocked network boundary:
  loading/results/title links, submitted filters reset page, pagination preserves
  filters, Back/Forward updates inputs, and a late response stays isolated.
- [x] Add failing DOM tests for empty/no-match/out-of-range recovery, retryable
  errors, 429 guidance, invalid URL filters, detail success/404/error and missing
  artwork/date/description. Include a long title and failed image.
- [x] Implement responsive screens with the existing design tokens and shared UI.
  Use native labeled controls and semantic list/link/navigation elements.
- [x] Add Catalog links with active state on list/detail routes and homepage.
- [x] Verify screen tests and unchanged account/session/navigation tests.

### Task 4: verification and handoff

**Files:** Update `web/README.md` and `docs/frontend-checklist.md`.

- [x] Run `pnpm --dir web test`, `lint`, `typecheck`, and `build`; inspect output.
- [x] Inspect browser at 320/768/1024/1440px: no overflow, usable filters,
  artwork fallbacks, wrapping titles, working pagination and detail navigation.
- [x] Verify keyboard labels/focus/skip navigation and browser Back/Forward;
  inspect console for runtime errors.
- [x] Exercise Next proxy to API with disposable catalog data if available;
  distinguish live and mocked evidence in the handoff.
- [x] Review the diff against the spec and resolve material findings.
- [x] Record exact check results, process state and next slice in the checklist.
