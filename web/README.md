# RateFreak web

Next.js App Router with TypeScript, ESLint, and CSS Modules. This app shares
one repository with the .NET API, but has its own dependencies and deployment.
Use Node.js 24 LTS and pnpm 10 (the exact pnpm version is pinned in package.json).

From the repository root:

```bash
pnpm --dir web install --frozen-lockfile
pnpm --dir web dev
```

Open http://localhost:3000. The homepage and application shell run without the API or database.

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
`GET auth/csrf`, `GET users/me` and `POST auth/register`, `auth/login`, `auth/logout`.
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

See the [root README](../README.md) for backend setup and API contracts.
