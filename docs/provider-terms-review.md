# Metadata provider terms review

Reviewed: 2026-11-13. Scope: current TMDB, MusicBrainz and IGDB adapters,
import persistence and provider attribution UI. This records public primary-source
findings and implementation gaps; it does not establish legal clearance or approve
a commercial deployment. RateFreak's MIT license does not license provider content.

Follow-up: [agreement access and clause evidence](provider-agreement-evidence.md)
records the retrieved official guidance, full-agreement retrieval attempts and
precisely scoped remaining contract checks. UI findings below describe the initial
inspection. [Credits/catalog attribution and notice packaging](release-compliance-verification.md)
are now implemented and verified. The full Twitch agreement was obtained by direct
retrieval; its scope/lifecycle questions and the still-inaccessible TMDB terms are
recorded in the follow-up, which supersedes initial access findings below.

## Initial release findings (see follow-up evidence)

| Priority | Finding | Required follow-up |
| --- | --- | --- |
| Before enabling TMDB in a release candidate | The required disclaimer is present on discovery/preview, but no approved TMDB logo or application About/Credits section was found | Add a reachable credits surface with approved branding and the required notice; verify it in the browser |
| Before enabling providers | The operator guide lacks a consolidated account/terms/commercial-use checklist | Document independently supplied credentials, operator acceptance and provider-specific restrictions |
| Before claiming storage/redistribution rights | Full TMDB API terms and Twitch agreement text were not retrievable in this review | Obtain and record the applicable agreements; resolve persistent storage, content removal and redistribution obligations |
| Before a commercial instance uses providers | MIT source availability does not establish commercial API eligibility | Operator records TMDB commercial permission, IGDB partnership and applicable MetaBrainz commercial service arrangement |
| Before distributing populated artifacts | Imported metadata and remote artwork references have separate rights | Distribute clean installation artifacts; review any included catalog/database/screenshots or artwork separately |

The scope above concerns provider-enabled use and claims about provider content.
It does not by itself establish that distributing clean source with disabled
adapters is prohibited.

## TMDB

TMDB's [official FAQ](https://developer.themoviedb.org/docs/faq) permits free
noncommercial API use with attribution and directs commercial users to its sales
team. It requires an approved TMDB logo and a prominent nonendorsement notice in
an application's About/Credits section. Branding must preserve approved styling
and must not imply endorsement. TMDB also states it does not own the API images
or data; API availability is not blanket copyright ownership clearance.

