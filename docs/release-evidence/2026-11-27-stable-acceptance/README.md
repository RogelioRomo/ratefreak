# Exact candidate reference acceptance — 2026-11-27

Scope: frozen revision `f54048c233e405843160bc16d857f8c32082599f`, version 0.1.0,
Linux amd64. This receipt qualifies only the image identities in the JSON files.
The parallel security review found compatible updates requiring a successor
candidate; these receipts do not clear that successor or public publication.

All application tests use disposable synthetic accounts/data, the frozen operator
and Compose, a separate localhost HTTPS port and trusted disposable CA. The
persistent developer PostgreSQL container and running application were untouched.
No provider credentials, imported content, external message, security test report,
image rebuild, retag or publication was used.

`fresh.json` covers real HTTPS first-run Admin, registration, secure session
cookies, CSRF and cross-origin rejection, forged ingress headers, protected
artwork decode/storage/privacy and account ZIP export, tracking, role denial and
logout. `browser.json` covers actual Chromium ordinary account/library/catalog/
notifications and restricted staff page, staff disabled updates, both themes,
320/390/768/1440 widths, keyboard action and logout concealment. The newer-release
UI is explicitly mocked; actual update checks remain disabled.

`upgrade.json` and `upgrade-preservation.json` cover the existing pinned `9cc808a`
source-build image cache, with exact IDs recorded. Original rows are compared in
all 30 original tables using their original columns. New columns/tables, appended
migration history and the new Songs seed are checked as allowed migration deltas.
Original artwork and Data Protection key bytes match the coordinated pre-upgrade
backup. This is a synthetic rehearsal; it does not establish a historical public
release or acceptance for arbitrary installations.

Two shipped acceptance-runner assumptions needed verification-only adaptations:
old baseline tools cannot prepare new current-Compose TMDB/IGDB bind directories,
so current candidate tools provision the current storage contract first; and
whole-database fingerprints cannot be equal across legitimate schema/seed
migrations. The local harness preserves original row projections and explicitly
checks the added Songs seed and six lifecycle tables. Frozen source is unchanged.
The shipped `prepare.py --resume` path needs those runner fixes in its successor.

`runtime-policy.json` records all six real reference roles with
`glibc.malloc.arena_max=1`, PostgreSQL JIT off and exact running application IDs.
Decoder/JIT source and artifact reachability reviews are separately owned by the
security qualification; this runtime receipt does not replace them.

`maintenance.json` records restart/persistence, saved-code password recovery and
single use/session invalidation, a coordinated stopped-writer backup and isolated
restore. Its independent erasure journal and TMDB/IGDB policies are newer than the
snapshot, each containing a real synthetic deletion or blocked identity. Restore
startup stays guarded until replay. Replay removes the erased user and clears
blocked provider metadata while retaining identities and surviving user tracking;
a complete song row, unrelated table fingerprints, original artwork and key bytes
are preserved. Repeated replay is idempotent. The local independent-journal
acknowledgment is a fixture setup, not proof of production replication.

`capacity.json` defines a six-user, 720-operation mixed public-read/private-read/
tracking-write workload, with declared latency and zero-error thresholds,
production limiters unchanged, observed host/container resources and samples.
`capacity-burst.json` retains the higher-rate attempt that exceeded the shared-IP
catalog budget and returned HTTP 429. The main passing paced workload represents six active accounts behind one NAT,
with 1,003 synthetic catalog items, 251 initial entries per account and 180
different detail targets. Fixture provisioning uses local SQL and is excluded
from measurements. `capacity-small.json` retains the earlier successful small
dataset trial. The main trial exercises paginated libraries, catalog reads and
tracking writes on the reference topology. It does not measure infrastructure
maximum capacity, catalogs beyond the specified dataset, concurrent exports/uploads, provider
traffic, distributed replicas or internet latency. Container CPU/memory were not
capped; the host was shared with other qualification work. Per-installation sizing
and perimeter/coordinated budgets remain operator duties.

`notification-gate.json` retains the external delivery gate. Existing records
establish private reporting is enabled but explicitly state no test report was
submitted and maintainer delivery is unverified. No read-only lookup can prove
receipt without existing delivered-notification evidence.

Private commands/logs, fixture credentials/keys, journal/policies and coordinated
backups remain under `.local/stable-qualification-2026-11-27/acceptance/` and must
not be published. Sanitized scripts reproduce the method and load secrets only
from private local fixture files. `cleanup.json` proves all disposable Compose
projects have no remaining containers or volumes. No persistent installation
changes were made. The successor candidate requires matching operational/security
qualification before any final clearance.
