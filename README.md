# RateFreak backend

Standalone .NET 10 modular monolith. Phase 1 foundation and Phase 2 local
accounts/catalog reads, Phase 4 personal library/ratings/reviews, and Phase 5
dynamic collections are implemented from `ratefreak-backend-plan.md`.
Next.js will be hosted separately and act as a thin browser-facing proxy; all
accounts, authorization and business logic remain in .NET. The frontend/proxy and
remaining business features are not implemented. The Phase 3 TMDB adapter is
implemented; local TMDB activation and live verification completed on 2026-10-29.
The checked-in default remains disabled until a server-side token is configured.

## 1. Prerequisites

- A current .NET 10 SDK (`global.json` accepts 10.0 feature bands).
- Docker Engine/Desktop with Compose and permission to use its daemon.
- NuGet connectivity for package and local tool restore.

From the repository root:

```bash
dotnet --info
docker info
docker compose version
dotnet restore RateFreak.sln
dotnet tool restore
dotnet build RateFreak.sln
```

Package versions are pinned in project files. EF Core/ASP.NET packages use 10.0.2
and Npgsql EF uses 10.0.0, a compatible set available in the implementation
environment. Explicit references raise the transitive Microsoft.OpenApi dependency
to 2.7.5 and the integration tests' SSH.NET dependency to 2026.0.0 to address
GHSA-v5pm-xwqc-g5wc, GHSA-mggc-4xg6-vcxf and GHSA-q939-rpr3-3284.
These are not claimed to be the latest security patches. Before
production, update compatible stable 10.x packages and the EF tool together, run
`dotnet list RateFreak.sln package --vulnerable --include-transitive`, and rerun
the complete verification below. Do not disable vulnerability auditing in CI.

## 2. Start local PostgreSQL

```bash
cp .env.example .env
```

Edit `.env` and replace `POSTGRES_PASSWORD` with a long random local password
(for example, generate one with `openssl rand -hex 32`). Keep `.env` untracked.
Then:

```bash
docker compose --env-file .env -f deploy/compose.yaml up -d --wait
docker compose --env-file .env -f deploy/compose.yaml ps
```

PostgreSQL 17 binds only to `127.0.0.1:55432`, with a persistent named volume.
The existing host PostgreSQL listener uses 5432; RateFreak uses 55432 on the host
and 5432 inside its container. Change `POSTGRES_PORT` if 55432 is occupied. Changing the password in `.env` does
not change the password of an already initialized database. The Compose account
is for local development; production requires separate least-privilege runtime
and migration credentials, backups and a tested restore procedure.

## 3. Configure the API

.NET does **not** read `.env` automatically. Compose uses it for PostgreSQL only.
Set the connection string with the same password and port you configured above:

```bash
dotnet user-secrets set 'ConnectionStrings:Default' 'Host=localhost;Port=55432;Database=ratefreak;Username=ratefreak;Password=YOUR_LOCAL_PASSWORD' --project src/RateFreak.Api
export ASPNETCORE_ENVIRONMENT=Development
```

User secrets are a development convenience, not encrypted production storage.
In deployment, inject `ConnectionStrings__Default` from your secret manager.
Do not commit connection strings or place credentials in issue reports/logs.
Missing or malformed configuration fails startup with a sanitized validation message.
A valid but unreachable database permits startup: readiness reports the failure.

Configuration:

| Key | Required | Purpose |
| --- | --- | --- |
| `ConnectionStrings__Default` | Yes | PostgreSQL connection string; Development can use user secrets instead |
| `ASPNETCORE_ENVIRONMENT` | No | Defaults to Production; Development exposes OpenAPI/Swagger UI and loads user secrets |
| `ASPNETCORE_URLS` | No | Listener binding; launch profile uses `http://localhost:5080` |
| `AllowedHosts` | In deployment | Defaults to `localhost;127.0.0.1`; set your API hostname in production |
| `Authentication__DataProtectionKeysPath` | Outside Development | Absolute durable directory for cookie/CSRF encryption keys; Development defaults to ignored `src/RateFreak.Api/.local/keys` |
| `POSTGRES_DB/USER/PASSWORD/PORT` | Compose only | Local database settings from `.env` |

