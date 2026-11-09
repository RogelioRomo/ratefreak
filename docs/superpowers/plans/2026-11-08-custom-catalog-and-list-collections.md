# Custom Catalog and List Collections Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans for native execution, or superpowers:subagent-driven-development if the user chooses per-task delegation. Steps use checkbox syntax for tracking.

**Goal:** Create privately or publicly rated custom catalog items with one filesystem-backed image and organize shared item identities in lists.

**Architecture:** Add categories, ownership and lifecycle states to existing catalog identities. Close all indirect privacy paths before enabling creation; then introduce uploads/moderation/frontend and finally replace collection fields with relational memberships. Destructive test-data clearing is an offline, separately authorized operation.

**Tech Stack:** Existing .NET/Identity/EF Core/PostgreSQL and Next/React/TanStack Query; persistent host filesystem for artwork.

**Spec:** [Approved design](../specs/2026-11-08-custom-catalog-and-list-collections-design.md). User authorized proceeding on 2026-11-08 after confirming filesystem image storage outside wwwroot.

## Global Constraints

- Preserve existing item UUIDs, provider associations, accounts and contributions. Backfill existing items as public/system-owned.
- Retain movie, tv_show, album and video_game; add custom independently of provider types.
- Private creation by default; publication requires an active public category. Unauthorized direct and indirect IDs return 404.
- Keep 0–5 ratings in 0.5 steps and existing review/comment contracts and moderation holds.
- One optional image: JPEG/PNG/WebP, 5 MiB input, 20 megapixels, static re-encoding to a maximum 2048-pixel longest edge. No arbitrary remote URLs or SVG uploads.
- Files on persistent host storage outside wwwroot; database contains references/metadata only. Access-sensitive responses and artwork use no-store.
- All mutations derive actors from session and enforce existing CSRF, shared abuse budgets and strict unknown-member rejection.
- No real database reset, live migration, deployment, merge or push is authorized by this plan. Use disposable test databases for proof.
- Leave changes uncommitted unless the user requests commits; document completed tasks and evidence in this plan.
- Read web/AGENTS.md and the installed Next guides before frontend implementation. Keep TypeScript interfaces in separate files.

## Review Focus

- A direct review/comment/image URL must stop revealing a formerly-public item after it becomes private (Tasks 2, 5, 8).
- Concurrent category merges, publications and list writes must not deadlock or admit private items into a newly public list (Tasks 3, 4, 7).
- A failed or interrupted image replacement must preserve the previously committed image, including during cleanup (Task 5).
- Old tabs must reject legacy field/title/data payloads and stale revisions after cutover (Tasks 7, 8).
- Ordinary admin browsing and public pagination totals must not reveal private titles, categories or counts (Tasks 2, 4).

## Execution setup and file boundaries

Current branch is main; implementation starts in an isolated feature worktree using
superpowers:using-git-worktrees, carrying these uncommitted design/plan documents
without removing them from the original checkout. Keep a task ledger under that
worktree's .local directory. Run baseline relevant suites before changes; record any
unavailable Docker/network/tool prerequisites without claiming skipped checks pass.

Application/Catalog owns contracts; Domain/Catalog owns category/item state;
Infrastructure/Catalog owns EF access predicates, commands and category operations.
Infrastructure/Artwork owns file storage, decoding and cleanup, independently of EF
artwork metadata configuration. Existing PersonalMedia, Social, Moderation and
Collections services consume the common policy. API endpoints own transport/CSRF;
web/server/api owns exact proxy allowances and bounded multipart/binary forwarding.

Every task follows RED → minimal implementation → GREEN. Use disposable
AccountDatabase/Testcontainers patterns and test-scoped temporary upload directories.
No task may enable private content before Tasks 1–2 are proven. Tasks 3–6 may be
developed sequentially with routing disabled until their dependencies are complete.

## Shared interfaces and contracts

Define these once, update all callers and test doubles together:

