# RateFreak web

Next.js App Router with TypeScript, ESLint, and CSS Modules. This app shares
one repository with the .NET API, but has its own dependencies and deployment.
Use Node.js 24 LTS and pnpm 10 (the exact pnpm version is pinned in package.json).

From the repository root:

```bash
pnpm --dir web install --frozen-lockfile
pnpm --dir web dev
```

Open http://localhost:3000. The app first checks installation state through the API;
a reachable, migrated API/database is required before normal pages are shown.

## Checks and production

```bash
pnpm --dir web lint
pnpm --dir web typecheck
pnpm --dir web test
pnpm --dir web build
pnpm --dir web start
```

`start` serves the production build and requires `build` first. Commit
`pnpm-lock.yaml` when dependencies change. Keep frontend tooling and packages
in this directory; no root JavaScript workspace or build orchestrator is needed.
The generated pnpm workspace configuration records ignored optional dependency
build scripts; it does not combine the frontend and backend into a JS workspace.

## API boundary

The browser client calls the same-origin `/api/v1/` proxy. It permits only
`GET auth/csrf`, `GET users/me`, `GET media`, `GET media/{guid}` and
`POST auth/register`, `auth/login`, `auth/logout`, `auth/recovery-code`,
`auth/reset-password` and `GET auth/recovery-status`, plus the feature routes below.
The media list accepts query parameters: `q`, `type`, `page`, `pageSize`. Duplicate/unknown parameters
and invalid values return 400; other routes reject query strings.
Other paths return 404 and unsupported methods return 405 locally. Authenticated
navigation checks the session; missing proxy configuration returns sanitized 503
responses and an account-unavailable state without preventing the shell from loading.

Set `RATEFREAK_API_URL=http://localhost:5080` in `web/.env.local` for development.
This server-only variable must be an HTTP(S) origin with no credentials, path,
query or fragment. Never use a `NEXT_PUBLIC_` variable for secrets or upstream
configuration. `RATEFREAK_WEB_ORIGIN` must match the browser's exact Origin for
writes. Missing/foreign Origin returns 403; absent server configuration returns
503. Auth writes require JSON, an X-CSRF-TOKEN and a body no larger than 64 KiB.

The proxy forwards only session and antiforgery cookies, preserves every separate
Set-Cookie update/deletion and Retry-After, and disables HTTP caching. It strips
caller identity/forwarding headers, refuses redirects, and aborts upstream work
after ten seconds or caller cancellation. Production cookie names start with
`__Host-`; development uses `ratefreak.session` and `ratefreak.csrf`. Run Next.js
development against .NET Development locally; production requires corresponding
production cookies and HTTPS. Do not weaken Secure attributes to bridge modes.

.NET retains authentication, authorization and business rules. OAuth redirects,
correlation cookies and identity linking remain a separate follow-up.

## Local accounts setup

Start the API/database following the root README. Generate one random secret
with `openssl rand -hex 32`, and configure the same 64-character value on both
servers. For the Development API, set `BrowserProxy:SharedSecret` using
`dotnet user-secrets set` with `--project src/RateFreak.Api`; production uses
`BrowserProxy__SharedSecret` from its secret manager. Never commit this value.

Put the following server-only settings in ignored `web/.env.local` (replace the
placeholder secret):

```dotenv
RATEFREAK_API_URL=http://localhost:5080
RATEFREAK_WEB_ORIGIN=http://localhost:3000
RATEFREAK_CLIENT_IP_MODE=loopback
RATEFREAK_PROXY_SECRET=YOUR_64_HEX_CHARACTER_SECRET
```

Restart both processes after configuring. Use `pnpm --dir web dev`, then open
`/register`, `/login` and `/account`. Loopback mode is Development-only and
requires a loopback API origin. Local browsers intentionally share the local IP
budget, as direct local callers would. It is not a public hosting mode.