## 4. Apply the first migration explicitly

```bash
dotnet ef database update --project src/RateFreak.Infrastructure --startup-project src/RateFreak.Api
```

`InitialFoundation` is the intentionally empty original baseline. The generated
`AddIdentityAccounts` migration adds Identity's users/roles/claims/login/token
relationships, UUID IDs, UTC timestamps, and unique normalized email/handle indexes.
Both migrations apply to an empty database, or the second extends an existing
foundation database. They are tested against real PostgreSQL. Application startup
never calls `Migrate` or `EnsureCreated`.

For future entity changes, generate and inspect a migration, then apply it:

```bash
dotnet ef migrations add DescriptiveName --project src/RateFreak.Infrastructure --startup-project src/RateFreak.Api --output-dir Persistence/Migrations
dotnet ef migrations has-pending-model-changes --project src/RateFreak.Infrastructure --startup-project src/RateFreak.Api
dotnet ef migrations script --idempotent --project src/RateFreak.Infrastructure --startup-project src/RateFreak.Api --output migration.sql
```

Review SQL before production application. Never remove a deployed migration or
run destructive rollback commands against production without a recovery plan.

## 5. Run and inspect

```bash
dotnet run --project src/RateFreak.Api --launch-profile http
```

In another terminal:

```bash
curl -i http://localhost:5080/health/live
curl -i http://localhost:5080/health/ready
curl -i http://localhost:5080/openapi/v1.json
curl -i http://localhost:5080/api/v1/does-not-exist
```

- `/health/live`: 200 while the process can serve requests; no database access.
- `/health/ready`: 200 only when PostgreSQL is reachable and migrations are applied;
  otherwise 503. Health bodies expose no connection details.
- `/openapi/v1.json`: Development only; documents account routes as JSON.
- `/swagger`: Development-only interactive Swagger UI, using that same document.
  The page and its bundled assets return 404 outside Development. `/` still has no homepage.

Open http://localhost:5080/swagger and expand an operation, then choose **Try it out**.
Register with a valid email, password, handle and display name; log in through
`POST /api/v1/auth/login`, then call `GET /api/v1/users/me` from the same browser.
Swagger UI uses browser session cookies and automatically obtains a fresh CSRF
request token before each same-origin API mutation, including register/login/logout.
No manual token copying or bearer authorization is needed. Normal rate limits still
apply, and these requests modify your local database. External requests receive no
CSRF token from this interceptor. Only the UI package is added; built-in .NET OpenAPI
remains the document generator.
- Missing routes return 404 Problem Details; unexpected exceptions return sanitized
  500 Problem Details. Both include a server-generated `traceId` correlated with
  JSON log scopes. Exception details belong in protected server logs only.

Local HTTP is for development. Production requires HTTPS at a trusted ingress,
explicit proxy/forwarded-header settings, host configuration and private health
monitoring. No trust-all proxy headers or permissive CORS policy is installed.
Outside Development, startup requires an absolute
`Authentication__DataProtectionKeysPath`. Mount it durably with access restricted
to the API user. Configure key encryption at rest or suitably protected storage
before production. Development persists keys in the ignored `.local/keys` directory;
losing keys invalidates sessions. Environment-specific application names keep
Development and Production key purposes separate. The proxy boundary requirements
are below; HTTPS and production ingress/proxy configuration remain deployment work.

## 6. Run tests

```bash
dotnet test tests/RateFreak.UnitTests
dotnet test tests/RateFreak.IntegrationTests --filter 'Category!=Database'
dotnet test RateFreak.sln
```

Unit tests cover configuration validation without exposing supplied credentials.
API tests cover health, OpenAPI exposure, 404/500 responses and key configuration.
Real PostgreSQL integration tests cover migration history/idempotency, normalized
uniqueness including concurrent registration and direct database writes, the account
flow, secure cookies, session persistence across server restart, CSRF, logout replay,
validation, lockout, and separate rate-limit budgets. Docker is required for the
full suite; no provider credentials are needed. Database tests fail rather than
silently skip if Docker is unavailable.

