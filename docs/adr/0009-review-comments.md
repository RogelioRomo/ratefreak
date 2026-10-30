# ADR 0009: public review comments

Status: Accepted implementation defaults, 2026-10-30. User requested continuing
API implementation after merging IGDB. Reviews-only was the stated default while
an optional collection-target preference remained unanswered.

Add flat public comments with an explicit ReviewId FK. Reuse cookie/CSRF ownership
and social budgets; author alone edits/deletes, not the parent review owner.
Bodies are plain text, trimmed, 1..2000 UTF-16 units using existing review Unicode
rules; optional spoiler flag defaults false. Multiple comments per author/review
are allowed. Public DTOs contain only safe author identity. All responses no-store.

Explicit relational targets keep referential integrity without a polymorphic target
registry. Collection discussions require their own privacy/concurrency rules and
are deferred. A generic discussion framework or nested thread model is unnecessary.
No inert moderation column or unprotected admin endpoint is introduced; moderation,
reporting and audit policies remain separate work, as do notification/activity feeds.

Use one additive table with body constraint, cascading author/review FKs and ordered
paging index. Repeatable-read listing keeps existence/count/page consistent. Create
and update transactions include response projection; relevant FK errors map to404
with rollback. Deletes use author predicate and return404 when missing/non-owned.
PATCH is a replacement and last-write-wins; POST creates a distinct comment per call.

Deleting a review deletes its entire thread. Deleting an account cascades authored
comments and its reviews' threads; unrelated comments remain. Migration rollback
drops comments data and must be backed up/reviewed before deliberate use.

See design/plan, README and checklist for exact endpoint contracts and evidence.
