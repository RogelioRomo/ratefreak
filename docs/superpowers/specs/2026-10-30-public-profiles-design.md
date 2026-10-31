# Public profiles and profile editing

Status: approved by user and implemented on feat/public-profiles, 2026-10-30.

## Intent and agreed scope

The user requested correcting the moderation audit DTO inconsistency, then public
profiles/editing. Audit DTO correction is a separate behavior-preserving change.
The user chose fixed handles with editable display name and bio. Public profiles
expose identity suitable for author/profile pages without leaking private account,
library or security information. This slice is backend only.

## Approach

Extend existing Identity AccountUser with nullable Bio and use a dedicated
IUserProfileService application interface with explicit request/response DTOs.
DisplayName, timestamps and stable handle already live on the account row, so this
is the sensible fit. A separate profile table would add joins and lifecycle handling
without an independent privacy/ownership boundary. Do not add a generic repository;
Infrastructure owns EF queries and transactional mutations behind the interface.

## API contracts

- GET /api/v1/users/{userId:guid}: anonymous public profile or404.
- GET /api/v1/users/by-handle/{handle}: same public profile, ASCII case-insensitive
  handle lookup. Validate existing handle format (3..30 ASCII letters/digits/underscore);
  malformed handles400, well-formed missing handles404. Preserve registered display
  casing in the response; no handle aliases or renames.
- PATCH /api/v1/users/me: authenticated owner-only partial update with CSRF;
  accept only displayName and bio. No target/actor supplied by the caller.200 with
  updated private AccountProfile; missing/deleted actor401. Empty object400.

PublicProfile fields: id,handle,displayName,bio,createdAt. No email, roles, moderation
state, security stamps, linked providers, private library/rating data or last-active
information. Existing follower/following APIs provide social pages separately.
No all-user directory, public library, per-author review/collection listings, counts
or private-profile switch in this slice; those can be composed/extended separately.
Existing public reviews and collections remain governed by their own visibility.

Private AccountProfile gains bio consistently on registration, password login,
external sign-in/onboarding and GET /users/me. Keep all existing fields, including
owner email, unchanged. Public reads always use PublicProfile and never reuse that
private DTO. Existing accounts return bio:null until edited.

## Editing and validation

Handles, email, passwords, roles and identity mappings cannot be edited here;
unknown or duplicate JSON properties400. Partial update distinguishes omitted
fields from explicit null. Omitted fields preserve stored values. DisplayName when
present must be a nonnull string, trimmed1..80 UTF-16 code units, valid Unicode,
without controls (including newlines). Bio when present accepts null to clear, or
trimmed literal text up to500 UTF-16 units with valid Unicode and only tab/CR/LF
controls. Empty/whitespace-only bio clears to null. No HTML rendering or Markdown
processing; return text literally. Wrong JSON types400. Requests over the existing
64-KiB cap remain rejected by the existing HTTP boundary.

Use dedicated profile-read60/IP/min and profile-write10/account/min fixed-window
budgets, no queue,429 Retry-After. Every profile response/error no-store, including
mixed-case/trailing-slash route matches. PATCH is owner-session derived,401 without
session and400 without valid CSRF. Only database-persisted profile fields change;
no role changes, session revocation, explicit profile-triggered cookie refresh or
provider calls. Existing security-stamp middleware may renew cookies on ordinary
authenticated requests independently of editing.

## Persistence and consistency

Add nullable Bio varchar(500) to users, with a database bound/nonnull-text CHECK
when present. Existing accounts/credentials/roles/mappings remain unchanged.
All user profile projections select explicit approved fields and are no-tracking.

PATCH uses one transactional atomic update of only the supplied fields, followed
by private response projection before commit. Concurrent edits of different fields
preserve both; same-field concurrent edits are last-write-wins. Update UpdatedAt
on successful edits. Do not overwrite Identity password/concurrency/security stamps,
email verification flags or handle normalization. Mark DisplayName/Bio/UpdatedAt
as EF concurrency tokens so stale Identity full-row writes fail rather than undoing
a committed profile edit; no extra columns or stamp changes. Logout locks/reloads
the account before stamp revocation, so it remains reliable across profile edits.
Row locks from the update protect
projection against concurrent account deletion; deletion that wins first maps401.
Do not acquire the moderation advisory lock: profile updates hold only the user row
and acquire no content/role locks, avoiding a new lock-order dependency. Authored
review/comment/follow identity projections naturally reflect the new display name.

Migration Down refuses if any Bio is nonnull, preventing accidental profile-text
loss. Safe downgrade/reapply preserves pre-existing account/password/security and
catalog/social/moderation data; document the guard and prefer forward fixes.

## Scope boundaries and security

No avatar upload, social links, email/password changes, editable handles, account
recovery/deletion, private profiles, profile moderation, rich text, notifications,
frontend or deployment. Bio is public user-generated text and therefore must have
clear frontend labeling/escaping when UI work starts. Profile reporting/moderation
is deferred and must be considered before exposing bios in a public launch.
Do not appoint real Admins or activate OAuth/providers during verification.

## Verification and delivery

Before implementation, add HTTP tests that fail on missing profile routes, then
cover public/private DTO separation, case-insensitive handle lookup, absent/deleted
users, auth/CSRF, input boundaries/Unicode/unknown/duplicate properties, partial vs
null updates, fixed handles and ownership, rate budgets/no-store/casing, propagation
of names to existing authored DTOs and Development OpenAPI. Test concurrent disjoint
updates and account deletion between update and projection. Verify original Identity
security fields/mappings remain unchanged and sessions stay usable after editing.
Prove migration refusal and safe downgrade/reapply on isolated PostgreSQL.

Run focused tests while implementing, full solution tests and Release build at
completion, EF model check, dependency audit and independent whole-branch review.
Apply/reapply the additive local migration without creating persistent verification
accounts. Update README, ADR and implementation checklist with actual results;
commit on feature branch. No merge or remote push inferred.

## Builder decisions

Exact internal DTO/service filenames, optional-field deserialization mechanism and
SQL/EF implementation belong to the builder, subject to these observable contracts.
A dedicated request type must preserve field presence without exposing entities.
