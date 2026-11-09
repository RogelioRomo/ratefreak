# Account data operations

The account lifecycle policy is [ADR 0017](adr/0017-account-data-lifecycle.md).
Implementation/testing does not authorize deleting a real account, resetting a
database or deploying a release.

## Retention users are shown

| Data | Retention |
| --- | --- |
| Live account/profile, credentials/recovery codes, library, ratings, reviews, authored comments, follows, owned lists and private catalog content | Removed when deletion commits |
| Recovery security events | Up to 90 days while an account exists; removed immediately with the account |
| Private artwork | Immediate removal attempted after commit; hourly orphan cleanup retries unreferenced files older than 24 hours |
| Resolved moderation reports and their related events | 365 days after report resolution; pseudonymized at account deletion |
| Open moderation reports | Until resolution, then the 365-day schedule; no fixed complete-erasure date while open |
| Standalone moderation events | 365 days from creation; account references pseudonymized on deletion |
| Public custom catalog items, categories and associated artwork | Remain without account attribution, preserving current visibility/holds and shared references |
| Database/artwork backup copies | Maximum 30 days, enforced by the operator on every backup store |
| Independent erasure journal | Longer than all restorable backups; contains account UUID and timestamps, never profile/credential data |

Do not tell users that absolutely all their data vanishes after 30, 90 or 365 days.
The final confirmation dialog must disclose these separate schedules. Staff notes
can contain identifying text; pseudonymization is not a promise of anonymity.

## Configure deletion before enabling it

Set `AccountData__ErasureJournalPath` to an absolute durable file path outside the
database/artwork backup set, for example
`/var/lib/ratefreak-erasures/account-erasures.jsonl`. Do not put this file in a
public/static directory or allow untrusted identities to write it. Restrict its
directory/file to the API/operator identity, encrypt/protect its storage, and
arrange an independent append-preserving backup/replication policy. Every API
replica and replay process needs the same journal; test the filesystem's locking
and durability semantics before using multiple replicas.

Preprovision the journal once on durable storage before enabling deletion. The
application requires an existing file and never creates or truncates it. On Linux,
with the absolute `AccountData__ErasureJournalPath` already configured, this
exclusive-create example flushes both the empty file and its parent directory:

```bash
python3 - <<'PYPROVISION'
import os
from pathlib import Path
path = Path(os.environ['AccountData__ErasureJournalPath'])
if not path.is_absolute():
    raise ValueError('An absolute journal path is required')
path.parent.mkdir(mode=0o700, parents=True, exist_ok=True)
file_fd = os.open(path, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600)
try:
    os.fsync(file_fd)
finally:
    os.close(file_fd)
directory_fd = os.open(path.parent, os.O_RDONLY | os.O_DIRECTORY)
try:
    os.fsync(directory_fd)
finally:
    os.close(directory_fd)
PYPROVISION
```

An existing path causes this example to stop rather than erase previous journal
records. Provision directory durability/permissions and storage replication using
the target platform's equivalent procedure. Never use a shell redirection or copy
command that could truncate an existing journal.

An unset/unavailable journal prevents account deletion. Confirmation/export must
remain usable. A deletion intent is flushed durably before database erasure;
completion is recorded after commit. A pending intent is a request to finish
erasure during restore, including an interrupted original attempt. Never discard
pending records merely because their account still exists in a backup. A torn
non-newline-terminated final append is not a successfully flushed intent: recovery
preserves all complete records and discards only that incomplete tail before the
next append. Malformed complete records fail closed; keep traffic stopped and
recover the protected journal from its independent replica rather than guessing
which requests to omit.

The journal is intentionally excluded from ordinary database/artwork restores:
restoring an older copy over it could resurrect deleted accounts. It is sensitive
operator-only security data, excluded from user exports and application logs. Do
not use account handles/emails as filenames or copy journal contents into support
tickets. Retain journal entries until every backup that could contain their
account has expired, plus the operational safety margin.

Apply the additive account-lifecycle migration offline with the matching API/web
release. Never reset existing data. The migration allows unowned public custom
items, nullable historical report authors and short-lived lifecycle grants.
Validate the journal path, storage permissions and writable disk space before
offering deletion in a deployment.

## Backups and restore

The approved backup deadline is **30 days**. Configure expiration for database
snapshots, artwork backups, object versions, replicated copies and any restorable
exports; ensure no longer-retained recovery copy bypasses this policy. Verify
expiration with the actual storage provider and record evidence in the release
gate. Source code cannot enforce a hosting provider's snapshot lifetime.

Take coordinated database/artwork backups as described in the
[catalog operations runbook](custom-catalog-operations.md). Restore only while
all HTTP instances, jobs and other writers are stopped. Restore matching release
artifacts, durable Data Protection keys and protected artwork permissions. Keep
the current independent erasure journal; never replace it from the old backup.

With the intended connection string and journal path supplied through secure
configuration, run the API executable from its release directory:

```bash
dotnet RateFreak.Api.dll --replay-account-erasures --database-name EXACT_DATABASE_NAME --confirm-erasure-replay
```

The explicit database name must match the configured database. This maintenance
command does not start HTTP, apply migrations, publish private content or reset
collections. It reapplies requested/completed erasures idempotently. Keep traffic
stopped on any failure; repair database/journal/storage access and retry. Do not
substitute an empty journal to make the command succeed.

After replay, verify deleted account IDs, credentials/recovery state and private
content are absent; retained public entries have no creator attribution; hiding
and moderation remain effective; no deleted private artwork is accessible through
the API. Run the orphan cleanup cycle and verify unreferenced private files are
eventually removed. Exercise the process against an isolated restore containing a
subsequently deleted test account before reopening production traffic.

## Export and runtime acceptance

Exports are private ZIP attachments, generated after fresh confirmation, limited
to 256 MiB. The browser proxy streams them within a five-minute budget. Account
data, personal contributions, owned catalog/list data and current owned artwork
are included; passwords, verifiers, provider subjects/tokens, storage paths and
staff-only notes are excluded. Missing required artwork or exceeded limits must
fail explicitly, leave the account intact and clean temporary export files.

Confirm cancellation removes temporary files, the proxy forwards attachment and
no-store headers, grant cookies have correct Secure/HttpOnly/path attributes,
expired/replayed grants fail, and provider-only users can confirm with their
already-linked identity. Test last-administrator protection, cookie replay after
deletion and stale concurrent writes using disposable accounts only.

Monitor sanitized retention/cleanup failures, export resource usage and journal
write failures. Background retention runs must catch up after downtime. An hourly
cleanup schedule means retention expiration occurs on the next successful sweep;
the process must be running and monitored. Do not put credentials, confirmation
grants, export bodies or authored text in logs.
