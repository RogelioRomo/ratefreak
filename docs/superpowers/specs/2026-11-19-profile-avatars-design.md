# Profile avatars

Status: approved in conversation, 2026-11-19, with avatars stored separately
from other uploaded content under the configured artwork root.

## Intent and agreed scope

Users can upload a custom profile image. Accounts without one use a fallback.
Avatars are the next selected product feature. Social links and other profile
expansion are outside scope. First-use/onboarding work is explicitly declined.

## Approaches

- Preset icons: simpler storage, but limited personalization.
- Custom image uploads: selected by the user, with a fallback when absent.

The design below includes builder recommendations accepted through the design
discussion, including the user's separate-folder requirement.

## User experience

Account settings provide an avatar preview, a file chooser, an explicit upload/save
button and a remove button. Replacing/removing an avatar does not submit changes to
the display name or bio. Show progress and actionable validation/retry errors;
failed uploads preserve the previous avatar. Uploads are public profile content.

Use a circular image with a neutral person icon fallback, including on image load
failure. Preserve adjacent names/handles and accessible labels; decorative avatars
beside names have empty alternative text. No crop editor: use a centered square
crop and show the result in the preview before upload. Respect image orientation.

Display avatars on the public profile and existing ordinary user identity rows:
reviews, comments, follows, feeds, notifications and collection owner information,
plus account settings. No unrelated navigation redesign or staff directory redesign.

## Upload, storage and access

Accept static JPEG, PNG and WebP up to 5 MiB, using the existing decoder's pixel,
dimension and concurrency bounds. Strip metadata by re-encoding; store a square
WebP no larger than 512 x 512. Do not add remote URLs, provider-image imports,
animated images, SVG uploads or an image-generation dependency.

Reuse protected filesystem storage primitives with explicit avatar ownership and
asset identities. Store avatar files and upload staging/lease files in the dedicated
`avatars/` subdirectory of `Artwork:RootPath`, for example
`<artwork-root>/avatars/<asset-id>.webp`. Existing catalog artwork stays in its
current location; no existing-file relocation is part of this feature. Avatars use
the same persistent volume while remaining separate from other content.
Reject symlinked avatar directories/files and noncanonical storage keys.
Teach cleanup about avatar references; catalog cleanup must never delete live
avatars. Backup/restore must explicitly support this allowlisted subdirectory;
existing flat catalog archives must remain readable without loosening path/link
validation. Serve only the currently accessible asset through a checked
API route with no-store and nosniff, without image optimization that retains copies.
Old asset URLs cease serving after replacement/removal. Anonymous users may read
current public avatars. Owner/staff inspection uses authenticated checked access.
Staff inspection requires a retained profile report or recorded direct-hide event;
staff status alone does not grant unrestricted access to hidden images. Direct
profile inspection uses the public URL when visible and a checked report/event
scope when hidden. If all retained inspection scope has expired, restoration of an
existing image is blocked until its owner removes it; the UI explains this state.

Writes require the current account, CSRF protection and bounded request/rate limits.
Bind writes to the editor's expected account UUID as well as avatar revision, so
a delayed cross-tab account switch cannot apply an old draft to a new account.
Use a dedicated avatar revision to reject stale changes across tabs; unrelated
profile edits must not overwrite avatars. Missing/unavailable storage produces a
clear error while the rest of account settings remains usable.
Mutations invalidate affected session/profile/identity queries. No push updates
are added: other browsers reflect changes on subsequent checked reads/refetches.
No-store prevents application caching but cannot revoke already downloaded images.

## Moderation and lifecycle

Extend existing profile reporting and hide/restore actions to include the
avatar with display name/bio. Update UI copy accordingly. Hiding substitutes the
fallback on every public surface and prevents direct public asset access. Owners
can replace a hidden image, but it stays hidden until staff restore the current
inspected profile. Avatar changes invalidate stale moderation decisions by advancing
ProfileUpdatedAt. Preserve existing report deduplication, reasons, audit rules and
self-moderation restrictions;
do not retain historical image snapshots in reports or audit logs.

Include the owner's current avatar in authenticated account exports, including when
hidden. Account deletion and erasure replay remove its ownership and live access;
delete files after commit with retry through orphan cleanup. Replaced/removed
assets also become eligible for cleanup. Existing backup retention policy applies.
Coordinate avatar mutations, export, erasure and cleanup through the existing
artwork locking protocol so exports remain consistent and cleanup cannot race
an upload's promotion/commit.

## Delivery and proof

Use an additive migration; existing accounts receive no avatar and render the
fallback. Persistent database migration is a separate operation under the existing
backup/review process. Update API contracts, documentation and artwork backup/restore
coverage. Reuse Next.js, CSS Modules, TanStack Query and the existing .NET services.
Keep object interfaces in separate frontend files.

Verify upload/replace/remove, invalid images and size limits, failed-save retention,
concurrent writes, public/hidden/old-URL access, moderation races, cleanup reference
safety, export/deletion and erasure replay. Browser acceptance covers ordinary and
staff accounts, keyboard use, both themes and existing desktop/mobile widths.

## Success

A user can set, replace and remove a custom avatar; existing identity surfaces
consistently show the current accessible image or fallback. Moderation and account
erasure cannot be bypassed by a direct image URL or retained application cache.
Previously downloaded copies are outside application revocation guarantees.