- `CatalogQuery(string? Search, string? MediaType, int Page, int PageSize, Guid? CategoryId = null, bool OwnOnly = false)`.
- `ICatalogQueries.SearchAsync(Guid? viewerId, CatalogQuery query, CancellationToken ct)` and `FindAsync(Guid? viewerId, Guid id, CancellationToken ct)`.
- Internal `CatalogAccess.Readable(Guid? viewerId) -> Expression<Func<MediaItem,bool>>`; `Manageable(Guid ownerId)` for owned archived/held management. Queries compose these before count/page/projection. Category aliases resolve before filtering.
- `ICatalogItemCommands.CreateAsync(Guid actorId, CustomItemCreate request, CancellationToken ct)` and `UpdateAsync(Guid actorId, Guid id, CustomItemUpdate request, CancellationToken ct)` return `CatalogResult<MediaDetails>`.
- Strict `CustomItemCreate(string? Title, string? Description, Guid? CategoryId, string? Visibility)` and `CustomItemUpdate(long? ExpectedRevision, string? Title, string? Description, Guid? CategoryId)`; lifecycle `ItemStateChange(long? ExpectedRevision)` bodies.
- `CatalogResult<T>(CatalogStatus Status, T? Value, Dictionary<string,string[]>? Errors = null)` where Status is Success/NotFound/Conflict/Invalid/Unavailable. Detail includes revision, category summary, provenance, visibility and creator UUID where authorized; never disclose email or storage keys.
- `ICategoryService.ListAsync(Guid? viewerId, string? search, bool ownOnly, int page, int pageSize, CancellationToken ct)`, `CreateAsync(Guid actorId, CategoryCreate request, CancellationToken ct)`, `RenameAsync`, `MergeAsync`, `SetHiddenAsync`; category mutation results reuse CatalogResult. Category DTOs carry UUID/name/scope/revision, not private item counts.
- Add `Guid? viewerId` to rating aggregate, review list/detail and comment list/detail methods; author mutations continue taking actor UUID and also check the parent item. Profile/history queries receive viewer UUID separately from profile-owner UUID. Library reads/writes use library owner as viewer.
- Artwork application contracts: `IArtworkStorage.StageAsync(Stream content, CancellationToken ct) -> StagedArtwork`; `PromoteAsync(StagedArtwork staged, Guid assetId, CancellationToken ct) -> string` storage key; `OpenReadAsync(string key, CancellationToken ct) -> Stream?`; `DeleteAsync(string key, CancellationToken ct)`. Storage accepts only its own generated keys. `IArtworkDecoder.DecodeAsync(Stream input, CancellationToken ct) -> DecodedArtwork` with bytes/content type/dimensions after bounded validation.
- `IArtworkService.PutAsync(Guid actorId, Guid itemId, long expectedRevision, Stream input, CancellationToken ct)` returns CatalogResult<MediaDetails>; `DeleteAsync` uses actor/item/revision; `ReadAsync(Guid? viewerId, Guid itemId, Guid assetId, CancellationToken ct)` returns an authorized stream/content type or null. File storage does not perform HTTP authorization.
- Final collection `ItemCreate(long? ExpectedRevision, Guid? MediaItemId, int? Position = null)` and `ItemUpdate(long? ExpectedRevision, int? Position)`; `ItemDetails` contains membership ID, nonnullable item ID, visible MediaSummary, position and timestamps. Remove SchemaVersion/Fields/Data/copied Title from final contracts.

## Locking and visibility snapshots

Extend the existing transaction-scoped moderation advisory lock to every new
category/item state write and all existing contribution/collection writes that
depend on item visibility. This intentionally coarse first release serializes these
writes across instances. Acquire it first, then category rows ordered by UUID, item
rows ordered by UUID, collection rows ordered by UUID, then child rows. Existing
writers currently taking parent locks first must be updated; nested service helpers
must not start their own transactions. Preserve expected-revision checks after locks.
Decode files before acquiring the advisory lock.

Use repeatable-read snapshots for reads requiring multiple queries to authorize and
then return content/counts. A request already authorized before a concurrent privacy
change may complete its coherent snapshot; subsequent requests must apply the new
state. Never cache authorization beyond the request. File reads authorize and open
the current immutable file within the same coherent operation; preserve opened
stream behavior during replacement and test the target hosting OS behavior.

