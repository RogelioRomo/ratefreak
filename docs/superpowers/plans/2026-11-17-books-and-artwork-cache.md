# Books and Shared Provider Caching Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [x]`) syntax for tracking.

**Goal:** Import Open Library books and cache public images and external metadata across all providers.

**Architecture:** Decorate registry-resolved provider adapters with a shared, bounded process cache. Add Open Library through the existing adapter/import model and an additive book/category migration. Use Next.js image optimization with persistent disk storage for public remote artwork while protected uploads retain authorization on each read.

**Tech Stack:** .NET 10, EF Core/PostgreSQL, Next.js 16.4.6 (patched during verification), React 19, TanStack Query 5, xUnit/Vitest, Python operator tools.

**Spec:** `docs/superpowers/specs/2026-11-17-books-and-artwork-cache-design.md` (approved; all-provider metadata scope clarified by owner).

## Global Constraints

- Work identities are `openlibrary` / `work` / `OL{positive digits}W`; type is `book`.
- Search cache TTL five minutes; detail TTL one hour; public image minimum TTL 24 hours, maximum disk 1 GiB.
- No new packages, shared credentials, bulk harvesting, public cache of uploads, persistent DB migration, deployment or publication.
- Tests use disposable storage/databases. Retain current UI identity and account/session boundaries.
- Implement in the current managed writable checkout; do not alter read-only Git metadata or commit/push. Track task evidence in `/tmp/ratefreak-books-progress.md`.

## Review Focus

- A cancelling preview must not cancel another viewer's identical upstream request; no-waiter work must stop.
- Disabled providers and invalid identities cannot bypass checks through cached results.
- Existing user categories named Books must not be silently repurposed or lost by migration.
- Direct optimizer requests for private local artwork must be refused even outside the common component.
- Cold/expired caches and web container recreation must have documented, verified behavior rather than an offline-availability promise.

---

### Task 1: Shared provider metadata cache

**Files:** Create `src/RateFreak.Infrastructure/MetadataImport/ProviderMetadataCache.cs`, `CachedMediaProvider.cs`, `CachedMediaProviderRegistry.cs`; modify `DependencyInjection.cs`; test `tests/RateFreak.ProviderContractTests/MetadataCacheTests.cs`.

**Interfaces:** Consumes existing `IMediaProvider`, `IMediaProviderRegistry`; produces a singleton cache and scoped registry preserving last-registration-wins. `CachedMediaProvider(IMediaProvider inner, ProviderMetadataCache cache)` exposes the unchanged `IMediaProvider` interface.

- [x] Write `MetadataCacheTests`: for each provider, repeated identical searches/details call the underlying provider once; changed type/provider/page/query/identity never collides; advance `ManualClock` past five minutes/one hour to force refetch; disabled/invalid calls never hit retained successes. Assert concurrent callers coalesce, one cancellation is independent, last cancellation stops upstream, failures/nulls are retried, and bounds reject excess in-flight work without leaking slots.
- [x] Run `dotnet test tests/RateFreak.ProviderContractTests --filter FullyQualifiedName~MetadataCacheTests`; expect missing cache implementation failures.
- [x] Implement a 16 MiB byte-accounted successful-result cache with maximum 64 concurrent distinct requests and shared-operation deadline 20 seconds. Cache immutable normalized results, remove completed flights, dispose cancellation sources, and preserve caller cancellation/failure taxonomy. Do not add storage to the application layer.
- [x] Run the focused tests and the whole provider suite; expect zero failures.

### Task 2: Open Library adapter and book schema

**Files:** Create `src/RateFreak.Infrastructure/Providers/OpenLibrary/{OpenLibraryOptions,OpenLibraryRequestGate,OpenLibraryMediaProvider,OpenLibraryMapping}.cs`; modify `src/RateFreak.Domain/Catalog/{MediaItem,Category}.cs`, `src/RateFreak.Application/MetadataImport/MediaProviders.cs`, `src/RateFreak.Api/Endpoints/MediaProviderEndpoints.cs`, `src/RateFreak.Infrastructure/{DependencyInjection.cs,Persistence/Configurations/CatalogConfiguration.cs}`; generate `AddBooks` migration/designer/snapshot; test `tests/RateFreak.ProviderContractTests/OpenLibraryTests.cs`, `OpenLibraryTransportTests.cs`, `tests/RateFreak.IntegrationTests/BookImportTests.cs`, `BookMigrationTests.cs`, and update `ProviderEndpointTests.cs`.

**Interfaces:** Produces `OpenLibraryMediaProvider : IMediaProvider`, capability `openlibrary` supports `book`; options `Enabled`, `Contact`, `TimeoutMilliseconds`; gate serializes and paces all outbound metadata/author requests at one/second. Task 1 decorates it automatically.

