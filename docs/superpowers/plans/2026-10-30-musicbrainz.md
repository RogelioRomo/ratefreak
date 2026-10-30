# MusicBrainz Albums Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans inline, with test-first steps and one final independent review.

**Goal:** Deliver Phase 6a album search, preview and protected import alongside TMDB.
**Architecture:** Explicit provider registry, bounded typed MusicBrainz adapter, shared transactional import. Widen the existing catalog type constraint; reuse personal media and collections.
**Tech Stack:** Existing .NET 10/HttpClient/EF/PostgreSQL/xUnit stack; no packages.
**Spec:** `docs/superpowers/specs/2026-10-30-musicbrainz-design.md` approved by user request to implement on 2026-10-30, with planning/execution delegated.

## Global Constraints

Use the approved spec's exact bounds: Album release groups only; exclude EP/single and nonmusical secondary types; one UUID per conceptual album; no artwork/annotation/ratings import. IDs are nonempty dashed UUIDs; existing TMDB positive int IDs unchanged. Search q 1–200, pages 1..500, 20 results. Provider options disabled by default, validated chosen contact when enabled, timeout 1500..15000/default6000. Fixed HTTPS origin, no redirects, 1 MiB/depth32 strict JSON. Artist bounds50/300/100/2000; secondary types20/80. Every outbound start >=1second apart, single active operation, no queue, overall timeout, <=1 retry and shared cooldown. Preserve existing security, rate budgets, imports and data. No public activation without chosen maintainer contact.

## Review Focus

- Expanding MediaTypes must never route album to TMDB's TV namespace (Task 1 tests).
- Upstream identity/type/string/partial-date corruption must produce 502/404 rather than a DB 500 (Task 2 tests).
- Cancellation, retries and cooldown across clients must preserve pacing and release the gate (Task 2 controlled-time tests).
- Repeated/concurrent imports must preserve existing curated data and return one canonical winner (Task 3 PostgreSQL tests).
- Migration Down with albums must fail without deleting rows/history (Task 3 migration test).

### Task 1: Provider selection and album identity

Files: Application/MetadataImport/MediaProviders.cs and new MediaProviderRegistry.cs; Domain/Catalog/MediaItem.cs; Infrastructure MetadataImport/MediaImportService.cs, DependencyInjection.cs, TMDB validation; Api/Endpoints/{MediaProviderEndpoints,CatalogEndpoints,PersonalMediaEndpoints}.cs. Tests: provider contract/endpoint and import suites.
Interfaces: `IMediaProviderRegistry` exposes `IReadOnlyList<ProviderCapability> Capabilities`, `IMediaProvider? Find(string name)`; provider identity helper validates `(provider,type,id)` and returns canonical ID, namespace and external URL. Keep existing TMDB TryId/ValidType meaning restricted to movie/tv_show. `IMediaImportService.ImportAsync(string provider,string externalId,string type,CancellationToken ct)` selects through registry. Keep adapter registration replacement in existing tests working by resolving IEnumerable<IMediaProvider>, not concrete adapters inside registry.

- [ ] Write RED tests for both capability entries (MusicBrainz disabled initially), explicit selection/default TMDB, album/type/UUID validation, TMDB album rejection and registry identity canonicalization.
- [ ] Run focused tests; verify missing behavior. Implement registry, identity, endpoint selection and generic import mapping/response consistency validation. Introduce disabled MusicBrainz adapter through Task 2 rather than fake production placeholder; Task 1/2 form one coherent passing commit.
- [ ] Add album canonical type, keep TMDB validation restrictive; update catalog/library filter messages. Verify existing TMDB suite remains green after Task 2.

### Task 2: Bounded MusicBrainz adapter

Files: Infrastructure/Providers/MusicBrainz/{MusicBrainzOptions,MusicBrainzRequestGate,MusicBrainzMediaProvider,MusicBrainzMapping}.cs; DI/default configuration; tests/RateFreak.ProviderContractTests/MusicBrainzTests.cs and controlled-time helper.
Interfaces: MusicBrainzMediaProvider implements IMediaProvider; MusicBrainzRequestGate singleton receives TimeProvider, exposes exclusive enter/exit, paced send and cooldown; HttpClient timeout infinite, gate owns whole-operation deadline. Options Enabled/Contact/TimeoutMilliseconds. Register TimeProvider.System only if absent so tests can control it.

- [ ] Write failing contract tests for request construction/escaping, normalization/exclusions/UUID mismatch, full/partial dates, malformed bounded JSON/strings, disabled/configuration, timeout/cancellation, transient/permanent errors, pacing and shared cooldown.
- [ ] Observe RED, implement fixed-origin adapter and bounded strict mapping, time-controlled gate and shared cooldown. Caller cancellation propagates; every terminal path releases the gate. Keep per-provider implementations focused, without unrelated TMDB refactor.
- [ ] Run provider contracts and endpoint tests; expect all pass. Commit Task 1/2 coherent integration.

### Task 3: Persistence, complete lifecycle and proof

Files: catalog model configuration/generated migration/snapshot; tests/RateFreak.IntegrationTests/MusicBrainzImportTests.cs and PostgresTests.cs; README, ADR0007, checklist.
Interfaces consume Task 1 registry/identity and Task 2 adapter; existing personal/collection contracts unchanged.

- [ ] Write RED real-PostgreSQL HTTP tests for new album import and existing TMDB compatibility, repeat/concurrent imports/no overwrite, malformed identity/type pre-HTTP, disabled repeat, ownership/CSRF, album library/rating/review/collection lifecycle and migration safe rollback.
- [ ] Generate/review widening-only AddAlbums migration; add explicit Down guard when albums exist. Run focused tests GREEN; retain pre-existing canonical IDs and mappings.
- [ ] Run full `dotnet test RateFreak.sln --no-restore --verbosity quiet`; Release build, EF consistency, transitive vulnerability audit and diff checks. Require zero failed/skipped and zero build warnings/errors.
- [ ] Apply/reapply local migration, verify health/readiness/Swagger/capabilities and disabled MusicBrainz behavior. Do not infer contact or make live MusicBrainz requests. Existing TMDB remains configured as before. Update documents with actual results and live activation pending contact.
- [ ] Commit and dispatch one independent whole-branch review; fix important findings with observed RED→GREEN tests and full suite. Keep branch for user integration choice; no automatic merge/push.

## Self-review

Spec covered by Tasks 1–3; transport/core contracts feed shared import. Migration widening and personal-media integration belong to Task 3. Public contact and artwork/social features remain out of scope. User authorized implementation directly after the design; no additional plan approval round.