### Task 1: Add catalog/category persistence without enabling custom creation

**Files:** Modify Domain/Catalog/MediaItem.cs and Infrastructure/Persistence/{RateFreakDbContext.cs,Configurations/CatalogConfiguration.cs}; create Domain/Catalog/{Category.cs,CategoryAlias.cs}, Infrastructure/Persistence/Configurations/CategoryConfiguration.cs and generated AddCustomCatalogFoundation migration/snapshot; create tests/RateFreak.UnitTests/CategoryRulesTests.cs and tests/RateFreak.IntegrationTests/CustomCatalogMigrationTests.cs. Source/test paths are under their existing src/RateFreak.* and tests/RateFreak.*Tests projects.
**Interfaces:** Produces item/category state and indexes consumed by Tasks 2–7. Stable seeded category UUIDs are migration constants, mapped one-to-one to the four legacy types.

- [x] Add failing `ExistingCatalogBackfillPreservesIdentityAndMappings`, `PrivateCategoryNamesAreUniquePerOwner`, `PublicCategoryNormalizedNameIsUnique` and `DeletingOwnerCannotCascadeCustomItems` tests. Assert legacy ownership is null/system/public, all UUIDs unchanged and no field data is cleared.
- [x] Run `dotnet test tests/RateFreak.IntegrationTests --filter FullyQualifiedName~CustomCatalogMigrationTests` and unit CategoryRulesTests; observe RED.
- [x] Implement custom item state, positive revisions, category ownership/scope constraints, public/private normalized-name unique indexes, restricted owner/category FKs and aliases. Normalize FormKC, whitespace collapse and invariant uppercase; names are 1–100 printable characters. Add additive migration and safe downgrade guards against custom/private data.
- [x] Run focused tests plus PostgresTests; verify fresh and populated upgrade, repeat migration application and direct SQL constraints. No pending model changes. Keep user routes disabled.

### Task 2: Enforce common item access across every existing surface

**Files:** Create Infrastructure/Catalog/CatalogAccess.cs; modify Application/{Catalog/Catalog.cs,PersonalMedia/PersonalMedia.cs,Social/ReviewComments.cs,Identity/UserProfiles.cs}, Infrastructure/{Catalog/CatalogQueries.cs,PersonalMedia/{LibraryService,RatingService,ReviewService}.cs,Social/ReviewCommentService.cs,Authentication/UserProfileService.cs,Collections/{CollectionQueries,CollectionCommands,CollectionItemCommands,CollectionMutation}.cs,Moderation/ModerationService.cs}, consuming API endpoints and DI; create IntegrationTests/{CatalogAccessTests,CatalogAccessConcurrencyTests}.cs.
**Interfaces:** Produces viewer-aware signatures and shared policy above; legacy public providers remain readable anonymously.

- [x] Add failing matrix tests `PrivateItemIsNotFoundThroughEveryDirectId`, `ProfileAndListTotalsExcludeInaccessibleItems`, `AdminOrdinaryReadsDoNotBypassPrivacy`, `CreatorRetainsStoredContributionsAfterUnpublish` and `HiddenCategorySuppressesPublicItem`. Exercise review/comment IDs, library/rating writes and delete endpoints as well as reads; inaccessible deletes return 404 rather than succeeding silently.
- [x] Run `dotnet test tests/RateFreak.IntegrationTests --filter FullyQualifiedName~CatalogAccess`; observe RED using directly seeded private/held/archived rows.
- [x] Implement predicates before projection/count/page/aggregate, management predicates separately, parent item authorization for child routes, snapshot reads and all caller/test-double signature updates. Add shared advisory locking to visibility-sensitive existing writes. Public review ownership and moderation holds remain independent.
- [x] Prove no title/category/body/count leak across anonymous/creator/stranger/Moderator/Admin. Barrier tests race privacy with rating, review, comment and membership writes, asserting a coherent snapshot or sanitized 404 and no deadlock. Run existing personal, social, profile, collection and provider suites.

### Task 3: Category APIs and administration

