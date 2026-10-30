# ADR 0006: Dynamic collections and schema evolution

Status: Accepted. User approved the full design and implementation plan on 2026-10-30.

Collections support mixed canonical media and custom entries, private/public
visibility (default private), and owner-only editing. Relational collections,
fields and items store custom configuration/data as JSONB with bounded runtime
validation for text, number, boolean, date and select. No new packages.

One canonical media reference per collection; repeated custom titles are allowed.
Public records are readable anonymously, private records only by their owner.
All reads are no-store; public owner summaries never include email.

Field keys/types and item media references are immutable. Label changes preserve
values. Required/configuration changes must leave all existing items valid;
required additions to nonempty collections are rejected. Deleting fields removes
values permanently after an owner-only impact preview and explicit confirmation.
The preview counts populated entries, excluding absent/null values.

All existing-collection mutations lock the parent row in a transaction and require
its current revision. Each successful mutation increments revision; field changes
also increment schemaVersion. A stale deletion preview cannot authorize deleting
values edited afterward. Deletion removes keys and definitions atomically.

Alternatives considered: duplicate media rows for viewing diaries; field deletion
blocked until manual cleanup; recoverable archived fields; per-field value tables
and arbitrary JSON schema languages. The approved first version favors predictable
lists, explicit deletion and bounded validation. Archives, backfills, type/key
migrations and collaborative editing remain deferred.

Public-accessible collection reads share catalog IP limits even when signed in;
owner-only deletion previews share personal-read limits; mutations share existing
personal-write limits. This preserves shared rate budgets rather than introducing
a separate collection allowance. No collection deletion endpoint or public launch.

See `docs/superpowers/specs/2026-10-30-collections-design.md` and README for the
complete contracts and `docs/implementation-checklist.md` for verification evidence.
