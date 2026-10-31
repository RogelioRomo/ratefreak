# Frontend Foundation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [x]`) syntax for tracking.

**Goal:** Deliver Phase 1 only: a responsive shell, reusable UI, TanStack Query setup and tested browser-to-API transport.

**Architecture:** Next.js server components compose the shell and homepage. A small client provider owns browser query caching; shared transport modules call a fixed-origin, allowlisted Next.js proxy. .NET retains all business rules and authentication.

**Tech Stack:** Existing Next.js App Router, React, strict TypeScript, CSS Modules, Node 24, pnpm 10; add TanStack Query v5, Vitest, React Testing Library and jsdom.

**Spec:** `docs/superpowers/specs/2026-10-30-frontend-design.md` (approved).

## Global Constraints

- Keep the existing Next.js App Router, React, strict TypeScript, CSS Modules, Node 24 and pnpm 10 setup in `web/`.
- Use interfaces for object contracts, including props, API inputs/outputs and configuration. Declare them in separate files inside `interfaces/` folders.
- Type aliases remain appropriate for unions, mapped types and tuples.
- Feature contracts live in `web/features/<feature>/interfaces/`; cross-feature contracts live in `web/shared/interfaces/`. UI-specific shared props live in `web/components/ui/interfaces/`.
- Route files compose screens. Components render UI. Feature hooks own queries and mutations. API modules own transport.
- Do not persist the query cache to browser storage initially.
- Do not introduce SSR query hydration in Phase 1.
- Phase 1 enables only GET auth/csrf and GET users/me for foundational tests; add each business route with its feature.
- Apply a 10-second upstream timeout and propagate request cancellation; return sanitized 502/504 Problem Details for transport failure/timeout.
- No authentication screens, business-feature screens, schema migrations, deployment, automatic commits or provider activation in Phase 1.
- Read `web/AGENTS.md` and relevant installed `web/node_modules/next/dist/docs/` guides before writing code. Preserve the existing Next.js configuration unless an evidenced incompatibility requires a documented change.
- Install compatible stable dependency versions after checking official documentation at execution time; commit no dependency changes outside `web/`.
- Session-change cache clearing and OAuth callbacks belong to Phase 2. Do not quietly enable mutation proxy routes in Phase 1.

## Review Focus

1. Multiple Set-Cookie headers, including Expires commas and deletions, remain separate and retain attributes (Task 2).
2. Encoded paths, absolute URLs and arbitrary forwarded headers cannot change the upstream destination or identity (Tasks 2–3).
3. Aborted/timed-out requests stop upstream work; transient failures produce sanitized errors without accidental retries of mutations (Tasks 2–4).
4. Empty/non-JSON/malformed responses and 429 Retry-After remain useful to the UI without exposing raw upstream bodies (Task 3).
5. Provider re-renders preserve browser cache while independent server renders never share a QueryClient (Task 4).

## File boundaries

All paths below are repository-relative. Tests are colocated with their owning module.

| Area | Responsibility |
| --- | --- |
| `web/shared/api/` | Browser request, error parsing, CSRF issuance |
| `web/shared/query/` | QueryClient construction and retry policy |
| `web/shared/interfaces/` | Shared transport and children contracts |
| `web/server/api/` | Fixed-origin configuration, route policy, cookie filtering, forwarding |
| `web/server/interfaces/` | Server configuration and proxy context contracts |
| `web/app/api/v1/[...path]/route.ts` | Thin Next.js handler delegating to proxy |
| `web/app/providers.tsx` | Client QueryClientProvider boundary |
| `web/components/layout/` | Header and shell, with colocated interfaces and CSS |
| `web/components/ui/` | Button and loading/empty/error feedback, separate props interfaces |
| `web/features/home/components/` | Homepage content; no fabricated data requests |

Do not create empty feature directories for later phases. Do not introduce barrel exports or a generic repository layer.

### Task 1: Test harness and shared transport contracts

**Files:** Modify `web/package.json`, `web/pnpm-lock.yaml`; create `web/vitest.config.ts`, `web/test/setup.ts`, `web/shared/interfaces/{problem-details,api-request-options,csrf-response,children-props}.ts`, `web/shared/api/{api-error,api-error.test}.ts`.