**Files:** Create Application/Catalog/Categories.cs, Infrastructure/Catalog/CategoryService.cs, Api/Endpoints/CategoryEndpoints.cs; modify Program.cs/DependencyInjection.cs and moderation contracts/audit; create UnitTests/CategoryValidationTests.cs and IntegrationTests/CategoryTests.cs.
**Interfaces:** Implements ICategoryService. GET/POST `/api/v1/categories`; PATCH `/categories/{id}` private-owner rename. Admin POST `/moderation/categories/{id}/rename`, `/merge`, `/hide`, `/restore` with expectedRevision/reason; merge additionally requires targetId and targetExpectedRevision.

- [x] Add failing `ConcurrentPublicNameCreatesProduceOneConflict`, `PrivateDuplicateErrorsDoNotRevealOtherOwners`, `PublishCategoryNameDoesNotPublishOtherPrivateItems`, `AdminMergeReparentsWithoutCycles` and `SeededCategoriesCannotBeHiddenOrMerged`. Confirm ordinary creators cannot administer public categories and self-moderation is denied.
- [x] Run `dotnet test tests/RateFreak.IntegrationTests --filter FullyQualifiedName~CategoryTests`; observe RED.
- [x] Implement listing with caller scope, explicit create, owner-private rename and transactional admin rename/merge/hold operations with same-transaction audit. Merge flattens aliases to the active target, preserves source IDs, and advances affected item/category revisions. Reject duplicates, hidden targets and stale revisions. Apply catalog read, personal-write and moderation-write budgets as appropriate.
- [x] Run focused unit/integration tests including simultaneous merges/publications with lock barriers; verify active public item access changes consistently on hide/restore and no private category names/counts leak.

### Task 4: Custom item lifecycle and reporting/moderation

**Files:** Create Application/Catalog/{CustomItemRequests,CatalogCommands}.cs, Infrastructure/Catalog/CatalogItemCommands.cs, Api/Endpoints/CustomItemEndpoints.cs; modify Domain/Moderation/ContentReport.cs, Application/Moderation/Moderation.cs, Infrastructure/Moderation/{ModerationService,ModerationCommands}.cs, Api/Endpoints/ModerationEndpoints.cs, ModerationConfiguration.cs and generated AddCatalogReportTargets migration; create IntegrationTests/{CustomItemTests,CatalogModerationTests}.cs.
**Interfaces:** Implements ICatalogItemCommands and state operations. POST `/media`, PATCH `/media/{id}`, POST `/media/{id}/{publish,unpublish,archive,restore}`; reporting adds item/category targets to existing reports routes rather than creating a separate queue.

- [x] Add failing tests for private default, public-category requirement, 1/300/301 title and 20,000/20,001 description boundaries, omitted/unknown fields, stale revisions, category holds, archive/restore-to-private and provider edit rejection. `UnpublishRepublishPreservesOtherUsersContributions` checks persisted rows and visibility. `ReportingPrivateIdsDoesNotRevealExistence` covers both targets.
- [x] Run `dotnet test tests/RateFreak.IntegrationTests --filter 'FullyQualifiedName~CustomItemTests|FullyQualifiedName~CatalogModerationTests'`; observe RED.
- [x] Implement creator-only commands, per-item revisions and explicit lifecycle transitions. Add nullable FK report targets with retained immutable target IDs, target-kind checks, existing role/self-moderation rules and same-transaction audit. Staff may inspect a formerly public target only through its retained report workflow; ordinary staff reads still use policy. Do not add account deletion.
- [x] Run focused tests and existing moderation tests; verify CSRF, shared report/write budgets, role recheck races and audit absence of copied bodies, credentials or file paths. Enable API creation only after Task 2 passes in full.

### Task 5: Persistent filesystem artwork and authorized delivery

