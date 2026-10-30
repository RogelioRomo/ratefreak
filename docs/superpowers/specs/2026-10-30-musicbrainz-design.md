# Phase 6a — MusicBrainz albums

Status: proposed written design. On 2026-10-30 the user approved starting Phase 6
with MusicBrainz full-length albums and one catalog item per release group.
Phase 6 social features and further providers remain separate slices.

## Intent and approach

Let users discover and import albums, then use them in the existing library,
ratings, reviews and collections. Preserve existing movie/TV/TMDB contracts and
stored data. Reuse .NET 10, existing layers, HttpClient, PostgreSQL and EF Core;
no new dependencies, frontend or speculative provider framework.

Use the existing IMediaProvider contract with an explicit registry for the two
implemented adapters, provider-specific identity validation, and one transactional
import service. The current routes already contain a provider path/body field;
this is the natural extension. Duplicating MusicBrainz routes/import transactions
would duplicate security/conflict behavior; an arbitrary configurable API framework
cannot supply provider-specific normalization and is unnecessary.

## Approved product scope and proposed defaults

- New canonical media type `album`. One MusicBrainz release-group UUID identifies
  the conceptual album; editions/remasters do not become separate catalog entries.
- Accept only release groups with primary-type Album, checking authoritative details
  on import as well as search results. Live albums, compilations and soundtracks
  remain albums when their primary type is Album; preserve secondary types as
  metadata. Exclude explicitly non-musical secondary types audiobook, audio drama,
  spokenword and interview. EPs, singles, broadcasts, recordings/tracks and unknown
  primary types are excluded. No MusicBrainz account/authentication or write-back.
- Album library statuses retain planned/in_progress/completed/paused/dropped; no
  track progress or special listened status. Existing 0–5 half-step ratings, public
  reviews, local-only aggregates and collection deduplication work for albums.
- Return album title and artist credit separately: title stays the upstream title;
  metadata contains a combined artistCredit string and an ordered artists array
  (artist UUID, credited name and join phrase), primaryType, secondaryTypes and
  firstReleaseDate as the upstream partial date string when present.
- releaseDate is a DateOnly only when the upstream date is complete and valid.
  Valid year/year-month precision remains in metadata; do not invent January 1 or
  the first day of a month. Missing dates stay null; malformed dates yield invalid
  provider response. Description and imageReference are null in this slice.
- Do not fetch/store annotation, tags, genres, provider ratings or cover art. This
  bounds scope to the required identity/core metadata; cover art is a separate API
  and rights concern. Attribution capability links to MusicBrainz. Licensing and
  commercial-service review remain prelaunch work, as with existing providers.

## API compatibility and routing

- GET /api/v1/media/providers returns both tmdb and musicbrainz capabilities,
  including enabled state and supported types. TMDB still advertises only movie/
  tv_show; MusicBrainz advertises only album.
- GET /api/v1/media/external/search adds optional provider. Omitted provider defaults
  to tmdb for backwards compatibility; album search explicitly uses
  provider=musicbrainz&type=album. No fan-out or automatic fallback.
- GET /api/v1/media/external/{provider}/{type}/{externalId} supports both adapters.
- POST /api/v1/media/import retains { provider, externalId, mediaType }, cookie auth,
  CSRF and account import budget. Existing TMDB bodies/positive integer IDs retain
  their behavior. MusicBrainz IDs must be nonempty dashed UUIDs (case-insensitive,
  canonicalized lowercase); reject other formats before HTTP/database writes.
- Unknown search/import provider returns 400; unknown provider preview returns 404,
  preserving the existing route behavior. Known provider with unsupported type or
  malformed ID returns 400. Unknown/non-album upstream details return 404.
- Public catalog and library filters accept album. Update messages/OpenAPI and
  ensure expanding MediaTypes cannot make the TMDB adapter accept album.
- Existing search pages stay 1..500, with 20 results/page; MusicBrainz uses offset
  (page-1)*20. Short pages after excluding unsupported upstream rows are valid;
  do not fetch extra pages to fill them or report false totals.
- MusicBrainz q is trimmed 1–200 printable, well-formed Unicode characters. Search
  title or artist using an escaped literal phrase inside a fixed Lucene query
  with an Album primary-type filter. User input cannot inject query operators,
  change type constraints, set arbitrary paths or supply outbound URLs.

## Provider adapter and transport

- Fixed HTTPS origin https://musicbrainz.org/ws/2/. Search release-group with a fixed
  limit/offset and fmt=json; lookup release-group/{uuid} with artist credits and
  fmt=json. No configurable origin, upstream credentials or automatic redirects.
- Explicit MusicBrainz options: Enabled=false, TimeoutMilliseconds=6000 (validated
  1500..15000), Contact (HTTPS maintainer/project URL or contact email when enabled).
  Emit a bounded, validated header RateFreak/0.1 (contact). Reject CR/LF, malformed
  contact/header syntax and oversized configuration with sanitized startup errors.
  Do not infer/send the user's Git email or invent a public project URL. Contact is
  only an outbound identification header, never returned in capabilities/errors.
- One singleton process-wide gate across all MusicBrainz adapter instances and all
  search/preview/import requests. At most one active operation, no queued operations;
  busy callers get 503 with Retry-After. Space every outbound request start at least
  one second apart, including retries. Use monotonic timing/testable TimeProvider.
