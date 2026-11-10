# Release verification and production gates

Updated: 2026-11-09. This record distinguishes local verification from deployed
acceptance. [STATUS.md](STATUS.md) owns the current backlog; the release-gate table
below owns production acceptance. Older sections are dated evidence, including
historical commit/migration/activation statements, not current operating instructions.

## Account lifecycle verification — 2026-11-09

Source: account lifecycle working tree on `aeb416e`; not committed or deployed.

| Check | Result |
| --- | --- |
| Frontend suite | 664 passed across 90 files |
| Backend test projects | 578 passed, zero skipped: 27 unit, 397 PostgreSQL integration, 154 provider contracts |
| Frontend TypeScript / ESLint | Passed |
| Next production build / API Release publish | Passed |
| EF model consistency | No pending model changes |
| Real browser account journey | Passed ZIP download, desktop/mobile final dialog, exact-handle confirmation, Cancel/Escape/focus restoration, deletion confirmation navigation, old-session rejection and public/private catalog handling |
| Actual ZIP inspection | Valid archive; owned public/private content included, credential/storage fields absent |
| Persistence/deployment changes during feature verification | Only disposable databases migrated and disposable test accounts erased; subsequent explicitly authorized local migration is recorded below |

An initial solution test attempt found Windows NuGet paths in two project asset
files. After restoring metadata from the existing Linux cache, unit/provider
projects passed; the full integration project passed separately. The totals above
combine those latest passing project runs, not a claim that the initial command
exited successfully.

Browser checks used a separate localhost Development API and frontend source copy
with a disposable PostgreSQL container and journal. No response interception was
used. Chromium's missing libraries were extracted into `/tmp` without changing
system packages. Desktop and mobile captures were inspected. This is local
acceptance, not proof of production HTTPS cookies, real OAuth provider consent,
external backup expiration or an infrastructure restore drill. Temporary browser
artifacts are not durable evidence fixtures; the automated regression tests and
operator procedures remain in the repository.

Fresh-review findings were fixed with regression tests: the final dialog now
repeats the active-account 90-day event policy; torn journal suffixes preserve
complete intents; real browser testing caught and verified a fix for a deletion
success/login redirect race. A journal must be durably preprovisioned with private
permissions, and the migration's unsafe ownership downgrade refuses to fabricate
lost authors. See [account operations](account-data-operations.md).

## Authorized local account migration — 2026-11-09

At the user's explicit request, applied
`20261109171007_AddAccountDataLifecycle` to the existing local `ratefreak` database
in `ratefreak-postgres-1`, starting from `20261108211351_ConvertCollectionsToLists`.
Created a private custom-format pre-migration backup and verified its archive
catalog. Confirmed the new migration history entry, both lifecycle tables and
nullable report-author column. All row counts and content fingerprints across
25 existing application tables remained identical. No account was erased and no
existing content was reset. No deployment, application restart or erasure-journal
provisioning was part of this action.

The local backup and machine-readable verification live in ignored `.local/`
paths; they contain installation data and are not committed documentation assets.
The backup is subject to the approved maximum 30-day retention policy. This local
migration does not establish production backup expiration or restore readiness.

## Earlier UI/session verification — 2026-11-09

Source baseline: local main `91a5cce`, containing the custom catalog/list rewrite,
category workflow refinements, UI spacing and silent background session checks.

| Check | Result |
| --- | --- |
| Frontend suite | 644 passed across 87 files |
| Backend solution suite | 554 passed, zero skipped: 27 unit, 373 PostgreSQL integration, 154 provider contracts |
| Frontend ESLint / TypeScript | Passed |
| Staged whitespace check | Passed before the commit |
| Session regression tests | Returning to a window/tab retains header links and visible/enabled drafts; confirmed expiry removes private content |
| Browser verification of today's changes | Not completed: installed Chromium could not start because `libnspr4.so` was missing |
| Production/Release builds and EF drift | Earlier checks passed on 2026-11-08; not rerun for today's UI/session changes |

Lower test totals than older snapshots follow retirement of field/entry collection
behavior; counts alone do not prove equivalent coverage. Session errors and explicit
account transitions still gate private UI; only routine pending refetches became silent.
No deployed end-to-end acceptance or production deployment is recorded by this pass.

Current integration facts superseding earlier notes:

- First-run setup migrated into the local project database on 2026-11-07.
- Local Google/GitHub activation and user-reported acceptance were recorded on
  2026-11-07; production HTTPS callback/consent checks remain open.
- The authorized local test-data cutover and four catalog/list migrations completed
  on 2026-11-08. This does not authorize another installation's reset or migration.
- Account tabs, OAuth and catalog/list work are integrated into local main; dated
  statements that they remain on an uncommitted feature branch are historical.

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

These remain open under the [current roadmap](STATUS.md) and accepted
[deployment direction](adr/0001-deployment-and-authentication.md). Historical
backend requirement lists are superseded where later ADRs changed the policy.