**Files:** Create Application/Catalog/Artwork.cs, Domain/Catalog/ArtworkAsset.cs, Infrastructure/Artwork/{ArtworkOptions,FileArtworkStorage,ArtworkDecoder,ArtworkService,ArtworkCleanupService}.cs, Persistence/Configurations/ArtworkConfiguration.cs, Api/Endpoints/ArtworkEndpoints.cs and AddArtworkMetadata migration; modify ApiSecurity.cs, Program.cs, DI, appsettings.json/.env.example; create UnitTests/ArtworkStorageTests.cs and IntegrationTests/ArtworkTests.cs.
**Interfaces:** Implements artwork contracts above. `Artwork:RootPath` must be an absolute persistent directory outside content/web roots (development uses explicit `.local/uploads` outside wwwroot). `Artwork:Enabled` defaults false until configured. Upload returns updated MediaDetails/revision; delete body carries expectedRevision. Multipart PUT contains exactly `file` and `expectedRevision`; download uses itemId/assetId, never storage key.

- [x] Check decoder candidate primary documentation, current security/advisory and license information; choose and pin a supported decoder in Infrastructure.csproj and record the decision. It must support bounded static JPEG/PNG/WebP decode/re-encode and reject animation; no shell image processing. Do not enable uploads without a suitable dependency.
- [x] Write failing `PrivateArtworkRequiresItemAccess`, `ReplacementFailureKeepsOldAsset`, `CleanupNeverDeletesInFlightUpload`, `RestartRetainsUploadedFile`, `TraversalAndSymlinkPathsAreRejected`, `DiskFullLeavesMetadataUnchanged` tests. Inputs cover MIME/extension spoofing, malformed files, dimensions, animation, 5 MiB + 1 byte, cancellation and stale revision; assert bytes never exist in DB schema.
- [x] Run focused Artwork unit/integration tests; observe RED.
- [x] Implement staging/promote/reference switch/post-commit delete with generated immutable paths; record staging leases so hourly cleanup only removes files older than 24 hours with no active lease or current DB reference. Cleanup skips all deletions when DB reference checks fail. Retry failures later; never return filesystem details to clients. Limit decode concurrency to two per process; reject source dimensions above 8192 per axis or 20,000,000 pixels before full decode. Persist re-encoded JPEG/PNG/WebP with metadata stripped and longest edge at most 2048.
- [x] Add route-specific multipart/request limits of 5 MiB file plus at most 64 KiB framing; retain 64 KiB limits on other APIs. Upload disabled returns sanitized 503; downloads use no-store/nosniff, authorization and bounded file streams. Handle missing files with 404/fallback and internal diagnostic events without paths.
- [x] Run focused tests including crash boundaries around promotion/commit/deletion, host filesystem persistence, authenticated binary delivery after public-to-private changes and concurrent replacement/removal. Verify configuration permission errors fail safely and cleanup is bounded per run.

### Task 6: Custom item frontend and multipart proxy

**Files:** Create web/server/api/artwork-policy.ts and artwork-proxy.test.ts; modify {proxy,request-body,mutation-guard,route-policy,catalog-query}.ts and tests there; create web/features/custom-catalog/{interfaces,api,components,hooks} files and app/catalog/{new,mine}/page.tsx; modify features/catalog/components/{media-artwork,media-detail-screen}.tsx and relevant catalog/personal/profile/filter/query-key/session files.
**Interfaces:** Exact route allowlists for Tasks 3–5; custom form DTOs match application contracts. Artwork upload uses FormData with session/CSRF headers and revision; ordinary JSON APIs keep their previous limits/content rules.

- [x] Add failing proxy tests `MultipartAllowedOnlyOnArtworkPut`, `BinaryArtworkPreservesSafeHeaders`, `ChunkedUploadCannotExceedLimit`, `PrivateImageResponseIsNeverCached`; preserve origin, attribution, cookie and timeout checks. Add UI tests for private-default creation, category reuse/create conflict, image retry without duplicate item, creator management, archive/restore and expired session draft handling.
- [x] Run `pnpm --dir web test -- server/api/artwork-proxy.test.ts features/custom-catalog`; observe RED.
- [x] Implement route-specific multipart boundary validation and bounded forwarding (do not blindly replace JSON Content-Type rules). GET image responses preserve Content-Type/nosniff/no-store; no Next image optimization. Add owner management, explicit publish/category selection and optional image controls using existing styling/accessibility conventions. Use neutral custom-item fallback and existing rating/review controls. Legacy provider type validation remains separate from custom category filters.
- [x] Scope item/category/library/profile/collection queries by viewer and clear on session transitions; invalidate all affected surfaces after lifecycle changes. Preserve form values and uncertain-write guidance; no automatic mutation retry. Use neutral tracking labels without changing stored status values.
- [x] Run focused frontend/proxy suites, typecheck and lint; verify private item IDs and cached imagery disappear on logout and account switches, including stale tabs and failed background refresh.

