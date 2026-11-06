# RateFreak

A monorepo containing a .NET 10 modular monolith API and a Next.js web app. Phase 1 foundation and Phase 2 local
accounts/catalog reads, Phase 4 personal library/ratings/reviews, and Phase 5
dynamic collections plus Phase 6a MusicBrainz albums, Phase 6b follows, Phase 6c IGDB games,
Phase 6d review comments, Phase 6e additional Google/GitHub sign-in and Phase 6f
moderation/reporting and Phase 6g public profiles/editing are implemented
from `ratefreak-plan.md`.
Next.js will be hosted separately and act as a thin browser-facing proxy; all
accounts, authorization and business logic remain in .NET. Frontend Phases 1–2 live
in `web/`: a responsive shell, shared UI, TanStack Query, local-account screens and
a restricted browser-facing proxy. Catalog and other business-feature screens remain future phases;
see `docs/frontend-checklist.md`. The Phase 3 TMDB adapter is
implemented; local TMDB activation and live verification completed on 2026-10-29.
The checked-in default remains disabled until a server-side token is configured.
IGDB and MusicBrainz live search/preview checks passed on 2026-10-30 using supplied
credentials/contact in a temporarily enabled process. Saved local flags remain
disabled; `.env` is not automatically loaded by the API. Authenticated live imports
and Google/GitHub live sign-in have not been verified. Last full backend suite:
590 tests pass; frontend suite: 123 tests pass.

## Repository and web app

The backend remains in `src/` and `tests/`. The independently built Next.js app
lives in [`web/`](web/README.md), with its own pnpm lockfile. Deployment assets
live in `deploy/` and shared documentation in `docs/`. Both apps can be changed
in one pull request and deployed independently; no monorepo build tool is required.

For the frontend, install Node.js 24 LTS and pnpm 10, then run from the repo root:

```bash
pnpm --dir web install --frozen-lockfile
pnpm --dir web dev
```

Open http://localhost:3000. The homepage works without the backend.
Run `pnpm --dir web lint`, `pnpm --dir web typecheck`, `pnpm --dir web test`, and
`pnpm --dir web build` to verify frontend changes. The backend setup follows.

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

## Follows: Phase 6b

| Method | Route | Contract |
| --- | --- | --- |
| PUT | `/api/v1/users/{userId}/follow` | Session + CSRF; follow target, 204 including repeats |
| DELETE | `/api/v1/users/{userId}/follow` | Session + CSRF; unfollow target, 204 including repeats |
| GET | `/api/v1/users/{userId}/followers` | Public paginated followers |
| GET | `/api/v1/users/{userId}/following` | Public paginated followed users |

Self-follow/unfollow returns 400. Unknown users return 404, including on DELETE.
Lists accept `page` (1..10000, default 1) and `pageSize` (1..100, default 20),
returning `{ items, totalCount, page, pageSize }`. Items expose only `id`, `handle`,
`displayName`, `followedAt`, ordered oldest first with user UUID breaking ties.
An existing user with no relationships returns an empty 200 page.

Relationships are directed and public; following grants no access to private
collections or libraries. All follows responses use `Cache-Control: no-store`.
Separate limits allow 60 reads/IP/minute and 30 writes/account/minute. Rejections
return 429 with `Retry-After`. Repeat/concurrent PUTs preserve the original follow
time. Database constraints prohibit self/duplicate/orphan relationships; deleting
an account cascades its incoming and outgoing follows. Apply `AddUserFollows`
explicitly using the EF update command above. Its rollback removes follows data;
EF commits each migration downgrade separately, so a later refusal does not undo
already-completed downgrades. Back up before any intentional rollback.

Public review comments and IGDB are implemented. Moderation, feeds, public profiles
and optional collection discussions remain deferred.

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
Limits are in-memory per API instance. Optional authenticated browser-proxy
attribution preserves client IP budgets across Next.js; configure both server
secrets and a verified hosting mode as described in `web/README.md` before use.
Shared budgets across multiple API instances remain deployment work.
Kestrel request bodies are limited to 64 KiB. Passwords are bounded before hashing.

