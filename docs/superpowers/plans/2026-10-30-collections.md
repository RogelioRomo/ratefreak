# Dynamic Collections Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement Phase 5 collections with validated custom fields, visibility, revision-safe edits and confirmed field deletion.

**Architecture:** Add a collections module to the existing Domain/Application/Infrastructure/API layers. Store relational collection/field/item records with JSONB data; serialize mutations through a locked collection row and reject stale revisions. No new packages.

**Tech Stack:** .NET 10, ASP.NET Core, EF Core/Npgsql, PostgreSQL 17, xUnit v3/Testcontainers.

**Spec:** `docs/superpowers/specs/2026-10-30-collections-design.md` (approved).

## Global Constraints

- Public/private visibility, default private; owner-only editing; mixed media and custom entries.
- At most 30 fields. Keys 1–40 lowercase ASCII letters/digits/underscores starting with a letter; reserved id, title, media_item_id, created_at, updated_at.
- Name 1–120; optional description ≤2000; labels 1–80; custom titles 1–200. Trim names/labels/titles, reject invalid Unicode and controls; allow tabs/newlines only in description/text values.
- Text maximum 1–4000 (default 1000); number is .NET decimal with inclusive optional bounds; boolean is JSON boolean; date is valid exact YYYY-MM-DD; select has 1–50 unique nonblank options ≤80 characters.
- Strict objects/configuration and unknown-property rejection; JSON depth ≤16 for custom data; existing 64 KiB body limit. Keys/types and item media references immutable.
- Required/configuration changes must validate all existing items. Required field additions to nonempty collections return 409.
- One canonical media reference per collection; independent custom entries. Explicit nonnegative positions, ties ordered by UUID.
- All existing-collection writes require positive expectedRevision. Revision starts at 1, schemaVersion starts at 1; every successful mutation increments revision, schema mutations also increment schemaVersion. Failed writes change neither.
- Field deletion preview counts only non-null values. Delete requires confirm=true and expectedRevision matching the preview. Delete definition and every stored key atomically; update affected item timestamps.
- Cookie authentication/CSRF for writes; 404 for foreign/private/missing resources; 400 validation; 409 stale/duplicate/incompatible schema. Reads no-store; reuse existing rate budgets.
- Additive migration only, no startup migration, no frontend/provider/account expansion, no collection deletion endpoint. Preserve persistent local data.

## Review Focus

- Duplicate JSON keys must return 400 before JSONB normalizes them; test in Tasks 1/3.
- Decimal overflow, malformed Unicode and PostgreSQL-forbidden NUL must return 400 rather than 500; test in Tasks 1/3.
- Privacy changes racing a multi-query read must not expose a private collection; coherent read snapshot test in Task 3.
- Schema/item mutations racing each other must never persist invalid data or apply stale deletion confirmation; real concurrent connections in Task 4.
- Delete with missing/false confirmation or missing revision must preserve definitions, values and timestamps; HTTP tests in Task 4.

## File map and shared interfaces

Create Domain/Collections/{Collection,CollectionField,CollectionItem,CollectionRules}.cs;
Application/Collections/{Collections,CollectionRequests,CollectionValidation}.cs;
Infrastructure/Collections/{CollectionQueries,CollectionCommands,CollectionFieldCommands,CollectionItemCommands}.cs;
Infrastructure/Persistence/Configurations/CollectionsConfiguration.cs;
Api/Endpoints/CollectionEndpoints.cs; tests in CollectionValidationTests.cs (unit)
and CollectionsTests.cs (integration). Modify DbContext, Infrastructure DI,
Program endpoint registration, PostgresTests, README, ADR/checklist and generated EF artifacts.
All paths are beneath the matching existing `src/RateFreak.*` or `tests/RateFreak.*Tests` project.

