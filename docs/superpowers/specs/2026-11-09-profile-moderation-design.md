# Profile reporting and reversible text moderation

Status: approved by the user on 2026-11-09.
Builds on staff management, committed as `30c57e7`.

## Intent and agreed direction

Users can report inappropriate profile text, and current Moderators/Admins can
inspect reports and reversibly hide or restore the display name and bio. When
hidden, the public sees the existing handle instead of the display name and no
bio. The account, public profile page, sign-in, follows, reviews, comments, lists,
and libraries continue working. This is content moderation, with no suspension,
handle renaming, account deletion, appeals, notification service, or private-account
inspection added in this slice.

## Approaches considered

1. Reversibly hide profile text: preserve the current name/bio for the owner and
   staff inspection, replace public display names with handles, and allow staff
   restoration. This is the recommended approach selected by the user.
2. Permanently clear offending fields: loses reversibility and can erase evidence
   before staff reconsider a decision. Not selected.
3. Hide the entire profile page: also removes unrelated public identity and follow
   navigation. Not selected.

Reuse the existing report form, report queue, inspection, visibility controls,
audit history, permissions, budgets, and session boundaries. There is no separate
profile-moderation dashboard or new visual system.

## Proposed behavior and policy defaults

The following policy defaults were included in the approved design:

- A profile page offers Report profile for other users. Signed-out visitors get
  the existing login-to-report flow; owners cannot report themselves. Reporting
  uses the existing reasons, optional details, CSRF, reporting budget and duplicate
  acknowledgement. Reports against hidden profile text are rejected as unavailable.
- Preserve the existing lifetime duplicate rule: one report per reporter/profile.
  New reporters may report later misconduct. Version-based repeat reporting is
  outside this slice, matching existing content-report behavior.
- Profile reports appear in the existing open/resolved queue as type `profile`.
  Staff inspection shows the current display name and bio, the handle, current
  hidden state, and inspected version. It explicitly identifies this as current
  content, without storing historical name/bio snapshots in reports or audit logs.
- Both Moderators and Admins can hide/restore profile text or dismiss a report.
  Staff cannot moderate their own profile or resolve reports against themselves.
  Every action requires a reason and confirmation. Hiding while resolving a report
  affects both name and bio together. Dismissal does not restore hidden text.
- Public APIs suppress the name/bio on hidden profiles, including direct profile
  lookups and identity summaries attached to reviews, comments, follows and
  collections. Public suppression also applies to the owner's public-facing views.
  Ordinary staff identity summaries and the admin account directory use the same
  safe display-name fallback; raw hidden text is shown only in authorized profile
  inspection and the owner's account settings/export.
- Owners retain their current name/bio in account settings and exports. Account
  settings explain when profile text is hidden. Owners can save corrections, but
  editing never republishes hidden text. Staff must inspect the current revision
  and restore it explicitly. Restoring publishes the current saved text.
- Public profile responses include a `profileTextHidden` indicator so the page can
  explain that profile text is unavailable and avoid offering a report on hidden
  text. It does not reveal the reason, reporter, or staff notes.
- Use the existing per-profile visibility-inspection page pattern, linked from
  report inspection. Resolved reports retain that link so staff can inspect and
  restore corrected text without reopening a report. Profile audit filtering is
  added alongside the existing account-role audit filtering; the two remain
  distinct target types.
- Hidden state is enforced by API projections. Successful moderation invalidates
  affected client queries; subsequent requests return the current state. This
  slice does not add push updates to already-open browsers.

## Data model and concurrency

Add `IsProfileHidden` (default false) and a dedicated profile-version timestamp to
`AccountUser`. Existing accounts start visible; the timestamp is initialized by
the migration. Update it on actual display-name/bio changes and visibility changes,
strictly monotonically at PostgreSQL microsecond precision. No-op edits do not
change it. Add both new properties as EF concurrency tokens so stale full-row
Identity operations cannot revert moderation state.

Expose the timestamp as the profile moderation target's `updatedAt`, preserving
the existing `expectedTargetUpdatedAt` action contract. Account/security changes
do not change this profile-specific version. Profile edits and moderation lock
the same user row. Existing global moderation-lock ordering and transaction-time
role checks remain authoritative. Stale hide/restore/resolve requests return 409;
deleted targets cannot be restored or hidden.

Add a nullable `ProfileId` live reference to `ContentReport`, with SET NULL on user
deletion. Extend the target constraint to accept only valid profile-target shapes;
other report types must have a null profile reference. The existing target UUID,
author/reporter identity, queue ordering, deduplication, and audit fields remain.

Use the existing account-erasure workflow to null live profile references and
pseudonymize profile report/audit target identifiers consistently. Deletion must
not leave a profile target lookup or a stored text snapshot behind. Remaining
reports expose a deleted target and permit dismissal only. Apply ADR 0017's current
retention/cleanup and restore-replay rules to these records. Owner exports include
their current text and hidden-state flag, with no staff notes or reports made by
other people.

## API and browser integration

- POST `/api/v1/users/{id}/reports`: signed-in reporting with existing guards.
- GET `/api/v1/moderation/profiles/{id}`: current staff-only profile inspection.
- POST `/api/v1/moderation/profiles/{id}/visibility`: existing visibility contract.
- Existing report resolution, list DTO unions, audit filters, safe return URLs,
  proxy route/query allowlists, and report-target routing gain `profile` support.
- Owner account DTOs gain an optional/default-false `profileTextHidden` field;
  public profile DTOs return the same flag with suppressed text when hidden.

Reads and errors remain no-store. Staff reads/writes use existing separate budgets;
ordinary protected writes require CSRF. Requests use current roles and the existing
session-change/denial cancellation rules. No new endpoint grants staff roles.

## Migration and local delivery

Generate and inspect an additive EF migration with default-visible accounts, live
reference/constraint changes and profile-version initialization. Prove migration
and rollback on disposable databases. Down migration must refuse to proceed while
profile reports exist, rather than silently deleting them to restore the old
constraint. Document the dependency on the matching application version.

Before making the new candidate available against the persistent local database,
prepare a private backup and reviewed migration/preservation evidence. Apply only
the verified additive local migration using the normal tool permission boundary;
do not reset data. Implementation, tests and browser acceptance use disposable
databases first. Production deployment remains outside this work.

## Verification and completion

- Integration: permissions, self-report/self-moderation, CSRF and rate limits;
  duplicate/hidden/deleted targets; public fallbacks on all identity surfaces;
  owner visibility/editing/export; hide/restore/dismiss and audit reasons;
  current-role denial; stale versions, concurrent edits, identity updates and
  deletion; erasure journal replay; migration defaults/constraints/rollback.
- Frontend: profile report controls, literal rendering, hidden-state notice,
  owner editing notice, report/inspection/audit routing, confirmation/cancellation,
  stale/failure handling, account-switch isolation and cache invalidation.
- Run relevant suites, lint/typecheck, production/Release builds, EF model check
  and an independent implementation review. Use real Chromium with temporary
  ordinary/staff accounts to verify report → hide → owner correction → staff
  restore, public identity fallbacks, session boundaries, and desktop/mobile fit.
- Update STATUS and the API/operator documentation with exact evidence and any
  pending persistent migration or staging acceptance.

## Design review

A fresh builder reviewed this proposal against its planning needs and reported
"nothing important left". The user subsequently approved this design.

## Left to implementation

Choose focused helper/module boundaries for safe public identity projections,
the timestamp field name, exact owner/public notice copy, and test fixtures.
Preserve the existing design system and avoid adding dependencies.
