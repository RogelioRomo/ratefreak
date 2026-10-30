# RateFreak Backend — Architecture & Implementation Plan

> **Purpose:** Implementation specification and reusable context for an LLM coding assistant.  
> **Status:** Proposed design; decisions requiring confirmation are listed below.  
> **Scope:** Entire RateFreak backend, built from scratch in .NET. Frontend is explicitly out of scope for now.

## 1. Product vision

Build a production-ready REST API for a social media-tracking and rating application. Users can discover, rate, review, organize, and discuss movies, TV shows, video games, music albums, books, and future media categories. Users can create custom collections with their own field definitions without modifying backend code. The API integrates with third-party metadata providers to simplify adding new catalog entries.

**The backend is the system of record for the entire application**, not a separate metadata microservice. It owns accounts, media catalog, imports, ratings, reviews, lists/collections, social features, and access control.

### Product capabilities

1. Account registration, sign-in, sessions, profile management, and authorization.
2. Canonical media catalog with explicit media types and stable IDs.
3. Search the local catalog and supported external metadata APIs.
4. Preview and import external media metadata safely.
5. User ratings, reviews, progress/status tracking, and personal library.
6. User-created collections with configurable field definitions and dynamic entries.
7. Social functionality: follows, comments, and optional activity feeds.
8. Admin moderation and catalog correction workflows.
9. Versioned API contracts for a frontend to be built later.

## 2. Engineering principles

- **Modular monolith first.** One deployable API and one PostgreSQL database, with clear module boundaries. Avoid microservices and message brokers initially.
- **Explicit core domain, dynamic custom data.** Media, users, ratings, and reviews have typed domain models; user-defined collection fields and values use metadata + JSONB.
- **External APIs are untrusted dependencies.** Validate, normalize, and isolate their data; do not expose provider DTOs as public contracts.
- **Database-enforced integrity.** Foreign keys, unique constraints, transactional writes, and concurrency handling are required.
- **Secure by default.** Least-privilege authorization, protected secrets, request limits, and input validation.
- **Incremental delivery.** Implement one working vertical slice at a time, with tests and migrations.
- **Avoid speculative complexity.** No generic repository abstraction over every EF Core entity, CQRS framework, event bus, or background infrastructure unless justified.

## 3. Technology stack

- .NET 10, ASP.NET Core Web API (Minimal APIs or controllers; choose one consistently).
- C# with nullable reference types enabled.
- Entity Framework Core with Npgsql provider.
- PostgreSQL 16+ (or a compatible supported version).
- ASP.NET Core Identity or a well-supported OIDC identity provider. **Default proposal:** ASP.NET Core Identity for local accounts, secure cookie or bearer-token strategy selected based on deployment requirements.
- `IHttpClientFactory` and supported .NET HTTP resilience libraries.
- Typed options with startup validation.
- OpenAPI, structured logging, health checks.
- xUnit and Testcontainers for integration tests.
- Docker / Docker Compose for local development and self-hosting.

Verify compatible library versions during implementation rather than assuming package versions.

## 4. Solution layout

```text
RateFreak.sln
src/
  RateFreak.Api/
    Endpoints/
    Contracts/
    Middleware/
    Program.cs
  RateFreak.Application/
    Identity/
    Catalog/
    MetadataImport/
    Ratings/
    Reviews/
    Collections/
    Social/
    Abstractions/
  RateFreak.Domain/
    Identity/
    Catalog/
    Ratings/
    Reviews/
    Collections/
    Social/
  RateFreak.Infrastructure/
    Persistence/
      Configurations/
      Migrations/
    Providers/
      Tmdb/
      MusicBrainz/
      Igdb/
    Authentication/
    Caching/
    DependencyInjection.cs
tests/
  RateFreak.UnitTests/
  RateFreak.IntegrationTests/
  RateFreak.ProviderContractTests/
deploy/
  compose.yaml
.env.example
README.md
```

Dependency direction: `Api -> Application -> Domain`; `Infrastructure -> Application/Domain`. API composes Infrastructure via dependency injection. Domain has no dependency on HTTP, EF Core, or provider SDKs. Organize by feature within layers; do not create an interface for every trivial class.

## 5. Domain model and database

Use UUID identifiers for application entities, UTC timestamps (`timestamptz`), explicit constraints, and migrations. Define naming conventions consistently.

### 5.1 Users and identity