**Interfaces:**
- `ProblemDetails`: optional `type`, `title`, `status`, `detail`, `instance`, `traceId`; `errors?: Record<string, string[]>`.
- `ApiRequestOptions`: `method?: "GET" | "POST" | "PUT" | "PATCH" | "DELETE"`, `body?: unknown`, `signal?: AbortSignal`.
- `CsrfResponse`: `requestToken: string`.
- `ChildrenProps`: `children: ReactNode`.
- Produce `ApiError extends Error` with `status: number`, `problem?: ProblemDetails`, `retryAfter?: string`. Network errors remain native errors; cancellation remains AbortError.

- [x] Verify dependency compatibility using official TanStack Query, Vitest and React Testing Library documentation and Node/pnpm versions. Install Query v5 as a runtime dependency, the test tools as dev dependencies, and add `test: "vitest run"`. Add `@testing-library/jest-dom` for semantic assertions.
- [x] Configure Vitest aliases for `@/`, default node environment, and jsdom only for `*.test.tsx`; load DOM matchers/cleanup in setup. Stub `server-only` only in test resolution, preserving its application boundary.
- [x] Write `api-error.test.ts`: a constructed 429 error retains status, validation fields and raw Retry-After; its message uses a safe title, never a request body. Run `pnpm --dir web test shared/api/api-error.test.ts` and observe missing implementation failure.
- [x] Implement the separate contracts and `ApiError` constructor `(status: number, problem?: ProblemDetails, retryAfter?: string)` with generic fallback `Request failed.`.
- [x] Repeat the focused test and run `pnpm --dir web typecheck`; both must exit zero.

### Task 2: Fixed-origin proxy with cookie and request isolation

**Files:** Create `web/server/interfaces/{api-proxy-config,proxy-context}.ts`, `web/server/api/{config,route-policy,cookies,proxy}.ts`, matching `*.test.ts` files, and `web/app/api/v1/[...path]/route.ts`.

**Interfaces:**
- `ApiProxyConfig`: `apiOrigin: string`, `webOrigin?: string`, `cookieNames: readonly string[]`, `timeoutMs: number`.
- `ProxyContext`: `params: Promise<{ path: string[] }>` (separate file; retain framework-compatible promise semantics).
- `readApiProxyConfig(): ApiProxyConfig`, called at request time rather than during build.
- `isAllowedRoute(method: string, path: readonly string[]): boolean`.
- `selectCookies(header: string | null, names: readonly string[]): string`.
- `proxyRequest(request: Request, path: readonly string[]): Promise<Response>`.
- Next handler: `GET(request: Request, context: ProxyContext): Promise<Response>`; explicitly reject POST/PUT/PATCH/DELETE/HEAD/OPTIONS with no-store 405 responses. No upstream mutation forwarding in this phase.

- [x] Write configuration/policy/cookie tests: missing/invalid upstream origin rejected; allow only HTTP(S) origin without credentials/path/query/fragment; permit a normal trailing slash and normalize it; only the two exact GET paths pass. Reject extra segments, dot segments, encoded separators, backslashes, absolute targets and unexpected query parameters (neither enabled route requires queries).
- [x] Add cookie tests for exact environment-selected session/CSRF names; strip unrelated, OAuth, similarly prefixed and forged identity cookies/headers. Production uses `__Host-ratefreak.session` and `__Host-ratefreak.csrf`; Development uses `ratefreak.session` and `ratefreak.csrf`.
- [x] Add mocked-fetch proxy tests: upstream 200/401/429 pass status/body/type and Retry-After; separate cookie headers preserve commas, Secure/HttpOnly/SameSite/path and expiry deletion; always return `Cache-Control: no-store`. Reject unsupported requests without fetching. Do not forward Authorization, X-User-Id, Host or X-Forwarded-* headers. Do not forward incoming Cookie wholesale.
- [x] Add transport tests: fetch uses fixed origin, `cache: "no-store"`, `redirect: "manual"` and a cancellation signal; upstream redirects produce sanitized 502 without following or relaying Location. Network error yields sanitized 502; fake-time 10-second timeout yields 504 and aborts upstream. Caller cancellation aborts upstream and is not converted to a successful response. Invalid configuration yields sanitized 503 without exposing the environment value.
- [x] Run `pnpm --dir web test server/api` and observe missing implementation failures.
- [x] Implement the modules and thin route handler. Import `server-only` in server modules. Allow only necessary request headers (`Accept` and selected cookies for these GET routes) and response headers (Content-Type, Retry-After, separate Set-Cookie); do not copy Content-Length/Content-Encoding after fetch decoding. Set no-store on local error and rejection responses too. Clean up cancellation listeners/timers on every exit.
- [x] Run the focused tests plus `pnpm --dir web typecheck`; expect all passing. Inspect route exports against installed Next.js docs, including handling of automatic HEAD behavior.

