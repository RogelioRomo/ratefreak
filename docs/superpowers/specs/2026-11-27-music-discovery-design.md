# Music discovery and recently rated

Owner approved the recommendations in conversation on 2026-11-27, explicitly
including full catalog support for songs. This supersedes the deferred artwork
and track scope of ADR 0007. Existing investigation and owner STATUS edits remain.

## Intent and approach

Add Cover Art Archive release-group front thumbnails to album search, previews
and imports. Use canonical HTTPS URLs, strict renderer validation, existing image
fallback, and direct browser delivery without a server image cache. This permits
upstream removal to take effect without retaining a local copy. Existing imported
albums with null references may derive their cover from their MusicBrainz identity
at read time; do not bulk overwrite stored media or uploaded artwork.

Add canonical `song` media with MusicBrainz recording identity, artist credits,
duration and disambiguation metadata. Search by recording title or artist; retain
distinct studio/live/remix recordings. Reuse catalog import, ratings, reviews,
lists and library contracts. Do not mislabel a recording as an album or select
arbitrary release artwork for songs. Missing covers retain standard fallback.

On authenticated home, show up to 12 recently rated accessible items, ordered by
rating UpdatedAt descending with a stable identity tie break. Include private
ratings only for their owner; no library entry required. Use artwork, title, media
label and own score in a responsive grid. Edited ratings move forward. Empty,
loading and retry states are independent of feeds. Hide on logout, identity
transition or session failure. Preserve Following/Community tabs and signed-out
home. Invalidate this account's section after rating changes.

## Alternatives

Search-only songs would prevent rating and saving them; owner selected full
support. Release-track identities fragment the same recording across editions;
recording identity matches the desired personal catalog. A persistent cover proxy
would add retention/removal operations; direct delivery is the smaller coherent
integration and matches Open Library's existing behavior.

## Constraints and verification

Keep .NET/EF PostgreSQL, Next.js, CSS Modules and TanStack Query. No new dependencies.
Separate TypeScript interfaces. Widen the catalog constraint with a Songs seed;
rollback refuses dependent songs/category usage rather than deleting user data.
No deployment or live database migration. Provider, API authorization/visibility,
migration, browser proxy and frontend behavior require focused tests, followed by
full suites, lint and typecheck. Cover artwork has separate rights from CC0 data;
update credits and documentation. No first-release gate is implied.