Contracts in Application/Collections:
- `CollectionPage<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)`.
- `CollectionAuthor(Guid Id, string Handle, string DisplayName)`.
- `CollectionSummary(Guid Id, CollectionAuthor Owner, string Name, string? Description, string Visibility, long Revision, long SchemaVersion, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt)`.
- `FieldDetails(Guid Id, string Key, string Label, string FieldType, bool Required, JsonElement Configuration, int Position)`.
- `CollectionDetails(CollectionSummary Collection, IReadOnlyList<FieldDetails> Fields)`.
- `ItemDetails(Guid Id, Guid? MediaItemId, string Title, JsonElement Data, int Position, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt)`; title resolved from catalog for canonical entries.
- `DeletionImpact(Guid FieldId, string Key, int PopulatedItemCount, long Revision)`.
- `CollectionResult<T>(CollectionStatus Status, T? Value, long? Revision, Dictionary<string,string[]>? Errors)` with Success, NotFound, Conflict, Invalid statuses. Use a nullable reference payload for all results; deletion payload is `CollectionRevision(long Revision)`.
- `FieldDefinition(string? Key, string? Label, string? FieldType, bool Required, JsonElement Configuration, int? Position)`.
- `CollectionCreate(string? Name, string? Description, string? Visibility, IReadOnlyList<FieldDefinition>? Fields)`.
- `CollectionUpdate(long? ExpectedRevision, string? Name, string? Description, string? Visibility)`.
- `FieldCreate(long? ExpectedRevision, FieldDefinition? Field)`; `FieldUpdate(long? ExpectedRevision, string? Label, bool? Required, JsonElement Configuration, int? Position)`.
- `ItemCreate(long? ExpectedRevision, Guid? MediaItemId, string? Title, JsonElement Data, int? Position)`; `ItemUpdate(long? ExpectedRevision, string? Title, JsonElement Data, int? Position)`.
- Requests use JsonUnmappedMemberHandling.Disallow. Missing fields cannot silently become valid values. Omitted visibility defaults private only at create; omitted Fields defaults empty; omitted position defaults zero on create, required on replace.
- `ICollectionQueries`: `ListAsync(Guid? viewerId, bool ownOnly, int page, int pageSize, CancellationToken ct)` → page of summaries; `FindAsync(Guid? viewerId, Guid id, CancellationToken ct)` → nullable details; `ItemsAsync(Guid? viewerId, Guid id, int page, int pageSize, CancellationToken ct)` → nullable page; `ItemAsync(Guid? viewerId, Guid id, Guid itemId, CancellationToken ct)` → nullable item; `DeletionImpactAsync(Guid ownerId, Guid id, Guid fieldId, CancellationToken ct)` → nullable impact.
- `ICollectionCommands`: `CreateAsync(Guid ownerId, CollectionCreate request, CancellationToken ct)` → result of details; `UpdateAsync(Guid ownerId, Guid id, CollectionUpdate request, CancellationToken ct)` → result of details.
- `ICollectionFieldCommands`: `AddAsync(Guid ownerId, Guid id, FieldCreate request, CancellationToken ct)` / `UpdateAsync(Guid ownerId, Guid id, Guid fieldId, FieldUpdate request, CancellationToken ct)` → result of FieldDetails; `DeleteAsync(Guid ownerId, Guid id, Guid fieldId, long? expectedRevision, bool confirm, CancellationToken ct)` → result of CollectionRevision.
- `ICollectionItemCommands`: `AddAsync(Guid ownerId, Guid id, ItemCreate request, CancellationToken ct)` / `UpdateAsync(Guid ownerId, Guid id, Guid itemId, ItemUpdate request, CancellationToken ct)` → result of ItemDetails; `DeleteAsync(Guid ownerId, Guid id, Guid itemId, long? expectedRevision, CancellationToken ct)` → result of CollectionRevision.

### Task 1: Bounded field and item validation

**Files:** Domain rules/entities, Application requests/validation/contracts; unit test file above.
**Consumes:** approved spec. **Produces:** shared contracts above and `CollectionValidation.ValidateFields(IReadOnlyList<FieldDefinition> fields)` / `ValidateData(JsonElement data, IReadOnlyList<FieldDefinition> fields)` returning validation error dictionaries; rules shared by all commands.

