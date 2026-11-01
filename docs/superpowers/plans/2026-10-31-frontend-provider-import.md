# Provider Discovery and Import Implementation Plan

> Use superpowers:executing-plans with the previously selected native execution.

**Goal:** Search configured providers, preview authoritative media and explicitly import when signed in.
**Architecture:** Extend the restricted proxy, feature-owned APIs and Query hooks; URL-owned discovery state and separate preview routes. Reuse catalog artwork and account/CSRF lifecycle.
**Tech Stack:** Existing Next.js/React/TypeScript/CSS Modules/TanStack Query/Vitest; no dependencies.
**Spec:** docs/superpowers/specs/2026-10-31-frontend-provider-import-design.md (approved).

## Global constraints

Preserve editorial dark/blue and Light design, separate object interfaces, existing feature checkout and all uncommitted work. No automatic commits/push/deploy, backend changes/migrations, real authenticated test imports or new dependencies. User separately requested enabling existing configured providers; that local runtime configuration is authorized.

## Review focus

- Unknown/duplicate parameters, mismatched provider/type/ID and encoded paths cannot widen proxy access.
- Provider changes and browser history restore valid draft/URL state without stale result leakage.
- Login returnTo accepts only canonical internal preview paths; existing account default remains.
- Import completion after route/account changes cannot overwrite another title/user; never replay a mutation automatically.
- Disabled providers, partial/empty pages and429/503 preserve truthful recovery and attribution.

### Task 1: Provider transport and state contracts

Files: web/server/api/provider-policy.ts and .test.ts; route-policy.ts/proxy.ts/proxy.test.ts. web/features/providers/interfaces/*, query/filters.ts and .test.ts, api/providers.ts and .test.ts, hooks/use-providers.ts.
Interfaces: provider identity canonicalizer, search validator, capability/external result/page/import contracts exactly match C# MediaProviders.cs; APIs getProviders/getExternalSearch/getExternalMedia/importMedia accept AbortSignal.
- [x] Add failing boundary tests: exact routes, positiveInt32 vs nonempty GUID identities, provider/type pairs, q controls/length, page1–500, duplicates/unknowns and unchanged catalog/auth restrictions.
- [x] Run focused tests; expected missing provider behavior fails.
- [x] Implement validated forwarding and contracts/API/filter/query keys; default known enabled capability only, URL validation with no query until submit.
- [x] Verify focused tests; expected all pass.

### Task 2: Search and preview UI

Files: web/app/catalog/discover/page.tsx and [provider]/[type]/[externalId]/page.tsx; web/features/providers/components/* and separate props; catalog screen/results links.
Interfaces: discovery screen uses capabilities and normalized URL filters, preview uses validated identity and provider detail query.
- [x] Add failing real QueryClient DOM tests: initial no search, disabled/capability error, explicit filter submission, supported type draft, URL/history restoration, late response isolation, empty pages, preview fields/fallback404/429/503 and safe attribution.
- [x] Run focused tests; expected missing screens fail.
- [x] Implement labeled native responsive controls, result links, bounded pagination without invented totals, existing artwork/fallback and shared error/loading components.
- [x] Add working catalog Find titles entry/recovery links; verify new and existing catalog/navigation tests.

### Task 3: Login continuation and identity-safe import

Files: web/features/providers/query/identity.ts; components/import-control.tsx and tests; web/features/auth/query/return-to.ts and tests, login-form.tsx/auth-entry.tsx.
Interfaces: safePreviewReturnTo(string|null): canonical preview path|null; import uses only provider/externalId/mediaType through existing CSRF API.
- [x] Add failing tests for safe/internal vs external/encoded return paths and unchanged default account login destination.
- [x] Add failing DOM tests for anonymous login link, checking/error session, pending double submission,201/200 success and cache invalidation,401 login recovery, explicit retry, abort/ignore on identity/route changes.
- [x] Implement mutation with AbortController and generation guard bound to current account/identity; no retries, optimistic success or automatic login replay.
- [x] Verify provider and auth/session suites; expected all pass.

### Task 4: Verification and documentation

- [x] Run full tests/lint/typecheck/build/diff check; expected green.
- [x] Batch browser captures both themes320/768/1024/1440; exercise keyboard/history, anonymous preview, synthetic import outcomes. Inspect overflow and console.
- [x] Verify live capability/search/preview reads only; distinguish provider availability from fixture coverage. No real import.
- [x] Fresh combined code/UI finish review and bounded fixes; design documentation comparison.
- [x] Update web/README.md, PRODUCT.md and frontend checklist with exact outcomes/process state; next slice personal library/ratings/reviews.