### Task 7: List-only schema and API with guarded offline cutover tooling

**Files:** Modify Domain/Collections/{Collection,CollectionItem}.cs, Application/Collections/{Collections,CollectionRequests,CollectionValidation}.cs, Infrastructure/Collections/{CollectionQueries,CollectionCommands,CollectionItemCommands,CollectionMutation,CollectionMapping}.cs, CollectionsConfiguration.cs and CollectionEndpoints.cs; remove CollectionField/CollectionFieldCommands/ICollectionFieldCommands and field-only contracts; generate ConvertCollectionsToLists migration; create Api/Maintenance/CollectionListReset.cs and IntegrationTests/{ListCollectionsTests,CollectionListResetTests}.cs; replace obsolete CollectionFieldsTests while retaining security/concurrency/removal coverage.
**Interfaces:** Final collection DTOs above, same endpoint URLs except retired field/impact routes. Offline `--reset-test-collection-data --database-name <name> --confirm-test-data-loss` command; no HTTP reset. Default dry-run without destructive flag reports safe target identity/counts/schema version; no connection credentials.

- [x] Add failing `NormalMigrationRefusesLegacyRowsWithoutDeletingThem`, `ResetTouchesOnlyEntriesFieldsAndCollectionRevisions`, `StaleEditorCannotWriteAfterReset`, `PublishingListRejectsPrivateMembers`, `HiddenMembershipsDoNotAffectVisibleTotals`, `LegacyTitleDataFieldsAreRejected` and `RemovingMembershipPreservesItemAndReview` tests. Include anonymous direct membership IDs and concurrent list/item visibility changes.
- [x] Run focused ListCollections/CollectionListReset tests; observe RED on disposable databases only.
- [x] Implement final list contracts/persistence, uniqueness and ordering with revision checks. Remove field routes, DI and documentation metadata. Visibility changes and adds use the common write lock and current item policy. Preserve existing memberships when item visibility changes; never leak inaccessible entries in summaries/counts.
- [x] Implement explicit offline reset transaction checking exact configured database name and acknowledgement, recording before/after counts, clearing only memberships/field rows and advancing retained collection revisions. Writer shutdown is an operator prerequisite; take application advisory lock too. Normal migration checks emptiness and raises clearly before removing columns/tables; never include DELETE in schema migration. Guard Down against falsely reconstructing lost field values.
- [x] Prove scoped preservation and failed-reset rollback with fixtures containing users, provider mappings, ratings, reviews, comments, libraries, reports/audits and collection metadata. Fresh databases require no reset. Do not run this command against the current or any deployed database in this implementation session.

### Task 8: List frontend and consumer compatibility gate

**Files:** Modify web/features/collections/{interfaces,api,hooks,query,components}, web/server/api/{collection-policy,route-policy}.ts and tests; remove field-specific components/query helpers/tests only when no remaining callers use them; modify web/README.md and README.md.
**Interfaces:** Consumes final Task 7 DTOs. Picker searches accessible catalog items with category filter, add sends existing ID/position/revision only; reorder sends position/revision only.

- [x] Inventory repository consumers/OpenAPI/proxy and document external-consumer status as unconfirmed until the operator confirms it. If external consumers exist, preserve/version legacy contracts before cutover; do not claim repository search proves absence of clients. This gate blocks real deployment, not implementing/testing the final contracts locally.
- [x] Add failing UI/proxy tests for mixed public/custom items, accessible picker, private-item list eligibility, reorder/remove, no field/free-title controls, retired routes, stale revision and session cache isolation. Count/page tests hide inaccessible rows without leaving phantom totals.
- [x] Run `pnpm --dir web test -- features/collections server/api/collection-policy.test.ts`; observe RED.
- [x] Replace collection editing with list metadata and catalog membership controls. Preserve error/draft/conflict behavior and exact large revisions. Remove field/impact proxy allowances and unknown legacy request properties. Invalidate schema-era client queries so old tabs must reload; explain that public list entries vary with item access.
- [x] Run all collection/proxy tests, typecheck and lint. Verify delete removes membership only and picker/create flows do not create orphan catalog items.

