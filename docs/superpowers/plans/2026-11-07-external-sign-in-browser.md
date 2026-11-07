# External sign-in browser implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [x]`) syntax for tracking.

**Goal:** Enable optional Google/GitHub sign-in, onboarding and linking through Next using existing API authentication.

**Architecture:** A dedicated OAuth adapter validates external redirects and follows only the fixed API completion hop. A secret-authenticated canonical-origin bridge lets existing API handlers generate frontend callbacks. Ordinary proxy routes keep rejecting redirects.

**Tech Stack:** ASP.NET Core Identity/OAuth, PostgreSQL, Next 16, React Query, Vitest, xUnit.

**Spec:** `docs/superpowers/specs/2026-11-07-external-sign-in-browser-design.md` (approved by user).

## Global constraints

- Preserve local passwords, saved-code recovery, normal User registration and explicit linking; no automatic email merge.
- Fixed destinations only; no secrets/tickets in browser JavaScript or logs.
- Providers remain disabled until operator configures credentials; no live activation, push or merge.
- Work on `feat/external-sign-in-browser`; execute inline, with fresh final review. Retain the existing local migration notes.

## Review focus

- Chunked tickets and callback cookie deletion must survive the internal completion hop (Task 2).
- Logout/session revocation during linking must remain authoritative (Task 1 + existing backend suite).
- Cancellation, denial and unknown upstream responses must produce clean sanitized restart pages (Tasks 2/3).
- Provider discovery failure must preserve usable password forms (Task 3).
- Multi-instance callback budgets must use authenticated client attribution rather than proxy socket IP (Task 1; production shared budgets remain open).

### Task 1: API canonical OAuth origin

Files: modify `Security/BrowserProxyIdentity.cs`, `Security/ExternalAuthentication.cs`; create `Security/ExternalBrowserOrigin.cs`; test `tests/RateFreak.IntegrationTests/ExternalBrowserProxyTests.cs` (existing partial ExternalAccountTests).

Interfaces: `BrowserProxyIdentity.IsAuthenticated(HttpContext): bool`; middleware `UseExternalBrowserOrigin(WebApplication)` before external auth guard, after installation guard. Header `X-RateFreak-Browser-Origin` must exactly match configured PublicOrigin; only authenticated attribution and exact external/login-start routes normalize scheme/host.

- [x] Write tests proving private API requests with valid proxy proof generate frontend redirect_uri, complete onboarding/link flows, preserve roles/replay/session revocation, reject absent/bad proof/origin, keep direct compatibility and isolate callback IP budgets.
- [x] Run focused backend tests; observe new trusted-origin assertions fail.
- [x] Implement authenticated proof, narrow canonical-origin middleware and attributed callback budget.
- [x] Run focused external suite; expected all pass.

### Task 2: Next OAuth boundary

Files: create `web/server/api/oauth-policy.ts`, `oauth-cookies.ts`, `oauth-proxy.ts` and tests; modify `proxy.ts`, `route-policy.ts` only for delegation and ordinary JSON routes.

Interfaces: `oauthRoute(method,path): {provider,kind}|null`; `proxyOAuthRequest(request,path): Promise<Response>`; `oauthCookieHeader(header,development): string`. Exact Google/GitHub allowlists, 8-KiB query cap, 16-KiB selected-cookie cap, 32 selected cookies, 5 external chunks maximum; fixed completion only. Start returns `{authorizationUrl}`, callback redirects cleanly to fixed pages; complete is not browser-exposed. Shared config requires webOrigin/proxySecret/clientIpMode.

- [x] Write behavior tests for valid starts/callback completion, staged cookies, chunks/deletion, origin/CSRF guards, hostile redirects, malformed data/query/cookies, timeout/disconnect, no-store and sanitized failures.
- [x] Run focused Vitest tests; observe missing adapter/behavior failures.
- [x] Implement adapter with fixed fetch origin, manual redirects, bounded response reads and cookie propagation; route ordinary discovery/pending/register/logins through existing proxy using OAuth cookie selection only where required.
- [x] Run focused boundary and existing proxy tests; expected all pass.

### Task 3: Browser UI and acceptance

Files: `web/features/auth/api/external.ts`, provider/onboarding/login-status interfaces, components for provider options/linked providers/onboarding/error; pages under `web/app/auth/external`; modify existing login/register/account screens; update README/web README/release evidence.

Interfaces: typed `getProviders`, `startExternal(provider,link)`, `getPendingExternal`, `registerExternal({handle,displayName})`, `getLinkedProviders`; sensitive starts are component-local and never cached. Browser navigation injected via a small `navigateToProvider(url)` helper for testability; call `window.location.assign`.

- [x] Write UI behavior tests covering independent availability, discovery failure/password fallback, duplicate-click prevention, onboarding retry/expiry, session transition and linking states.
- [x] Run new tests; observe missing UI behavior failures.
- [x] Implement using incumbent auth components/styles, fixed destinations and existing session transition APIs. No new design system or dependencies.
- [x] Run tests/lint/typecheck/build and full backend tests; EF model consistency and diff checks must pass.
- [x] Perform bounded browser/visual acceptance against dedicated services if available. Record limits honestly; no live provider acceptance without owner credentials.
- [x] Fresh reviewer checks whole diff against spec; fix material findings with regression proof. Leave changes reviewable on feature branch, without merging/pushing.

Verified 2026-11-07: 668 frontend / 625 backend tests, lint, typecheck, production build, EF model consistency and diff checks passed. Browser acceptance and review evidence are in the release record. The user subsequently approved a local commit, merge to main and deletion of the feature branch after live local testing. No remote push is included.
