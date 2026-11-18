# Wikidata movie and TV lookup evaluation

Tested 2026-11-18 against live public Wikidata and Commons APIs. This is a
read-only feasibility trial, not an implemented provider, migration or production
benchmark. No application code, provider settings or database content changed.
See the [usage review](wikidata-usage-review-2026-11-18.md) for CC0 and access rules.

## Result

Wikidata is a plausible source for permanently stored structured metadata, but
this sample does not establish a replacement for TMDB's plot/poster experience.
All 12 selected works had usable display names, short English identification
descriptions, date statements, genres and IMDb identifiers. Only one had a
poster-property value; general images and logos must not be assumed to be posters.

The sample deliberately includes six films and six series: popular titles,
international works, animation, an older film and ambiguous names. It is small
and hand-selected, not a statistical coverage assessment. IDs below were selected
by inspecting results/descriptions, not accepted automatically as the first match.

| Intended work | General search rank | Direct type-filter rank | General image P18 | Poster P3383 |
| --- | --- | --- | --- | --- |
| [Fight Club (1999)](https://www.wikidata.org/wiki/Q190050) | 3 | 2 | No | No |
| [The Matrix (1999)](https://www.wikidata.org/wiki/Q83495) | 2 | 1 | Yes | No |
| [Parasite (2019)](https://www.wikidata.org/wiki/Q61448040) | 4 | 1 | No | No |
| [Dune (2021)](https://www.wikidata.org/wiki/Q60834962) | Outside first 10 | 7 | No | No |
| [Metropolis (1927)](https://www.wikidata.org/wiki/Q151599) | 10 | 1 | Yes | Yes |
| [Spirited Away (2001)](https://www.wikidata.org/wiki/Q155653) | 1 | Outside first 10 | Yes | No |
| [Breaking Bad](https://www.wikidata.org/wiki/Q1079) | 1 | 1 | No | No |
| [The Office (US)](https://www.wikidata.org/wiki/Q23831) | 1 | 1 | No | No |
| [Dark](https://www.wikidata.org/wiki/Q28443710) | 9 | 1 | No | No |
| [Severance](https://www.wikidata.org/wiki/Q101096725) | 1 | 1 | Yes | No |
| [El Chavo del Ocho](https://www.wikidata.org/wiki/Q1323981) | 3 | 1 | Yes | No |
| [The Last of Us (TV)](https://www.wikidata.org/wiki/Q87131973) | 1 | 1 | No | No |

These ranks describe the observed English searches on this date. They can change.

## Search and normalization findings

- General `wbsearchentities` returned mixed subjects: Fight Club's game preceded
  the film; Dune included the landform, books, music and games. The intended work
  was first in 5/12 searches and within the first ten in 11/12.
- `action=query&list=search` with a direct
  `haswbstatement:P31=Q11424` (film) or `Q5398426` (TV series) restriction improved
  first-place matches to 9/12 and retained 11/12 within ten. This is an initial
  filter, not a complete classification strategy. The
  [official search syntax](https://www.mediawiki.org/wiki/Help:Extension:WikibaseCirrusSearch)
  supports statement restrictions.
- Spirited Away uses the anime-film class `Q20650540` rather than a direct film
  statement. One additional query including both classes returned it first.
  Across these tested routes, every intended work was found; no complete
  subclass-aware search or pagination implementation is claimed.
- A batch `wbgetentities` read retrieved the 12 selected works. Three display
  names required the multilingual `mul` label fallback. Requesting English labels
  alone initially omitted Fight Club, The Matrix and Dune labels.
- Films used publication dates (`P577`); the sampled series used start times
  (`P580`). Some works had several territorial/festival dates and qualifiers.
  Do not blindly select the first value as a universal release date. Preserve
  precision and provenance when choosing a display date.
- English descriptions were brief identification phrases, not plot paragraphs.
  Wikipedia article text was not fetched. It has separate reuse requirements.
- Original titles are not always appropriate display names: Parasite's original
  title is Korean, and Dark's observed English original-title claim differed
  from its label. Names, ranks, qualifiers and field selection need deliberate
  handling rather than copying every claim into a catalog field.

## Artwork findings

Five of the twelve records had a general `P18` image; eleven had a `P154` logo;
only Metropolis had `P3383` film-poster data. These counts concern these properties
on these records, not every possible image elsewhere on Wikimedia.

Commons `imageinfo` supplied file URLs, thumbnail URLs and license/credit metadata
for five first general-image references and the Metropolis poster. Examples:

- The Matrix's first general image was a GLMatrix screensaver screenshot, carrying
  an Attribution license label; it was not the theatrical poster.
- Severance's general image was a Bell Labs building photograph, marked CC BY-SA 2.0.
- El Chavo's general image was marked CC BY-SA 3.0.
- Metropolis's general image was a set photograph; its separate poster file was
  marked Public domain in the retrieved Commons metadata.
- Spirited Away's first general image was title artwork, marked Public domain.

Those are Commons metadata observations, not independent rights determinations.
An image pipeline must preserve/check the actual file's attribution and licensing;
Wikidata's CC0 statement does not license the referenced image bytes. Thumbnail
URLs were obtained, but this trial did not download or visually qualify images.
See [Commons reuse guidance](https://commons.wikimedia.org/wiki/Commons:Reusing_content_outside_Wikimedia).

## Transport and evidence

The test identified itself as RateFreak with the project's GitHub contact URL,
serialized requests and spaced request starts by at least one second. No API key,
operator email, account login or SPARQL query was used. There were 32 successful
API requests across searches, batch entity/label reads and Commons metadata.
Observed median request time was 0.328 seconds; the slowest was 1.680 seconds.
These timings are one local run, not a latency or availability guarantee.

An initial request with `maxlag=5` returned HTTP 200 with a JSON `maxlag` error
reporting query-service lag. The trial recorded it and did not enter a retry loop.
The small human-requested lookup trial then omitted that optional background-work
parameter, consistent with the
[interactive-task guidance](https://www.mediawiki.org/wiki/Manual:Maxlag_parameter).
This does not justify omitting `maxlag` for future background ingestion or ignoring
429/Retry-After instructions. No bulk download was attempted.

Raw responses, selected QIDs, request timings, the initial lag response and computed
metrics are retained in ignored `.local/wikidata-evaluation/2026-11-18/`.
`summary.json` contains the per-title metrics; `entities.json` retains source
statements. This trial did not request revision metadata. The temporary evaluation harness was copied there
for reproducibility. No provider adapter or application test fixture was added.

## Recommended next decision

If sparse artwork and brief descriptions are acceptable, design a Wikidata adapter
with robust type selection, multilingual name fallback and qualified dates.
If movie posters and substantial plots are required, this trial does not establish
Wikidata alone as a suitable replacement. Resolve that product tradeoff before
replacing TMDB or migrating existing entries. A future adapter still needs bounded
transport/cache behavior and focused tests; this evaluation does not implement it.