## Accounts: Phase 2 first slice

| Method | Route | Contract |
| --- | --- | --- |
| GET | `/api/v1/auth/csrf` | Returns `{ requestToken }`, sets antiforgery cookie |
| POST | `/api/v1/auth/register` | `{ email, password, handle, displayName }` -> 201 account profile |
| POST | `/api/v1/auth/login` | `{ email, password }` -> 200 profile + session cookie |
| POST | `/api/v1/auth/logout` | Requires session + CSRF; 204, revokes **all** account sessions |
| GET | `/api/v1/users/me` | Requires session; returns private profile including email |

Registration does not log in automatically. Fetch `/auth/csrf` first, retain the
cookie, and send the returned token as `X-CSRF-TOKEN` on register/login/logout.
Fetch a fresh token after login/logout because it is bound to the current identity.
All examples are relative to `/api/v1`; the exact table routes are authoritative.

Handles use 3–30 ASCII letters/digits/underscores, unique case-insensitively.
Display names use 1–80 trimmed characters without control characters. Emails are
trimmed and validated, with both original and normalized forms bounded to 254
characters, and unique after normalization.
Passwords use 12–128 characters, uppercase/lowercase/digit and at least four unique
characters. Passwords are hashed by Identity and never returned. Login accepts email,
not handle. Unknown accounts, wrong passwords and lockout return the same generic
401. Five failed attempts lock the account for 15 minutes.

Sessions are non-persistent browser cookies with an eight-hour non-sliding ticket.
They are HttpOnly, host-only, SameSite=Lax and Secure outside Development. Production
uses `__Host-ratefreak.session`; local Development HTTP uses `ratefreak.session`.
Logout changes the account security stamp; validation on every authenticated request
rejects previously copied cookies and logs out other devices. This costs a database
lookup per authenticated request. A missing session returns 401 rather than an HTML
redirect. Auth and `/users/me` responses use `Cache-Control: no-store`.

Abuse limits: registration 5/IP/hour, login 10/IP/minute, CSRF issuance 30/IP/minute;
429 Problem Details includes `Retry-After`. Invalid requests consume the budget.
Limits are in-memory per API instance; trusted forwarding and per-browser rate
limits at the future Next.js proxy must be configured before public deployment.
Kestrel request bodies are limited to 64 KiB. Passwords are bounded before hashing.

The future Next.js proxy must forward the session **and** antiforgery cookies,
relay all `Set-Cookie` updates/deletions, forward `X-CSRF-TOKEN`, reject cross-origin
browser mutation requests, and never cache private/auth responses. It must not
invent a user ID header; .NET always authenticates/authorizes the actual cookie.
Do not expose a generic forwarding endpoint accepting arbitrary target URLs.
No permissive CORS policy or proxy implementation is included here.

This slice is for local/private development: email ownership is not verified yet.
Before public launch, implement email verification and recovery/password reset,
finish HTTPS/ingress and key-storage hardening, and review abuse controls/account
retention. Profile editing, public profiles, admin endpoints, TMDB token activation
and additional media providers remain pending.
See `docs/adr/0004-local-accounts.md` for the recorded implementation defaults.

## Structure and project references

```text
RateFreak.sln
src/RateFreak.Api             -> Application, Infrastructure (composition)
src/RateFreak.Application     -> Domain
src/RateFreak.Domain          -> no framework/package dependencies
src/RateFreak.Infrastructure  -> Application, Domain; EF Core/Npgsql
tests/RateFreak.UnitTests
tests/RateFreak.IntegrationTests
deploy/compose.yaml
```

Application contains plain account contracts, input rules and an account-service
interface. Infrastructure implements that interface with ASP.NET Identity; Identity
entities and EF mappings stay there. Domain contains the canonical catalog models and remains free of framework dependencies. No generic repository
or CQRS framework is introduced. Provider contract tests exercise the TMDB adapter
without live credentials.

