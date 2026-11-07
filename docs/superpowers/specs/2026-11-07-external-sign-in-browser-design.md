# Google and GitHub browser sign-in

## Intent and scope

Finish the existing Google/GitHub API integration with browser sign-in,
registration and explicit account linking through the Next frontend. Preserve
local passwords, saved recovery codes, existing account IDs and session controls.
No RateFreak email verification or email delivery is added. Provider-only recovery
continues to depend on the provider or another previously linked provider.

The user selected the recommended Next proxy approach on 2026-11-07. This document
specifies that approach; the user approved the written design on 2026-11-07.
Detailed implementation defaults below originated as the builder’s proposal.

## Approaches

1. **Selected: dedicated OAuth support through Next.** The browser uses one public
   origin and existing host-only application cookies. The API retains OAuth
   handlers, credentials, token exchanges, one-time challenges and account policy.
   Next gains narrowly allowed redirect/cookie handling for these routes.
2. **Direct browser navigation to the API.** Reuses existing API redirects but
   requires a same-origin ingress arrangement or additional cross-host session
   design. It does not fit the current frontend-owned host-only cookie boundary.
3. **Move authentication into Next or a hosted service.** Duplicates or replaces
   proven API account/session logic and adds migration work; outside this feature.

## User experience

Login and Create account display independently enabled Google/GitHub options.
Disabled providers are omitted. A discovery failure preserves password forms and
offers a small retry state for provider options. Reuse existing auth styling,
keyboard focus, error and loading components; prevent duplicate starts.

Selecting a provider fetches fresh CSRF protection and starts a same-origin POST.
The frontend then navigates the whole browser to the validated authorization URL.
No popup or cross-origin fetch redirect is used.

The provider returns to the frontend's fixed callback path. The server completes
the API flow before redirecting to a clean frontend URL, so OAuth codes/state do
not appear in a rendered app page or its scripts. Known users land at home with
their normal session. New users reach `/auth/external/register`, choose a handle
and display name, and receive a normal User account/session. No local password or
recovery code is invented for provider-only accounts. Existing backend validation,
verified-provider-email requirements and five-minute onboarding lifetime remain.
Handle conflicts allow retries; expired/missing pending state offers restart.

Account settings show linked providers and available Link Google/Link GitHub
actions. Linking returns to account settings, preserves the existing identity and
refreshes linked status. There is no unlinking or password-setting control.
Email collisions explain that the user should sign in to the existing account
and explicitly link. Cancellation/errors provide a sanitized restart path.

For this slice, destinations are fixed: home after login/registration, account
after linking, profile completion for a new identity, and a dedicated error page
after failure. Arbitrary return URLs and cross-provider account switching are
excluded. Navigation reloads the app; ordinary session discovery clears stale
identity state. Onboarding success uses the existing session transition and
cross-tab synchronization. Refetch CSRF after authentication changes.

## Routes and proxy boundary

Keep the API's existing JSON contracts and fixed routes. Add browser support for:

- GET `/api/v1/auth/providers` and `/api/v1/auth/external/pending`.
- POST `/api/v1/auth/external/{google|github}/start`.
- GET `/api/v1/auth/external/{google|github}/callback`.
- GET `/api/v1/auth/external/complete` (server-followed fixed internal hop only).
- POST `/api/v1/auth/external/register`.
- GET `/api/v1/users/me/logins`.
- POST `/api/v1/users/me/logins/{google|github}/start`.

The ordinary proxy continues rejecting redirects. A dedicated OAuth adapter owns
start and callback navigation; do not turn the proxy into a general redirector.
Start uses existing exact write-Origin validation, JSON limits, CSRF, attribution,
timeouts and no-store. For an allowed API authorization redirect, return only a
validated `{ authorizationUrl }` to the initiating browser; forward its independent
Set-Cookie headers. Validation permits only HTTPS Google authorization at
`accounts.google.com/o/oauth2/v2/auth` or GitHub at
`github.com/login/oauth/authorize`, matching the chosen provider, with callback
`redirect_uri` exactly equal to the configured public frontend callback and with
nonempty state. Reject credentials, fragments, duplicate critical parameters and
other hosts/paths; never fetch the external authorization URL on the server.
Confirm Google's installed handler endpoint before fixing that allowlist in code.

