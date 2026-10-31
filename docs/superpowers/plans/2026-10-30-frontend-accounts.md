# Frontend Accounts Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans for native execution, task by task. This continues the approved frontend roadmap at the user's explicit request after committing Phase 1.

**Goal:** Implement Phase 2 local registration, login/logout, session navigation and a protected account page.

**Architecture:** Keep .NET Identity and HttpOnly cookies authoritative. Feature-owned hooks use TanStack Query, a client session boundary and the same-origin restricted proxy. Authenticate proxy-supplied network attribution separately from user authentication so rate limits retain the real caller's IP.

**Tech Stack:** Existing Next.js/React/TypeScript/CSS Modules/TanStack Query/Vitest; existing .NET 10 integration tests. No new runtime dependencies or migrations.

**Spec:** `docs/superpowers/specs/2026-10-30-frontend-design.md`, approved Phase 2 roadmap and handoff in `docs/frontend-checklist.md`, existing account contracts/ADR 0004. User authorized continuing Phase 2; maintain native execution and existing interface conventions.

## Global constraints

- Separate interface files inside `interfaces/` directories; focused components, API modules and hooks.
- Register does not sign in. Login uses email. Logout revokes all account sessions.
- .NET owns authentication/authorization/validation. Never return or store browser bearer tokens.
- Keep external sign-in/linking, email delivery/recovery, profile editing and catalog screens out of this slice.
- No secrets in source, browser bundles, query keys, logs or test artifacts. No mutation retries.
- Only exact local-account routes/methods are enabled. Validate fixed web Origin, JSON bodies <=64 KiB and CSRF before mutation forwarding.
- Keep existing branch; Phase 1 is committed as f68535e. Phase 2 remains reviewable and uncommitted at completion unless subsequently requested.

## Review focus

1. Spoofed forwarding/identity headers cannot split abuse budgets or authenticate an account (Tasks 1–2).
2. Missing/foreign Origin, unsupported routes, missing CSRF and oversized bodies never reach upstream (Task 2).
3. Old in-flight queries and CSRF completions cannot populate a newly signed-in or signed-out session (Task 3).
4. 401 means signed out; 403/429/5xx stay explicit errors, and logout failure never claims success (Tasks 3–4).
5. Registration success is anonymous; passwords do not survive submitted mutations in Query cache/history (Tasks 3–4).

### Task 1: Trusted proxy rate-limit attribution

**Files:** `src/RateFreak.Api/Security/BrowserProxyOptions.cs`, `BrowserProxyIdentity.cs`; modify `ApiSecurity.cs`, `Program.cs`; add `tests/RateFreak.IntegrationTests/BrowserProxyTests.cs`.

**Contract:** Optional `BrowserProxy:SharedSecret` is exactly 64 hex characters (32 random bytes). Requests with the matching `X-RateFreak-Proxy-Key` and a single valid `X-RateFreak-Client-IP` use canonicalized IP for existing IP-based rate policies. Compare the secret in constant time. Headers are only service-to-service attribution; never synthesize user claims. Without configured attribution, ignore these headers and use the actual connection address. With configured attribution, reject partially supplied/invalid credentials or IPs with sanitized 403; header-free direct API callers retain connection-address budgets.

- [x] Write HTTP tests: independent forwarded IPs have independent CSRF budgets (30/min); same IP hits 429 at 31; IPv4-mapped addresses share their IPv4 bucket; forged/missing key, comma-delimited or invalid IP rejected; disabled mode ignores forged headers; spoofed X-Forwarded-For cannot bypass a budget.
- [x] Observe RED with `dotnet test tests/RateFreak.IntegrationTests --no-restore --filter FullyQualifiedName~BrowserProxyTests --verbosity quiet`.
- [x] Implement validated options, middleware before rate limiting and the shared IP partition helper. Preserve existing permit counts, direct-client behavior and cookie authentication.
- [x] Repeat focused tests to GREEN; later run full backend suite because the shared limiter changes.

### Task 2: Origin-guarded mutation proxy

**Files:** `web/server/api/{config,route-policy,proxy}.ts`, new `request-body.ts`, `client-attribution.ts` and tests; extend `web/server/interfaces/api-proxy-config.ts`; update catch-all route exports.

**Contracts:** Keep GET csrf/users/me and add POST auth/register/login/logout only. `RATEFREAK_WEB_ORIGIN` becomes mandatory for writes and must exactly match Origin (no trust in Host). Forward only JSON Content-Type, X-CSRF-TOKEN, chosen cookies and server-authenticated attribution headers. `readRequestBody(request: Request): Promise<Uint8Array>` bounds streaming reads to 65,536 bytes, returning local 413 on overflow. GET remains usable without attribution for the Phase 1 foundation; writes require complete attribution configuration.

**Attribution:** `RATEFREAK_PROXY_SECRET` matches backend secret. `RATEFREAK_CLIENT_IP_MODE=vercel` requires runtime `VERCEL=1`, HTTPS upstream and a single valid `x-vercel-forwarded-for` address supplied by Vercel. `loopback` is permitted only in Development and with a loopback upstream; all local callers share the true local loopback budget. Do not support arbitrary trusted header names or production loopback. Missing attribution in production writes fails closed with sanitized 503. Rebuild service headers from server state, ignoring caller proxy-key/client-IP headers.

- [x] Add regression tests for exact routes, same-origin writes, missing Origin/CSRF/JSON, spoofed incoming credentials, production mode restrictions, independent platform IPs, multiple Set-Cookie/deletion headers, unsupported methods, UTF-8 byte/body limits and cancellation.
- [x] Observe RED with `pnpm --dir web test server/api`.
- [x] Implement focused helpers and delegate GET/POST through the shared proxy; other methods retain no-store 405. Forward body within timeout/cancellation lifecycle. Preserve existing cookie/header isolation.
- [x] Repeat tests/typecheck to GREEN.