- `users`: ID, handle (unique case-insensitively), display name, profile fields, timestamps, moderation state.
- Identity tables for passwords, login providers, sessions/tokens as appropriate to the chosen authentication approach.
- Never store plaintext passwords, access tokens, or secrets.
- Account deletion and data retention policies must be designed before launch.

### 5.2 Canonical catalog

`media_items`

| Column | Purpose |
|---|---|
| `id` | Internal UUID |
| `media_type` | movie, tv_show, album, game, book |
| `title` | Canonical title |
| `description` | Optional synopsis |
| `release_date` | Nullable date |
| `image_reference` | Optional artwork reference |
| `metadata` | JSONB for optional category-specific metadata |
| `created_at`, `updated_at` | Audit timestamps |

Use stable enum/string mappings. If a media type develops extensive typed fields and relationships, introduce a dedicated subtype table rather than forcing every query into JSONB. Define whether TV seasons/episodes and album editions are separate entities before adding them.

`media_external_ids`

- `id`, `media_item_id` FK, `provider`, `external_id`, `external_url`, `last_synced_at`.
- Unique `(provider, external_id)` **within the provider's identity namespace**; include media subtype in the key if a provider reuses IDs across categories.
- Multiple external mappings may point to one canonical media record.
- Do not merge records on title alone; ambiguous matches require review.

`media_metadata_provenance` (optional initial implementation, recommended before automated refresh)

- Source provider, last update, ownership/override policy per field or group.
- Preserve user/admin corrections during refresh.

### 5.3 Personal library and ratings

`user_media`

- `user_id` FK, `media_item_id` FK, `status`, `started_at`, `completed_at`, timestamps.
- Unique `(user_id, media_item_id)` if each user has one personal library record per catalog item.
- Status options may differ by media type (e.g., watched, reading, playing); define a consistent domain policy.

`ratings`

- `id`, `user_id`, `media_item_id`, `score`, timestamps.
- Unique `(user_id, media_item_id)` for one active rating per user/media item.
- Choose rating scale and precision explicitly; enforce with DB CHECK constraints and application validation.
- Aggregate ratings from this table or a maintained summary; never treat imported provider scores as RateFreak community scores.

`reviews`

- `id`, `user_id`, `media_item_id`, `body`, spoiler flag, visibility/moderation state, timestamps.
- Decide whether multiple reviews per user/media are supported; enforce the selected policy.
- Sanitize/render rich text safely; never trust client HTML.

### 5.4 Dynamic collections

`collections`

- `id`, `owner_user_id`, `name`, `description`, `visibility`, `schema_version`, timestamps.

`collection_fields`

- `id`, `collection_id`, immutable `key`, mutable `label`, `field_type`, `required`, `configuration` JSONB, `position`, timestamps.
- Supported MVP types: text, number, boolean, date, select.
- Validate field count, names, key uniqueness, allowed options, length, numeric bounds, and reserved names.

`collection_items`

- `id`, `collection_id`, nullable `media_item_id` FK, `data` JSONB, `position`, timestamps.
- A row can reference canonical media or represent a fully custom item.
- All writes validate `data` against the collection's current field definitions.
- Define policies for schema evolution: label rename preserves data; key/type change requires explicit migration; field deletion requires confirmation and retention policy.
- Define whether duplicate media references are permitted within a collection; enforce accordingly.
- Index `collection_id`, `media_item_id`, and common ordering columns. Add targeted JSONB indexes only for demonstrated queries.

For an initial MVP, owner-only editing and public/private visibility are sufficient; collaborator permissions can follow later.

### 5.5 Social and moderation

- `user_follows`: follower/followee foreign keys, unique pair, no self-follow.
- `comments`: author, target type/ID or explicit review/collection foreign keys, body, timestamps, moderation state. Prefer explicit relational targets when feasible.
- Optional reactions, notifications, activity feed, and reporting can be added in later phases.
- Admin roles and moderation actions must be authorization-protected and auditable.

## 6. External metadata providers

### 6.1 Initial providers

- **TMDB:** movies and TV shows.
- **MusicBrainz:** music metadata.
- **IGDB:** video games (later phase).
- Books: choose a suitable provider after checking coverage, terms, and rate limits.

Each provider requires custom request/response/authentication logic, but all conform to one application-facing contract. Provider credentials in environment variables enable pre-implemented adapters; **configuration alone cannot integrate an arbitrary new API**.

### 6.2 Provider contract