Production attribution supports the planned Vercel host: set client IP mode to
`vercel`, configure the real HTTPS origins and matching secret, and deploy through
Vercel (`VERCEL=1`). Only its `x-vercel-forwarded-for` address is accepted; ordinary
caller forwarding/key/address headers are discarded. Next constructs its own
service-authenticated headers. .NET validates the service key in constant time
and uses the normalized client address for existing IP budgets, never user claims.
Malformed/partial attribution returns 403. With no backend secret configured,
attribution headers are ignored and direct connection IP budgets remain in effect.
Both server settings are therefore required to isolate proxied production clients.

No fixed Vercel egress IP or trust-all ASP.NET forwarded-header configuration is
assumed. A different production ingress needs a separately verified attribution
adapter. Confirm actual platform headers, secrets and HTTPS in deployment;
multi-instance shared abuse budgets and account-recovery policy remain release work.

See [Vercel's request-header contract](https://vercel.com/docs/headers/request-headers).

## Account behavior

Registration creates an account and directs the user to log in; it never signs in
automatically. Login uses email. `/account` checks a fresh session before showing
private data. Logout revokes sessions on all devices and protected pages redirect
to `/login`; logout from a public page leaves that page open. Failed requests show
validation/generic login/rate-limit guidance without claiming logout succeeded.

Passwords are cleared from submitted form controls and kept out of mutation
variables, query keys and browser storage. Login/logout cancel old requests, reset
pending CSRF issuance and clear old cached data. Session checks run on protected
entry, mount, window focus/pageshow, and every minute while active. BroadcastChannel
notifies other windows of identity changes using a signal with no account payload.
Private content is hidden while identity is being revalidated; network/429 errors
are shown explicitly instead of being interpreted as logged out.

## Catalog

`/catalog` browses the local catalog without signing in. Submit the labeled title
search and media-type filter to update the URL; pagination preserves filters,
and Back/Forward restores them. Pages contain up to 20 titles. Invalid URL
filters show a reset link without requesting data. Empty/no-match/out-of-range
pages provide recovery, and failures provide retry or rate-limit guidance.

`/catalog/{guid}` shows the title, format, release date, description and artwork.
Missing records have a distinct not-found state; missing fields and failed
images have honest fallbacks. Artwork accepts only the canonical HTTPS TMDB
w500 and IGDB cover formats emitted by the existing provider adapters; other
references are not loaded. No generic image proxy or metadata HTML rendering.

Catalog data uses the existing API origin and requires the .NET API/database.
There is no bundled seed catalog, automatic provider import or demo-data mode.
Provider search/preview/import is available through Find titles. Query cancellation
and complete filter keys prevent older searches replacing newer results.

## Provider discovery and import

Use **Find titles from providers** in the catalog, or open `/catalog/discover`.
Search is explicit, with provider/type/title/page in the URL. Only configured,
enabled providers can be searched. Results open authoritative previews; signed-in
visitors can explicitly import into the shared catalog. This does not create a
personal library entry. New and existing imports have distinct outcomes and a
link to the canonical title. Import requests are never automatically replayed.

Anonymous previews offer Log in to import and return to the same preview after
login. Continuations accept only canonical internal provider-preview, library and
catalog-detail paths;
other login destinations retain the existing `/account` behavior.

The restricted proxy additionally permits GET `media/providers`, GET
`media/external/search`, GET `media/external/{provider}/{type}/{externalId}` and
POST `media/import`. Only external search accepts `q`, `provider`, `type`, `page`;
unknown/duplicate parameters, invalid provider/type pairs and identities are
rejected locally. Import uses the existing session, exact Origin, attribution
secret, JSON/body limit and fresh CSRF protections. All responses remain no-store.

### Enable local providers

.NET does **not** load the root `.env` automatically. Normal Development startup
loads .NET user-secrets; environment variables passed to the process override them.
Copy existing IGDB ClientId/ClientSecret and MusicBrainz Contact into their
`Providers:Igdb:*` / `Providers:MusicBrainz:*` user-secret keys privately, then set:

```bash
dotnet user-secrets set 'Providers:Igdb:Enabled' 'true' --project src/RateFreak.Api
dotnet user-secrets set 'Providers:MusicBrainz:Enabled' 'true' --project src/RateFreak.Api
dotnet run --project src/RateFreak.Api --launch-profile http
```

Restart an already running API after configuration changes. IGDB needs the Twitch
app client credentials; MusicBrainz needs a maintainer contact (no API key for these
read-only requests). Do not place provider credentials in `NEXT_PUBLIC_` settings.
GET `http://localhost:5080/api/v1/media/providers` reports enabled flags without
credentials. Existing root README provider sections describe each exact contract.
The current local setup has all three providers enabled and matching browser-proxy
settings for frontend writes. Those local secret values are not tracked.

## Personal library, ratings and reviews

Open `/library` after signing in, or open a catalog title to track it, rate it or
write a public review. Library Status/Media type filters and pagination live in
the URL. Saved dates load before editing; tracking PUT replaces all fields, so
status changes preserve the dates shown. Dates do not infer status. Removing a
library entry leaves its rating and review intact.

Ratings accept0–5 in0.5 steps, including zero. Each title shows the local community
count/average separately. Reviews are public plain text, one per user/title,
editable up to10000 UTF-16 units. Spoilers start concealed in the public list.
Review pagination is independent of the personal editor. Conflicting creation
keeps the draft and requires an explicit choice to load an existing visible review.
Tracking removal and review deletion use inline confirmations.

Private reads and caches belong to account plus media/filter identity. During a
background session check, mounted drafts stay concealed/inert and their reads and
writes pause; an in-flight write is cancelled without replay. The same verified
account resumes its drafts. Confirmed logout/account change discards them. Failed
writes preserve input; mutations never retry automatically. Login continuations
add only canonical `/library` and `/catalog/{guid}` to the existing provider paths.

The proxy adds exact library GET/PUT/DELETE, own rating/review GET, rating
PUT/DELETE, public aggregate/review-list GET, review POST and review-ID PATCH/DELETE
routes. Only library and review lists accept their bounded validated query keys.
Every write retains existing exact Origin, attribution, JSON/body and CSRF guards.
The two new .NET reads are `GET users/me/library/{mediaId}` and
`GET media/{mediaId}/review`; they derive the owner from session, return404 when
absent, and never reveal hidden review content. No schema migration is needed.

## Collections

`/collections` lists public collections plus the signed-in viewer's own private
collections. Apply All accessible or My collections explicitly; scope and 20-item
paging live in the URL. Anonymous My collections offers login. Collection reads
wait for verified session identity and use viewer-specific cache keys, including
an explicit anonymous identity. Content is concealed during identity checks.

`/collections/new` creates a collection with name, optional description and
Private/Public visibility; Private is the default. Existing owners can edit these
fields at `/collections/{guid}`. Public visibility also exposes existing entries.
Collection detail also supports typed fields, paginated entries and owner entry
creation. There is no collection deletion control.

Edits send all metadata plus the loaded revision. Background refreshes preserve
dirty drafts and their revision. A conflict retains the draft and requires an
explicit Reload latest metadata before another save; reloading replaces the draft
and refreshes the detail snapshot. Writes never retry automatically. An uncertain
create offers My collections to check the result before creating again. Session
checks conceal and retain same-account drafts, cancel pending writes and pause
reads; confirmed identity changes discard them. GUID cache identities are canonical
lowercase. Visibility saves clear obsolete snapshots for other viewers.

The proxy adds only GET/POST `collections` and GET/PATCH `collections/{guid}`.
Only the GET list accepts `ownOnly`, `page`, `pageSize`; unknown/duplicate filters
and other query-bearing routes are rejected. Existing Origin, CSRF, JSON/body,
cookie, attribution, cancellation and no-store protections remain required.
Login continuations add only canonical `/collections`, `/collections/new` and
lowercase-GUID detail paths, without query strings or automatic write replay.
No backend changes or migrations are needed for these collection interfaces.

### Fields and entries

Collection owners can define up to 30 text, number, boolean, date or select fields.
Keys and types are immutable; labels, required settings, configuration and integer
positions are editable. Required/configuration changes must preserve every saved
entry's validity; incompatible proposals return a conflict without changing data.
Adding a required field is disabled for a populated collection. Positions permit
ties; there is no automatic renumbering or drag sorting.

Add entry supports a custom title or explicit selection from the local catalog.
Canonical identity cannot change on edit and duplicate canonical titles within
one collection are rejected. No provider import or personal-library write happens
as a side effect. Entry paging uses `itemsPage` in the collection URL. Dedicated
`/collections/{guid}/items/{guid}` pages show literal field values and owner-only
editing/removal. Optional Set a value toggles distinguish unset from false, zero
and empty text. Replacement writes preserve all unchanged values.

Dynamic numbers and numeric bounds use a feature-owned exact JSON/decimal codec,
preserving the backend's .NET decimal range without JavaScript rounding. Shared
transport callbacks are opt-in; other features retain their existing JSON behavior.
Collection/item read bundles bracket the item read with two collection-revision
reads and retry once if it changed. Drafts keep the revision loaded at editor
initialization. Only one owner editor opens locally at a time. Confirmed writes
close their editor before refresh; a failed/interrupted refresh blocks further
writes and retries only the read.

Field removal requires a fresh owner-only impact preview and explicit permanent
deletion confirmation, including when no entries have populated values. Deletion
uses the preview's revision; a stale revision or session uncertainty invalidates
consent and requires a new preview/click. Removing an entry likewise requires
fresh review and explicit confirmation; catalog/personal data remain independent.

The proxy additionally permits exact collection item GET/POST, item-ID GET/PATCH/
DELETE, field POST, field-ID PATCH/DELETE and deletion-impact GET paths. Item lists
accept only page/pageSize. DELETE queries accept a positive safe-integer
expectedRevision; field DELETE additionally requires exactly confirm=true.
Other query-bearing field/item routes stay closed. Origin/CSRF/JSON/body-limit,
attribution, cookie, cancellation and no-store protections apply throughout.

## Appearance

The editorial design applies to Home, catalog, media details, library, login, registration and account pages.
Use the header's sun, moon and monitor buttons for **Light**, **Dark** or **System**.
Buttons have accessible names, tooltips and pressed states. First
visits use Dark. Saved theme choices take precedence. The browser remembers the preference in `ratefreak.theme`;
System follows device changes, and other tabs receive preference updates.
Storage restrictions do not prevent switching in the current document.

A static first-party script applies the stored choice before hydration. Only
the theme preference is persisted; account data, passwords and query caches are
not stored by the theme system. Theme changes preserve mounted forms and do not
change session state. Shared semantic tokens and component guidance are recorded
in the root `DESIGN.md`.

## Frontend conventions

- Shared object contracts live in separate files in `shared/interfaces/`.
  Feature contracts belong in `features/<feature>/interfaces/`; shared UI props
  belong in `components/ui/interfaces/`. Prefer interfaces for objects, type
  aliases for unions/mapped types/tuples. Keep props out of component bodies.
- Routes compose screens, components render UI, feature hooks own queries and
  mutations, and API modules handle transport. Keep files focused by responsibility.
- The browser client is `apiRequest<T>("users/me", options)`; paths are relative
  to `/api/v1/`. It supports cancellation, JSON/204, structured validation errors
  and Retry-After. Writes acquire fresh CSRF tokens, coalescing pending issuance;
  they do not retry automatically.
- TanStack Query owns browser server-state caching: one stable client per mounted
  provider, one-minute freshness, five-minute garbage collection, focus refetch,
  one transient network/5xx retry, no 4xx or mutation retries. No storage persistence
  or SSR hydration. Feature keys must include filters and private account identity.
- Session transitions clear account-scoped cached data and pending CSRF state.
  Future private feature keys must include the account identity and filters.
- Vitest runs transport tests in Node and component tests in jsdom (per-file
  `@vitest-environment jsdom` directive). Tests mock network I/O, not domain rules.

Progress and the next session's scope are in [the frontend checklist](../docs/frontend-checklist.md).
Local release evidence and outstanding launch gates are in the
[release verification record](../docs/frontend-release-verification.md).

See the [root README](../README.md) for backend setup and API contracts.

## Public profiles and editing

`/users/{handle}` shows display name, handle, public bio and member date.
Handles use case-insensitive lookup; email remains private. Review authors and
Account link to these pages. `/account` offers display name (1–80 characters)
and plain-text bio (up to500 characters) editing. Empty bio clears it. Both
fields are submitted together; concurrent same-field updates use the backend's
last-write-wins behavior. Unknown save outcomes require reloading before another
save. Session uncertainty hides the editor while preserving same-account drafts.

The proxy additionally allows exact `GET users/by-handle/{handle}` and
`PATCH users/me`, with existing Origin/CSRF/body/no-store protections.
Review comments are available through public review discussion pages.

## Follows and public connections

Profiles include public Followers/Following lists (20 rows per page). Selection
uses `connections=followers|following` and `page=1..10000`; Back/Forward restores
it. Anonymous visitors can read lists and log in with profile continuation.
Signed-in nonowners can explicitly Follow or Unfollow. Both actions are safe to
repeat; the API has no point relationship-state lookup, so the UI does not show
an inferred toggle. A confirmed save stays confirmed if list refresh fails.
Unknown outcomes offer deliberate repeat-safe actions without automatic retries.

Additional exact proxy routes: `GET users/{guid}/followers|following` with bounded
`page`/`pageSize`; `PUT|DELETE users/{guid}/follow` with the existing mutation guards.

## Review discussions and comments

Public review cards link to `/reviews/{guid}`. Discussions show the original review
and public comments, oldest first, with independent spoiler reveal controls and
literal text. URL `page` (1–10000) controls20-row comment paging. Anonymous visitors
can read or log in with discussion continuation. Comment authors can post1–2000
characters, edit body/spoiler status, and confirm deletion. The review author cannot
edit another author's comments. Edits use backend last-write-wins behavior.

Unknown write outcomes preserve drafts and block another submission until a full
reload; POST never retries automatically. Session checks hide/disable editors while
retaining same-account drafts. Background read failures retain editors and offer
retry; confirmed writes remain confirmed when refresh fails.

Additional exact proxy routes: `GET reviews/{guid}`, `GET|POST
reviews/{guid}/comments`, and `GET|PATCH|DELETE comments/{guid}`. Only list GETs
accept bounded `page`/`pageSize`; mutations retain existing Origin/CSRF/JSON/body
protections. Reporting and moderation are described below; deployment verification
remains open.

## Star ratings

Ratings use five stars with half-star selection (0.5–5), hover previews, keyboard
arrows and touch support. A separate0-star choice retains existing zero ratings.
Save rating explicitly persists the numeric score; Remove rating returns to Not
rated. This replaces the score dropdown while retaining the existing API/session
and draft behavior.

## Reporting and moderation

Other people's reviews/comments offer explicit inline reporting with seven
reasons. Other requires details; details are limited to 1000 characters. Anonymous
visitors can log in and return, without automatically submitting. Duplicate reports
show a terminal message. Unknown write outcomes block replay until deliberate
reload; details remain out of URLs, browser storage and retained mutation data.

Signed-in Account pages link to `/moderation`. The API decides staff access;
ordinary accounts receive access-denied guidance. Staff can inspect Open/Resolved
report queues, dismiss reports or hide their targets, inspect review/comment IDs
directly to hide/restore, and filter audit history at `/moderation/audit`.
Spoilers require explicit reveal. Every decision requires a reason and inline
confirmation bound to the exact server revision. Conflicts retain the reason and
require fresh inspection; deleted targets permit dismissal only.

Private staff caches are account scoped. Session uncertainty conceals/disables
content and aborts work; authorization denial removes private data and persists
until an explicit successful access check. Unknown write outcomes block replay.
Moderator-role administration is separate and has no frontend/proxy route.

The proxy allows exact report POST routes and moderation report/target/audit GETs,
report resolve POST and target visibility POST. Validated URL filters and existing
Origin, CSRF, JSON/body, cookie, cancellation and no-store protections apply.


## Recovery codes

Local signup now includes a save-code step before login. `/reset-password` accepts
a handle, saved code and new password; `/account` generates/replaces codes after
current-password confirmation. Plaintext codes stay in component memory until
acknowledgement/navigation, never shared query/mutation caches or browser storage.
They are shown once; code replacement invalidates the previous one. Reset signs
out every device and requires login. No verification/recovery email is sent, and
admins cannot override recovery. See [ADR 0014](../docs/adr/0014-account-recovery.md)
for loss-of-code and provider-only account behavior. Optional Google/GitHub browser
flows and explicit linking are described below.


## First-run setup

The root installation boundary checks `GET /api/v1/setup/status` before mounting
normal account/session handling or app navigation. A pending fresh installation
shows `/setup`, asking for the API-configured deployment secret and first-admin
account details. Save the returned recovery code, then continue to login. The
Next proxy allows only setup status GET and setup POST, retaining its existing
Origin, CSRF, body-size, fixed-upstream and no-store controls.

Never configure the deployment secret in Next.js; users submit it in the masked
setup form, and .NET verifies it. Keep the existing proxy configuration in place.
Unknown setup status blocks normal content with retry. Confirmed completion is
permanent, so later focus/status failures preserve existing editor drafts. Session
changes cannot clear the independent installation-status query cache. Once complete,
`/setup` redirects home and normal Create account registrations stay unprivileged.

See the root README and [ADR 0015](../docs/adr/0015-installation-setup.md) for API
secret provisioning, additive migration, existing-account upgrades and offline
administrator maintenance. Existing databases are not migrated automatically.


## Google and GitHub browser sign-in

The login/signup forms display only enabled providers discovered from the API.
Provider discovery failure leaves password forms usable. Account settings show
linked providers and explicit Link actions; no unlinking or automatic email merge.
New provider identities complete their handle/display name at
`/auth/external/register`, then sign in as normal Users. Provider-only accounts
recover through their provider; no local password/recovery code is created.

Keep the existing `RATEFREAK_API_URL`, `RATEFREAK_WEB_ORIGIN`,
`RATEFREAK_PROXY_SECRET` and authenticated client-IP configuration. Set API
`Authentication:External:PublicOrigin` to the exact frontend origin, without a
trailing slash, and register these callback URLs with the OAuth apps:

```text
<frontend-origin>/api/v1/auth/external/google/callback
<frontend-origin>/api/v1/auth/external/github/callback
```

Google must be a Web application client; GitHub uses an OAuth App. Use separate
local/production apps. API-only clients may still use the original API-origin
flow. Set provider Enabled/ClientId/ClientSecret only in the API's private
configuration. No provider credentials belong in Next environment variables or
`NEXT_PUBLIC_`. Both remain disabled until explicitly configured.

A dedicated adapter validates authorization hosts/paths, callback URL, state and
PKCE parameters before returning a navigation URL. The browser navigates in the
same tab. The callback forwards only bounded recognized OAuth parameters and
follows exactly one fixed completion hop to the private API. It carries temporary
HttpOnly external/correlation cookies, including bounded ticket chunks, and all
independent issuance/deletion headers. Ordinary API routes still reject redirects.
The API accepts the frontend origin only with valid shared-secret proxy attribution.
It never trusts arbitrary forwarding headers. All auth responses are no-store.

Destinations are fixed: home after sign-in/registration, account after linking,
onboarding for new identities, and a sanitized error page after cancellation or
failure. Codes/state never reach a rendered app page. Configure ingress logs and
telemetry to omit callback queries, cookies and response bodies. Production
acceptance still requires live credentials, HTTPS ingress, protected durable keys
and the other release gates; local controlled-provider tests do not prove them.