Callback GET is a top-level provider navigation and does not require write Origin
or CSRF. Existing API correlation/state/PKCE and one-time flow validation remain
authoritative. Bound the raw query to 8 KiB and reject duplicate recognized
parameters. Forward only `code`, `state`, `error`, `error_description`, `error_uri`;
discard unrelated provider extras. Never include callback values in app messages,
logs, cache keys or telemetry. An API callback redirect may be followed only to
the exact configured `/api/v1/auth/external/complete` endpoint, without query or
fragment, at most once. Do not forward cookies to any other origin. Carry newly
issued external cookies from callback to completion and forward all independent
cookie issuance/deletion headers back to the browser. A redirect must not turn
into an uncontrolled fetch. Reject malformed/unexpected completion bodies.

The adapter translates completion JSON into fixed frontend redirects: profile ->
home; `{ requiresRegistration: true, provider }` -> onboarding; `{ linked: true,
provider }` -> account. Errors redirect to a fixed error screen using only a small
internal error-code vocabulary, never upstream text or OAuth query values.
Callback/redirect responses set no-store and `Referrer-Policy: no-referrer`.

## Cookies, canonical origin and API changes

Extend only OAuth route cookie selection to include the environment's exact
external-cookie name and bounded ASP.NET correlation names for `google`/`github`.
Inspect the installed handlers' actual names and chunking convention; include
legitimate bounded external-ticket chunks if emitted. Reject duplicate selected
cookie names and excessive cookie count/size. Ordinary routes keep their current
session/CSRF allowlist. Preserve HttpOnly, host-only, SameSite and production Secure
attributes; never weaken cookie security or expose tickets/tokens to JavaScript.

Set `Authentication:External:PublicOrigin` to the browser-visible frontend origin.
The API currently compares that origin with its private incoming scheme/host, so
it needs a narrowly scoped trusted-proxy bridge. Extend existing shared-secret
proxy authentication to expose server-owned proof of valid attribution. For
external start/callback/completion and link-start routes only, an authenticated
proxy request can use the configured canonical origin for OAuth URL generation
and origin validation. Require the proxy's declared browser origin to exactly
match API PublicOrigin. Trust neither arbitrary Host nor public forwarded headers.
Use the configured origin to normalize the OAuth request, not the header value.
Missing/invalid proof or mismatched origin fails closed. Direct API use retains
its existing actual-origin check. Other API routes retain existing host semantics.

Keep external callback abuse partitioning consistent with the authenticated client
attribution already used by starts; current callback limiter uses the proxy socket
IP. Shared/perimeter production budgets remain a separate release gate.

No database migration is expected: external identities and challenges already
exist. Do not change saved-code recovery or installation setup. Pending setup
must still block every provider path, including direct callbacks.

## Configuration and activation

Both providers stay independently disabled until valid API credentials and exact
callback origins are configured. Client ID/secret remain in API user-secrets or
deployment secret storage, never Next public environment or committed files.
Document separate development/production app registration and public frontend
callbacks. Existing API-only clients remain supported. Do not register remote apps,
enable providers, push, deploy or change real accounts as part of implementation.
Live acceptance requires owner-provisioned credentials later; automated tests use
controlled provider responses and clearly report that limit.

## Verification

Prove proxy start allowlists, fixed callback hop, cookie propagation/deletion,
duplicate/chunked cookie limits, timeout/disconnect, malformed JSON, no-store and
sanitized failure handling. Test hostile redirect hosts/paths, duplicate critical
parameters, canonical-origin spoofing and missing/invalid proxy secrets.

Use real PostgreSQL backend tests for canonical browser-origin start/callback,
trusted attribution, direct API compatibility, setup blocking, replay rejection,
link session revocation and ordinary-role assignment. Retain existing OAuth
state/correlation/PKCE/security coverage and fake only external provider HTTP.

Frontend tests cover independently enabled providers, unavailable discovery with
usable passwords, navigation, duplicate-click prevention, handle validation/retry,
expired onboarding, session/cache transition, linked-provider display and failed
linking. Verify real browser navigation across Next/API/disposable PostgreSQL with
controlled provider responses if practical; distinguish it from live provider
acceptance. Inspect keyboard focus, light/dark states and widths 320/768/1024/1440.
Run frontend lint/typecheck/tests/build, full backend tests, EF model consistency
and diff checks. Obtain independent review before claiming completion.

## Primary references

- Existing accepted account policy: [ADR 0010](../../adr/0010-external-sign-in.md).
- [Google web server OAuth](https://developers.google.com/identity/protocols/oauth2/web-server).
- [GitHub OAuth authorization](https://docs.github.com/en/apps/oauth-apps/building-oauth-apps/authorizing-oauth-apps).
- Installed Next route-handler guide: `web/node_modules/next/dist/docs/01-app/01-getting-started/15-route-handlers.md`.