The Next.js account proxy forwards session **and** antiforgery cookies, relays
separate `Set-Cookie` updates/deletions and `X-CSRF-TOKEN`, rejects foreign/missing
browser mutation Origin, bounds JSON bodies to 64 KiB, and never caches responses.
It uses a fixed upstream and an explicit local-account allowlist. Service-authenticated
client-address headers affect rate limits only; .NET authenticates/authorizes the
actual user cookie. No permissive CORS policy is needed. OAuth/provider/catalog
proxy routes are not enabled by this slice.

Local password accounts intentionally have unverified emails and use saved-code
password recovery without email delivery; see [ADR 0014](docs/adr/0014-account-recovery.md).
External sign-in below provides provider-managed authentication/recovery. Before
public launch, finish HTTPS/ingress and key-storage hardening, verify recovery on
the deployed release, and review abuse controls/account retention. Profile editing, public profiles, admin endpoints and further media providers remain pending. TMDB activation is
verified locally; IGDB and MusicBrainz live search/preview are also verified using
the supplied credentials/contact. Normal runtime enablement remains a configuration
step; authenticated live imports were not part of those read-only checks.
See `docs/adr/0004-local-accounts.md` for the recorded implementation defaults.

### Optional GitHub and Google sign-in

Both are additional sign-in options; password registration/login remain available.
No email delivery service is used. New provider accounts have no local password;
recovery depends on the provider or another identity explicitly linked beforehand.
A matching email **never** merges accounts or recovers an existing password account.
If the email already belongs to an account, sign in to that account first and link.

| Method | Route | Behavior |
|---|---|---|
| GET | `/api/v1/auth/providers` | Names and enabled flags only |
| POST | `/api/v1/auth/external/{provider}/start` | CSRF; redirects to Google/GitHub; already signed in -> 409 |
| GET | `/api/v1/auth/external/{provider}/callback` | OAuth middleware callback; validates state/correlation/PKCE |
| GET | `/api/v1/auth/external/complete` | Fixed callback destination; linked identity -> profile/session, new identity -> `{ requiresRegistration: true, provider }` |
| GET | `/api/v1/auth/external/pending` | Short-lived onboarding status; no normal session required |
| POST | `/api/v1/auth/external/register` | CSRF, `{ handle, displayName }`; 201 profile/session |
| GET | `/api/v1/users/me/logins` | Session; `{ hasPassword, providers }`, no provider IDs/tokens |
| POST | `/api/v1/users/me/logins/{provider}/start` | Session + fresh CSRF; redirects to explicitly link provider |

Provider names are exactly `google` and `github`. Starts return 404 for unknown
providers, 503 when disabled, 400 for an origin mismatch. Callback failures return
sanitized 400; missing/expired/consumed tickets return 401. Email or handle/login
collisions return generic 409. Choose another handle and retry onboarding within
its five-minute lifetime. New identities require a valid provider-verified email:
Google's verified email or GitHub's verified primary email, including private email.
An existing provider mapping continues working without a current email. Linking
preserves the account's email and verification state and requires the original
session/security stamp to remain valid; it cannot move a login between accounts.
Only one identity per provider per account is allowed. No unlink/password-setting
endpoint is included.

The client initiates starts with a CSRF-protected POST and follows the OAuth
redirect as a browser navigation. A fetch client must handle navigation deliberately;
ordinary cross-origin fetch redirects are not a frontend login implementation.
After callback, the browser reaches the fixed completion API JSON. No arbitrary
return URL is accepted. Frontend buttons and final page redirects are future work.
Fetch a fresh CSRF token after authentication changes. All these responses, including
errors and redirects, use no-store. Callback endpoints belong to middleware rather
than the generated OpenAPI operation list.

Temporary external cookies are HttpOnly, host-only, SameSite=Lax, Secure outside
Development, and expire in five minutes without sliding renewal. They grant no
normal account access. PostgreSQL stores one-time challenge IDs/expiry only; successful
completion consumes the ID atomically, preventing copied completion-cookie replay.
Expired rows are cleaned during subsequent starts. Onboarding creation and login
mapping are transactional. Access/refresh tokens are never persisted or returned.
Outbound requests use fixed HTTPS endpoints, no redirects, 10-second per-request
timeouts and 64-KiB buffered response caps. OAuth handler diagnostic logging is
disabled because framework errors/trace messages can contain provider error values
and full authorization URLs. Keep ingress/access logs from recording auth queries,
cookie headers or response bodies as part of deployment configuration.

