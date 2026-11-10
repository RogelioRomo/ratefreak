# ADR 0018: Profile reporting and reversible text moderation

Status: approved, 2026-11-09.

Signed-in users can report another account's visible profile text through the
existing report workflow. Current Moderators and Admins can inspect current
display names/bios, dismiss reports, and reversibly hide or restore both fields
with a reason and confirmation. Staff cannot decide their own profile reports.
The existing lifetime reporter/target duplicate rule remains in effect.

Hiding preserves account usability and the public profile URL. Public identity
projections show the handle instead of the name and omit the bio, including author,
follow and collection summaries. Ordinary staff identity summaries and the admin
directory use the same fallback. Only profile inspection and the owner's private
settings/export expose the current hidden text. Profile moderation grants no access
to credentials, email, private libraries or staff-role assignment.

Owners can save corrections without republishing hidden text. Staff restore only
the current inspected version; a dedicated profile timestamp rejects stale
decisions without treating unrelated security updates as profile changes. No text
snapshots are added to reports or audit logs. Account erasure nulls live profile
references and pseudonymizes profile report/audit targets; ADR 0017 retention and
erasure replay still apply.

Suspensions, handle changes, appeals, notifications and version-based repeat
reporting remain separate scope decisions. Existing browsers refresh through the
normal query/session behavior; this slice adds no push updates.

See the [approved design](../superpowers/specs/2026-11-09-profile-moderation-design.md)
and [account operations](../account-data-operations.md).