### Task 3: Browser API client and mutation CSRF plumbing

**Files:** Create `web/shared/api/{request,parse-api-error,csrf}.ts` with matching `*.test.ts` files. Consume Task 1 interfaces.

**Interfaces:**
- `apiRequest<T>(path: string, options?: ApiRequestOptions): Promise<T>`; callers use `T = void` for 204 endpoints.
- `parseApiError(response: Response): Promise<ApiError>`.
- `getCsrfToken(signal?: AbortSignal): Promise<string>` and `resetCsrfState(): void`.
- Paths are relative to `/api/v1/`, e.g. `users/me`; disallow leading slash, scheme, fragment, dot/backslash segments or encoded path separators. Validated URL query strings remain available for future feature filters.

- [x] Write request tests: GET uses same-origin credentials and no-store; passes AbortSignal; JSON body is encoded with correct content type; 204 returns undefined without parsing; invalid paths reject before fetch. Assert malformed successful JSON produces a safe error rather than rendering arbitrary text.
- [x] Write parser tests: valid Problem Details retains field errors/traceId and Retry-After; non-JSON, empty and malformed error bodies use safe status-based fallback. Preserve 401/403/429/500 statuses. No raw HTML appears in an error message.
- [x] Write CSRF tests: every non-GET request fetches `auth/csrf` before its own request and attaches X-CSRF-TOKEN; concurrent issuance shares only the pending operation, while a later mutation obtains a fresh token. Reset discards pending state and prevents stale completion repopulating it. Failed issuance blocks the mutation and is not cached. Aborting one waiter does not cancel issuance for others or allow that waiter's mutation to run.
- [x] Run `pnpm --dir web test shared/api` and observe failures for missing modules.
- [x] Implement transport, parser and CSRF modules. Use a separate GET path for token acquisition to avoid recursive mutation interception. The pending issuance is shared but each caller observes its own AbortSignal. Never automatically retry writes. Phase 1 unit tests exercise write plumbing with mocked fetch only; live proxy writes stay disabled. Phase 2 must call reset around session transitions.
- [x] Run the same tests and typecheck; expect zero failures.

### Task 4: Stable TanStack Query provider

**Files:** Create `web/shared/query/{create-query-client,create-query-client.test}.ts`, `web/app/providers.tsx`, `web/app/providers.test.tsx`; modify `web/app/layout.tsx` to consume `ChildrenProps` and render the provider inside body.

**Interfaces:**
- `createQueryClient(): QueryClient` creates an independent client each call.
- `Providers(props: ChildrenProps)` holds a stable client for its mounted browser tree, using the documented TanStack/React initialization pattern compatible with this Next.js version. Never store a server-shared instance at module scope.

- [x] Write QueryClient tests asserting staleTime 60,000, gcTime 300,000, focus refetch enabled and mutations retry false. Query retry permits one retry for TypeError network errors or ApiError status >=500; rejects 4xx, AbortError and arbitrary programming errors.
- [x] Write provider tests: child can consume query context; rerender preserves its client and cached data; two independently mounted providers do not share data. Factory calls also produce independent caches. Render tests use a harmless probe component with its props interface in `web/test/interfaces/` if needed.
- [x] Run `pnpm --dir web test shared/query app/providers.test.tsx` and observe missing implementation failures.
- [x] Implement the factory and provider; keep the root layout a server component. Add no devtools, persistence, hydration or actual account query in this phase.
- [x] Repeat tests and typecheck; expect passing results.

