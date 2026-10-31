# Frontend delivery checklist

Design: [frontend roadmap](superpowers/specs/2026-10-30-frontend-design.md).

- [x] Agree on feature-by-feature delivery and separate implementation sessions.
- [x] Agree on separate interface files, focused components and TanStack Query.
- [x] Review and approve Phase 1 written design.
- [x] Write [Phase 1 implementation plan](superpowers/plans/2026-10-30-frontend-foundation.md).
- [x] Review implementation plan and select native execution.
- [x] Phase 1: foundation, transport/proxy, Query provider and shell.
- [ ] Phase 2: local accounts and session lifecycle.
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
Phase 1 using native execution on `feat/frontend-foundation`. Work remains
uncommitted, as specified. No backend code, migrations or provider settings changed.

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

## Phase 2 handoff and blockers

Start with local registration, login/logout and authenticated session hooks/screens.
Registration does not automatically sign in; backend login uses email. Add only
the exact required proxy methods/routes, enforce trusted configured web Origin for
all mutations (reject missing/foreign Origin), and forward X-CSRF-TOKEN.

**Resolve before enabling account flows or public use:** backend IP-based abuse
budgets currently see the Next.js server's address. All proxied browsers therefore
share CSRF issuance's 30/minute budget (and other IP-based budgets). Design trusted
client attribution and per-browser limits together with the deployment boundary;
never blindly trust browser-supplied X-Forwarded-For. Recorded review finding is
accepted as a Phase 2 integration blocker; Phase 1 is local foundation only.

Cancel and clear private queries and call `resetCsrfState()` on identity changes;
private keys include account identity and filters. Validate live Next-to-.NET
session/CSRF cookie issuance/deletion and real disconnect propagation in that slice.
OAuth redirects/correlation cookies and linking remain a separate follow-up.

Review checked cookie separation at the proxy Response boundary; browser storage
with live .NET sessions and multi-client throttling remain Phase 2 integration
checks. No authentication UI, live writes, external-provider calls, deployment,
SSR query hydration or persistent browser cache shipped in Phase 1.

## Implementation decisions

- Kept the existing checkout on a new local feature branch to preserve the approved
  uncommitted planning files and installed dependencies. Files share this checkout;
  no filesystem-isolated worktree was created.
- Used `vitest.config.mts`, current oxc JSX options and per-file jsdom directives
  for Vitest 5 compatibility. Future DOM tests must include the directive.
- Updated `@types/node` from 20 to 24 to satisfy Vitest's peer requirement and
  describe the actual runtime. No runtime Node upgrade was needed.
- Deferred the shared backend-IP bucket to the explicit Phase 2 blocker above;
  enabling account traffic before resolving it could throttle all proxied users.

No deferred minor code-review findings.
