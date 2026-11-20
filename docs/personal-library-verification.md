# Personal library verification — 2026-11-20

The approved scope is title search, sorting, own-rating display and inline status
updates. Implemented on `feat/personal-library` and merged into local `main` through
`42ed721` on 2026-11-20 after explicit user authorization. See the
[design](superpowers/specs/2026-11-20-personal-library-design.md)
and [plan](superpowers/plans/2026-11-20-personal-library.md).

## Behavior

- Search is a trimmed case-insensitive literal substring, limited to 200 UTF-16
  units with NUL rejected. `%`, `_` and backslashes do not act as wildcards.
- Sorting supports recently added/completed, case-insensitive title, highest own
  rating and lowest own rating. Undated/unrated values sort last; deterministic
  creation-time/media-ID ties and filtering precede pagination.
- Each entry includes only the caller's nullable score. Zero remains a real score.
  Scores are projected in the list query, without a browser request per row.
- PATCH `users/me/library/{id}/status` requires CSRF, expected account and expected
  status. Under the existing write/moderation lock it changes only status/update
  time, preserves dates, rejects stale statuses and never recreates deleted rows.
- URL filters survive navigation. Per-row controls keep unsaved drafts on date-only
  refetches, announce success/failure and repair focus/pagination after removal.
  Explicit conflict recovery reconciles identity before private refetches. Rating
  changes/removal invalidate library scores/order.

## Automated evidence

The following commands completed successfully on the final implementation:

| Command | Result |
| --- | --- |
| `dotnet test RateFreak.sln` | 32 unit, 473 integration, 196 provider contracts; 701 total, zero skipped |
| `pnpm --dir web test` | 870 tests across 111 files |
| `pnpm --dir web lint` | Passed |
| `pnpm --dir web typecheck` | Passed |
| `pnpm --dir web build` | Production build passed |
| `dotnet publish src/RateFreak.Api -c Release -o .local/library-release` | Passed |
| `git diff --check` | Passed |

New API tests cover combined literal searches, length/duplicate/unknown input,
all sorts, nulls/zero scores, own-rating privacy, equal-key pagination, hidden
items, preserved dates, concurrent status conflicts, deleted entries, strict
body/header validation, identity and CSRF. Frontend tests cover query/proxy/API
contracts, drafts, conflicts, failures, account transitions, rating invalidation,
announcements and focus on surviving/removed rows.

The initial feature tests failed before implementation (8 API failures, 5 browser
contract failures and 10 UI failures). Independent review produced four important
findings: identity reconciliation during conflict recovery, rating-cache
invalidation, stable row identity/focus, and NUL rejection. Regressions reproduced
five frontend failures plus the API NUL 500; final checks pass after the fixes.
The review's announcement finding was also addressed and covered by successive
save/failure tests. No known review finding is deferred.

## Browser acceptance and limits

Real local Chromium used the actual Next.js proxy/API and a separately migrated,
disposable PostgreSQL database. An ordinary account used 25 synthetic titles
across the five provider media types; no provider data or real personal records
were copied into fixtures. The browser verified literal search, all five sorts,
zero-score ordering, Back navigation, status-only date preservation, explicit
conflict refresh, a removed last row on page 2 returning to page 1 with results
focus, surviving-row focus, and logout concealment. Both themes at
1440/768/390/320px had no horizontal overflow or browser page errors. Desktop and
narrow screenshots were visually inspected.

The candidate uses Development API/frontend over local HTTP. Production web
build and API Release publication are separate checks, not deployed-cookie,
HTTPS/OAuth, provider-eligibility or production-load acceptance. Backend visibility
and identity constraints have integration coverage; no new staff browser workflow
is claimed. No persistent migration or live-user mutation was performed.

Local preview services at localhost:3100 (web) and localhost:5180 (API) use only
that disposable database. The original localhost:3000/5080 services remain separate.
Ignored evidence is under `.local/library-acceptance/` in the feature worktree:
`browser-verification.json` and eight theme/viewport screenshots. These paths are
local evidence, not release artifacts or promises of future running services.

## Implementation rulings

- Used the native managed worktree and existing installed dependencies; the
  running original checkout was left unchanged.
- Combined the query/status API commit because the application/service/test files
  overlap; the approved behavior and subsequent browser contract stayed the same.
- Dropdown tests target the visible combobox; the component also intentionally
  carries a hidden native form control with the same label.
- The isolated browser candidate uses matching Development frontend/API settings;
  the production build was verified separately. Browser runtime libraries were
  extracted into `/tmp`, without system-wide installation or project dependencies.

These affect workflow/test setup, not the agreed product scope. Test fixtures
carry a persistent mocked session cache to match the real provider's active query.
No production-scale performance claim is made from the fixture results.

## Responsive control placement follow-up

The user requested tracking controls to the right on wide screens and below title
details on smaller screens. Each row now groups metadata and tracking separately;
at 64rem (1024px) the content uses two columns, and below that it stacks in reading
order. All 15 library interaction tests, lint, typecheck and a fresh production web
build passed. Chromium measured the actual control/detail positions at
1440/1024/1023/768/390/320px in both themes: right at 1024px and wider, below at
smaller widths, with no horizontal overflow or browser page errors. Desktop and
mobile screenshots were inspected. Evidence: ignored
`.local/library-acceptance/layout-verification.json` and `library-layout-*.png`.

## Local main integration

The user requested commit and local merge on 2026-11-20. Local `main` advanced
from `145de29` to `42ed721` by fast-forward. The merged checkout passed the full
backend suite (32 unit, 473 integration and 196 provider contracts; zero skipped),
all 870 frontend tests, lint, typecheck and `git diff --check`.

The existing API was restarted on the merged code using its existing local
database, provider configuration, artwork and key paths. API readiness, Home,
Library and browser setup proxy returned HTTP 200. OpenAPI includes the new
status-only PATCH. No schema migration or authenticated persistent-data write was
performed. Merge-check logs and runtime evidence are in the main checkout's ignored
`.local/localhost/library-merge/` directory. The managed feature worktree and its
separate synthetic preview remain available; no remote push was performed.
