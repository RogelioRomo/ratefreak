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

The browser client calls the same-origin `/api/v1/` proxy. Phase 1 permits only
`GET auth/csrf` and `GET users/me`; account screens and mutation forwarding arrive
in Phase 2. Other paths return 404 and unsupported methods return 405 locally.
The homepage makes no API calls. Missing proxy configuration returns sanitized
503 responses at request time and does not prevent building or serving the homepage.

Set `RATEFREAK_API_URL=http://localhost:5080` in `web/.env.local` for development.
This server-only variable must be an HTTP(S) origin with no credentials, path,
query or fragment. Never use a `NEXT_PUBLIC_` variable for secrets or upstream
configuration. `RATEFREAK_WEB_ORIGIN` is reserved for the exact browser origin
(for example `http://localhost:3000`) that Phase 2 mutation origin checks will use.
If supplied now, it must also be a valid HTTP(S) origin.

The proxy forwards only session and antiforgery cookies, preserves every separate
Set-Cookie update/deletion and Retry-After, and disables HTTP caching. It strips
caller identity/forwarding headers, refuses redirects, and aborts upstream work
after ten seconds or caller cancellation. Production cookie names start with
`__Host-`; development uses `ratefreak.session` and `ratefreak.csrf`. Run Next.js
development against .NET Development locally; production requires corresponding
production cookies and HTTPS. Do not weaken Secure attributes to bridge modes.

.NET retains authentication, authorization and business rules. OAuth redirects
and correlation cookies need their own explicit Phase 2 integration. Mutation
requests are intentionally not enabled yet; that slice must add exact-origin
validation and CSRF forwarding alongside the route allowlist.

**Before enabling account flows or publishing:** the backend currently partitions
CSRF issuance (30/minute) and other IP limits by its immediate connection address.
Browsers behind one Next.js instance therefore share a backend IP bucket. Settle
trusted ingress/client attribution and per-browser abuse controls together with
Phase 2 integration. Do not blindly copy browser X-Forwarded-For headers; .NET
must accept attribution only from explicitly trusted proxies. Phase 1 is local
foundation work and is not ready for public account traffic.

## Frontend conventions

- Shared object contracts live in separate files in `shared/interfaces/`.
  Feature contracts belong in `features/<feature>/interfaces/`; shared UI props
  belong in `components/ui/interfaces/`. Prefer interfaces for objects, type
  aliases for unions/mapped types/tuples. Keep props out of component bodies.
- Routes compose screens, components render UI, feature hooks own queries and
  mutations, and API modules handle transport. Keep files focused by responsibility.
- The browser client is `apiRequest<T>("users/me", options)`; paths are relative
  to `/api/v1/`. It supports cancellation, JSON/204, structured validation errors
  and Retry-After. Future writes acquire fresh CSRF tokens, coalescing pending
  issuance; they do not retry automatically. This plumbing is unit-tested, while
  live mutation routes remain disabled.
- TanStack Query owns browser server-state caching: one stable client per mounted
  provider, one-minute freshness, five-minute garbage collection, focus refetch,
  one transient network/5xx retry, no 4xx or mutation retries. No storage persistence
  or SSR hydration. Feature keys must include filters and private account identity.
- Phase 2 must cancel/clear account-scoped queries and call `resetCsrfState()`
  around session transitions before displaying another identity's data.
- Vitest runs transport tests in Node and component tests in jsdom (per-file
  `@vitest-environment jsdom` directive). Tests mock network I/O, not domain rules.

Progress and the next session's scope are in [the frontend checklist](../docs/frontend-checklist.md).

See the [root README](../README.md) for backend setup and API contracts.