```csharp
public enum MediaType { Movie, TvShow, Album, VideoGame, Book }

public sealed record ExternalMediaResult(
    string Provider,
    string ExternalId,
    MediaType Type,
    string Title,
    string? Description,
    DateOnly? ReleaseDate,
    string? ImageUrl,
    JsonElement Metadata);

public interface IMediaProvider
{
    string Name { get; }
    IReadOnlySet<MediaType> SupportedTypes { get; }

    Task<IReadOnlyList<ExternalMediaResult>> SearchAsync(
        string query, MediaType type, CancellationToken cancellationToken);

    Task<ExternalMediaResult?> GetDetailsAsync(
        string externalId, MediaType type, CancellationToken cancellationToken);
}
```

The above is a starting interface, not an immutable contract. Consider structured search options (limit, page, locale) and a provider error model as requirements become clear. Keep provider-specific DTOs and HTTP clients in Infrastructure. Never accept arbitrary provider URLs from clients.

### 6.3 Search and import flow

1. Client searches local catalog; optionally searches enabled external providers.
2. Search service selects compatible providers and executes requests with bounded concurrency.
3. Adapters normalize results; response includes provider-specific success/failure status.
4. Client requests details for one selected result.
5. Client submits `{ provider, externalId, mediaType }` for import.
6. Server re-fetches or validates authoritative provider details; never blindly persists client-supplied metadata.
7. Resolve existing external mapping; otherwise create canonical media and mapping in a transaction.
8. Database unique constraints prevent races; on a uniqueness conflict, fetch the existing record.
9. Return canonical media ID; optionally add to the authenticated user's library through a separate explicit operation.

**Idempotency:** Repeating the same import should return the existing canonical record. Cross-provider duplicate detection is separate from same-provider identity matching.

### 6.4 Provider reliability and legal considerations

- Per-provider timeouts and cancellation.
- Bounded retries for transient failures only; respect `Retry-After` and quotas.
- Partial results if one provider fails; distinguish no matches from unavailable providers.
- Cache search responses where provider terms allow, with per-provider TTLs.
- Do not assume that images or metadata may be permanently copied or redistributed. Review provider licensing, attribution, caching, and image usage policies before implementation.
- Structured logs, metrics, and health diagnostics; never log API tokens.

## 7. API design (proposed v1)

Use `/api/v1`, JSON contracts, pagination, consistent error envelopes (Problem Details), and OpenAPI. Finalize endpoint naming before coding.

### Identity and users

| Method | Route | Purpose |
|---|---|---|
| POST | `/api/v1/auth/register` | Register |
| POST | `/api/v1/auth/login` | Sign in |
| POST | `/api/v1/auth/logout` | Sign out / revoke session |
| GET | `/api/v1/users/me` | Current user |
| PATCH | `/api/v1/users/me` | Update profile |
| GET | `/api/v1/users/{id}` | Public profile |

Authentication endpoints depend on the chosen session/token strategy. Add email verification, password reset, and abuse protections before public launch.

### Catalog and provider integration

| Method | Route | Purpose |
|---|---|---|
| GET | `/api/v1/media` | Local catalog search/filter |
| GET | `/api/v1/media/{id}` | Canonical item details |
| GET | `/api/v1/media/external/search?q=&type=` | Search enabled providers |
| GET | `/api/v1/media/external/{provider}/{type}/{externalId}` | Preview external details |
| POST | `/api/v1/media/import` | Import selected external record |
| POST | `/api/v1/media/{id}/refresh` | Authorized metadata refresh |
| GET | `/api/v1/media/providers` | Provider capabilities/status |

### User media, ratings, reviews

| Method | Route | Purpose |
|---|---|---|
| GET | `/api/v1/users/me/library` | Personal library |
| PUT | `/api/v1/users/me/library/{mediaId}` | Upsert library status |
| DELETE | `/api/v1/users/me/library/{mediaId}` | Remove from library |
| PUT | `/api/v1/media/{mediaId}/rating` | Set current user's rating |
| DELETE | `/api/v1/media/{mediaId}/rating` | Remove rating |
| GET | `/api/v1/media/{mediaId}/reviews` | List reviews |
| POST | `/api/v1/media/{mediaId}/reviews` | Create review |
| PATCH | `/api/v1/reviews/{id}` | Edit owned review |
| DELETE | `/api/v1/reviews/{id}` | Delete owned review |

### Collections