- [x] Write provider tests for work identity, canonical image URLs, search pagination/literal input, bounded text/year/authors, absent optional data, malformed detail identity, disabled contact validation, cancellation, transport bounds, throttling and pacing. Write integration tests for a book import, concurrent duplicate handling, local catalog reads without provider access, and safe migration/category collision/downgrade refusal.
- [x] Run new filtered provider/integration tests; expect missing book support failures.
- [x] Implement adapter with fixed HTTPS origins, 20-result explicit-field search, exact work detail lookup, bounded optional author enrichment/cache, plain descriptions, positive CoverID references and honest year precision. Use email-only contact, 10-second operation timeout, 1 MiB JSON limit/depth 32, no redirects, and finite retries under pacing. Add `book` to supported domain/input/API paths and use category UUID suffix `0005`.
- [x] Generate/review migration: widen type check, seed Books with stable ID, explicitly refuse conflicting public Books names/occupied seed IDs with actionable error and no data mutation. Down migration refuses dependent books/category memberships/aliases. Apply only to disposable databases.
- [x] Run focused contracts and integration tests, including EF pending-model check; expect no failures/skips.

### Task 3: Books in existing frontend workflows

**Files:** Modify `web/features/catalog/{interfaces/catalog-filters.ts,query/filters.ts,components/media-artwork.tsx}`, `web/features/providers/{interfaces/provider.ts,query/identity.ts,query/filters.ts}`, `web/server/api/catalog-query.ts`, relevant provider route policies, `web/features/credits/components/credits-screen.tsx`; add/update provider/filter/catalog/credits tests.

**Interfaces:** Consumes Task 2 `book`/`openlibrary` capability/identity and current React Query hooks. Existing imported catalog metadata/external IDs remain API-compatible.

- [x] Add tests for Open Library discovery selection, work preview/import URLs, ISBN/edition rejection, Book filter/label/icon and Credits/source links. Assert invalid identities are rejected by browser proxy before upstream access.
- [x] Run targeted Vitest files; expect missing Book/Open Library behavior failures.
- [x] Extend exhaustive type/provider maps, canonical identity validation and capability ordering; reuse the existing discovery/preview/import and shared Query cache. Render bounded author names and publication year in book details if not already exposed by the generic metadata UI.
- [x] Run frontend tests/typecheck/lint; expect no failures.

### Task 4: Public image cache and persistent self-hosted storage

**Files:** Modify `web/next.config.ts`, `web/features/catalog/components/media-artwork.{tsx,module.css}`, create `web/shared/artwork/provider-images.ts` for a shared policy if needed; add `media-artwork.test.tsx`, `web/server/provider-image-policy.test.ts`; modify `deploy/self-host/{compose.yaml,entrypoint.py,test_operator.py}`.

**Interfaces:** Provider URLs remain canonical remote references; public rendering uses `next/image`, protected upload routes remain `<img>`/`no-store`. Cache storage is `${RATEFREAK_INSTANCE_DIR}/storage/image-cache` mounted at `/app/.next/cache/images` for UID/GID 10001.

- [x] Write tests proving approved TMDB/IGDB/Open Library source formats and rejection of arbitrary/private/local/query/redirect variants, cached `<Image>` output, retained protected `<img>` output, fallback behavior, and idempotent protected storage preparation.
- [x] Run new frontend/operator tests; expect caching/storage behavior failures.
- [x] Configure narrow patterns, deny local sources except the validated Open Library CoverID adapter (required by observed Archive redirects), no arbitrary redirects/private IP/SVG, response cap 5 MiB, 86,400-second minimum TTL, 1,073,741,824-byte disk cap, finite widths/quality and accurate responsive sizes. Persist only the image-cache directory while retaining the rest of the tmpfs/read-only setup. Exclude cache from recovery backups.
- [x] Run focused and whole frontend/operator suites; production build must accept installed-version image options.
- [x] Use a production runtime with controlled upstream responses to prove MISS -> HIT, simultaneous requests, outage/expiry behavior and direct local/private optimizer rejection. Exercise cache persistence through container recreation using disposable storage. Live Open Library cover smoke checks verify upstream response/redirect assumptions; document first-load limits.

### Task 5: Documentation, verification and independent review

**Files:** Update `README.md`, `web/README.md`, `docs/{STATUS.md,self-hosting.md,provider-terms-review.md,provider-agreement-evidence.md}` and create `docs/books-and-provider-cache-verification.md`; record task evidence in ledger.

**Interfaces:** Documents delivered behavior/configuration and limits of Tasks 1–4, including unresolved external cover rights and no persistent database application.

- [x] Document Open Library contact/configuration, works versus editions, metadata/cache TTLs and byte/concurrency bounds, cache restart/purge/backup behavior, local uploads and direct-origin guideline distinction.
- [x] Run `dotnet test RateFreak.sln --no-restore -m:1`, `pnpm test`, `pnpm lint`, `pnpm typecheck`, `pnpm build`, `dotnet publish src/RateFreak.Api -c Release --no-restore`, and Python operator/compliance suites. Expect zero failures/skips; investigate concrete failures before further broad reruns.
- [x] Verify books/search/import and image/fallback behavior using disposable API/DB and production Next.js in desktop/mobile and both themes; inspect screenshots once, correct actual defects together, confirm once.
- [x] Request one fresh whole-change reviewer using this spec/plan and ledger; fix important findings with reproducing tests, rerun affected proof, and report exact remaining operational limits.
- [x] Update STATUS/evidence with actual results; run `git diff --check` and inspect the final changes. Leave reviewable uncommitted changes in the shared workspace; no push/deployment/live migration.