- [ ] Write parameterized failing unit tests covering every supported field type and limits: 30/31 fields, key/label limits and reserved/duplicate keys, configuration properties/options, decimal bounds/overflow, leap dates, required/optional/null/blank values, unknown/duplicate JSON keys, nesting 16/17, NUL/controls and unpaired surrogates. Assert valid values remain typed and invalid values yield errors rather than exceptions.
- [ ] Run `dotnet test tests/RateFreak.UnitTests --no-restore --filter FullyQualifiedName~CollectionValidationTests`; observe RED before adding implementation.
- [ ] Implement entities, request/result contracts, strict field-configuration parsing and validation. Use ordinal matching and invariant date parsing; validate raw JSON strings before GetString can replace malformed surrogate escapes. JSON elements returned from parsing must outlive their documents.
- [ ] Run the focused unit command; expect all cases PASS. Commit this coherent slice on the feature branch.

### Task 2: Collection creation, persistence and visibility reads

**Files:** CollectionsConfiguration, DbContext, DI, CollectionQueries/CollectionCommands, CollectionEndpoints, Program, migration/snapshot, integration test file.
**Consumes:** Task 1 contracts/rules. **Produces:** queries and create command; all three mapped DbSets/tables and migration; HTTP create/list/detail endpoints.

- [ ] Write failing HTTP tests using the existing AccountDatabase/ApiFactory patterns: authenticated create returns 201 and usable Location; default visibility private; owner sees it, strangers receive 404; public collection appears anonymously; ownOnly requires login. Reject empty/invalid names, bad initial fields, unknown members; initial schema creation must be atomic. No email in any response; no-store on success/error including route casing/trailing slash. Assert missing session 401, missing/stale CSRF 400.
- [ ] Run `dotnet test tests/RateFreak.IntegrationTests --no-restore --filter FullyQualifiedName~CollectionsTests`; observe missing-route RED.
- [ ] Implement relational mappings, indexes and constraints (visibility, positive revision/schema version, lengths, supported field types, positions, JSON object types, canonical/custom title shape), cascading ownership and restricted media FK. Generate `AddCollections` with EF CLI, inspect additive Up/dependent-first Down.
- [ ] Implement create transaction, accessible query predicates and repeatable-read multi-query reads. Register services and routes. Public-accessible reads use catalog rate budget (including authenticated calls), owner-only deletion previews use personal-read, writes personal-write. Set no-store through matched endpoint metadata/filter rather than case-sensitive path checks. Use expectedRevision in bodies and `X-Collection-Revision` on every successful existing-collection mutation (including 204).
- [ ] Run focused integration tests and existing PostgresTests; assert migration applies twice and HasPendingModelChanges is false. Commit slice.

### Task 3: Revision-safe metadata and items

**Files:** CollectionCommands, CollectionItemCommands, CollectionQueries, CollectionEndpoints, CollectionsTests.
**Consumes:** Task 2 locked collection storage and queries. **Produces:** metadata update and all item commands/routes.

- [ ] Add failing HTTP tests: custom title required, canonical media must exist and forbids title override, immutable media references, complete data replacement, validation from Task 1, duplicate canonical media returns 409 only within same collection, repeated custom titles allowed, position ordering and stable pagination. Missing/null/nonpositive/stale revisions rejected with no data change. Test all item routes enforce visibility/ownership, all writes require CSRF, and every returned Location resolves.
- [ ] Add a read-race test using a DB interceptor/barrier: visibility update between collection authorization and item query cannot cause an inconsistent authorization/data snapshot. Assert either coherent public snapshot or 404, never mixed authorization and data.
- [ ] Run focused tests and observe RED, then implement commands under transaction/`SELECT ... FOR UPDATE` on owner-filtered collection. Check revision after acquiring lock; validate before changes; increment revision and parent timestamp once. Constraint races must map only recognized duplicate/FK errors to sanitized 409/404, without swallowing other DB failures.
- [ ] Run unit plus focused integration suites; assert null/overflow/NUL/duplicate JSON inputs return 400, never 500; tied-position pagination has no repeats. Commit slice.

