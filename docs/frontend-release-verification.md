# Frontend release verification

Date: 2026-11-06. Recovery-code verification follows the original pass below; public-launch approval remains pending.
This record covers the current checkout, including uncommitted installation-setup work.
Git records local commit state; these changes are not deployed or production verified.

## Original verification pass — 2026-11-05

| Check | Result |
| --- | --- |
| Frontend tests | 588 passed across 84 files |
| Full backend suite | 591 passed, zero skipped: 64 unit, 373 real PostgreSQL integration, 154 provider contract |
| ESLint and TypeScript | Both exited 0 |
| Next production build | Exited 0; all application and proxy routes generated |
| API liveness and readiness | `/health/live` and `/health/ready` returned 200 |
| Real catalog through Next proxy | `/api/v1/media` returned 200 |
| Live Chromium navigation | 11 routes rendered without application errors |
| Responsive checks | All 11 routes fit at 320, 768, 1024 and 1440px |
| Keyboard | Tab selected Skip to content; Enter focused main |
| Authorization and URL guards | Anonymous account/staff API reads 401; admin proxy 404; invalid/duplicate catalog page 400 |
| Cache boundary | All five authorization/filter probes returned `Cache-Control: no-store` |
| Browser runtime exceptions | None observed |

Browser routes: Home, catalog list, existing catalog detail, provider discovery,
collections, login, registration, library, account, moderation queue and audit.
Account redirected to login; private library and moderation showed login guidance.
This was an anonymous cross-feature smoke test, not a new signed-in write test.
Prior signed-in flow evidence, concurrency, role revocation, responsive theme
captures and guarded cleanup are recorded in the frontend checklist.

Browser traffic used Chromium → Next on port 3100 → the running Development API
on port 5080 → local PostgreSQL. There was no response interception, bundled demo
data or synthetic API response. This pass made no account/content writes and
created no identities, privileges or catalog records. Existing catalog content
was read only. Backend integration tests use separate disposable real PostgreSQL
containers; unit/contract tests retain their existing mocked boundaries.

Temporary reproducibility artifacts are `/tmp/release-browser.mjs`,
`/tmp/release-browser-results.json` and `/tmp/release-health-*.json`; these are
local evidence, not committed automation. They require the local running services
and Chromium CDP on port 9222. They contain no credentials.

## Outstanding release gates

These remain open under the existing [backend launch requirements](implementation-checklist.md)
and [deployment direction](adr/0001-deployment-and-authentication.md).

| Gate | Evidence needed before launch |
| --- | --- |
| Account retention/deletion | Saved-code recovery and the no-email-verification policy are now accepted and locally implemented ([ADR 0014](adr/0014-account-recovery.md)); retention/deletion/export policy remains open, and recovery still needs deployed acceptance |
| Real production ingress | Deployed HTTPS origins, exact write Origin, matching server secrets and verified Vercel address headers, or a separately verified adapter for another host |
| Production sessions | Real `__Host-` Secure/HttpOnly cookies, independent Set-Cookie forwarding/deletion, CSRF rejection, all-device logout and no private response caching through the actual ingress |
| Durable Data Protection | Restricted persistent key storage, encryption/protected storage, restart and replica continuity, and controlled key recovery |
| Database recovery | Protected backups and a successful isolated restore drill with migration/version checks; no production rollback without a reviewed recovery plan |
| Abuse controls | Coordinated/perimeter budgets for multiple API instances; current rate limiters are process-local |
| Provider rights and attribution | Approved metadata/artwork storage and usage terms, required notices/logos, and commercial licensing where applicable |
| Operational checks | Private health monitoring, sanitized logging, secret rotation, representative load testing and deployed client-disconnect propagation |
| Deployed end-to-end acceptance | Dedicated ordinary/staff accounts exercising the complete journey on the actual candidate release, with recorded cleanup |

Do not bridge Development and Production by weakening Secure cookies, enabling
loopback attribution in production, trusting arbitrary forwarding headers or
resetting abuse limits. The production build proves compilation; local HTTP
Development checks do not prove the deployment gates above.

## Repeating local checks

From the repository root, with installed dependencies and Docker available:

