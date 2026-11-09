# RateFreak frontend design and phased roadmap

Date: 2026-10-30. Historical approved frontend roadmap. The phases are now
implemented locally; use [current status](../../STATUS.md) for remaining work and
current decisions. The original scope and implementation wording below are dated.

## Intent

Deliver the existing .NET capabilities through the Next.js application in small,
runnable phases across separate sessions. Keep components focused and contracts
explicit so future work does not require loading large files or rebuilding context.

## Constraints and architecture

- Keep the existing Next.js App Router, React, strict TypeScript, CSS Modules,
  Node 24 and pnpm 10 setup in `web/`.
- .NET owns authentication, authorization, validation and business rules. Browser
  requests use a thin same-origin Next.js proxy; provider secrets remain server-only.
- Use interfaces for object contracts, including props, API inputs/outputs and
  configuration. Declare them in separate files inside `interfaces/` folders.
  Type aliases remain appropriate for unions, mapped types and tuples.
- Feature contracts live in `web/features/<feature>/interfaces/`; cross-feature
  contracts live in `web/shared/interfaces/`. UI-specific shared props live in
  `web/components/ui/interfaces/`. No speculative interfaces or wrapper abstractions.
- Route files compose screens. Components render UI. Feature hooks own queries
  and mutations. API modules own transport. Split by responsibility, without an
  arbitrary file-length rule or extracting every trivial element.
- Use TanStack Query for browser server-state caching. Keep ephemeral UI state
  local, and shareable filters/pagination in URL parameters. Do not persist the
  query cache to browser storage initially.
- Keep server components by default; introduce client boundaries only where hooks,
  interactions or the Query provider need them. Do not introduce SSR query hydration
  in Phase 1; revisit when an implemented page needs it.

## Approaches

Chosen: feature-by-feature vertical delivery. Each phase delivers a working flow
and its focused tests. This exposes integration issues early and fits separate sessions.

Alternative: build all screens with mock data first. This gives faster visual
coverage but delays verifying API and authentication behavior. The user selected
feature-by-feature delivery.

## Delivery roadmap

| Phase | Working outcome | Dependencies |
| --- | --- | --- |
| 1: Foundation | Responsive shell, navigation, shared UI, Query provider, API client and bounded proxy | Existing scaffold |
| 2: Accounts | Register, log in/out, session and protected routes; external sign-in as its own follow-up slice | 1 |
| 3: Catalog | Search/filter/paginate, detail, provider preview and authenticated import | 1–2 |
| 4: Personal library | Library statuses/filters, ratings and one editable review per media | 2–3 |
| 5: Collections | Metadata/visibility first, then fields/items and destructive schema-change confirmation | 2–4 |
| 6: Profiles/social | Public profiles/editing, follows, review comments in separate slices | 2–4 |
| 7: Moderation/release | Reports/moderation, cross-feature accessibility and end-to-end checks, deployment verification | Relevant prior flows |

Each session implements one agreed slice, records actual checks and outstanding
work, and stops. Detailed later-phase plans are written when their phase starts,
using actual backend contracts rather than copying stale roadmap statements.

## Phase 1 scope

Deliver a responsive homepage and header with working Home navigation; other
features appear as informational upcoming capabilities, without dead navigation
or fabricated account/search functionality. Proposed visual default: understated
CSS Modules layout with readable typography, visible keyboard focus and reusable
spacing/color variables. Visual branding remains open for a later user decision.

Build shared Button, LoadingState, EmptyState and ErrorState only. Keep the shell
separate from these primitives. No additional component framework in this slice.

Install TanStack Query v5, Vitest, React Testing Library and jsdom (versions verified
against official documentation and the installed runtime at implementation time).
Use one stable browser QueryClient, never a module-level shared server cache.
Initial defaults: staleTime 60 seconds, gcTime 5 minutes, refetch on window focus,
no mutation retries; retry queries once only for network failures or 5xx responses.
Do not retry 4xx responses automatically. Later phases may override per query.
Feature keys include all filters and account identity for private data. Session
changes cancel and clear account-scoped data; Phase 2 implements and tests that
lifecycle alongside the account hooks. HTTP no-store and in-memory Query caching
are distinct: the proxy never caches responses, including private data.

The browser client sends credentials same-origin, supports AbortSignal, parses
Problem Details/validation errors and Retry-After, handles 204 without JSON parsing,
and uses a fresh CSRF token before each mutation initially. Token issuance is
coalesced for concurrent requests of the same identity, never retained across login
or logout. The client accepts only relative API paths and never logs bodies/tokens.

Use a server-only RATEFREAK_API_URL, validated as an HTTP(S) origin without path,
credentials, query or fragment. The proxy forwards to that fixed origin and an
explicit route/method allowlist. Phase 1 enables only GET auth/csrf and GET
users/me for foundational tests; add each business route with its feature.
Reject unknown paths/methods locally. Never accept a caller-supplied destination.
Relay status, content type, Retry-After and every Set-Cookie value separately.
Forward only the configured Development/Production session and antiforgery cookie
names; never trust caller-supplied identity or forwarding headers. Reject foreign
Origin on mutations; require an exact configured public origin through
RATEFREAK_WEB_ORIGIN when mutation routes are introduced. Do not derive trust
from an arbitrary Host or forwarded header. Missing Origin on browser mutation
requests is rejected. Preserve cookie attributes/deletions; do not weaken Secure.
Use manual redirect handling and reject upstream redirects until OAuth is designed
in Phase 2. Apply a 10-second upstream timeout and propagate request cancellation;
return sanitized 502/504 Problem Details for transport failure/timeout.

OAuth redirects/correlation cookies require their own allowlist and callback
design in the external-sign-in slice. Generic proxying must not silently enable them.

## Verification and completion

Phase 1 must pass lint, typecheck, focused frontend tests and production build.
Transport tests use mocked upstream HTTP; no credentials or live provider calls.
Test cookie forwarding and multiple Set-Cookie/deletion headers, restricted paths,
no-store, cancellation, sanitized errors and client parsing. Verify responsive
shell and keyboard navigation manually at narrow and desktop widths.

No authentication screens, business-feature screens, schema migrations, deployment,
automatic commits or provider activation in Phase 1. Shipping publicly remains
subject to the repository's deployment and account-recovery decisions.

## Handoff

Use `docs/frontend-checklist.md` for progress and resume notes. Each completed
slice records changed behavior, exact verification results and the next slice.
Keep the backend checklist's historical evidence intact.