The solution was created with `dotnet new sln --name RateFreak --format sln` and
`dotnet sln RateFreak.sln add` for the six projects. References are explicit in
`.csproj` files, so normal restore/build reproduces the dependency graph.

## Stop the local database

```bash
docker compose --env-file .env -f deploy/compose.yaml down
```

This preserves data. Adding `--volumes` deletes the local database volume.

See `docs/adr/` for decisions and `docs/implementation-checklist.md` for current
verification results and remaining work.


## Canonical catalog (Phase 2)

Public reads are available in Development Swagger at http://localhost:5080/swagger:

- `GET /api/v1/media?q=planet&type=movie&page=1&pageSize=20`
- `GET /api/v1/media/{id}`

Supported types are `movie` and `tv_show`; TV seasons/episodes are deferred. Search
matches literal, case-insensitive title substrings. Search text is trimmed and limited
to 200 characters; null characters are rejected. Page defaults to 1 (maximum 10000),
page size to 20 (maximum 100). Results contain `items`, `page`, `pageSize`, and
`totalCount`, ordered by title then UUID. Count and page are separate queries, so
concurrent future imports can change the count between requests. Lists omit metadata
and descriptions; details include both and the external identity mappings. Invalid
queries return 400 Problem Details, missing/malformed IDs 404. Catalog reads share
an independent 60/IP/minute budget with 429 and Retry-After on exhaustion.

`AddCanonicalCatalog` adds `media_items` and `media_external_ids`; apply it using the
existing explicit migration procedure. PostgreSQL enforces media types, nonblank
titles, JSON-object metadata, foreign keys and unique provider/namespace/external-ID
triples. Namespaces distinguish, for example, TMDB movie 42 from TV 42. A canonical
record can have several provider mappings. Titles are never used as unique identity.
Deletion is restricted while external mappings exist.

The catalog starts empty and contains no demo records. TMDB imports are available through the authenticated Phase 3 flow below; there is
no general-purpose catalog write endpoint. No external image or metadata
URL is fetched by this slice. Current search uses bounded offset pagination and
substring scans; add search indexes/another pagination strategy when measured
volume requires them. Email verification and password recovery remain pending
before public launch.


## TMDB integration (Phase 3)

