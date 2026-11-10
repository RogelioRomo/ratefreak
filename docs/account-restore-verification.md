# Local account erasure restore verification

Verified 2026-11-10 against source revision
`e7e1385f3449d03ef756a35ca13a82d2316aee20`. This is local application and recovery
procedure evidence, not target-host storage, backup expiration or production
acceptance. Existing project accounts, database, roles and configuration were unchanged.

## Executed drill

A dedicated PostgreSQL 17 container hosted fresh `drill_source` and
`drill_restored` databases. The existing project container remained stopped.
A temporary harness used real application services, EF migrations, Identity and
filesystem artwork with disposable accounts. API access checks used the real
application in TestServer without intercepted responses, not a browser or deployed HTTPS.

1. Migrated the empty source and seeded an owner and unrelated observer,
   public/private items and artwork, a hidden public item, library/review content,
   linked credentials, recovery state, a profile report and a moderation event.
2. Exclusively provisioned and flushed a mode-0600 journal in a private independent
   directory outside the database/artwork backup set.
3. With no application writer running, created a PostgreSQL custom-format dump
   and matching artwork archive; validated the dump catalog.
4. Deleted the owner through password confirmation and the real account service.
   Verified durable intent and completion records.
5. Restored the dump into the separate database and restored matching artwork.
   Confirmed the old account and private artwork existed before replay. Kept the
   current journal and the drill's existing Data Protection keys.
6. A freshly built Release API maintenance process refused a wrong database name
   with exit 1; the account remained present afterward.
7. Ran the Release erasure replay command with the exact restored database name,
   without HTTP or other writers. Verified the checks below, replayed again and
   rechecked them, including stable profile-report pseudonymization.
8. Removed the dedicated container and its database volume.

## Results

| Check | Result |
| --- | --- |
| Account, linked credentials, recovery state/events | Absent after replay |
| Owner library, authored review, private item | Absent after replay |
| Public items | Retained without creator attribution |
| Hidden public item | Moderation hold preserved |
| Unrelated account and library contribution | Retained |
| Profile report/audit | Live reference removed and targets pseudonymized; report pseudonym stable on second replay |
| Private artwork | Removed from storage; API returns 404 |
| Old orphan artwork | Removed by maintenance cleanup |
| Public artwork | Retained; anonymous API download succeeds with no-store |
| Anonymous private/hidden item access | 404 |
| Replay idempotency and exact-database guard | Passed |
| Focused integration suite | 13 passed, zero failed/skipped (`AccountData` / `ProfileModeration` filter; only matching tests counted) |
| Fresh API Release build | Passed, zero warnings/errors |

Private machine-readable results, hashes and run logs are under
`.local/restore-drill/evidence/`; test results are in
`.local/restore-drill/test-results/lifecycle.trx`. These ignored local files are
not portable release artifacts; this document summarizes their scope.

## Remaining operational acceptance

Local shell/user-secrets configuration and the local startup runner contain no
configured erasure journal. Deletion should remain unavailable until one is
durably provisioned and configured. The disposable drill journal is not the
project's operational journal.

For each self-hosted installation, actual 30-day backup expiration,
independent journal replication, protected durable keys, storage durability/locking
and named operator records require operational verification. This drill did not establish
replica continuity, full-table restore fingerprints, browser flows, export
cancellation or representative recovery time/load. Continue with the
[self-hosted release plan](self-hosted-release-plan.md).