Budgets per API instance: starts 10/IP/minute, completion/pending/discovery share
60/IP/minute, onboarding 5/IP/hour, login-list/link starts 10/account/minute.
Callbacks have a separate 30/IP/minute budget before authentication middleware.
429 responses include Retry-After. Deployment-wide limits require ingress/shared
coordination.

Register a Google **Web application** OAuth client and a GitHub **OAuth App** using
their official setup guides:
[Google](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/social/google-logins?view=aspnetcore-10.0),
[GitHub](https://docs.github.com/en/apps/oauth-apps/building-oauth-apps/authorizing-oauth-apps).
Keep separate development/production credentials. Configure exact callbacks using
the API's canonical browser origin, for example:

```text
http://localhost:5081/api/v1/auth/external/google/callback
http://localhost:5081/api/v1/auth/external/github/callback
```

Use the actual running port/scheme. Production requires HTTPS. The enabled-provider
configuration requires `Authentication:External:PublicOrigin` (origin only, no path,
query, fragment or credentials), and each provider's `Enabled`, `ClientId`,
`ClientSecret` under `Authentication:External:Google` / `GitHub`. Environment names
use double underscores, as shown in `.env.example`; .NET does not automatically
load that file. Put secrets in API user-secrets or deployment secret storage and
enable only after credentials/callbacks are configured. No real credentials are
included or activated. Google consent/audience restrictions still apply to live users.

Before deployment, configure AllowedHosts, durable Data Protection keys and trusted
HTTPS/proxy forwarding so request scheme/host exactly match PublicOrigin. A future
Next.js proxy must also forward external/correlation cookies and callback traffic;
do not rewrite callback origins opportunistically. This slice does not configure
that proxy or publicly deploy the API.

The additive `AddExternalSignIn` migration creates replay-state storage and replaces
the nonunique login user index with unique `(user_id, login_provider)`. Existing
duplicate pairs cause migration failure, without deleting mappings. Downgrade
removes transient challenges and restores the old index, preserving users, hashes
and login mappings; in-flight sign-ins must restart. Old application versions do
not understand these flows. See ADR 0010 and the approved external-sign-in design.

## Structure and project references

```text
RateFreak.sln
web/                         Next.js App Router / TypeScript / pnpm
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

Supported types are `movie`, `tv_show`, `album` and `video_game`; TV seasons/episodes and album
editions/tracks are deferred. Search
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
| GET | `/api/v1/users/me/library/{mediaId}` | Your own tracking entry;404 when absent |
| PUT | `/api/v1/users/me/library/{mediaId}` | Replace tracking status and optional dates |
| DELETE | `/api/v1/users/me/library/{mediaId}` | Remove your entry; 204 even if absent |
| GET | `/api/v1/media/{mediaId}/review` | Your own visible review;404 when absent/hidden |
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
TV, albums and games alike. Dates are optional date-only values; completion cannot precede start.
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
The original backend Phase4 added no frontend; the web app now exposes library,
ratings and public reviews, including two authenticated own-record reads with no
schema change. Private reviews and new moderation interfaces remain outside this slice; collections are
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
and custom JSON deeper than 16 are rejected with 400. Numeric values and bounds must be exactly representable as
.NET decimal; rounding/underflow is rejected. Persisted numbers use the validated
decimal representation so extreme exponent syntax cannot reach PostgreSQL unchanged.
Duplicate property names are rejected during deserialization of all API JSON request
bodies, before binding can hide earlier values. Existing 64 KiB body cap applies.

Lists default to page 1/pageSize 20, bounded at 10000/100. Public-accessible reads
share the catalog 60/IP/minute budget, including signed-in calls; owner-only deletion
previews share personal-read 60/account/minute. Writes share personal-write
30/account/minute. 429 includes Retry-After. These limits remain per API instance.

Apply `20261030160901_AddCollections` explicitly with the existing EF database
update command. Account deletion cascades collections/fields/items at DB level;
catalog deletion is restricted while referenced. No collection deletion endpoint,
field archive/restore, bulk migration, collaboration or frontend is included.
Email verification/recovery and deployment hardening remain required before launch.


## MusicBrainz albums (Phase 6a)

MusicBrainz adds `album` media with search, preview and protected import through the
existing provider routes. A canonical album represents a MusicBrainz release group,
so editions/remasters share a single rating/review identity. Only primary type Album
is accepted; live albums, compilations and soundtracks can qualify. EPs, singles,
tracks and explicitly non-musical audiobook/audio-drama/spokenword/interview groups
are excluded. No MusicBrainz write-back or user account is involved.

The adapter is **disabled by default**, including the current local configuration.
Automated tests use simulated responses; no real MusicBrainz calls are required.
Live search and preview succeeded on 2026-10-30 for Random Access Memories using
the supplied maintainer contact and a temporary process with the provider enabled.
The process was stopped and saved settings were unchanged. No live import was made.
To enable actual search/import, choose a maintainer contact email or HTTPS project/
contact URL that you consent to send to MusicBrainz in the outbound User-Agent.
Do not use the example below as an actual contact or infer one from Git settings.

| Configuration | Default / requirement |
| --- | --- |
| `Providers:MusicBrainz:Enabled` | false |
| `Providers:MusicBrainz:Contact` | Chosen email or HTTPS contact URL required when enabled; max 256 ASCII characters, no header controls |
| `Providers:MusicBrainz:TimeoutMilliseconds` | 6000; allowed 1500–15000, includes pacing/retry/body time |

Store chosen Development settings with .NET user-secrets; production configuration
uses `Providers__MusicBrainz__...`. No API key is currently required for read-only
MusicBrainz access. This identification enables local requests, not public deployment.
See [MusicBrainz API](https://musicbrainz.org/doc/MusicBrainz_API) and
[identification/rate limits](https://musicbrainz.org/doc/MusicBrainz_API/Rate_Limiting),
checked 2026-10-30.

Routes:

- `GET /api/v1/media/providers` lists all three configured metadata adapters, supported types,
  attribution and enabled states, without contact/tokens.
- `GET /api/v1/media/external/search?provider=musicbrainz&type=album&q=Year%20Zero&page=1`
  searches album title or artist with an escaped literal phrase. Pages 1–500,
  20 upstream results/page; filtering may produce shorter pages. No raw Lucene input.
- `GET /api/v1/media/external/musicbrainz/album/{releaseGroupUuid}` previews an album.
- `POST /api/v1/media/import` with
  `{ "provider": "musicbrainz", "externalId": "RELEASE-GROUP-UUID", "mediaType": "album" }`
  imports authoritative server-fetched details. Use a real nonempty dashed UUID;
  uppercase UUIDs normalize to lowercase. Requires the existing session and CSRF flow.

Omitting provider on search still selects TMDB. TMDB retains movie/tv_show support
and integer identity rules. Unknown search/import providers and invalid type/ID
pairs return 400; unknown preview provider returns 404. Valid non-album/missing
MusicBrainz details return 404. Existing imports return 200 with the same canonical
ID even when the provider is later disabled; first imports return 201. Concurrent
imports create one item/mapping, never title-merge or overwrite curated records.

Album title remains separate from artist credits. Metadata includes `artistCredit`,
ordered `artists` (id/name/joinPhrase), `primaryType`, `secondaryTypes` and optional
`firstReleaseDate`. A valid year/year-month is preserved without inventing a day;
`releaseDate` is non-null only for a complete valid date. Description/imageReference
remain null. No cover art, annotation, tags, genres or provider ratings are imported.
Albums work with existing library statuses, ratings, reviews, catalog/library type
filters and collections; imports do not automatically create personal records.

Outbound transport uses a fixed HTTPS origin with redirects disabled. One shared
process-wide gate allows one MusicBrainz operation, with no waiting operation queue;
every outbound start, including retry, is at least one second apart. At most one
retry is allowed; 429/503 Retry-After establishes cooldown across requests/clients.
Long cooldowns reject new work rather than sleeping past the operation deadline.
The complete upstream duration is retained internally; only the exposed Retry-After
header is capped at 86400 seconds.
Responses are bounded to 1 MiB, depth 32 and unique JSON properties. Strict string,
UUID, date, artist-credit and metadata bounds reject invalid upstream data with 502.
Busy/disabled/limited/unavailable is 503 (Retry-After when applicable), timeout 504.
Caller cancellation propagates. TMDB uses its own client settings and existing gate.

Public external operations share the existing 20/IP/minute budget, imports share
10/account/minute and provider capabilities share catalog limits. In-process pacing
cannot coordinate multiple instances or other clients sharing the same egress IP;
coordinate aggregate access before such deployment. MusicBrainz documents roughly
one request/second/IP and requires a meaningful maintainer User-Agent.

Apply `20261030164447_AddAlbums` using the normal explicit EF update command.
The migration widens only the media type CHECK constraint. Downgrade refuses when
albums exist, preserving records/history; handle album data explicitly before a
rollback. Without albums, downgrade/reapply preserves existing movies/TV.

[MusicBrainz licensing](https://musicbrainz.org/doc/About/Data_License) distinguishes
core CC0 data from supplementary data; its API documentation distinguishes free
non-commercial service from commercial plans. Review applicable terms before
public/commercial launch. Cover art is a separate service and is deferred.
Comments/moderation, books and existing email/recovery/deployment requirements
remain pending. This completes Phase 6a, not all of Phase 6.


## IGDB video games (Phase 6c)

IGDB adds `video_game` through the existing search, preview and authenticated import
routes. Main games, remakes and remasters are separate canonical items, one per
IGDB games ID; platform variants are not separate entities. DLC, expansions,
bundles, ports, mods and editions are excluded in this slice. Existing TMDB/movie/TV
and MusicBrainz/album behavior is preserved; default search remains TMDB.

The adapter is **disabled by default**. No real credentials or live IGDB calls are
needed by the tests. Configure server-side Twitch app credentials through user-secrets
or deployment secret storage when you choose to activate it; do not put real
credentials in tracked files or command history. No Twitch user login is added.
Live search and preview succeeded on 2026-10-30 for The Witcher 3: Wild Hunt (ID 1942)
using supplied credentials in a temporary enabled process. Saved flags remain false;
the process was stopped and no live import was made. This verifies credentials and
read requests, without enabling the provider in the normal runtime configuration.

| Configuration | Contract |
| --- | --- |
| `Providers:Igdb:Enabled` | false |
| `Providers:Igdb:ClientId` | Required when enabled; 1..256 ASCII alphanumeric characters |
| `Providers:Igdb:ClientSecret` | Required when enabled; same bounds, server secret |
| `Providers:Igdb:TimeoutMilliseconds` | 6000; allowed 500..15000, covers auth, pacing, retries and bodies |

Environment names use `Providers__Igdb__...`. Twitch client_credentials requests
send credentials in a form body to a fixed HTTPS token endpoint. The app token
stays in memory, cached for at most 55 minutes and renewed at least 60 seconds before
expiry (short tokens used only for the current request). A 401 invalidates the cache
and allows one fresh-token retry; no refresh-token/user-session flow is involved.
Credentials and token values are absent from public capability/error responses.

- `GET /api/v1/media/external/search?provider=igdb&type=video_game&q=Witcher&page=1`
- `GET /api/v1/media/external/igdb/video_game/1942`
- `POST /api/v1/media/import` with session and CSRF, body
  `{ "provider": "igdb", "externalId": "1942", "mediaType": "video_game" }`

IDs are positive Int32 decimals with leading zeros canonicalized. Namespace is
`game`. Public IGDB pages use slugs rather than API IDs; the optional `externalUrl`
is null until a separate slug-link policy is implemented. Search pages 1..500
contain at most 20 results; input is escaped inside a fixed APICalypse query.
Valid excluded/unknown games return 404 on preview/import; disabled valid requests
return 503; invalid provider/type/IDs return 400 before outbound access. Imports
return 201 once, then 200 without upstream HTTP or catalog overwrites. Existing
transactional duplicate-race handling prevents orphan entries.

Normalized data includes title, optional summary, validated fixed-CDN cover reference,
gameType, optional Unix `firstReleaseTimestamp`, and up to 20 normalized platforms
and genres (id/name). Canonical `releaseDate` stays null because the first-release
timestamp alone does not establish day precision. No upstream scores, arbitrary
URLs or raw provider fields are imported. A missing optional field is allowed;
malformed data returns sanitized 502. Token JSON is bounded 16KiB; game JSON 1MiB,
depth 32, with duplicate properties rejected.

One operation is active per process with no queue. Games sends start at least 250ms
apart, including retries; each operation allows at most two games sends. Token
failures do not retry inside the operation. 429/503 sets a shared complete cooldown;
new calls are rejected throughout it. Only exposed Retry-After seconds are capped
at 86400. Long cooldowns fail promptly; short game failures can retry inside the
same deadline. Caller cancellation propagates; deadline expiry returns 504.
Named auth/game clients disable redirects and redact credential headers. Multi-instance
or shared-credential deployments require coordinated rate limiting.

Apply `20261030212206_AddVideoGames` explicitly with the EF command above. It only
widens the media-type CHECK. Down locks the table and refuses if any games exist;
without games, downgrade/reapply preserves earlier catalog data and follows schema.
Never attempt production rollbacks without a backup and reviewed recovery plan.

Primary references: [IGDB API](https://api-docs.igdb.com/) (current game_type field,
requests, pagination, images and rate limits) and
[Twitch app-token flow](https://dev.twitch.tv/docs/authentication/getting-tokens-oauth/#client-credentials-grant-flow).
Review IGDB/Twitch terms, commercial partnership requirements, metadata/image rights
and attribution before public use. This implementation does not constitute live
activation or public-launch readiness.


## Review comments (Phase 6d)

Flat comments are available on public reviews. Collections, threaded replies,
moderation/reporting and notifications are separate future work.

| Method | Route | Contract |
| --- | --- | --- |
| GET | `/api/v1/reviews/{reviewId}/comments` | Public paginated comments, oldest first |
| POST | `/api/v1/reviews/{reviewId}/comments` | Session + CSRF; `{ body, isSpoiler? }` -> 201 and Location |
| GET | `/api/v1/comments/{id}` | Public comment detail |
| PATCH | `/api/v1/comments/{id}` | Author + CSRF; replace body/spoiler -> 200 |
| DELETE | `/api/v1/comments/{id}` | Author + CSRF -> 204; missing/non-owned -> 404 |

Bodies are trimmed, then must contain 1–2000 UTF-16 code units of valid plain text.
Only tab, CR and LF controls are allowed; blank text, NUL and unpaired surrogates
are rejected. Text is returned literally; clients must render it as text. Spoiler
flags default to false and must be handled by the frontend. PATCH replaces the
body and flag, preserving comment ID, author, target and creation time; simultaneous
edits use last-write-wins semantics. Duplicate/unknown JSON properties return 400.
POST creates a new comment each time; it has no replay/idempotency key.

Details expose `id`, `reviewId`, `author` (id/handle/displayName), `body`, `isSpoiler`,
`createdAt` and `updatedAt`. Email and account/security fields are never included.
The parent review's author cannot edit or delete another person's comment. No
follow relationship is required. Removing a review cascades all its comments;
removing an account removes its authored comments and the comments on its reviews.

Lists return `{ items, page, pageSize, totalCount }`, with page 1..10000 (default 1)
and pageSize 1..100 (default 20). Order is creation time then UUID. A read snapshot
keeps parent existence, count and page consistent. Existing empty threads return
200; missing reviews/comments return 404. Public comment endpoints use no-store,
including validation, authentication and rate-limit responses. Reads share the
60/IP/minute social budget with follows; writes share 30/account/minute. A 429
includes Retry-After. Other catalog/personal limits remain separate.

`AddReviewComments` adds only the comments table, its body constraint, cascading
review/account foreign keys and paging/author indexes. Apply it with the explicit
EF update command above; startup never migrates the database. Down drops comments
and their data, so back up and review rollback impact first. Transactional writes
and foreign-key handling prevent parent/account deletion races from producing
orphan comments or a failed response read after a successful write.


### Reporting and moderation

Ordinary signed-in accounts may report public reviews/comments. Moderator and Admin
are explicitly assigned Identity roles; Admin can also moderate. Registration never
grants privileges. Staff permissions use current database membership; revoked access
cannot be retained through old cookie role claims. Staff cannot moderate their own
posts or resolve reports against them.

| Method | Route | Access / behavior |
| --- | --- | --- |
| POST | `/api/v1/reviews/{id}/reports` | Signed-in + CSRF; report public review |
| POST | `/api/v1/comments/{id}/reports` | Signed-in + CSRF; report public comment |
| GET | `/api/v1/moderation/reports` | Staff; `status=open` or `resolved` |
| GET | `/api/v1/moderation/reports/{id}` | Staff; private report and current target |
| POST | `/api/v1/moderation/reports/{id}/resolve` | Staff + CSRF; dismiss or hide |
| GET | `/api/v1/moderation/reviews/{id}` | Staff; inspect even hidden content/version |
| GET | `/api/v1/moderation/comments/{id}` | Staff; inspect even under hidden parent |
| POST | `/api/v1/moderation/reviews/{id}/visibility` | Staff + CSRF; hide/restore |
| POST | `/api/v1/moderation/comments/{id}/visibility` | Staff + CSRF; hide/restore |
| GET | `/api/v1/moderation/audit` | Staff; optional `targetType`, `targetId` filters |
| PUT | `/api/v1/admin/moderators/{userId}` | Admin + CSRF; grant Moderator |
| DELETE | `/api/v1/admin/moderators/{userId}` | Admin + CSRF; revoke Moderator |

Reports accept `{reason, details?}`. Reasons: `spam`, `harassment`, `hate`,
`sexual_content`, `violence`, `spoiler`, `other`. Details are trimmed plain text up
to1000 UTF-16 units, required for other; valid Unicode, only tab/CR/LF controls.
First report201, lifetime duplicate from same reporter/target409, self-report400,
missing/hidden target404. Acknowledgements contain only id/status/createdAt. No
public report counts, reporter listings or automatic hiding from report volume.

Resolutions accept `{decision: "dismiss"|"hide", reason, expectedTargetUpdatedAt}`.
Visibility accepts `{hidden: true|false, reason, expectedTargetUpdatedAt}`. Inspect
current target first; stale timestamps or already resolved reports return409.
Deleted targets can only be dismissed with null expectedTargetUpdatedAt. Required
reasons are trimmed1..1000 plain-text UTF-16 units. Successful commands204. Same-state
visibility and role requests are idempotent and produce no extra audit event.
Role requests, including DELETE, carry a JSON `{reason}` body. Unknown/duplicate
JSON properties return400. No HTTP endpoint grants Admin.

Lists use page1..10000/pageSize1..100 (defaults1/20). Reports sort oldest first,
audit newest first, with UUID tie order. Responses/errors use no-store; anonymous401,
nonstaff403. Staff details expose public identity fields only, never email/security
data. Limits per API instance: reporting5/account/hour; moderation reads60/account/min,
writes30/account/min; role changes10/admin/min, no queue,429 Retry-After. These are
separate from existing social budgets; deployment-wide enforcement is separate work.

Hiding a review makes its public detail and entire comment thread404 and removes
it from public lists/counts. Direct comment reads also enforce parent visibility.
Restoring the review leaves individually hidden comments hidden. Authors can edit
or delete their hidden content, but editing never republishes it. Their successful
mutation response may contain their own hidden text; public GET stays404. Hidden
reviews still occupy the author/media uniqueness slot. Ratings remain independent.

Appoint the first Admin explicitly by **existing account UUID** in an operator
shell with normal API database configuration. For example, from the repository:

```bash
dotnet run --project src/RateFreak.Api --no-launch-profile -- --appoint-admin <account-uuid> --reason "Initial administrator appointment"
```

The command exits without starting HTTP or applying migrations, records an operator
audit event, and is idempotent. Exit0 success,2 invalid/missing account,1 operational
failure. No account is created; credentials/emails never establish privilege. Keep
secrets in normal environment/user-secrets storage; `.env` is not loaded automatically.
Use the same environment/content-root configuration as your normal runtime. Admin
changes beyond moderator assignment remain operator-managed; no real Admin has
been appointed by implementation tests outside their isolated databases.

Reports and audit metadata survive account/content deletion via nullable live
references and retained opaque UUIDs. No copies of post bodies/emails/handles are
retained; original report details and decision reasons are retained. There is no
automatic expiry or purge in this slice. Before public launch, establish retention,
erasure/export and staff access policies. History is append-only through the app,
not tamper-proof against database administrators. Operator events identify an offline
operator action; deployment/OS logs must identify the person when required.

`AddModeration` is additive and defaults existing content to visible. Its downgrade
locks the affected tables and refuses if hidden content, reports or audit history
exist, preventing accidental republication/history loss. Restoration leaves audit
history, so it does not make rollback eligible. Prefer forward migration fixes and
backups; do not delete history merely to bypass the guard. Coarse database transaction
locking serializes moderation/reporting and review/comment writes; assess throughput
before large deployment. Suspensions, appeals, notifications and frontend remain deferred.

### Public profiles and editing

| Method | Route | Access / behavior |
| --- | --- | --- |
| GET | `/api/v1/users/{userId}` | Public profile by UUID |
| GET | `/api/v1/users/by-handle/{handle}` | Public profile by case-insensitive handle |
| PATCH | `/api/v1/users/me` | Signed-in owner + CSRF; partial display name/bio edit |

Public profiles contain **only** `id`, `handle`, `displayName`, `bio`, `createdAt`.
Email, security fields, roles, private library/ratings and provider mappings remain
private. Missing users404; malformed handles400. Handles remain fixed and retain
their registered casing. No all-user directory or private-profile switch. Existing
follower/following routes remain separate. Public author-post lists are not added.

PATCH accepts only `{displayName?, bio?}`. Omitted fields are preserved; supply at
least one. Explicit `bio:null` or trimmed blank bio clears it. Display name must be
nonnull, trimmed1..80 UTF-16 units, valid Unicode without controls. Bio accepts at
most500 trimmed UTF-16 units, valid Unicode, only tab/CR/LF controls. Return literal
text and escape it in the UI. Unknown/duplicate properties, wrong types and invalid
values400. Handles, email, passwords and roles cannot be changed here. Successful
PATCH200 returns the private owner `AccountProfile`, including email and bio.
Registration, local/external login/onboarding and existing GET `/users/me` consistently
include `bio` (null until edited), preserving existing owner fields.

All profile responses/errors are no-store, including matched case/trailing-slash
variants. Public reads share profile-read60/IP/min; edits10/account/min. Budgets are
independent of social/catalog limits;429 Retry-After, no queue, per API instance.
Unauthenticated edits401; invalid/missing CSRF400. Caller identity comes from the
session, never the request body. There is no edit-by-target-ID operation.

Updates change only supplied profile fields and UpdatedAt. Disjoint concurrent edits
preserve both; same-field edits are last-write-wins. Profile edits leave credentials,
Identity stamps, role/login mappings and sessions unchanged. Existing authentication
middleware can independently renew session cookies. EF concurrency checks prevent
stale Identity full-row writes from reverting profile edits; logout locks/reloads
current account state before revocation. Review/comment/follow author DTOs reflect
updated display names through their existing joins.

`AddUserBios` adds one nullable bounded users.bio column and a CHECK. Downgrade locks
the table and refuses while any bio exists, preventing silent text loss. Prefer
forward fixes; review backup/data policy before intentional rollback. No avatar
uploads, handle/email/password changes, public library, profile reports/moderation,
frontend or deployment. Profile moderation and account erasure/export/retention
policy remain public-launch work.


## Saved-code password recovery (2026-11-06)

Local accounts intentionally do not require email verification or email delivery.
This approved policy supersedes historical verification/recovery requirements
above; see [ADR 0014](docs/adr/0014-account-recovery.md). Email is not ownership proof.

- Local registration returns the existing profile fields plus `recoveryCode`.
  Save the code before continuing to login. Registration still does not sign in.
- `GET /api/v1/auth/recovery-status` (authenticated) returns `hasPassword` and
  `hasRecoveryCode`, without any secret.
- `POST /api/v1/auth/recovery-code` (authenticated + CSRF) accepts `{password}`
  and returns `{recoveryCode}`. It creates/replaces the code after password and
  current-session confirmation. Existing users enroll here through `/account`.
- `POST /api/v1/auth/reset-password` (CSRF) accepts `{handle,recoveryCode,password}`
  and returns a replacement `{recoveryCode}` after an atomic password reset and
  all-device session revocation. The browser form is `/reset-password`.

Only the issuing response shows a code. Store it privately in a password manager
or offline. Consumed/replaced codes stop working. If a reset response is lost,
try the new password; after login, generate a new code in settings. No automatic
retry, email reset, admin override or local-password creation for external-only
accounts is provided. Lost password plus lost code requires a previously linked
provider; otherwise recovery is impossible. Google/GitHub remain optional backend
capabilities, with browser integration/live configuration a separate follow-up.

Apply the additive `AddAccountRecovery` migration using the existing reviewed
migration process; the API does not migrate at startup. It adds recovery state and
credential-free security events, leaving existing users unenrolled. Do not use
production rollback without a reviewed recovery plan. Production hardening and
retention/deletion/export policy remain release gates.
