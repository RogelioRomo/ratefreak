# Wikidata reuse and access review

Reviewed 2026-11-18 against official licensing and access documentation. This note
covers permissions and service rules; the live movie/TV coverage experiment is
recorded separately. No account registration, provider contact, agreement
acceptance, application configuration change or bulk download was performed for
this review.

## Answer for RateFreak

**Wikidata's structured data can be downloaded and kept for reuse, including in
commercial applications. It is not accurate to describe every aspect of using
Wikidata as unrestricted.** The data dedication, access to Wikimedia's servers,
linked images and text from other projects have different rules.

| Material or activity | Applicable distinction |
| --- | --- |
| Structured Wikidata entities, such as movie labels, descriptions and statements | CC0 public-domain dedication; suitable for permanent local metadata imports |
| Requests to hosted Wikimedia APIs | User-Agent, load, throttling and other service policies still apply |
| Large-scale database acquisition | Use published dumps rather than repeatedly traversing entities through interactive endpoints |
| Linked posters, photographs or logos | Check the actual file's rights/license; Wikidata's CC0 does not license the image bytes |
| Wikipedia article summaries or plots | Separate text-reuse license and attribution requirements; not automatically CC0 |

## Structured-data permission

[Wikidata's licensing page](https://www.wikidata.org/wiki/Wikidata:Licensing)
places structured data in the main, Property, Lexeme and EntitySchema namespaces
under CC0; text in other namespaces is separately licensed. The
[CC0 instrument summary](https://creativecommons.org/publicdomain/zero/1.0/)
permits copying, modification and redistribution, including commercial use,
without asking permission. It does not waive third-party privacy/publicity,
patent or trademark rights or provide a warranty.

**Implementation inference:** retaining selected structured movie/TV metadata in
PostgreSQL is compatible with this model. CC0 imposes no six-month copyright
cache ceiling or requirement to erase the imported metadata merely because it
becomes old. Updating records can improve accuracy, but is a separate product
decision rather than an invented expiry condition. Keeping source QIDs/revisions
and offering source links is useful provenance.

## Hosted API access

The [Wikimedia API Usage Guidelines](https://foundation.wikimedia.org/wiki/Policy:Wikimedia_Foundation_API_Usage_Guidelines)
require accurate User-Agent identification, observance of throttling instructions
and content licenses, and avoidance of excessive or disruptive traffic. Numerical
limits may change by endpoint. Operators must not evade limits by spreading
requests across user agents. The policy also disallows white-labeling that hides
the Wikimedia Foundation as the API provider; it explicitly leaves content
republication under its free licenses intact. API access can be blocked even
though previously acquired CC0 data remains reusable under that dedication.

For the [MediaWiki Action API](https://www.mediawiki.org/wiki/API:Etiquette),
identify the application and provide contact information, serialize/group reads
where possible, use compression and handle throttling with backoff. Background
work should use `maxlag`. The
[Wikibase REST API documentation](https://www.wikidata.org/wiki/Wikidata:REST_API)
also points to User-Agent and global rate-limit requirements. Do not apply one
fixed request budget indiscriminately to Action API, REST API and query service.

The [current Wikidata access guide](https://www.wikidata.org/wiki/Wikidata:Data_access)
instructs clients to pause on HTTP 429 and honor `Retry-After`. It recommends
database dumps for very large result sets, rather than retrieving entities one
at a time. The [database-download documentation](https://www.wikidata.org/wiki/Wikidata:Database_download)
provides JSON/RDF download routes. A small, paced discovery trial does not need
to acquire a full dump.

SPARQL access has its own operational limits and endpoint lifecycle. The current
[WDQS backend migration guidance](https://www.wikidata.org/wiki/Wikidata:SPARQL_query_service/WDQS_backend_update)
announces a move from Blazegraph to QLever and directs clients to the migration
documentation. Historical query-limit pages should not be treated as an immutable
production budget for the current service.

## Artwork and descriptive text

[Commons reuse guidance](https://commons.wikimedia.org/wiki/Commons:Reusing_content_outside_Wikimedia)
requires checking the file's own copyright/license information. Many files require
creator credit, license links and sometimes ShareAlike for adaptations. The
presence of an image filename or URL in a CC0 Wikidata statement does not make
the referenced file CC0. Other rights can also apply.

Long plot summaries obtained from Wikipedia would be a separate integration.
[Wikipedia's reuse policy](https://en.wikipedia.org/wiki/Wikipedia:Copyrights)
sets attribution and licensing conditions for its text and treats media
separately. Keep a Wikidata-only trial limited to its structured descriptions and
statements rather than assuming a linked Wikipedia article is part of the CC0
metadata.

For a future adapter, a sound starting scope is paced title search and entity
lookups, permanent selected-field storage, explicit source attribution/provenance,
and no automatic image ingestion until per-file licensing is handled. This is a
scope recommendation, not an implemented adapter or a whole-project clearance.
