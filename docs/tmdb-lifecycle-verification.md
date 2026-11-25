# TMDB lifecycle verification

Verified locally on 2026-11-24 (America/Los_Angeles) in
`feat/tmdb-content-lifecycle`, based on `ecff792`. This implements the
[approved design](superpowers/specs/2026-11-24-tmdb-content-lifecycle-design.md)
and [plan](superpowers/plans/2026-11-24-tmdb-content-lifecycle.md).
The implementation is isolated from `main`; no persistent developer instance was
migrated and no release, remote tag or image was published.

## Result

TMDB imports refresh from five UTC calendar months after successful synchronization
and lose expired provider fields at six months. Failed requests do not renew the
clock. Per-identity retries and a persisted provider cooldown respect rate limits
and Retry-After; expiry runs independently of refresh/network availability. Invalid
policy withholds stored catalog responses while age-only cleanup continues.
Unrelated moderation and empty-list creation remain available. Finite timestamps
outside the .NET range are scrubbed and normalized without starving healthy items.

Removal retains the catalog UUID, TMDB IDs, categories, moderation flags and
independent library/rating/review/list data. The shared title becomes
"Unavailable title" and provider description, date, metadata and poster reference
are cleared. Authoritative not-found responses remove fields without waiting for
expiry. Later valid synchronization can restore the same identity unless blocked.

TMDB posters use the dedicated same-origin handler and managed cache. Actual
successful image fetch time controls their own five/six-month deadlines; a
metadata refresh alone cannot renew cached bytes. Delivery uses `no-store` and
`nosniff`; old remote/local optimizer routes reject TMDB posters. Startup/minute
sweeps cover unvisited files. Kernel advisory locks survive helper exit and release
on Node process death, allowing automatic cleanup after a crash.

Offline per-ID/global removal writes an independent durable policy before database
and poster cleanup. Current policy is required separately during isolated restore;
old snapshots cannot revoke blocks. Preparation refuses a lost initialized policy,
cleanup refuses unexpected mounts, and policy aliases/hard links are rejected.
See the [operator runbook](self-hosting.md#tmdb-refresh-expiry-and-removal) for paths,
commands, timing/capacity, monitoring and source-history limits.

## Executed checks

| Check | Result |
| --- | --- |
| Backend full solution | 778 passed: 62 unit, 208 provider contracts, 508 disposable PostgreSQL integration; zero failed/skipped |
| Frontend full suite | 1,013 passed across 121 files |
| Frontend lint / typecheck / production build | Passed |
| Operator / release / compliance | 72 / 26 / 20 passed |
| EF model and upgrade SQL | No pending model changes; additive migration `20261124233146_AddTmdbContentLifecycle` creates three control tables, their foreign keys/index, a schedule row and migration history |
| Production API / web / tools images | All three Docker builds passed, including API Release publish, EF bundle and production Next build |
| Standalone web process | Startup refresh/cleanup and real one-minute sweep; invalid-policy denial with independent expiry; active-lock contention, cancellation and actual Node SIGKILL recovery passed |
| Fresh reference installation | Packaged preparation/migration; trusted HTTPS, secure cookies, CSRF/Origin rejection, ordinary/staff access, protected uploads and ZIP export passed |
| Bookworm baseline upgrade | All 29 original application-table count/content fingerprints unchanged; all original migration-history rows unchanged; exactly the new migration row and three new control tables allowed |
| Ordinary-account TMDB journey | Synthetic imported title with library completion, rating, review and list membership; same-origin poster delivered with no-store/nosniff |
| Coordinated backup and isolated restore | Snapshot initially contained old provider fields; startup remained blocked until replay of current independent policy; fields scrubbed, UUID/TMDB ID and all four user-data fingerprints preserved |
| Replay verification | Refused a blocked poster reference on an otherwise fresh, unblocked title; reconciliation cleared only that image; verification then passed |
| Global termination | All TMDB provider fields removed, user data retained |
| Chromium | Ordinary login, catalog and library in Light/Dark at 1440/390px, before and after removal; poster/fallback, no horizontal overflow, no runtime errors or direct browser TMDB requests; TLS verification enabled |
| Policy/mount defenses | Real same-device bind-mount cleanup refusals; expected instance/tools binds; hard-link and same-inode bind alias restore refusal with original policy preserved |
| Cleanup | Successful fresh/upgrade/restore projects have no containers or named volumes; interrupted acceptance attempts were also torn down |

The final backend counts were independently read from TRX counters and copied to
persistent private evidence. Local raw logs, TRX, screenshots, controlled transport
fixtures and backup files remain under `.local/tmdb-lifecycle/` or disposable
acceptance directories. They are not public release artifacts. Public sanitized
results and exact image identities are in
[verification.json](release-evidence/2026-11-24-tmdb-lifecycle/verification.json),
with the [source-input manifest](release-evidence/2026-11-24-tmdb-lifecycle/source-inputs.json).

## Review and proof scope

Independent review found five Important gaps: crash-stranded poster locking,
worker-wide cooldown, overbroad policy gates, restore aliases and an out-of-range
sync timestamp starving a batch. Each received a failing regression before its
fix and relevant full-suite verification. Operator preparation/mount safeguards
were also reviewed and tested. Replay verification and operator documentation were
aligned with the actual blocking and scheduling rules.

The upgrade driver provisions new cache/policy paths before starting the old
images because the current Compose manifest mounts them. This accommodates
current packaging; it does not reproduce every historical installer. Fingerprint
comparison permits only the three named new control tables and the one new
migration-history row. All original rows still compare exactly. Initial harness
attempts used an obsolete library table name, a relative protected-policy path and
an equality check that included the expected new history row; those harness issues
were corrected before the final successful proofs.

Images are nonofficial dirty-tree development proofs using the recorded base
revision and a source-input manifest, not a newly qualified stable candidate.
The operator Python files copied into the tools image were hash-compared with
final source. No real provider credentials or live imports were used: HTTP
contract/standalone tests use controlled upstreams, and browser/reference artwork
is a synthetic PNG. This proves the lifecycle and deployment wiring, not a live
provider account's eligibility or approval of every deployment topology.

Per-ID maintenance cannot discover unknown search/preview-only or external
historical poster filenames; the operator supplies those through `--poster`.
Global termination blocks every filename. Previously delivered browser copies
cannot be recalled. Running sweeps require accessible storage and adequate
capacity; they do not establish deletion on an offline or failed host.

The TMDB storage/image-processing clarification is recorded in
[provider evidence](provider-agreement-evidence.md#current-disposition-2026-11-24).
Operator eligibility, applicable attribution, remaining IGDB/Open Library questions,
residual security clearance, final stable source/image qualification, corresponding
source/notice reconciliation and authorized publication remain independent release
gates. These proofs do not relabel or clear the older candidate/security evidence.
