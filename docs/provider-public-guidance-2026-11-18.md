# Open Library and MusicBrainz public guidance follow-up

Reviewed 2026-11-18 against official sources and the current provider mappings.
This supplements [agreement evidence](provider-agreement-evidence.md); no form
was submitted, agreement accepted, account authenticated or provider contacted.
Findings below distinguish published guidance from implementation inferences.

## Open Library

The [API guidance](https://openlibrary.org/developers/api) supports low-volume,
human-directed book discovery, encourages response caching, requests application
registration and identifying application/contact information in the User-Agent.
Published limits are one request/second unidentified and three/second identified.
It rules out API bulk harvesting, distributing requests across IPs and use as
high-traffic commercial infrastructure; bulk access belongs on data dumps.
RateFreak's on-demand search/preview, five-minute search cache, one-hour detail
cache and one-request/second metadata gate are consistent with that documented
usage pattern **as an implementation inference**. Per-process pacing alone does
not coordinate multiple replicas or unrelated services sharing egress.

The linked [application registration form](https://docs.google.com/forms/d/1119CgdEWHtaAy2lOpUmtbe4S-w6xhJUin-nqP7Bl1XQ/viewform)
asks for contact email, User-Agent and services used, plus application details.
Its required acknowledgments include using the APIs from one IP at a time and
understanding that access is provided without warranty/support, individual cases
are not approved, and service can be denied. Supplying the email in `.env` does
not submit this form. One registration covering every independent self-hosted
operator is not established by the form.

The [Covers API guidance](https://openlibrary.org/dev/docs/api/covers), last edited
August 13, 2024, asks public-facing pages to use image `src` URLs on
`covers.openlibrary.org` and prohibits crawling the cover API. Its 100 requests
per IP per five minutes restriction excludes CoverID and OLID lookups. RateFreak
uses CoverIDs, but its same-origin optimizer URL differs from that requested
display arrangement. The page does **not** expressly describe a blanket caching
ban or expressly authorize the implemented server proxy/24-hour minimum cache.
Metadata-cache encouragement and public image HTTP cache headers do not resolve
that separate permission question. Existing source links supply the requested
courtesy attribution.

The [catalog-use FAQ](https://openlibrary.org/help/faq/using) describes public-domain
library donations and requests new catalog contributions under CC0. However,
the [licensing page](https://openlibrary.org/developers/licensing), last edited
June 27, 2021, preserves possible preexisting rights in contributions/jurisdictions
while asserting no new Internet Archive proprietary rights. Together these
support catalog reuse; they do not establish that every contributed description
or book cover is public domain or licensed under RateFreak's MIT license.

### Internet Archive terms scope

Open Library's footer links [Internet Archive terms](https://archive.org/about/terms).
The retrieved public HTML is only a JavaScript shell. Its official static-page
loader retrieves `ia-page-terms` metadata; the
[public metadata response](https://archive.org/metadata/ia-page-terms) supplies
the agreement body with a displayed Terms of Use date of December 31, 2014.
The terms describe Collections access for scholarship/research, require lawful,
noninfringing/fair use or compliance with an applicable declared content license,
and reserve content removal and termination of access. They do not give a general
copyright warranty or unrestricted artwork redistribution license.

**Scope remains unresolved:** the relationship between those general Collections
terms, Open Library's specific catalog reuse guidance, and Archive-hosted cover
delivery needs confirmation. This finding is not a determination that every
Open Library API integration is prohibited. No new mandatory cache expiry or
post-termination purge rule was established from this source.

Retrieval evidence: ignored
`.local/release-compliance/provider-agreements/2026-11-18/archive-terms-metadata.json`, 21,345 bytes,
SHA-256 `b4bceb2d384b79ca841dbf8a0911ca8afbb8fd90838cc1bd721ba8549d97f1ed`.
The inspected loader is
`https://archive.org/offshoot_assets/static-page-COlFiBEy.js`.
These are retrieval observations, not an agreement acceptance record.

## MusicBrainz / MetaBrainz

The [data license](https://musicbrainz.org/doc/About/Data_License) distinguishes
CC0 core data from CC BY-NC-SA 3.0 supplementary data and separately identifies
cover art. The [field overview](https://musicbrainz.org/doc/MusicBrainz_Database)
places release-group titles, artist credits, types and identifiers in core data;
release dates are also core. Annotations, tags/genre associations, ratings,
statistics, search indexes and edit history are supplementary.

The current [mapper](../src/RateFreak.Infrastructure/Providers/MusicBrainz/MusicBrainzMapping.cs)
retains release-group title/MBID, artist names/MBIDs/join phrases, album/secondary
types and first-release-date precision. It omits supplementary fields and artwork.
**Inference:** this selected mapping remains consistent with the core-data
classification; adding fields or storing entire responses needs another review.
Core-data retention does not require inventing an expiry interval.

The [hosted API rules](https://musicbrainz.org/doc/MusicBrainz_API) make noncommercial
service use free and direct commercial users to plans/contact; public lookups
currently need no API key but require meaningful identification. The
[rate-limit policy](https://musicbrainz.org/doc/MusicBrainz_API/Rate_Limiting)
requires maintainer contact, applies an aggregate IP budget of approximately one
request/second absent another arrangement, and discourages polling for changes.
Caching and existing process-local pacing reduce load, but do not by themselves
prove shared-egress compliance. CC0 content rights do not waive hosted-service rules.

[MetaBrainz account tiers](https://metabrainz.org/supporters/account-type) explicitly
describe dataset/Live Data Feed signup and provide a nonprofit/open-source-author
tier. They do not establish that every noncommercial individual API lookup requires
a commercial account. Record the operator's actual service arrangement if one is
needed; no paid plan was selected by this review.

## Concrete remaining operator questions

1. Open Library: does the bounded same-origin image optimizer and persistent
   cache have permission despite the requested direct `src` arrangement? What
   cache/removal conditions apply, and how do general Archive terms apply to this
   book-discovery use and cover delivery?
2. Open Library: should the project and each independent self-hosted instance
   register separately? Confirm the actual intended use, contact and single-IP
   deployment before submitting any required acknowledgments.
3. MusicBrainz: is the intended instance noncommercial, or does it need a hosted
   service agreement? Who coordinates the one-request/second aggregate egress
   budget? Review additions beyond the selected core fields separately.
4. Both: keep populated exports/screenshots/artwork outside blanket MIT-content
   claims and establish an operator process for rights complaints/removal.

No runtime defaults, credentials, database content or provider configuration
changed in this review, and no live catalog-provider API test was run here.