### Task 3: Account contracts, APIs and session lifecycle

**Files:** `web/features/auth/interfaces/{account-profile,login-request,register-request,session-context-value}.ts`; `api/accounts.ts`; `hooks/{use-session,use-login,use-register,use-logout}.ts`; `components/session-provider.tsx`; `session/{query-keys,transition}.ts`; tests and shared field interfaces as consumed.

**Contracts:** `AccountProfile { id,handle,displayName,email,createdAt,bio? }`; request interfaces match backend camelCase. `getCurrentAccount(signal?: AbortSignal): Promise<AccountProfile | null>` maps only 401 to null. `login(input: LoginRequest): Promise<AccountProfile>`, `register(input: RegisterRequest): Promise<AccountProfile>`, `logout(): Promise<void>` call existing client.

Session context exposes profile/loading/error, transition state and refresh. Session query key `['session']` contains no password or email. Successful identity transitions cancel old queries, reset CSRF, clear caches, and seed the new profile/null before new queries can run. Treat a later session 401 as an identity transition and clear private data; on focus revalidation do not leave another account's cached data visible. Serialize transitions and block conflicting submits. Registration mutation must discard submitted variables after settlement; login/register hook reset removes password-bearing mutation history. No persistent storage. Logout 401 counts as already signed out; other errors preserve an honest retry state.

- [x] Write behavioral tests for API 401/403/429 handling, registration remaining anonymous, login/logout CSRF reset, cancellation of stale data, session expiry clearing caches, duplicate transition prevention and removal of password-bearing mutation state.
- [x] Observe RED with `pnpm --dir web test features/auth`.
- [x] Implement separate API/hooks/session modules; mount SessionProvider inside Query provider. Keep mutation payloads out of user-visible query/debug state and clear settled mutation records.
- [x] Repeat tests/typecheck to GREEN.

### Task 4: Accessible account screens and navigation

**Files:** Thin `web/app/{login,register,account}/page.tsx`; focused auth screen/form/field/error/account components, their separate props interfaces and CSS Modules; client auth navigation inside existing server-rendered SiteHeader.

**Behavior:** Register fields: display name, handle, email, password; native required/max lengths and short password rule help. On 201 show an account-created confirmation with a login link, no automatic login. Login email/password; success navigates to `/account`. No redirect-target parameters in this slice, preventing open redirects. Signed-in login/register routes offer account navigation. Protected `/account` first resolves session; anonymous navigates to `/login`; failed session load shows retry, never profile data. Account page shows private profile read-only with sign-out (explicitly describes all-device logout). Header shows login/register or account/sign-out based on resolved state, not a fabricated identity. Pending navigation is neutral.

Forms use autocomplete, labels, aria-invalid/describedby, visible field/server error summaries, disabled pending submission and preserved non-password fields on failure. Display 429 Retry-After as readable retry guidance. Never display raw exception messages or supply password recovery/external provider links that do not work. Reuse Button/LoadingState/ErrorState; add only consumed TextField and form-error building blocks.

- [x] Write component tests for labeled controls, field-validation errors, generic rejected login, 429 guidance, successful register staying anonymous, successful login/account redirect, protected route loading/error/anonymous behavior, logout failure and pending double-submit protection.
- [x] Observe RED, implement focused components and thin routes, then run tests/typecheck to GREEN.
- [x] Run lint, full frontend tests, typecheck and production build. Run full backend suite. Use sandbox escalation only when a required check needs it.
- [x] Verify running pages at mobile/desktop with keyboard navigation and a live local Next-to-.NET account lifecycle using isolated temporary test data if available. Report any unverified integration explicitly.
- [x] Update README/checklist with actual evidence, secret/trust-mode setup, deployment limits and next-session scope. Independent whole-change review, address significant findings with regression tests, then stop before external sign-in or Phase 3.

## Sources and implementation decisions

The approved hosting direction is Vercel for Next.js; fixed known Next egress IPs are not assumed. A separate server-only shared credential authenticates attribution across that boundary. Vercel supplies `x-vercel-forwarded-for`; restrict trust to Vercel runtime/deployment, never generic self-hosted requests. A self-hosted ingress adapter needs a separately verified trust configuration.

- Vercel headers: https://vercel.com/docs/headers/request-headers
- ASP.NET proxy guidance: https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer?view=aspnetcore-10.0

Public release still requires actual hosting/HTTPS/key-storage and local-account recovery decisions. This phase implements the attribution mechanism and tests isolation; it does not claim a deployment has been configured.

## Completion evidence

Completed 2026-10-30; Phase 1 committed f68535e; Phase 2 is complete and approved for commit/merge to main.
Final checks: 123 frontend tests, lint/typecheck/build, 590 backend tests all pass.
Live isolated Next/.NET/PostgreSQL browser lifecycle, cross-tab logout and mobile/
desktop screenshots passed. Independent review session-coherence finding fixed
with regression tests; see the frontend checklist for operational limits.

Ruling: Add one-minute visible-tab session revalidation, protected-entry checks and
credential-free BroadcastChannel/focus/pageshow synchronization after review found
persistent-layout caches could hide expiry indefinitely. Defer synchronization
safely across a pending account action. This supersedes initial infinite freshness.

Ruling: Successful logout from a protected page uses its single anonymous redirect
to login; public-page logout stays in place. Removed competing navigation to home
after a live-browser race and a failing regression reproduced it.