| Gate | Evidence needed before launch |
| --- | --- |
| Account retention/deletion | [ADR 0017](adr/0017-account-data-lifecycle.md) defines export/deletion and 90/365-day event retention with 30-day backup expiration. Verify lifecycle flows, independent durable erasure journaling, actual backup expiration and restore replay in the target deployment; see [runbook](account-data-operations.md) |
| Profile moderation and staff operations | Profile moderation and Admin removal are implemented locally; policy is defined in [ADR 0019](adr/0019-staff-access-and-operator-attribution.md). Verify deployed permissions/retention, authenticated named operator correlation, protected external records with 365-day expiration, and actual deployed Admin removal/recovery with last-Admin protection; see [staff runbook](staff-access-operations.md). Granting Admin remains operator-only; removal is supported in Staff management and `--revoke-admin` |
| Catalog/list compatibility and storage | Confirm external clients and target-database migration/data handling; provision persistent protected artwork storage outside wwwroot |
| Real production ingress | Deployed HTTPS origins, exact write Origin, matching server secrets and verified Vercel address headers, or a separately verified adapter for another host |
| Production sessions | Real `__Host-` Secure/HttpOnly cookies, independent Set-Cookie forwarding/deletion, CSRF rejection, all-device logout and no private response caching through the actual ingress |
| Durable Data Protection | Restricted persistent key storage, encryption/protected storage, restart and replica continuity, and controlled key recovery |
| Database and artwork recovery | Coordinated protected database/artwork backups expiring within 30 days and an isolated restore drill with independent account-erasure replay, migration/version and file-access checks; no production rollback without a reviewed recovery plan |
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

### Existing local database migration — 2026-11-07

At the user's request, applied `20261106225209_AddInstallationSetup` to the
existing local project database after creating a restricted local PostgreSQL
custom-format backup. Verified migration history and the singleton: `id = 1`,
completed, no initial-admin ID. Existing-account installations are closed without
automatic promotion. Verified preserved counts: 2 users, 8 catalog items and
0 collections; current Admin memberships: 0. The database container was started
for this operation and left running. No API/frontend server or provider was
started, and no account was promoted. Backup is local only, under
`/tmp/ratefreak-installation-migration/`; this does not satisfy the production
backup/restore gate.

## Google/GitHub browser integration — 2026-11-07

Approved design: [browser sign-in](superpowers/specs/2026-11-07-external-sign-in-browser-design.md).
The existing API OAuth integration now has optional login/signup buttons, new-user
handle/display-name completion and explicit account-settings linking through Next.
Provider credentials remain API-only and independently disabled until configured.
No live provider activation, remote app registration, push or deployment occurred.

| Check | Result |
| --- | --- |
| Frontend suite | 668 passed across 90 files |
| Backend suite | 625 passed, zero skipped: 64 unit, 407 integration, 154 contracts |
| Frontend lint / typecheck | Both exited 0 |
| Next production build | Passed with onboarding and sanitized error routes |
| EF model consistency | No pending model changes; no new migration needed |
| Browser journey | Google onboarding, normal-role denial of staff API, GitHub linking, same-account GitHub login, cancellation and clean callback destinations passed |
| Responsive/theme checks | 48 checks across six states, Light/Dark, widths 320/768/1024/1440; no overflow |
| Keyboard / runtime | Skip link reachable by keyboard; no runtime exceptions |
| Mechanical design scan | No findings |

The dedicated OAuth adapter validates provider authorization hosts/paths, exact
frontend callback URI, state and PKCE parameters. It forwards bounded recognized
callback queries and bounded external/correlation cookies, follows only the fixed
API completion hop and preserves independent cookie issuance/deletion headers.
Ordinary proxy routes still reject redirects. The API uses its configured public
origin only with authenticated shared-secret proxy attribution and an exactly
matching browser-origin declaration; arbitrary forwarding headers cannot establish
trust. Callback budgets now use authenticated client attribution like starts.
Existing replay, CSRF, setup, recovery and explicit-linking protections remain.

The independent review identified disabled provider controls surviving browser
Back/bfcache restoration. A regression reproduced the failure; a persisted
pageshow reset restores controls and the focused/full frontend suites pass.
Link starts use local busy state so a session transition does not discard their
error UI; backend original-session/stamp checks remain authoritative. Login starts
continue coordinating with local password actions through the session provider.

Browser evidence used real Chromium -> Next on 3103 -> a loopback HTTP bridge to
the real API in TestServer -> a separate disposable PostgreSQL database migrated
from empty. The test fixture explicitly initialized that disposable installation;
no project account or setup marker was changed. Only external provider token/profile
HTTP and provider authorization navigation were controlled; application/API responses
were real. This proves browser/proxy integration, not real provider consent or
production ingress. Captures include enabled login/signup, profile-completion form,
linked-provider settings, expired onboarding and error states. Next Development
diagnostics visible in captures are tooling. Temporary scripts, captures, private
runtime state and results are under `/tmp/ratefreak-oauth-browser/`; they are local
evidence, not shipped automation. Production ingress, cookies, keys, coordinated
abuse controls and live Google/GitHub acceptance remain release gates.

