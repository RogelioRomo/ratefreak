# ADR 0011: reporting, moderation and role administration

Status: Accepted design, 2026-10-30; implementation verification in checklist.

Ordinary signed-in accounts may report public reviews/comments. Identity Moderator
and Admin memberships authorize private moderation; Admin alone assigns/revokes
Moderator. Admin implicitly moderates. No privileged role on registration or through
email matching. Current database membership is checked for each privileged request
and checked again under the transaction lock for writes. No HTTP Admin grant.

Use reversible hidden flags independent of spoilers and ratings. A hidden review
hides its whole thread; restoring it retains individual comment holds. All public
list/detail/count and parent-comment-create paths apply this policy. Authors retain
edit/delete ownership, including hidden posts; edits do not republish hidden content.
Staff cannot moderate their own content, even with Admin membership.

Explicit nullable review/comment report FKs retain referential integrity while
SET NULL preserves reports after deletion. Immutable target and original actor UUIDs
survive; these are pseudonymous, not anonymous. Retain original report text and
resolution, plus append-only application audit events. No copied content bodies,
handles, emails or account secrets in audit history. Exact historical text is not
recoverable. This is not tamper-proof storage against database administrators.

All real role/visibility changes and report resolutions append audit events in the
same transaction. Optimistic target UpdatedAt checks reject stale moderation.
A shared transaction-scoped PostgreSQL advisory lock precedes account/content locks
and serializes staff commands, reporting and review/comment mutations. This coarse
lock intentionally trades throughput for simple correctness in this first slice.
It coordinates API instances sharing the same database, but rate budgets remain
per instance. Direct database operators must follow the documented maintenance policy.

Admin bootstrap is an explicit offline command targeting an existing UUID and
requiring a reason; no startup elevation or real development appointment. Its audit
actor is operator, with no personal operator identity claim. OS/deployment access
logs must provide operator attribution when required.

No expiry/purge job in this slice: reports and audit metadata are retained until
an explicit later policy is implemented. Public launch still requires retention,
account erasure/export and privileged-access policy decisions. Never claim content
or account deletion erases reports/audit metadata. Migration Down refuses if any
hidden content, reports or audit history exist; restoration alone does not remove
history. Prefer forward fixes; do not erase history merely to make rollback pass.

See moderation design/plan and README for routes, validation, rates and evidence.
