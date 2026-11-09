# ADR 0017: Account export, deletion and bounded retention

Status: approved, 2026-11-09. Extends [ADR 0014](0014-account-recovery.md) and
[ADR 0016](0016-custom-catalog-and-list-collections.md).

Account owners can download an authenticated ZIP export and permanently delete
their account. Both require fresh ownership confirmation: a current local
password or an already-linked provider identity belonging to the same account.
Confirmation produces an operation-specific, five-minute, single-use grant bound
to the account's current security stamp. No email ownership matching, emailed
exports, administrative password reset or account-recovery loophole is added.

Deletion has a final modal dialog with Cancel, the exact current handle and an
explicit permanent-deletion button. The dialog explains the retention schedule
and preserved public contributions before submitting the irreversible action.
Account/profile, sign-in/recovery state, personal contributions, private catalog
content and owned collections are removed from the live database transactionally.
All sessions become invalid. The last administrator must appoint another first.

Public custom items and public categories keep their UUIDs, content and current
artwork without creator/owner attribution, preserving other users' references.
Hidden/archived states stay intact. They remain reportable and staff-moderatable
without a live author; a null creator is never interpreted as a system import.
Private items are erased with dependent references and discussions, including
other people's comments on erased reviews. Deletion never implicitly publishes
private content.

Active-account recovery security events expire after 90 days and are removed
immediately on account deletion. Moderation records are pseudonymized on deletion;
resolved reports and related events expire 365 days after resolution. Open reports
remain until resolution, while standalone moderation events expire after 365 days.
Reporter-supplied details are removed; historical actor/author identifiers receive
a new random pseudonym with no account lookup, and live reporter references become
null. Staff notes may contain identifiers, so these records are not described as
guaranteed anonymous.

The approved maximum backup retention is 30 days. Operators must enforce expiration
for every restorable database/artwork copy and verify it before production. Live
account deletion does not itself delete external backup objects. A protected,
durable erasure journal outside the database/artwork backup set records only
account UUID and erasure request/completion times. Restores replay pending and
completed erasures before serving traffic. Journal retention outlasts all backups;
the journal does not contain profile text or map to moderation pseudonyms.

Private artwork is removed after commit with retry by the existing hourly orphan
sweeper for files older than 24 hours. Cleanup failures do not falsely report that
the account survived a committed erasure. Export archives are temporary, never
cached or persisted for later download, and exclude credentials and staff-only
material. Ordinary background session checks remain silent.

These are product/operating policies, not a legal compliance determination.
Deployment, backup expiration, independent journal durability and restore acceptance
remain explicit release gates. See the [design](../superpowers/specs/2026-11-09-account-data-lifecycle-design.md)
and [operator runbook](../account-data-operations.md).
