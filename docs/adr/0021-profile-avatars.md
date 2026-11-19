# ADR 0021: Account-owned profile avatars

Status: accepted, 2026-11-19. Extends ADR 0017 account lifecycle and ADR 0018
profile moderation. The user selected uploads with a fallback and a separate
filesystem folder; no social links or onboarding are included.

Account settings accept static JPEG/PNG/WebP up to 5 MiB. The shared bounded image
decoder corrects orientation, center-crops and re-encodes at most 512-square WebP.
Original files/metadata are not retained. A circular person icon represents absent
or unavailable avatars on public identity surfaces.

Files and upload stages/leases live in `avatars/` within the configured artwork
root, separate from existing catalog files on the same persistent volume. Avatar
metadata belongs to the account, with an additive migration and a dedicated
revision. A scalar current asset ID enables single-query identity projections;
mutations keep that pointer, ownership metadata and revision consistent in one
transaction. Writes require CSRF and an expected account UUID as well as revision,
preventing old editors from modifying a different account after a cookie switch.

Public images use current visibility and asset checks, owner images use current
account checks, and staff images require current permissions plus retained report
or direct-hide moderation scope. No path is a static-file or public optimizer
bypass. Hidden/replaced/deleted images fail subsequent inappropriate URL requests;
no-store is not a guarantee of revoking previously downloaded copies.

The profile hold covers avatar, display name and bio together. Owner corrections
stay hidden, and avatar changes advance the existing inspected-profile version.
Direct inspections show public images or a checked retained scope before restore.
If inspection records have expired, an existing hidden image cannot be restored
until its owner removes it. This preserves the existing 365-day moderation record
policy without silently widening staff image permissions.

Account exports include the current processed avatar, including when hidden.
Deletion/replay erase avatar ownership and access and attempt file removal after
commit; public catalog content/artwork retain their separate detachment policy.
An hourly bounded avatar sweep retries orphan files older than 24 hours. Backup
writers/validators allow only the avatar folder and flat catalog content, with
separate inventory checks and safe legacy archive support. Normal backup/journal
retention and restore-replay gates continue to apply.

See the [design](../superpowers/specs/2026-11-19-profile-avatars-design.md),
[verification](../profile-avatars-verification.md) and
[storage guide](../self-hosting.md#profile-avatar-storage).