```bash
pnpm --dir web lint
pnpm --dir web typecheck
pnpm --dir web test
pnpm --dir web build
dotnet restore RateFreak.sln
dotnet test RateFreak.sln --no-restore -m:1
git diff --check
```

Run live browser checks against the API using the private local configuration
described in [web setup](../web/README.md). Never copy secret values into this
record. A Next build can disrupt an already-running dev server's generated files;
restart the dev server before further browser checks if necessary.


## Recovery-code verification — 2026-11-06

Final verification on the recovery checkout:

| Check | Result |
| --- | --- |
| Frontend suite | 600 passed across 85 files |
| Full backend suite | 606 passed, zero skipped: 64 unit, 388 real PostgreSQL integration, 154 provider contract |
| ESLint / TypeScript | Both exited 0 |
| Next production build | Exited 0, including `/reset-password` |
| Real browser recovery journey | Passed through Next/API/disposable PostgreSQL |
| Responsive recovery checks | Four states at 320/768/1024/1440px, no overflow |
| Runtime exceptions / design scan | None observed / no findings |
| Diff whitespace | `git diff --check` exited 0 |

Approved policy: local emails remain unverified; no recovery mail or admin reset
is supported. New local accounts receive a saved code; existing local users enroll
or rotate it after current-password confirmation. A valid single-use code resets
the password, returns a replacement and revokes all sessions. Without a password,
saved code or previously linked provider, recovery is impossible. External-only
accounts retain provider recovery; Google/GitHub browser sign-in/linking is the
next separate slice, with no credentials or live activation provisioned here.

Backend checks cover real PostgreSQL code consumption, concurrent replay, stale
tracked user/session rejection, legacy enrollment, provider-only denial, generic
failures, CSRF/no-store, password/lockout rules and attempt budgets surviving API
restart. Fault-injection tests prove registration and reset roll back if security
event persistence fails. Registration OpenAPI includes the recovery code.
Frontend tests cover save acknowledgement, code/password exclusion from mutation
caches, dismissal, reset errors and unsaved-code survival during background session
read failures. Fresh review findings were fixed with observed failing/passing tests.

Live browser evidence used dedicated temporary services: Chromium → Next on 3101
→ Development API on 5091 → a disposable PostgreSQL container migrated from empty.
No interception or synthetic API responses were used. The journey proved signup
without login, password-confirmed rotation, obsolete-code rejection, reset with
replacement code, revocation of two existing signed-in browser sessions, old
password rejection and login with the replacement password to the same account.
Registration save, account recovery, reset form and reset save states fit at
320/768/1024/1440px; inspected desktop/mobile captures have codes masked. No browser
runtime errors were observed. Test identities/content existed only in the disposable
database. Dedicated service processes were stopped and that container removed;
existing project services/data were not migrated or modified.

Temporary evidence is under `/tmp/ratefreak-recovery/`, including browser-smoke.mjs,
browser-results.json, masked screenshots and verification logs. Browser automation
requires the local Playwright/Chromium cache and temporary shared libraries.
These are local evidence, not committed automation or production proof.

At the user's explicit follow-up request, applied `20261106222423_AddAccountRecovery`
to the existing local project PostgreSQL database on 2026-11-06. Verified migration
history advanced from `20261031021811_AddUserBios`, both recovery tables exist, and
existing row counts are preserved: 2 users, 8 catalog items, 0 collections. Both new
recovery tables are empty; existing users remain unenrolled until they generate a
code. The stopped project container was started for this operation and left running.
Evidence: `/tmp/ratefreak-recovery/local-migration-results.json`. The API still does
not migrate at startup. This was a local database update, not a deployment.
Remaining deployment, Data Protection, backup/restore, abuse, licensing and policy
gates above remain open.


## Installation setup verification — 2026-11-06

Final verification:

| Check | Result |
| --- | --- |
| Frontend tests | 616 passed across 87 files |
| Full backend suite | 618 passed, zero skipped: 64 unit, 400 integration, 154 provider contract |
| ESLint / TypeScript | Both exited 0 |
| Next production build | Exited 0, including `/setup` and dynamic routes |
| Real first-run browser journey | Passed through dedicated Next/API/disposable PostgreSQL |
| Responsive setup | 16 state/theme/width checks, no overflow |
| Browser exceptions / design scan | None observed / no findings |
| Diff whitespace | `git diff --check` exited 0 |