| Method | Route | Purpose |
|---|---|---|
| POST | `/api/v1/collections` | Create collection and schema |
| GET | `/api/v1/collections` | List accessible collections |
| GET | `/api/v1/collections/{id}` | Details and schema |
| PATCH | `/api/v1/collections/{id}` | Update metadata |
| POST | `/api/v1/collections/{id}/fields` | Add custom field |
| PATCH | `/api/v1/collections/{id}/fields/{fieldId}` | Edit field |
| DELETE | `/api/v1/collections/{id}/fields/{fieldId}` | Remove field |
| POST | `/api/v1/collections/{id}/items` | Add item |
| GET | `/api/v1/collections/{id}/items` | List items |
| PATCH | `/api/v1/collections/{id}/items/{itemId}` | Edit item |
| DELETE | `/api/v1/collections/{id}/items/{itemId}` | Remove item |

### Social (later phase)

Follows, comments, and moderation endpoints should be specified when their underlying rules are confirmed. Avoid exposing unfinished routes.

## 8. Authentication, authorization, and security

- Select a browser/mobile-compatible authentication strategy deliberately. For browser sessions, prefer secure HttpOnly cookies with CSRF protection where applicable; for bearer tokens, define secure issuance, expiration, rotation, and revocation.
- Use ASP.NET Core Identity or a trusted OIDC provider; do not build password hashing or authentication protocols from scratch.
- Require authentication for personal writes and catalog import; use roles/policies for admin and moderation actions.
- Enforce resource ownership and collection visibility on **every** relevant query and mutation.
- Rate-limit public searches, login, registration, and imports separately.
- Validate all inputs, apply body-size limits, and restrict outbound HTTP to configured provider hosts.
- Never store third-party secrets in client-accessible tables, logs, or responses.
- Protect connection strings and provider tokens using environment secrets or a secret manager.
- Plan backups, restoration drills, account deletion, and audit logs.

## 9. Configuration

Example environment variables (ASP.NET Core maps `__` to nested configuration keys):

```dotenv
ASPNETCORE_ENVIRONMENT=Development
ConnectionStrings__Default=Host=localhost;Database=ratefreak;Username=ratefreak;Password=change-me

Providers__Tmdb__Enabled=true
Providers__Tmdb__AccessToken=

Providers__MusicBrainz__Enabled=true
Providers__MusicBrainz__UserAgent=RateFreak/1.0 (contact: admin@example.invalid)

Providers__Igdb__Enabled=false
Providers__Igdb__ClientId=
Providers__Igdb__ClientSecret=

MediaSearch__TimeoutMilliseconds=4000
MediaSearch__MaxResults=10
```

The User-Agent above is a placeholder: use real, appropriate contact information in deployment. Do not commit real secrets. Maintain `.env.example` and document each setting. Use typed options with startup validation: enabled providers must have their required configuration, while disabled providers must not be invoked.

## 10. Data consistency and concurrency

- Use EF Core migrations under source control; no automatic destructive schema changes on startup.
- Wrap multi-table imports in a database transaction.
- Enforce unique provider mappings in PostgreSQL; handle concurrent import conflicts.
- Use optimistic concurrency or explicit version checks for editable resources where simultaneous updates matter.
- Normalize user handles for uniqueness; define case-insensitive comparison.
- Define delete behavior and cascade rules for every relationship.
- Do not overwrite user-generated content during metadata refresh.
- Treat imported provider ratings as external metadata, never as local user ratings.

## 11. Observability and operations

- Structured logs with correlation/request IDs.
- Metrics: HTTP latency, provider latency/error rate, import conflicts, cache hit rate, database health.
- Readiness/liveness endpoints with appropriate dependency checks.
- Database migration procedure, backups, and tested restore instructions.
- Dockerfile, Compose example, and a deployment README.
- Secrets injected at deployment, not baked into images.
- HTTPS at the ingress/reverse proxy; configure forwarded headers and trusted proxies.

## 12. Testing and acceptance criteria

### Unit tests

- Media normalization and validation.
- Provider selection by type and enabled status.
- Rating scale and collection schema validation.
- Ownership and authorization policies.
- Import orchestration with mocked providers.

### Provider contract tests

- Correct request authentication and query encoding.
- Mapping representative API responses to canonical DTOs.
- Handling missing fields, malformed responses, rate limits, and timeouts.
- No real provider credentials required for normal CI.

### Integration tests

- EF Core migrations against real PostgreSQL via Testcontainers.
- Foreign keys, unique constraints, and transaction behavior.
- Concurrent imports of the same external record.
- Collection field validation and schema changes.
- Access-control enforcement across users.

### MVP definition of done

