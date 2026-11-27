# ADR 0022: MusicBrainz songs, covers and personal recent ratings

Accepted 2026-11-27 by the owner. Supersedes ADR 0007's deferral of artwork and
track/song support, preserving its album identity and transport restrictions.

Songs use MusicBrainz recording UUIDs under `recording`, with a canonical `song`
media type and Songs seeded category. Album UUIDs remain under `release-group`;
the same UUID cannot alias these media types. Use existing import and personal
media lifecycles; no edition-specific track identities or inferred song artwork.

Cover Art Archive's canonical release-group front-500 endpoint supplies album
artwork. Validate the exact origin/path and let browsers fetch images directly,
like existing Open Library covers. Missing covers use the shared fallback. No
persistent image cache or optimizer is involved; upstream removals take effect
without retained server copies. Legacy album reads derive missing references from
validated mappings without overwriting stored artwork. Credits distinguish image
rights from CC0 metadata. CAA redirects are followed by browsers, not server fetches.

Recently rated is private owner data: 12 accessible rated items ordered by rating
UpdatedAt descending, then media ID. Include zero/private ratings and items with
no library record. Preserve Following/Community tabs beneath it and signed-out
home. Account-keyed queries, no-store responses, session gates and mutation
invalidation preserve privacy and freshness. Edits move items forward.

Songs seed/constraint migration refuses conflicting categories on upgrade and any
dependent content, aliases or seed edits on downgrade. No automatic data removal.

See ../superpowers/specs/2026-11-27-music-discovery-design.md for the approved scope
and ../superpowers/plans/2026-11-27-music-discovery.md for execution evidence.
