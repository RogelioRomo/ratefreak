# ADR 0012: public profiles and owner profile editing

Status: Accepted design and implemented, 2026-10-30. User chose fixed handles with
editable display name and bio, then approved the written design.

Keep profile fields on the existing AccountUser row. Add nullable Bio varchar(500)
with a nonblank/bounded database CHECK; no separate profile table or generic
repository. Application IUserProfileService and explicit PublicProfile/AccountProfile
DTOs separate public identity from owner-only email/account information.

Public GET by UUID or ASCII case-insensitive handle returns only id,handle,
displayName,bio,createdAt. No email, roles, provider mappings, private library/rating
state or last-active timestamp. Handles retain registered casing and cannot change.
No account directory, public-library or author-post listing in this slice.

Owner-session/CSRF PATCH /users/me accepts only displayName/bio and preserves omitted
fields. Private setter flags track presence without adding JSON properties. Explicit
null clears bio; null displayName is invalid. Trimmed displayName1..80 valid UTF-16
units without controls; bio<=500 units, valid Unicode, only tab/CR/LF controls, blank
clears to null. Text is literal; future UI must escape it. Strict JSON rejects
unknown/duplicate properties and invalid types. Separate read60/IP/min and
write10/account/min budgets, no queue,429 Retry-After; no-store on profile responses.

Atomic field-selective ExecuteUpdate plus response projection share a transaction.
Concurrent disjoint edits merge; same-field last-write-wins. No Identity stamp,
credentials, handle normalization, roles or login mapping changes on profile edits.
All local/external account response paths include persisted bio in owner DTOs.
Existing authored review/comment/follow joins naturally show updated display names.

DisplayName/Bio/UpdatedAt are additional EF concurrency tokens: Identity's full-row
updates must fail on stale profile values rather than reverting committed edits.
This changes no columns or Identity stamps. Reproduced stale UserManager write
reversion and fixed it. Logout locks/reloads the user row before security-stamp
revocation so a concurrently edited profile neither gets reverted nor prevents
logout. Other overlapping stale Identity writes may return their existing optimistic
concurrency failure; callers can retry using fresh account state. Existing stamp
middleware can renew cookies on authenticated requests independently of this feature;
profile editing does not explicitly refresh sessions.

Migration Down locks users and refuses if any bio is nonnull, avoiding silent text
loss. Safe downgrade/reapply preserves prior Identity/catalog/moderation data; prefer
forward fixes and reviewed backups. No profile reporting/moderation yet; consider it
before public launch alongside account deletion/retention/export policies.
No avatar upload, social links, credentials/handle edits, private profiles, frontend,
real verification accounts, provider activation or deployment in this slice.

See README, approved design/implementation plan and checklist for routes and proof.