- Whole-operation timeout includes pacing waits, HTTP, response body and retries.
  At most one retry for transient transport/408/5xx failures, only if deadline and
  provider cooldown permit. A 429 or 503 with Retry-After establishes shared cooldown
  across subsequent operations; retry only a <=1-second cooldown within the same
  deadline. Missing Retry-After on 429/503 uses at least one second. Never retry a
  permanent 4xx, malformed body or caller cancellation. Clamp exposed Retry-After
  to 1..86400 seconds. Pacing is process-local; multiple instances/shared egress
  require coordinated limiting before deployment, not a claim of IP-wide enforcement.
- Existing TMDB concurrency/retry behavior remains unchanged. Public external calls
  share existing external-search 20/IP/minute; imports share 10/account/minute;
  capability reads share catalog budget. No additional collection/social allowance.
- Bound upstream body to 1 MiB even without Content-Length; JSON depth 32 with
  duplicate keys rejected. Validate object/array structure, <=20 search results,
  UUID/title/type/date/artist fields and normalized metadata bounds before storage.
  Limit artist credits to 50 entries, each name <=300 and join phrase <=100;
  combined credit <=2000. Secondary type array <=20 strings, each <=80. Titles
  1–300 after trim; reject controls/NUL/unpaired surrogates in stored strings.
- Validate requested UUID equals returned UUID; never trust mismatched identity or
  client-supplied metadata. A confirmed non-Album detail is 404; malformed identity,
  missing required title/type/artist-credit, wrong JSON types and invalid strings
  are InvalidResponse. Empty artist-credit is allowed and yields no artist names.
- Preserve sanitized provider failure mappings: disabled/busy/unavailable/limited
  503 (Retry-After when known), timeout 504, invalid response 502, missing 404.
  Errors/logs never expose response bodies, supplied contact or credentials.

## Import and persistence

- Generalize application import input to include provider selection; keep adapter
  transport concerns out of shared import persistence. Resolve the chosen adapter
  and its supported type/ID namespace explicitly, not through a TMDB-specific helper.
- MusicBrainz external mapping: provider musicbrainz, namespace release-group,
  canonical UUID, externalUrl https://musicbrainz.org/release-group/{uuid}.
  TMDB provider/namespace/ID/URL mappings remain exactly as before.
- Return existing mapping before making HTTP requests, preserving curated data and
  existing disabled-provider repeat behavior. Never merge different artists/albums
  by title. Upstream alias/merged-ID redirect resolution is deferred; redirects do
  not silently create a second identity or follow arbitrary URLs.
- Create canonical item and mapping atomically; on the existing unique constraint
  conflict, roll back loser and return the winner. Repeat imports return 200 and
  preserve ID/metadata; new imports 201. No library entry/rating/review auto-created.
  Validate provider details fit Domain/storage constraints before opening transaction.
- EF migration widens only ck_media_items_type to include album, preserving rows,
  tables and IDs; no new business tables or automatic startup migrations. Down must
  explicitly refuse if albums exist, explaining why rollback requires data handling,
  rather than deleting albums or returning a misleading successful downgrade.

## Verification and completion

Contract tests use fake MusicBrainz HTTP responses for exact search/lookup request
construction, normalization, dates, exclusions, UUID mismatch, JSON/size/string
limits, disabled options, identification, caller cancellation, timeout, pacing,
retry and shared cooldown. Use controlled time/barriers rather than real sleeps.
Existing TMDB contract tests must remain green, including album rejection.

PostgreSQL HTTP integration tests exercise both capabilities, explicit selection,
backward-compatible TMDB calls, ID/type validation before HTTP, protected imports,
repeat/conflict semantics, no overwrites, concurrent import winner, album catalog/
library filters, ratings/reviews/collection lifecycle and unchanged movie/TV data.
Verify migration apply/reapply, snapshot consistency and safe downgrade refusal.
Run full suite, Release build, transitive package audit and independent review;
update README, ADR and checklist with actual evidence. Preserve existing local data.

Live MusicBrainz activation requires user-supplied maintainer contact information;
implementation/tests do not require it. Leave disabled when unavailable and record
that live verification remains pending. Never publish personal contact information
without the user's explicit choice. Social features, IGDB/books, artwork and all
existing public-launch requirements remain pending after this Phase 6a slice.

## Primary sources checked 2026-10-30

- https://musicbrainz.org/doc/Release_Group — conceptual album versus editions.
- https://musicbrainz.org/doc/Release_Group/Type — primary/secondary classifications.
- https://musicbrainz.org/doc/MusicBrainz_API — search/lookup, JSON, no API key,
  non-commercial service and commercial-service direction.
- https://musicbrainz.org/doc/MusicBrainz_API/Search — release-group fields and paging.
- https://musicbrainz.org/doc/MusicBrainz_API/Rate_Limiting — meaningful User-Agent,
  maintainer contact, one request/second/IP, throttling and scheduling guidance.
- https://musicbrainz.org/doc/About/Data_License — core CC0 versus supplementary
  licensing; cover art belongs to the separate Cover Art Archive.

These references guide implementation; they do not certify legal compliance or
commercial permission. Recheck exact response shapes during adapter contract work.
