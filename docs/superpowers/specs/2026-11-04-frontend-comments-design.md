# Frontend review discussions

User requested implementation of review comments after follows. Preserve prior
uncommitted work and native execution; use existing Next.js/CSS Modules/Query.

Add `/reviews/{guid}` with a public review and paginated comments (URL `page`,
1..10000, page size20). Link from public review entries with Comments; no invented
counts. Read GET reviews/{id}, GET reviews/{id}/comments. Render literal text,
author/profile links, dates, independent review/comment spoiler reveal controls,
loading/unavailable/429/retry/empty/out-of-range states. Dedicated discussion page
avoids eager comment reads for every review and gives login a stable continuation.
Unknown or duplicate paging parameters cause no comment fetch.

Authenticated users can add public body1..2000 UTF-16 units and isSpoiler; only
comment authors get edit/delete. Review ownership grants no comment ownership.
Edit replaces body and spoiler flag (backend last-write-wins). Delete uses inline
confirmation; cancel makes no request. Forms label public visibility, fields and
server errors. Render spoilers concealed by default; don't put hidden body into
preview/confirmation copy. Preserve same-account drafts during session checks,
hide/inert/disable controls and abort pending writes. Account changes clear editor
state and stale mutation completions cannot update a new identity.

Use exact proxy allowlists with existing Origin/CSRF/JSON/body/no-store guards.
POST is not idempotent: no automatic replay. Unknown outcomes preserve drafts,
block resubmission and instruct a full reload/check of the discussion. Validation
and rate-limit rejections preserve editable drafts. Confirmed writes close/reset
the corresponding editor before independent list invalidation. Read failures never
replay or relabel confirmed writes. Public query namespaces survive initial session
discovery; confirmed identity changes retain existing cache-clearing policy.

Approaches: chosen dedicated review discussion page fits existing endpoint model
and protects the shared social read budget. Eager inline threads would multiply
reads and independent paging/form state on the catalog page. No replies, reactions,
notifications, moderation controls, dependencies or backend/schema changes.

Verify proxy/security/URL contracts, author/anonymous controls, spoiler behavior,
validation/draft/unknown outcomes/session races and known-write/read separation.
Run full frontend checks, real PostgreSQL comment integration tests, real browser
flows and theme/viewport/keyboard checks; guarded temporary data cleanup. No commit,
merge, push or deployment. Phase7 moderation/release follows this slice.