Dedicated OAuth test API/frontend processes were stopped, the disposable database
container/volume removed and temporary runtime credentials/key material deleted.
The project database container remains running after its earlier authorized
migration. No browser-slice commit or merge has been made.

### Localhost development reload follow-up — 2026-11-07

The user reported repeated installation-status errors after starting the real app
on localhost:3000. Direct API and both frontend setup-status HTTP probes returned
200 with completed state. Real Chromium reproduced 185 main-frame navigations in
eight seconds, repeatedly aborted setup requests and Turbopack entry-chunk
`restart` messages. The failure was in development refresh, before the setup
response could finish, rather than installation state or database availability.

Stopped only the frontend, preserved its generated `.next/dev` cache under
`/tmp/ratefreak-local/previous-dev-cache` and restarted with identical environment
and a fresh development cache. The setup response then completed and the homepage
rendered. A subsequent Chromium check confirmed both homepage and login remained
stable without reloads, installation errors or runtime exceptions. No product
code/security setting or database state changed. API/frontend remain running at
localhost:5080/3000 for user testing. Runtime state and sanitized reproduction
scripts/evidence are under `/tmp/ratefreak-local/`; frontend log is now
`web-fresh.log` there.


### Local provider activation and merge verification — 2026-11-07

The user provisioned separate local GitHub and Google OAuth applications and
supplied credentials in the ignored `.env`. At their request, copied the client
IDs/secrets and enabled flags into the API's Development user-secrets store,
using stdin and suppressing secret output. Kept public origin at
`http://localhost:3000`. Restarted the API and verified both enabled flags through
Next. The user subsequently confirmed that the live local provider flow works.
This is user-reported local acceptance; deployed HTTPS/provider acceptance remains
open. No credential values are included in committed files or this record.

For the requested local commit/merge, reran all frontend/backend suites, lint,
typecheck and production build, and checked the staged content for the supplied
OAuth credentials. Frontend was stopped during generation/build, then restored
with a fresh Development cache to avoid the earlier HMR loop. Browser checks
verify stable home/login, successful setup-status resolution and both provider
options without account writes or intercepted responses. Test logs are local under
`/tmp/ratefreak-oauth-merge*`. Missing cached NuGet files required a restore before
the backend rerun; this did not change dependency versions. No remote push or
production deployment is authorized by this local merge request.

Final rerun passed: 668 frontend tests across 90 files; 625 backend tests with no
failures or skips (64 unit, 407 real PostgreSQL integration, 154 provider contracts).
Lint, typecheck, production build, EF model consistency, browser stability/provider
visibility and staged whitespace/credential checks passed. The earlier independent
review's only material finding has regression coverage and is fixed. These checks
apply to the same source tree selected for the requested local fast-forward merge.

## Account settings sidebar/tabs — 2026-11-07

Account now presents four sections using an accessible vertical tab menu: Overview,
Profile, Sign-in options and Password recovery. On mobile the menu precedes content.
Existing forms remain mounted in hidden/inert inactive panels so profile drafts
and one-time recovery codes survive selection changes. Active private panels are
concealed during unavailable sessions and restored after successful retry. No API,
authentication policy or database migration changed.

All 674 frontend tests (91 files), lint, typecheck, production build and whitespace
checks pass. Independent review found no material/minor issues. A real Chromium
UI check with explicitly controlled account/logins/recovery read responses verified
64 tab/theme/width combinations, keyboard selection and draft preservation with
no overflow or runtime exceptions. This was frontend layout verification, not a
new authenticated backend write/consent test. No real user/account data changed.
Evidence and captures: `/tmp/ratefreak-account-tabs/`. Local frontend was restored
with a fresh Development cache after the production build. This slice is not
committed or merged; it is available locally on `feat/account-settings-tabs`.


## Custom catalog and list collections — 2026-11-08

- [x] Private-default custom items/categories, common access predicates and retained contributions.
- [x] One host-filesystem image, safe decoding, access-checked delivery and failure/cleanup proof.
- [x] Reference-only collection memberships, revision checks, guarded migration and offline reset tool.
- [x] Custom item/category/list UI and exact multipart/proxy routes; viewer-scoped caches.
- [x] Backend554/554 and frontend638/638; lint/typecheck and production/Release builds pass.
- [x] Chromium owner/anonymous lifecycle and image restart/replace/remove proof; keyboard and responsive Light/Dark captures.
- [x] Independent review findings fixed and re-reviewed; initially uncommitted in the isolated worktree, subsequently committed on main as `91a5cce` (2026-11-09).
- [x] Local test database cutover explicitly authorized; backup, scoped reset, four migrations, preservation checks and API readiness passed.
- [ ] Production rollout: confirm external clients and separately arrange hosting/deployment.

See [execution evidence](superpowers/plans/2026-11-08-custom-catalog-and-list-collections.md#execution-evidence--2026-11-08)
and [operations](custom-catalog-operations.md). Implementation tests used disposable
databases; the separately authorized local test-data cutover followed on 2026-11-08.
No production reset or deployment is recorded.