The adapter uses server-side bearer authentication, consistent with the
[application-authentication documentation](https://developer.themoviedb.org/docs/authentication-application).
Each operator should register their own application and inject its token privately;
release artifacts should not contain a project-wide shared credential. This is
the proposed distribution practice, not a claim that the full agreement has been
reviewed or that credentials are transferable.

Implementation evidence:

- [TmdbMediaProvider.cs](../src/RateFreak.Infrastructure/Providers/Tmdb/TmdbMediaProvider.cs)
  supplies the required notice text and TMDB home link. It maps title, overview,
  release date, selected numeric metadata and a canonical remote poster URL.
- [ProviderAttribution](../web/features/providers/components/provider-attribution.tsx)
  renders text/link on discovery and preview. It renders no logo.
  No About/Credits route was found under `web/app`.
- [MediaImportService](../src/RateFreak.Infrastructure/MetadataImport/MediaImportService.cs)
  persists mapped data and external identity; an existing import returns without
  refreshing. No provider expiration/removal mechanism was found in this path.
- [MediaArtwork](../web/features/catalog/components/media-artwork.tsx) displays the
  remote TMDB image directly, with a failure fallback. It does not download provider
  images into the custom-artwork filesystem.

The [full API terms](https://www.themoviedb.org/api-terms-of-use) were blocked by
the browsing service's robots policy. The FAQ does not settle a retention period,
bulk redistribution rights or all deletion obligations. Those are unresolved;
do not invent a cache lifetime or infer permanent-storage permission from the
current implementation. [TMDB rate-limit guidance](https://developer.themoviedb.org/docs/rate-limiting)
also requires respecting 429 responses and warns that limits can change.

## MusicBrainz

[MusicBrainz data licensing](https://musicbrainz.org/doc/About/Data_License)
distinguishes CC0 core data from supplementary data under CC BY-NC-SA 3.0;
cover art is separate. The [database field overview](https://musicbrainz.org/doc/MusicBrainz_Database)
places names, identifiers, artist credits, types, dates and relationships in core
data; annotations, tags/genre associations, ratings, search indexes and edit
history are supplementary. This does not make an entire arbitrary API response
CC0.

The [API documentation](https://musicbrainz.org/doc/MusicBrainz_API) separately
requires a meaningful User-Agent, limits clients to one call per second and directs
commercial service users to commercial plans/contact. Core-data licensing alone
does not waive hosted-service conditions. Its
[rate-limiting guidance](https://musicbrainz.org/doc/MusicBrainz_API/Rate_Limiting)
requires maintainer contact and advises against polling for metadata changes.

Implementation evidence:

- [MusicBrainzMapping](../src/RateFreak.Infrastructure/Providers/MusicBrainz/MusicBrainzMapping.cs)
  retains release-group title/MBID, artist credits/MBIDs, album/secondary types and
  first release date. It does not retain raw JSON, search score, tags, annotations,
  ratings or edit history. These selected fields appear consistent with core data;
  that is an implementation-to-documentation inference, not a blanket license
  classification for future fields.
- [MusicBrainzOptions](../src/RateFreak.Infrastructure/Providers/MusicBrainz/MusicBrainzOptions.cs)
  requires operator contact and constructs `RateFreak/0.1 (contact)`.
  [MusicBrainzRequestGate](../src/RateFreak.Infrastructure/Providers/MusicBrainz/MusicBrainzRequestGate.cs)
  spaces requests by one second within one API process. Shared egress/multiple
  instances need coordinated pacing; independent local gates do not establish an
  aggregate provider budget.
- MusicBrainz credit/link appears on discovery/preview. The mapper returns no
  artwork reference; no Cover Art Archive integration was found in the adapter.

The [Cover Art Archive policy](https://musicbrainz.org/doc/Cover_Art_Archive)
warns users to respect artist/label rights and use images at their own risk.
Its [API specification](https://musicbrainz.org/doc/Cover_Art_Archive/API) requires
image requests to go through `coverartarchive.org`. Adding album artwork later
requires its own rights and implementation review; MusicBrainz CC0 does not cover it.

## IGDB

The [official IGDB API documentation](https://api-docs.igdb.com/) describes
noncommercial use under the Twitch Developer Services Agreement and commercial
partnerships. Its business FAQ allows local storage, requests static visible
attribution for partners and permits retaining retrieved data after partnership
termination. It requires application credentials, recommends backend access to
avoid token exposure, and documents four requests/second and eight concurrent
requests. Removed/replaced image files remain available for 30 days, so stored
image references can become stale. These statements do not independently license
all underlying artwork copyrights or establish unrestricted bulk redistribution.

Implementation evidence:

- [IgdbMediaProvider](../src/RateFreak.Infrastructure/Providers/Igdb/IgdbMediaProvider.cs)
  accesses the API from the backend with operator-supplied app configuration.
- [IgdbMapping](../src/RateFreak.Infrastructure/Providers/Igdb/IgdbMapping.cs) stores
  title, summary, game type, release timestamp, platforms/genres and a remote cover
  URL. The shared import service retains this data without a refresh path.
- Discovery/preview displays IGDB text/link. The normal
  [catalog detail screen](../web/features/catalog/components/media-detail-screen.tsx)
  does not render provider credit, despite external identities being available
  from the catalog API. A permanent reachable credits surface would support
  attribution when users only browse imported content. Confirm placement against
  the operator's actual partnership agreement.

The initial browser retrieval of the [Twitch Developer Services Agreement](https://legal.twitch.com/en/legal/developer-agreement/)
returned only a shell. Direct retrieval subsequently obtained the full text; see
[section-level comparison and hash evidence](provider-agreement-evidence.md#twitch-full-text-follow-up).
IGDB schedule/partnership scope and content lifecycle still need confirmation; the
FAQ is not a substitute for the applicable agreement.

## Acceptance evidence still needed

- Record applicable agreements and operator commercial/noncommercial eligibility;
  never include credentials or confidential agreements in the public repository.
- Verify an application credits page, TMDB approved logo/notice, visible provider
  attribution and links on the release candidate.
- Define the content correction/removal and refresh policy after contractual
  storage requirements are established. Disabling an adapter currently stops API
  access; it does not erase imported data or remote image references.
- Verify provider aggregate request budgets for the supported topology.
- Keep dependency redistribution notices separate from provider metadata/artwork
  permissions, and review populated release artifacts individually.

Verification for this note: read-only source inspection and official web pages on
the date above. No provider credentials were used, no live import or legal agreement
acceptance was performed, and no implementation was changed.