TMDB's developer API is free for **non-commercial** use with attribution. Commercial
projects must arrange a license with TMDB; do not assume a monetized RateFreak is
covered. Source: [TMDB FAQ](https://developer.themoviedb.org/docs/faq), checked
2026-10-29. Review the full terms accepted in your TMDB account before deployment;
the terms page could not be retrieved by the documentation crawler. Long-term
metadata/image usage and commercial rights require that review. Search caching,
artwork copying/proxying and automatic metadata refresh are not implemented.

Attribution: **This product uses the TMDB API but is not endorsed or certified by
TMDB.** Source: [The Movie Database](https://www.themoviedb.org). The future
frontend must display this notice and an [approved TMDB logo](https://www.themoviedb.org/about/logos-attribution)
in its About/Credits section, with RateFreak's branding more prominent. The backend
provider-capability endpoint exposes the notice and source link; this does not
replace the future UI's logo/attribution requirement.

Create a TMDB account and request developer API access in
[account API settings](https://www.themoviedb.org/settings/api). Use the **API Read
Access Token**, not a user session token or browser-side key. Store it only in
.NET user-secrets during development or the production secret store. Do not paste
it into chat, source, `.env.example`, Swagger requests, or a shell argument saved
in history. Configure these .NET keys:

| Key | Default / requirement |
| --- | --- |
| `Providers:Tmdb:Enabled` | `false`; enable only once the token is configured |
| `Providers:Tmdb:AccessToken` | Required when enabled; secret |
| `Providers:Tmdb:Language` | `en-US`; two lowercase language letters, hyphen, two uppercase region letters |
| `Providers:Tmdb:TimeoutMilliseconds` | `4000`; allowed 500–15000 |

For agent-assisted local activation without shell commands, save only your token
in the ignored `.local/tmdb-token.txt` file (prepared with owner-only permissions)
and tell the agent it is ready. This file is an input handoff, not application
configuration: the agent will move the value into .NET user-secrets, enable TMDB,
remove the handoff file, restart and verify live access without printing the token.

Environment variables use `__` instead of `:`. .NET does not automatically load
`.env`. Invalid enabled-provider configuration fails startup with a sanitized
message. The adapter's API origin is fixed HTTPS and redirects are disabled. No
client-supplied URL is fetched and the token appears only in a redacted bearer
header. Provider availability does not affect local catalog health/readiness.

In [Development Swagger](http://localhost:5080/swagger):

1. `GET /api/v1/media/providers` lists capabilities and enabled configuration state
   (not a live TMDB health check; never returns the token).
2. `GET /api/v1/media/external/search?q=Alien&type=movie&page=1` searches TMDB.
   Type is `movie` or `tv_show`, page 1–500, trimmed search 1–200 characters without
   controls. Results contain at most 20 normalized items, excluding adult records.
3. `GET /api/v1/media/external/tmdb/movie/11` previews normalized details.
4. Register/login, then `POST /api/v1/media/import` with
   `{ "provider": "tmdb", "externalId": "11", "mediaType": "movie" }`.
   Swagger supplies fresh CSRF automatically. Extra JSON properties are rejected.

Imports re-fetch details from TMDB rather than trusting preview/client fields.
New imports return 201 and a canonical detail Location; repeats return 200 and the
same `mediaId` without re-fetching or overwriting local corrections. Creating the
item and provider mapping is transactional, including concurrent imports. Movie
and TV IDs use separate namespaces. No title-based merging or personal-library
write occurs. No schema migration was needed beyond Phase 2's catalog tables.

Missing provider details return 404. Disabled/busy/unavailable providers return 503,
upstream rate limits return 503 plus Retry-After, malformed upstream data returns
502, and upstream timeouts return 504. These are sanitized Problem Details, never
silent empty results or raw upstream errors. Public external search/preview share
20/IP/minute; imports allow 10/account/minute. Limits are per API instance.
Outbound work is capped at four concurrent operations without a queue, one retry
for transient failures, 1 MiB responses and the configured overall timeout. 429
retries wait for Retry-After only when it permits a wait of at most one second;
longer waits are passed back. No quota or SLA is assumed from TMDB.

Automated contract tests use representative fake HTTP responses; integration tests
run imports against real PostgreSQL including concurrent duplicate requests. No
real credentials or live TMDB calls are needed for normal test runs. **Local live TMDB
verification passed on 2026-10-29** (search, preview, protected import and repeat identity). Email verification,
password recovery and deployment hardening remain required before public launch.


## Personal library, ratings and reviews (Phase 4)

All mutations use the existing session cookie and `X-CSRF-TOKEN` flow. Swagger
supplies fresh tokens automatically. Authenticated operations derive ownership
from the session; request bodies cannot specify a user ID and reject extra fields.

| Method | Route | Behavior |
| --- | --- | --- |
| GET | `/api/v1/users/me/library` | Your private paginated library; optional `status` and `type` |
| PUT | `/api/v1/users/me/library/{mediaId}` | Replace tracking status and optional dates |
| DELETE | `/api/v1/users/me/library/{mediaId}` | Remove your entry; 204 even if absent |
| GET | `/api/v1/media/{mediaId}/rating` | Your rating; 404 when unrated |
| PUT | `/api/v1/media/{mediaId}/rating` | Set/update your rating |
| DELETE | `/api/v1/media/{mediaId}/rating` | Remove your rating; 204 even if absent |
| GET | `/api/v1/media/{mediaId}/ratings` | Public local community count and average |
| GET | `/api/v1/media/{mediaId}/reviews` | Public reviews, newest first |
| POST | `/api/v1/media/{mediaId}/reviews` | Create your review; 201 or 409 if already reviewed |
| GET | `/api/v1/reviews/{id}` | Public review details |
| PATCH | `/api/v1/reviews/{id}` | Replace your review body and spoiler flag |
| DELETE | `/api/v1/reviews/{id}` | Delete your review |

Library PUT example:

```json
{ "status": "completed", "startedAt": "2026-10-01", "completedAt": "2026-10-02" }
```

Statuses are `planned`, `in_progress`, `completed`, `paused`, `dropped`, for movies
and TV alike. Dates are optional date-only values; completion cannot precede start.
PUT replaces all fields, so omitted dates become null. Dates do not automatically
change status and status changes do not infer dates. No episode/progress counters.

Rating PUT: `{ "score": 4.5 }`. Scores run **0–5 inclusive, in 0.5 steps**.
Omitted/null scores are invalid; zero is a real rating. One current rating per
user/media. Public aggregate: `{ "count": 2, "average": 4.25 }`; averages need not
be half steps. Empty aggregates have `count: 0`, `average: null`. Provider scores
are never included. Setting a rating does not require a completed library status.

Review POST/PATCH: `{ "body": "My review", "isSpoiler": false }`. One public,
editable review per user/media; body required for both POST and PATCH. Trimmed
body length 1–10000 UTF-16 code units. Tab/newline allowed, other controls and
malformed Unicode rejected. Omitted `isSpoiler` defaults to false, including edits.
Text is stored literally: clients must render it as text, never insert raw HTML.
Spoiler content is returned with its flag; clients decide when to reveal it.
Public authors expose ID, handle and display name, without email. Editing/deleting
another user's review returns 404, as does an unknown review.

Libraries, ratings and reviews have independent lifecycles. Removing a library
entry does not remove the rating or review; rating/review creation does not add
a library entry. Concurrent library/rating PUTs upsert one record; duplicate review
creation yields 201/409. Account deletion cascades these records at the database
level; no account-deletion endpoint is introduced here. Catalog deletion is restricted.

Both lists use `page` 1–10000, `pageSize` 1–100 (default 1/20). Stable creation-time
ordering with UUID tie-breakers; total count and page use one consistent DB snapshot.
Private reads send no-store and allow 60/account/minute; writes share 30/account/minute.
Public reviews/aggregates share the existing 60/IP/minute catalog budget. A 429
includes Retry-After. Limits remain per instance. Authenticated mutations require CSRF.

Apply `AddPersonalMedia` with the EF database update command above. The additive
migration creates `user_media`, `ratings`, `reviews`, constraints and indexes only.
Phase 4 adds no frontend, private reviews or moderation workflow; collections are
implemented separately in Phase 5 below.
Email ownership/recovery and production hardening remain required before public launch.


## Dynamic collections (Phase 5)

Collections can combine catalog media and custom entries, with `text`, `number`,
`boolean`, `date` and `select` fields. Visibility is `private` by default or `public`;
only the owner can edit. Private collections return 404 to everyone else. Public
lists include public collections plus the signed-in caller's private collections;
`ownOnly=true` lists only your collections and requires sign-in. All collection
responses use no-store because visibility may change. Text must be rendered as text.

Create a collection with `POST /api/v1/collections`:

```json
{
  "name": "Movie night",
  "visibility": "private",
  "fields": [
    { "key": "watched_with", "label": "Watched with", "fieldType": "text",
      "required": false, "configuration": { "maxLength": 200 }, "position": 0 }
  ]
}
```

Creation returns 201 with Location and `{ collection, fields }`. The collection
contains its owner summary, metadata, `revision` and `schemaVersion` (both start at 1).
Every write to an existing collection requires `expectedRevision`; a stale value
returns 409, without applying changes. Every successful mutation returns
`X-Collection-Revision` with the new revision, including 204 responses. Fetch the
collection again after a conflict and review its current state before retrying.
All writes use the existing cookie and fresh CSRF token flow; unknown body fields
are rejected, including client-supplied ownership and immutable properties.

| Method | Route under `/api/v1/collections` | Behavior |
| --- | --- | --- |
| POST | `/` | Create metadata and initial fields atomically |
| GET | `/` | List accessible collections; optional `ownOnly`, `page`, `pageSize` |
| GET | `/{id}` | Metadata, ordered fields and current revision |
| PATCH | `/{id}` | Replace name, description and visibility with expectedRevision |
| POST | `/{id}/fields` | `{ expectedRevision, field: { ...definition } }` |
| PATCH | `/{id}/fields/{fieldId}` | Replace label, required, configuration and position |
| GET | `/{id}/fields/{fieldId}/deletion-impact` | Owner-only populated-item count and revision |
| DELETE | `/{id}/fields/{fieldId}?expectedRevision=N&confirm=true` | Permanently remove field and its values |
| GET | `/{id}/items` | Paginated accessible items, ordered by position then UUID |
| POST | `/{id}/items` | Add canonical media or custom entry |
| GET | `/{id}/items/{itemId}` | Accessible entry details |
| PATCH | `/{id}/items/{itemId}` | Replace custom title, data and position |
| DELETE | `/{id}/items/{itemId}?expectedRevision=N` | Remove entry; 204 with new revision |

Add a custom entry with `POST /{id}/items`:

```json
{ "expectedRevision": 1, "title": "Local short film",
  "data": { "watched_with": "Friends" }, "position": 0 }
```

For a canonical entry, supply `mediaItemId` and omit `title`; the catalog supplies
its title. Each canonical media item may occur once per collection (duplicate 409),
but can appear in other collections. Custom entries may repeat titles. The item
kind and media reference are immutable. Item PATCH replaces the full data object,
custom title and position; omitted optional data keys are cleared. Custom titles
are required; canonical titles cannot be overridden. No library/rating/review
records are created or changed by collection operations.

Field keys/types are immutable; label changes preserve values. Adding a required
field to a nonempty collection returns 409. Making a field required or tightening
its configuration succeeds only if every existing item remains valid. A successful
field change increments schemaVersion as well as revision; item and metadata
changes increment revision only. Positions are nonnegative integers; ties use UUID
ordering. Creation defaults omitted positions to zero; replacement requires them.

Deletion is permanent: first fetch deletion-impact, show its populatedItemCount,
and ask the owner to confirm before sending DELETE with that revision. Missing or
false confirmation returns 400. Any intervening collection edit makes the revision
stale (409), requiring a fresh preview. The API removes the definition and all its
keys, including null-valued keys, in one transaction. Null/absent values do not
count as populated. Unrelated fields/entries survive; affected item timestamps update.

Limits: 30 fields; names 1–120, descriptions ≤2000, labels 1–80, custom titles 1–200.
Keys are 1–40 lowercase ASCII letters/digits/underscores, starting with a letter;
`id`, `title`, `media_item_id`, `created_at`, `updated_at` are reserved. Keys are unique.
Configuration is type-specific: text `maxLength` 1–4000 (default 1000); number
optional inclusive decimal `min`/`max`; boolean/date `{}`; select `options` contains
1–50 unique nonblank strings of at most 80 characters, matched exactly. Dates use
valid `YYYY-MM-DD`; values are never coerced. Required fields reject absent/null
values and blank text. Optional fields may be absent/null. Unknown keys, duplicate
JSON keys, malformed Unicode, NUL, invalid types/bounds, irrelevant configuration
and custom JSON deeper than 16 are rejected with 400. Existing 64 KiB body cap applies.

Lists default to page 1/pageSize 20, bounded at 10000/100. Public-accessible reads
share the catalog 60/IP/minute budget, including signed-in calls; owner-only deletion
previews share personal-read 60/account/minute. Writes share personal-write
30/account/minute. 429 includes Retry-After. These limits remain per API instance.

Apply `20261030160901_AddCollections` explicitly with the existing EF database
update command. Account deletion cascades collections/fields/items at DB level;
catalog deletion is restricted while referenced. No collection deletion endpoint,
field archive/restore, bulk migration, collaboration or frontend is included.
Email verification/recovery and deployment hardening remain required before launch.