User approved first-run browser setup protected by an API deployment secret. See
[ADR 0015](adr/0015-installation-setup.md). Fresh database migration seeds a pending
singleton; existing-account upgrades seed completed state without promotion. Setup
creates the first local Admin and recovery code atomically, then closes permanently.
Ordinary registration remains unprivileged. Missing singleton/secret/database is
fail closed; direct API calls and OAuth callbacks cannot bypass the guard.

Tests exercise concurrency, CSRF/no-store/IP budgets, invalid/missing secrets,
legacy migration, permanent closure after admin deletion/restart/secret removal,
and rollback if admin audit storage fails. Existing domain/account fixtures are
explicitly initialized; separate fresh-install databases exercise the real guard.
Database-free endpoint contract tests use a test-owned completed-status double;
the unreachable-database test uses the real guard.

Fresh review identified possible loss of normal editor drafts on background setup
status failure after completion. A failing regression reproduced it; the fix stops
automatic status checks after confirmed permanent completion and preserves normal
children on later errors. Unsaved recovery codes retain precedence during pending
setup checks. Invalid setup-status response shapes are rejected before app access.
The production build initially exposed Next dynamic pathname resolution outside
Suspense; the root now supplies a loading boundary per installed Next documentation.

Real Chromium verification used dedicated Next on 3102 → Development API on 5092
→ a fresh disposable PostgreSQL database, without response interception. Catalog
navigation redirected to setup, direct catalog API returned 503, an incorrect secret
was rejected, and correct setup returned a recovery code without signing in. Saving
it allowed login to the initial Admin and staff reads (200). Completed `/setup`
redirected home. Normal Create account subsequently created an ordinary user whose
staff read returned 403; a repeated setup POST returned 409 even with the secret.
No runtime errors. Form/save-code states fit at 320/768/1024/1440px in Light and Dark
(16 state/theme/width checks); inspected desktop/mobile captures mask recovery codes.
Next Development diagnostics visible in captures are tooling, not product controls.

Dedicated service processes were stopped and the disposable database removed;
the temporary test deployment secret was deleted. No real accounts were promoted,
existing database migrated or real installation secret provisioned. Evidence is in
`/tmp/ratefreak-setup/`: browser-results.json, masked captures and verification logs.
No commit, merge, push, deployment or provider activation is part of this pass.
Production ingress, keys, backup/restore, abuse, licensing and policy gates remain.


Outage regression evidence: a real unreachable PostgreSQL connection initially
returned 500 because the EF provider wraps its connection exception. Narrow
exception filters now recognize that database wrapper and return sanitized 503
for status, guarded APIs and setup submission; CSRF remains available over HTTPS.
The focused outage regression and final full backend suite pass.


### Interactive local demo follow-up

When starting a fresh user-facing Development demo, Next's implicit instant-navigation
validation repeatedly reloaded routes intentionally withheld by the installation
boundary. A real browser reproduced the loop at `/` and the loading state at `/setup`.
Configured the installed Next option `experimental.instantInsights.validationLevel`
to `manual-warning`, retaining Cache Components/partial prefetching and requiring
explicit segment opt-in for that Development performance diagnostic. No API/session
security or production rendering setting was weakened. A fresh browser then reached
the still-unclaimed setup form with no runtime errors.

Dedicated interactive demo is at `http://127.0.0.1:3102/setup` with API 5092 and a
separate fresh `setup_demo` PostgreSQL container. State/process IDs are in
`/tmp/ratefreak-setup-demo/smoke-state.json`; its private `deployment-secret.txt` is
for the user to enter. The demo was deliberately left running for manual testing. Its initial readiness
check left setup unclaimed; the user subsequently tested the flow and confirmed
it works, then explicitly requested a local commit and merge to main. Existing project accounts/database remain unchanged.
This runtime state is temporary and must be rechecked in later sessions.
