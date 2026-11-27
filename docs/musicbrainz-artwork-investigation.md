# MusicBrainz artwork investigation

## Implementation status

The recommendations and subsequent owner requests were implemented on
2026-11-27: album covers, full song catalog support with associated-release
artwork, and the signed-in Recently rated section. Music discovery now defaults
to showing only results whose covers load, with an option to show all results.
See [ADR 0022](adr/0022-musicbrainz-songs-and-artwork.md) and the
[implementation plan](superpowers/plans/2026-11-27-music-discovery.md).
The investigation below records the original behavior before these changes;
its references to future work and unresolved decisions are historical.

## Original investigation context

Investigated 2026-11-27 on local `main` at `7239a25`, with the API and web app
running for owner review. Scope: investigate and document only; no application,
configuration, database or test changes.

MusicBrainz album artwork is currently unimplemented. The backend deliberately
returns `imageReference: null` for both search and detail results, so the UI has
no image URL to load and displays its standard "Artwork unavailable" fallback.
This is an existing product limitation recorded in the original design, rather
than a regression introduced by the IGDB/Open Library implementation.

## Runtime reproduction

Through the running web app's API proxy:

```text
GET /api/v1/media/external/search?provider=musicbrainz&type=album&q=Radiohead&page=1
HTTP 200; 20 results; 0 non-null imageReference values

GET /api/v1/media/external/musicbrainz/album/d8dd81ab-d840-4663-8da3-d8c27c2eaf91
HTTP 200; title "Radiohead Box"; imageReference null
```

Other sampled search results, including "The Best of Radiohead", also had null
image references. These successful metadata responses reproduce the missing
artwork before image delivery or optimization is involved. The inspected mapping
sets the field to null unconditionally, so this is not specific to this artist.
Private runtime evidence is saved at
`.local/localhost/musicbrainz-artwork-investigation/api-evidence.json`.

## Cause and supporting evidence

| Layer | Finding | Repository evidence |
| --- | --- | --- |
| Provider adapter | Search and details both use `MusicBrainzMapping.Map`; the adapter requests release-group metadata and does not resolve artwork. | [MusicBrainzMediaProvider.cs](../src/RateFreak.Infrastructure/Providers/MusicBrainz/MusicBrainzMediaProvider.cs), lines 17–39 |
| Mapping | The `ExternalMediaResult` constructor receives a literal null for `ImageReference`. | [MusicBrainzMapping.cs](../src/RateFreak.Infrastructure/Providers/MusicBrainz/MusicBrainzMapping.cs), line 54 |
| Search UI | Each search result passes `item.imageReference` to `MediaArtwork`. | [discovery-screen.tsx](../web/features/providers/components/discovery-screen.tsx), line 34 |
| Shared renderer | Null references produce the album icon and "Artwork unavailable" instead of an image element. | [media-artwork.tsx](../web/features/catalog/components/media-artwork.tsx), lines 11–30 |
| Image source policy | Recognized provider sources cover TMDB, IGDB and Open Library; there is no album-cover source. This would also need consideration if artwork support were added. | [provider-images.ts](../web/shared/artwork/provider-images.ts), lines 21–25 |
| Existing contract | `Details_use_release_group_identity_artist_credits_and_core_metadata_only` explicitly asserts that `ImageReference` is null. | [MusicBrainzTests.cs](../tests/RateFreak.ProviderContractTests/MusicBrainzTests.cs), lines 27–42 |
| Original scope | ADR 0007 excludes artwork import. The README states that description/image references remain null and cover art is deferred. | [ADR 0007](adr/0007-musicbrainz-albums.md), lines 6–9; [README](../README.md), lines 921–955 |

The mapping's latest change is the original MusicBrainz feature commit,
`eb3a334`. Comparing `7239a25` with its parent shows no changes under
`src/RateFreak.Infrastructure/Providers/MusicBrainz/`. The recent optimizer
restrictions therefore do not explain this symptom: the current adapter supplies
no cover URL in the first place.

## Impact and disposition

The same mapping feeds previews and first imports, so current MusicBrainz
imports also begin without provider artwork. Album metadata, ratings, reviews
and library functionality are separate from this missing artwork integration.
Clearing browser or optimizer caches cannot create a missing image reference.

Adding covers would require a separately scoped implementation: select and
review an artwork source, map it to the existing release-group identity, define
delivery/validation and any cache/removal behavior, and verify search, preview,
catalog and library consumers. No source or policy choice is made by this note.
No fix was attempted, and no new tests or imports were created. The existing
contract was inspected rather than rerun; runtime verification covered the two
read-only endpoints above, not a separate browser session or an upstream artwork
availability audit. The pre-existing `docs/STATUS.md` edits were preserved.

## Owner request for track and song search

On 2026-11-27, the owner requested an option to search individual tracks/songs
through MusicBrainz in addition to the existing album search. The current
integration supports only the `album` type and searches release groups.

Record this as requested future work. This request documents the desired search
option; it does not yet define track/song identities, preview/import behavior,
ratings or library support, or whether it must ship in the first release. Those
decisions remain open for implementation planning. No application changes were
made for this request.

## Owner request for recently rated items on the home page

On 2026-11-27, the owner requested a new home-page section listing items that
the signed-in account recently rated. Show this section first, before the
existing Following and Community sections, and display the items' artwork to
make the home page more visually pleasant.

Requested section order:

1. Recently rated: the current account's recently rated items, newest ratings first,
   with artwork.
2. Following.
3. Community.

Preserve the existing Following and Community sections. The item count, layout,
empty state, signed-out behavior and treatment of edited ratings remain decisions
for implementation planning. Respect existing item visibility and artwork
fallback rules. This is recorded future work; no application changes were made.

## Resume in a later session

Read this file to continue the MusicBrainz artwork investigation and plan the
requested track/song search and home-page Recently rated section. The owner
wants to return in another session to implement these changes together. Artwork
support still needs an implementation decision; the search and home-page requests
above record the desired product changes. Resolve the open scope decisions before
implementation, and do not infer that these additions are first-release gates.