### Task 4: Compatible schema evolution and confirmed deletion

**Files:** CollectionFieldCommands, CollectionQueries, CollectionEndpoints, CollectionsTests.
**Consumes:** shared revision/locking protocol and validation. **Produces:** field add/update, deletion-impact and confirmed delete routes.

- [ ] Write failing tests: add optional field; reject required addition to nonempty collection; label/position edits preserve data; reject key/type changes as unknown properties; tighten configuration/required only if all stored items remain valid; any failure rolls back definition, values, revision/schemaVersion/timestamps.
- [ ] Write deletion tests: preview counts populated values only; anonymous/foreign preview denied; missing/false confirmation or missing revision leaves all data intact; stale revision returns 409; confirmed fresh delete removes every key and definition atomically, preserves unrelated data and updates affected item timestamps.
- [ ] Add real concurrent HTTP writes on independent scoped contexts: same revision permits exactly one successful mutation and one 409; field schema edit racing an item insertion cannot persist invalid data; deletion racing an item update cannot erase data under an outdated confirmation. Gate concurrency with barriers rather than sleeps.
- [ ] Run focused tests and observe RED. Implement locked transactions, revalidate existing items for compatible schema changes, and atomically remove JSONB keys during delete. Increment schemaVersion only for successful field changes, revision for all successful mutations.
- [ ] Run unit and focused integration tests; prove all concurrency paths PASS with no 500/deadlock and valid final data. Commit slice.

### Task 5: Database proof, complete verification and handoff

**Files:** CollectionsTests, PostgresTests, README, docs/adr/0006-collections.md, checklist and design status.
**Consumes:** all implemented APIs/storage. **Produces:** verified Phase 5 with repeatable migration and documentation.

- [ ] Add failing DB tests for direct duplicate media/key writes, invalid visibility/positions/JSON root, missing references, restricted catalog deletion and account cascade through fields/items. Add tests for invalid pagination and independent rate budgets/Retry-After on collection reads/writes, public→private access revocation and limits on error responses.
- [ ] Observe RED for any uncovered failure; implement only necessary fixes and run focused tests to GREEN.
- [ ] Run `dotnet test RateFreak.sln --no-restore --verbosity quiet`, `dotnet build RateFreak.sln -c Release --no-restore`, `dotnet ef migrations has-pending-model-changes --project src/RateFreak.Infrastructure --startup-project src/RateFreak.Api`, and `git diff --check`. Inspect all outputs; require zero failed/skipped tests, zero build warnings/errors and no model drift. Audit dependencies using the existing dotnet list command.
- [ ] Apply/reapply migration locally with existing Development configuration; never print credentials. Start API on 5080 and verify health, Swagger/OpenAPI, collection create/item validation/duplicate conflict/confirmed field deletion/visibility using a temporary verification user. Remove only that user's records via account cascade; retain all pre-existing records. Record actual results, not expected results.
- [ ] Document routes/request examples, revision header/conflicts, five field types, data replacement, privacy and permanent deletion semantics. Mark Phase 5 checklist complete only with evidence; keep public-launch requirements and future phases pending.
- [ ] Run one independent whole-branch review, fix important findings with observed RED→GREEN tests, rerun required proof after changes. Finish with branch/commit summary and validation evidence; no merge, push or deployment implied.

## Plan self-review

All approved spec sections map to Tasks 1–5. Shared DTOs/services are defined once
above; task dependencies are sequential. Review Focus cases belong to Tasks 1/3/4.
Use native execution with one final independent review: the tasks share validation,
storage and revision semantics, so separate implementers add coordination cost.
