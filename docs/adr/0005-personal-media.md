# ADR 0005: Personal library, ratings and reviews

Status: Accepted for Phase 4. Date: 2026-10-29.

User explicitly chose ratings from 0–5 inclusive in 0.5 increments, replacing the
provisional 0–10 scale, and approved recommended private libraries and public
plain-text reviews (one editable review per user/media). Remaining choices were
delegated to the implementer. This decision does not change provider metadata.

Independent tables/services keep tracking, ratings and reviews independently
editable/removable. Tracking statuses planned/in_progress/completed/paused/dropped
are shared across movie/TV; dates explicit and optional, completion >= start.
Public review author omits email; owner-only mutation with session-derived IDs.
Public means readable without login; moderation/private-review workflow deferred.
SQL aggregates over current local ratings avoid counter drift. No persisted
community/provider score mixing. PostgreSQL atomic pair upserts handle concurrent
tracking/rating writes; unique review conflicts return 409.

See docs/superpowers/specs/2026-10-29-personal-media-design.md and README for input
bounds, pagination, independent lifecycle, deletion, spoiler and security semantics.
