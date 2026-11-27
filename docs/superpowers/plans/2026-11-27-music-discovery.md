# Music discovery implementation plan

> **For agentic workers:** Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Ship album covers, full song support, and personal Recently rated home cards.
**Architecture:** Extend the existing provider/catalog pipeline with recording identities; add an owner-scoped recent ratings query and use the shared artwork renderer.
**Tech Stack:** .NET 10, EF Core/PostgreSQL, Next.js, TypeScript, TanStack Query.
**Spec:** docs/superpowers/specs/2026-11-27-music-discovery-design.md

## Global constraints

No new dependencies, no deployment/live migration, preserve existing owner edits.
Use direct canonical CAA URLs without persistent image caching; separate TS interfaces.

## Review focus

- Song identity must remain distinct from release-group identity for identical UUIDs.
- Cover misses and malformed references must retain artwork fallback.
- Private ratings must never appear for another account or signed-out visitors.
- Recently rated must include rated items without library rows and honor visibility.
- Rollback must refuse songs and category usage/changes without data loss.

### Task 1: Music provider and catalog

Files: MusicBrainzMapping.cs, MusicBrainzMediaProvider.cs, MediaProviderContracts.cs,
MediaItem.cs, Category.cs, CatalogConfiguration.cs and EF migration/snapshot;
web provider identities, media labels/types and proxy filters.
Produces: `song` catalog type, `recording` namespace, album cover references.
- [x] Add failing provider tests for recording details/search, identity isolation, invalid duration/response and album covers; run ProviderContractTests.
- [x] Implement bounded recording mapping and search/details; preserve transport limits.
- [x] Add guarded Songs seed/constraint migration and tests for up/down refusal.
- [x] Extend web type/identity/filter contracts and test song routing.
- [x] Run provider/unit/migration/import tests and web focused tests.

### Task 2: Album artwork delivery

Files: provider-images.ts, media-artwork.tsx, catalog read projection, artwork tests and credits.
Consumes: canonical album cover URL; produces safe artwork in search/preview/catalog/library.
- [x] Add failing tests for canonical CAA reference acceptance, lookalike rejection and stored album fallback.
- [x] Implement strict URL helper and direct browser delivery without persistent cache; preserve uploaded images.
- [x] Document source/rights and run artwork/provider tests.

### Task 3: Recently rated

Files: PersonalMedia.cs, RatingService.cs, PersonalMediaEndpoints.cs, web personal-policy.ts,
new recent ratings interfaces/API/hook/component/CSS and HomeScreen; rating mutation invalidation.
Produces: authenticated `GET /api/v1/users/me/ratings/recent` returning at most 12
`{ media, score, updatedAt }` items in `{ items }` envelope.
- [x] Add failing API tests covering owner isolation, ordering, edited ratings, no library row, inaccessible items and limit.
- [x] Implement query and endpoint using CatalogAccess.Readable(userId), rate limiting and no-store.
- [x] Add proxy allowlist and tests; reject all query parameters.
- [x] Add failing component tests for cards before feed, empty/error/loading, account transitions and signed-out behavior.
- [x] Implement responsive grid and rating invalidation; run focused tests.

### Task 4: Verification and review

- [x] Run dotnet test RateFreak.sln and pnpm test, lint, typecheck in web.
- [x] Review full diff, address findings, document results and remaining limits.
- [x] Commit only implementation-owned files; preserve investigation and STATUS edits.

## Execution record

Owner approved scope and requested implementation; proceed inline in the shared
checkout, preserving existing changes. No additional approval gate is needed for
these authorized reversible edits. This record tracks task completion and deviations.


Tasks 1–3 complete. Evidence: provider contracts 221 passed; focused integration
41 passed plus song migration safeguards 4 passed; artwork/identity checks 28
passed; new home/privacy tests 5 passed; music metadata and related UI tests 35
passed. Final frontend suite: 1087 passed across 128 files, including the added
proxy-isolation case. Lint, Next.js type generation/TypeScript, production build
and mechanical design detector passed (no detector findings). Independent
read-only review found no actionable bugs. Full .NET suite passed: 62 unit, 523 integration and 221 provider contract
tests (806 total, zero failures or skips).

Ruling: retain the existing home feed wrapper and insert Recently rated between
its heading and tabs, so invalid feed filters keep their current no-fetch behavior.
Ruling: legacy album cover resolution also applies to collection and library
history summaries, preventing artwork differences across those consumers.
Live upstream artwork and desktop/mobile browser captures have not been run;
verification uses simulated provider responses, real isolated PostgreSQL databases,
DOM component tests and production compilation. No live database was migrated.

Implementation committed locally; no push or deployment. Existing owner STATUS
changes and the original investigation file were excluded from the commit.
