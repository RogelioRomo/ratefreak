# Provider implementation verification

Implemented on 2026-11-27 in `feat/provider-implementation`, based on `1e71d442`.
This follows the settled [handoff](provider-implementation-handoff.md). The
five/six-month IGDB interval is RateFreak policy, not a provider-approved deadline.
The research limits recorded in the handoff remain applicable.

IGDB metadata starts refreshing at five UTC calendar months and expires at six
without a successful refresh. Retry/backoff and persisted provider cooldowns do
not renew the successful timestamp. Cleanup preserves internal/source identities,
moderation, ratings, reviews, libraries, history and collections. Confirmed game
absence and operator removal write independent durable identity/cover blocks;
age expiry alone remains recoverable. API disablement does not purge content.

IGDB artwork now uses `/igdb-covers/<imageid>.jpg`, a dedicated private cache and
`no-store` delivery. Actual successful image fetch time controls its own calendar
deadlines. Startup/minute sweeps, delivery and publishing check the current policy;
old optimizer routes cannot fetch provider artwork. The existing TMDB lifecycle
and dedicated cache remain in place. The implementations deliberately preserve
separate provider policies, locks, tables and caches rather than refactoring TMDB.

Open Library covers use validated direct `covers.openlibrary.org` URLs and bypass
both the source proxy and Next.js optimization. Preview, catalog and library use
the shared renderer, preserving dimensions, accessible fallbacks and source links.
The obsolete proxy route is removed. Browser contact, custom CSP considerations
and one-time disposal of legacy optimized images are in the
[operator runbook](self-hosting.md#provider-configuration-and-image-cache).

The reference package provisions a separate IGDB policy/cache even with API
access disabled. The API can persist confirmed removals; web reads policy without
write access. Offline removal blocks startup after an interruption until cleanup
succeeds. Isolated restore requires both providers' current independently
protected policies and reconciles them before traffic can reopen. Preparation
refuses a missing initialized policy and preserves managed caches and uploads
while safely clearing obsolete optimizer files once.

## Executed checks

| Check | Result |
| --- | --- |
| Backend full solution | 793 passed: 62 unit, 516 PostgreSQL integration, 215 provider contracts; zero failed/skipped |
| Frontend full suite | 1,072 passed across 126 files |
| Web lint, typecheck, production build | Passed |
| Operator / release / compliance | 100 / 26 / 20 passed |
| EF model consistency | No pending model changes; additive `20261127171719_AddIgdbContentLifecycle` migration |
| Production API / web / tools builds | Passed, including Release API publish, EF bundle and standalone Next build |
| Fresh reference installation | Packaged preparation/migration, trusted HTTPS, cookies, CSRF/Origin checks, ordinary/staff access, protected uploads and ZIP export passed |
| Chromium desktop/mobile | Direct Open Library URLs across preview/catalog/library; managed IGDB catalog/library images; no runtime errors; 1440/390px without overflow; TLS verification enabled |
| Runtime source denial | Old IGDB/Open Library optimizer requests rejected; removed Open Library source route rejected |
| Offline removal and isolated restore | Current independent policies carried into an old snapshot; removed game fields scrubbed; source ID retained; every other table fingerprint preserved except expected media/lifecycle/cover-history changes |
| Global termination and cleanup | Packaged IGDB termination passed; all disposable containers and named volumes removed |

## Evidence scope

Verification results and exact development image identities are recorded in
[verification.json](release-evidence/2026-11-27-provider-implementation/verification.json).
The controlled browser image responses and seeded IGDB cache used synthetic PNGs.
Provider previews were intercepted only in the verification browser; actual optional
adapters stayed disabled. Early harness attempts needed fixture category lookup,
Playwright driver mounting, URL normalization and browser DNS corrections; a
redundant root-UID maintenance call was removed because protected policies belong
to the runtime UID. These were verification harness changes, not application
changes. The successful restore continued from the verified pre-removal backup.
Historical-image upgrades/rollback were not requalified by this development proof.

Private build logs and disposable acceptance fixtures are under
`.local/provider-implementation/`; backend TRX results were collected separately.
The [source-input manifest](release-evidence/2026-11-27-provider-implementation/source-inputs.json)
binds runtime changes to these nonofficial development images.

An independent source review found no remaining critical or important issues.
Review caught the required writable API policy mount; a regression test now
protects that distinction from the read-only web mount.

## Limits and remaining release work

These are development proofs, not a frozen stable candidate. No persistent
installation was migrated, real provider credentials used, provider contacted,
release published or branch merged by this work. Final stable source/image,
security, notices/source-sidecar, all-layer privacy and complete installation
package qualification remain the release gates in [STATUS](STATUS.md).

Unknown search/preview-only or historical IGDB cover filenames require explicit
operator `--poster` arguments for identity removal; global termination blocks all
filenames. A cover CDN 404 is treated as unavailable artwork and can recover;
confirmed game absence and explicit removal use durable blocks. Previously
delivered browser copies cannot be recalled. Direct Open Library covers follow
the provider's browser cache and redirect behavior.

Successful durable removal requires writable, healthy policy storage. A failed
policy write aborts refresh without renewing the successful timestamp; it is not
evidence of completed removal. Offline/failed storage cannot establish deletion.
Operators still record real contact/registration, applicable terms, aggregate
request budgets and complaints/removal handling. Provider correspondence remains
optional unsent material unless separately authorized.