### Task 9: Integration proof, operations documentation and independent review

**Files:** README.md, web/README.md, .env.example, docs/{implementation-checklist,frontend-checklist,frontend-release-verification}.md, docs/adr/0006-collections.md and new docs/adr/0016-custom-catalog-and-list-collections.md; this plan/spec evidence sections.
**Interfaces:** Consumes Tasks 1–8. Produces documented filesystem provisioning/backup and an explicit reviewed cutover runbook, not a live reset or deployment.

- [x] Add any missing cross-feature regression tests discovered in the coverage matrix, observe RED and make minimal fixes before final checks. Confirm category aliases, moderation holds and provider imports retain their original identities/semantics.
- [x] Run `dotnet test RateFreak.sln`, `dotnet build RateFreak.sln -c Release`, `dotnet ef migrations has-pending-model-changes --project src/RateFreak.Infrastructure --startup-project src/RateFreak.Api`, `dotnet list RateFreak.sln package --vulnerable --include-transitive` and `git diff --check`; record actual results. Require zero failed/skipped tests, no model drift and no new build warnings. Audit findings must be assessed, never silently omitted.
- [x] Run `pnpm --dir web test`, `pnpm --dir web lint`, `pnpm --dir web typecheck` and `pnpm --dir web build`; record actual results. Use isolated temporary processes/database/filesystem to verify migrations fresh/upgraded, API/proxy OpenAPI and upload persistence across API restart.
- [x] Exercise owner/stranger/anonymous browser journeys for Blue hydrangea → Flowers → private/public → upload/replace/remove → rating/review/comment → My garden/Gift ideas → reorder/remove/archive, plus category administration/reporting. Verify keyboard and responsive Light/Dark modes. Never use real user accounts or the existing database for reset proof.
- [x] Document `Artwork:RootPath`, ownership/permissions, persistent container mounts, disk capacity/cleanup, coordinated database/filesystem backup and restoration checks. Document offline target verification, writer shutdown, before/after preservation counts, exact reset acknowledgement, matching API/frontend release and cache invalidation.
- [x] Obtain an independent whole-branch review emphasizing indirect leaks, concurrency and filesystem failure boundaries. Fix material findings with observed RED→GREEN regressions, then rerun affected checks. Mark preliminary phases complete only with evidence; preserve ADR 0006 history while linking its approved replacement. No merge, push or deployment implied.

## Self-review and status

Spec coverage: identity/categories Tasks 1/3; full privacy Task 2 and all later
surfaces; lifecycle/reporting Task 4; one filesystem image Task 5; custom UI Task 6;
list/reset Task 7; list UI/consumers Task 8; complete acceptance/operations Task 9.
Interfaces use the same viewer/revision/identity contracts across all consumers.
Review Focus cases have explicit tests in their owning tasks. Library vocabulary is
in Task 6; account deletion remains deferred with restrictive FKs in Task 1.

Implementation and independent review are complete in the managed isolated worktree
`/home/rhaydon/.codex/worktrees/2f8a/ratefreak`. Native execution was selected;
changes remain uncommitted. The evidence below records actual checks; named test
cases in task instructions describe scenarios and may have different method names
in the final test suites. No live reset, migration or deployment was performed.


## Execution evidence — 2026-11-08

- Full backend: 27 unit + 154 provider + 373 integration = **554 passed**, zero
  failures/skips (`/tmp/ratefreak-proof-backend.log`). Retired collection field/data
  tests were removed with their contracts; retained revision/session/security
  coverage plus list/privacy/reset tests cover the replacement.
