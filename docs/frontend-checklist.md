# Frontend delivery checklist

Design: [frontend roadmap](superpowers/specs/2026-10-30-frontend-design.md).

- [x] Agree on feature-by-feature delivery and separate implementation sessions.
- [x] Agree on separate interface files, focused components and TanStack Query.
- [x] Review and approve Phase 1 written design.
- [x] Write [Phase 1 implementation plan](superpowers/plans/2026-10-30-frontend-foundation.md).
- [x] Review implementation plan and select native execution.
- [x] Phase 1: foundation, transport/proxy, Query provider and shell.
- [x] Phase 2: local accounts and session lifecycle.
- [ ] Phase 2 follow-up: external sign-in and identity linking.
- [ ] Phase 3: catalog browsing/search and details.
- [ ] Phase 3 follow-up: provider preview/import.
- [ ] Phase 4: library, ratings and reviews.
- [ ] Phase 5: collection metadata and visibility.
- [ ] Phase 5 follow-up: fields, items and schema-change confirmation.
- [ ] Phase 6: profiles/editing, follows and comments in focused slices.
- [ ] Phase 7: moderation and release verification.

## Resume notes

2026-10-30: approved roadmap, written design and Phase 1 task plan; completed
Phase 1 using native execution on `feat/frontend-foundation`. Phase 1 was committed
as `f68535e` at the user's request, then Phase 2 was implemented on the same branch.
Phase 2 is complete and approved for commit/merge to main. Its backend changes affect proxy attribution
and rate-limit partitioning only; no migrations or provider settings changed.

## Phase 1 evidence

- [x] Vitest/Testing Library harness; object contracts and props in separate
  `interfaces/` files. Node types updated to match the Node 24 runtime.
- [x] Restricted fixed-origin GET proxy (`auth/csrf`, `users/me`), separate cookie
  headers/deletions, no-store, sanitized errors and timeout/cancellation handling.
- [x] Browser client: validated paths, JSON/204, Problem Details/validation errors,
  Retry-After, cancellation and fresh/coalesced CSRF token issuance.
- [x] TanStack Query v5 provider: stable mounted cache, independent clients,
  one-minute freshness, five-minute GC, focus refetch and bounded transient retries.
- [x] Responsive server-rendered shell/homepage and shared Button/loading/empty/error
  components. No nonfunctional search/login controls or dead feature links.
- [x] Test-first focused checks; final `pnpm --dir web test`: **84/84 tests pass**
  across 12 files. `lint`, `typecheck` and `build` all exit zero. Build succeeds
  without API environment configuration; homepage is static, proxy is dynamic.
- [x] Production HTTP smoke: homepage 200; unconfigured enabled proxy route 503;
  unknown GET route 404; POST/HEAD/OPTIONS 405; proxy responses use no-store.
- [x] Headless Chrome at 375px and 1280px: screenshots inspected, no horizontal
  overflow or runtime exceptions; Tab to skip link, Enter focuses main,
  Shift-Tab and Home navigation work. Screenshots saved in `/tmp/ratefreak-phase1-375.png`
  and `/tmp/ratefreak-phase1-1280.png` (temporary local artifacts).
- [x] Independent whole-change review. Fixed error-body cancellation being converted
  into ApiError, plus the analogous token-body path and signal-completion race;
  four regression tests observed RED→GREEN, followed by the full green suite/build.

## Phase 2 evidence

Plan: [frontend accounts](superpowers/plans/2026-10-30-frontend-accounts.md).

- [x] Thin `/register`, `/login`, `/account` routes, feature-owned interfaces/APIs/hooks,
  labeled forms and field errors, generic login errors, 429 guidance and protected
  account display. Registration stays anonymous; logout explains all-device revocation.
- [x] Exact-Origin/CSRF/JSON/body-size guarded POST account allowlist. Other methods
  and business routes remain closed; server-only attribution never creates a user.
- [x] Fixed the shared proxy-IP budget mechanism through authenticated attribution;
  eight backend HTTP tests prove independent client budgets, mapped-IP normalization,
  forged-header rejection and unchanged direct-client behavior. See ADR 0013 and
  `web/README.md` for matching server secrets and verified runtime-mode requirements.
- [x] Session transitions cancel old requests, clear caches and pending CSRF state;
  submitted passwords are absent from mutation variables/history and browser storage.
- [x] Independent review found unbounded session freshness. Fixed with active-tab
  one-minute polling, window focus/pageshow and protected-entry checks, plus
  credential-free BroadcastChannel invalidation. Tests cover expiry without manual
  refresh and two independent clients changing accounts/signing out.
- [x] Browser-discovered competing logout redirects reproduced RED and fixed GREEN:
  protected-page logout now goes to login; public-page logout remains on that page.
- [x] Final frontend checks: **123/123 tests across 19 files**, lint and typecheck
  pass without warnings; production build passes with static account templates and
  a dynamic API proxy. Required build/test subprocesses ran outside the sandbox.
- [x] Full backend suite: **590/590**, no failures/skips (64 unit, 154 provider,
  372 integration). No provider credentials/live external requests needed.
- [x] Real browser flow through Next.js → .NET → isolated PostgreSQL: anonymous
  protected-page redirect; registration 201 remains anonymous; rejected login clears
  password; successful login creates HttpOnly session/CSRF cookies; private profile
  loads; logout 204 removes session cookie; another open tab redirects to login
  and `/users/me` returns 401. No browser runtime exceptions.
- [x] Screenshots inspected at 375px and 1280px, no horizontal overflow, keyboard skip
  navigation works. Temporary artifacts: `/tmp/ratefreak-phase2-login-375.png`,
  `/tmp/ratefreak-phase2-register-1280.png`, `/tmp/ratefreak-phase2-account-{375,1280}.png`.
  Verification used a disposable database/container and temporary processes, not
  existing accounts or persistent API configuration.

## Next session and release limits

Local account Phase 2 is complete. External Google/GitHub sign-in, callback/correlation
cookie handling and explicit identity linking are a separate optional follow-up.
Phase 3 catalog browsing/search/details can then consume the existing transport.
Future private query keys must include account identity and every filter.

Configure the same proxy secret on both servers and the appropriate trusted mode
before using the account UI. Production Vercel headers/HTTPS/secrets have not been
provisioned or verified by local tests; another ingress needs a verified adapter.
Cross-instance shared rate budgets, recovery policy and deployment hardening remain
release work. Live client disconnect propagation through a deployed ingress remains
an operational check; unit tests cover abort signals/timeouts and live local
navigation cancellation was observed reaching the proxy.

## Implementation decisions

- Kept the existing checkout on a new local feature branch to preserve the approved
  uncommitted planning files and installed dependencies. Files share this checkout;
  no filesystem-isolated worktree was created.
- Used `vitest.config.mts`, current oxc JSX options and per-file jsdom directives
  for Vitest 5 compatibility. Future DOM tests must include the directive.
- Updated `@types/node` from 20 to 24 to satisfy Vitest's peer requirement and
  describe the actual runtime. No runtime Node upgrade was needed.
- Deferred the shared backend-IP bucket to the explicit Phase 2 blocker above;
  Phase 2 now implements authenticated attribution. Omitting/misconfiguring the
  corresponding backend setting still leaves proxied callers sharing a budget.
- Phase 2 uses an explicit Vercel trust mode instead of assuming static Next egress
  IPs; self-hosted production support requires a separate ingress adapter.
- Session synchronization and bounded checks supersede the initial infinite-freshness
  implementation considered during Phase 2. Other-device revocation is discovered
  at the next check; same-browser windows receive immediate invalidation signals.

No deferred minor code-review findings.