- API starts from documented local setup.
- Database migrations apply to an empty PostgreSQL instance.
- A user can register and authenticate securely.
- One configured provider can search and preview media.
- Importing a media record twice does not create duplicates.
- Authenticated users can add imported media to their library and rate it.
- Users can create a collection with custom fields and add validated items.
- Unauthorized writes are rejected.
- OpenAPI and automated tests are available.
- Production secrets are not exposed.

## 13. Implementation phases

### Phase 0 — Confirm decisions

Finalize identity strategy, media-type granularity, rating scale, review cardinality, collection visibility, import permissions, and first provider. Record decisions as short ADRs.

### Phase 1 — Foundation

Create solution/projects, dependency injection, EF Core setup, PostgreSQL Compose configuration, migrations, logging, Problem Details, health checks, OpenAPI, and test infrastructure.

### Phase 2 — Identity and catalog

Implement users/authentication, media entities, external ID mapping, catalog read endpoints, ownership/authorization policies, and tests.

### Phase 3 — First provider and ingestion

Implement TMDB adapter, normalized search/preview, transactional import, conflict handling, resilience, and provider contract tests.

### Phase 4 — Personal library and ratings

Implement user-media status, ratings, review CRUD, aggregate calculations, and permission tests.

### Phase 5 — Dynamic collections

Implement collection metadata, field definitions, JSONB item storage, runtime validation, schema-change rules, visibility, and tests.

### Phase 6 — Expand providers and social features

Add MusicBrainz, then IGDB and book provider as needed; implement follows, comments, moderation, and optional feeds.

### Phase 7 — Production hardening

Load/performance tests, caching based on observed bottlenecks, provider licensing review, audit logs, backups/restore drill, deployment documentation, and security review.

**Each phase should finish with a runnable application, migrations, tests, and updated documentation.**

## 14. Decisions to resolve before coding

1. **Authentication:** ASP.NET Core Identity with cookies, token-based authentication, or an external OIDC provider?
2. **Catalog taxonomy:** Are TV seasons/episodes and album editions first-class records?
3. **Ratings:** 1–5, 0–10, half-star increments, or another scale?
4. **Reviews:** One review per user/media or multiple reviews?
5. **Import policy:** Can every authenticated user import into the shared catalog, or only moderators/admins?
6. **Collection items:** May a collection contain mixed media types and entirely custom entries?
7. **Privacy:** Public/private collections only for MVP, or unlisted/shared collections too?
8. **Media artwork:** Link externally, proxy images, or store copies (subject to licensing)?
9. **Hosting:** Single self-hosted Docker deployment initially, or multi-instance production from day one?
10. **Initial delivery:** Confirm TMDB as the first provider and movie/TV as the first media types.

Until decided, implement conservative defaults and explicitly document assumptions; do not silently invent product rules.

---

## 15. Instructions for an LLM implementation assistant

Use this document as the source specification for building the RateFreak backend from scratch.

**Working rules:**

1. Do **not** use Payload CMS, Next.js, or an existing RateFreak application as a backend dependency.
2. Build a standalone ASP.NET Core .NET 10 API with PostgreSQL and EF Core.
3. Before coding, identify unresolved decisions in Section 14. Ask only blocking questions; state safe defaults for the rest.
4. Propose a short implementation plan for **one phase at a time**. Do not generate the entire system in a single response.
5. Explain major architectural decisions and tradeoffs briefly, then provide concrete commands and complete code for the current phase.
6. Keep domain and application logic independent from external provider APIs and EF Core details where practical.
7. Use secure defaults, proper cancellation, typed configuration, structured errors, and database constraints.
8. Include migrations and automated tests with each implemented vertical slice.
9. Do not introduce speculative microservices, event buses, complex generic frameworks, or unnecessary dependencies.
10. Maintain a changelog/implementation checklist showing what is complete, what is pending, and any deviations from this plan.
11. If a provider's API details, terms, or current .NET package APIs are uncertain, verify them against official documentation before implementing.
12. Prioritize code that compiles, tests that run, and small reviewable changes.

**Suggested first prompt:**

> We are building RateFreak from scratch as a standalone .NET 10 Web API. Read `ratefreak-plan.md` as the architecture specification. Start with Phase 0 and Phase 1 only. Identify the blocking decisions, recommend sensible defaults, then guide me through creating the solution, project references, PostgreSQL Docker Compose setup, EF Core DbContext and first migration, OpenAPI, error handling, and basic tests. Keep the solution simple, modular, and production-minded. Do not implement the frontend or external media providers yet.