- Full frontend: **638 passed in 85 files**, no skipped tests
  (`/tmp/ratefreak-proof-frontend.log`). Final conflict-submit/typed fixture follow-up
  passes 2/2 (`/tmp/ratefreak-proof-ui-focused.log`). Lint, typecheck and production
  build pass. Release .NET build passes with zero warnings/errors.
- EF model drift check passes (dummy design-time configuration, no database operation).
  NuGet audit reports no vulnerable packages in all seven projects from current sources.
  Final whitespace check passes.
- Privacy and concurrency suites cover direct/indirect IDs, formerly public
  contributions, ordinary admin isolation, private-category references, publication
  against concurrent holds and list insertion. Provider suites preserve identities.
- Disposable PostgreSQL tests prove fresh/backfill migrations, guarded refusal of
  legacy rows, exact-target dry-run/reset and retention of users/catalog/collection
  metadata/ratings/reviews. No existing installation was cleared.
- Real Chromium journeys cover Blue hydrangea/Flowers, private/public item and image
  access, same identity in My garden/Gift ideas, rating/review/comment, reorder/remove,
  category hold/restore/report-scoped artwork, unpublish and archive/restore. Image
  replacement survived API restart; obsolete/private image URLs return404 and removal
  clears the file/reference. The final image request resumed after an ordinary rate
  limit; uncertain operations were reconciled rather than replayed.
- Five desktop/mobile Light/Dark captures checked no horizontal overflow and keyboard
  Title→Description navigation. Initial inspection plus one confirmation completed;
  UI detector returned zero findings. Evidence: `/tmp/ratefreak-browser/`.
- Independent whole-change reviewer found no Critical, two Important list recovery
  defects and one Minor stale-editor defect. RED→GREEN fixes and delta review confirm
  all resolved. Unavailable-reference removal is explicit, owner/revision protected
  and discloses no hidden IDs/counts; public metadata edits remain possible.
- Filesystem/cutover/backup runbook: [operations](../../custom-catalog-operations.md).
  External deployed consumers remain **unconfirmed**. That blocks real cutover until
  operator confirmation/versioning and separately approved data handling; it does not
  block implementing or testing these final local contracts.
- Task-owned browser API/frontend/container stopped; temporary cookies and fixture
  credential scripts removed. Screenshots/logs retained. Existing app/database untouched.

Remaining delivery action: operator-controlled integration/deployment only. Keep the
uncommitted worktree per the approved plan; no merge, push or commit is implied.


## Main working-copy integration — 2026-11-08

After the user requested next steps, the reviewed changes were transferred to
`/home/rhaydon/documents/repos/ratefreak`, preserving the original worktree and
backing up prior main-checkout files under `.local/custom-catalog-integration`.
No Git history or index changes were made. Fresh main-checkout verification:
backend554/554, frontend638/638, lint, typecheck, production frontend build,
Release API publish, EF model-drift check and whitespace checks all pass.
The initial restricted frontend build failed while parsing a TypeScript subprocess;
direct configuration output and normal-permissions production build pass without
source changes. Evidence is `/tmp/ratefreak-main-*.log`.

Local artifacts are under `.local/releases/custom-catalog`: published API with
native codecs, idempotent migration SQL for review and file checksums. The SQL was
not applied. Existing collection data disposition and external-client compatibility
remain pending operator confirmation before any database cutover or deployment.


## Authorized local test-database cutover — 2026-11-08

The user subsequently confirmed that the local database contains disposable test
data and authorized the required clearing/migrations. Target identity was verified
as `ratefreak` at `127.0.0.1:55432` with no active writers. A private custom-format
backup was saved and its archive validated. The exact-target offline reset removed
one old entry and one custom field, retaining the collection itself. Four migrations
applied successfully, with history advancing from14 to18.

Counts and original-row fingerprints across21 retained tables match; the two
accounts, eight catalog items, rating, review and library entry remain unchanged.
Collection metadata remains unchanged and revision advances3→5 for reset+migration.
Repeat migration succeeds. A temporary published API returned200 for liveness,
readiness, catalog and categories, and was stopped. No permanent hosting or public
deployment was performed. Local evidence and backup are under
`.local/custom-catalog-cutover/`; this resolves the local database gate. Any future
production/external-client rollout remains a separate operation.