### Task 5: Responsive shell, shared feedback and handoff

**Files:** Create `web/components/layout/{app-shell,site-header}.tsx`, colocated CSS Modules, `web/components/layout/interfaces/app-shell-props.ts`, `web/components/layout/app-shell.test.tsx`; create `web/components/ui/{button,loading-state,empty-state,error-state}.tsx`, colocated CSS, `web/components/ui/interfaces/{button-props,loading-state-props,empty-state-props,error-state-props}.ts`, and `web/components/ui/feedback.test.tsx`; create `web/features/home/components/home-intro.tsx` and its CSS Module; modify `web/app/{layout.tsx,page.tsx,globals.css,page.module.css}`, `web/README.md`, `docs/frontend-checklist.md`.

**Interfaces:**
- `AppShellProps extends ChildrenProps`.
- `ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement>` with `variant?: "primary" | "secondary"`.
- `LoadingStateProps`: `label?: string` (default `Loading…`).
- `EmptyStateProps`: `title: string`, `description?: string`.
- `ErrorStateProps`: `title?: string` (default `Something went wrong.`), `description?: string`, `onRetry?: () => void`.
- `AppShell`, `SiteHeader`, `HomeIntro`, `Button`, `LoadingState`, `EmptyState`, `ErrorState` are named exports. Only interactive feedback needs a client boundary. Button defaults to `type="button"`.

- [x] Write semantic component tests: one main landmark/id `main-content`, a keyboard skip link, named Home link resolving `/`, rendered children; upcoming features are text rather than inactive links. Loading announces status; errors announce alert and expose a functional Retry button only when supplied; Button honors disabled and explicit type; empty state has a heading.
- [x] Run `pnpm --dir web test components` and observe missing component failures.
- [x] Implement focused components and homepage composition. Preserve existing product wording where useful, include movies/TV/albums/games, and identify library/collections/reviews as upcoming. Use CSS variables, responsive wrapping, readable contrast, reduced-motion support if adding animation, and visible focus. Do not add pretend search/login controls. Reuse or retire old homepage CSS through actual usage; leave no dead imports.
- [x] Run component tests, then `pnpm --dir web lint`, `pnpm --dir web typecheck`, `pnpm --dir web test` and `pnpm --dir web build`; all must exit zero. Build must work without API environment configuration because validation is request-time.
- [x] Start the app locally and check narrow (375px) and desktop (1280px) layouts, Tab/Shift-Tab focus, skip link and Home navigation. Record what was actually checked; if browser tooling is unavailable, record manual verification as outstanding rather than claiming it passed.
- [x] Update `web/README.md` with test commands, server-only RATEFREAK_API_URL, optional future RATEFREAK_WEB_ORIGIN, Development/Production cookie expectations, timeout/proxy restrictions and interface locations. Document local browser/API runtime mode alignment; do not change Secure cookies to compensate for mismatched modes.
- [x] Update `docs/frontend-checklist.md` with results and Phase 2 handoff: local account screens, origin-guarded mutation allowlist, CSRF reset/cache clearing on session changes, and separate OAuth work. Record exact test counts/commands, known limits and any pending visual check. Run `git diff --check` and inspect changes for secrets and unrelated edits. Stop after Phase 1; do not commit or start Phase 2 automatically.

## Plan self-review

All Phase 1 design requirements map to the five tasks. Origin enforcement for
mutation forwarding and identity-aware cache clearing remain explicit Phase 2
work because no mutation proxy routes or session transitions ship in Phase 1.
Tasks 2–4 exercise the five review conditions above. Future feature keys and
account DTOs are deliberately not invented before their consuming feature exists.

## Execution handoff

Recommended: native execution, implementing these tasks in order in one Phase 1
session. These tasks share transport contracts, so sequential execution keeps
changes easy to inspect. The user may choose subagent-driven execution instead.
Review the completed Phase 1 result before moving to Phase 2 in another session.

## Completion evidence

Completed 2026-10-30 using native execution. See `docs/frontend-checklist.md` for
84 passing tests, lint/typecheck/build results, browser checks, review fixes and
the backend-IP rate-limit blocker for Phase 2. No commits or deployment performed.
