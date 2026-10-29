# TMDB search, preview and import

Continue Phase 3 under the approved architecture. Use a small typed HttpClient
adapter rather than a provider SDK: only movie/TV search and details are required,
and provider DTOs must stay private. No background refresh or frontend work.

Configuration: Providers:Tmdb:Enabled=false by default, AccessToken secret,
TimeoutMilliseconds=4000 (500..15000), Language=en-US (fixed validated locale).
Enabling requires a valid nonblank bearer token. Base API origin is fixed to
https://api.themoviedb.org/3/, redirects disabled; no user URLs are fetched.

Public GET /api/v1/media/providers lists capabilities/configured enabled state and
attribution (not an upstream health probe). GET /api/v1/media/external/search accepts
q (trimmed 1..200, no controls), type=movie|tv_show, page=1..500; TMDB only initially.
GET /api/v1/media/external/tmdb/{type}/{externalId} previews normalized details.
Both share a 20/IP/minute external budget. Search results are bounded to 20.
POST /api/v1/media/import accepts only provider, externalId, mediaType, requires
session + CSRF, and allows 10/account/minute (also bounded by global outbound gate).
Unknown JSON properties on the import contract are rejected; imported values always
come from a fresh details fetch. External IDs are positive Int32 decimal IDs,
canonicalized before lookup. Existing identities return 200 without HTTP; new
imports return 201 with Location pointing at the canonical detail endpoint.

Atomic canonical item + mapping writes use an explicit transaction. A concurrent
unique-mapping conflict rolls back both rows, then reads the winning mapping.
Movie and TV provider namespaces remain distinct; no merging on titles. Existing
metadata is never overwritten, even on repeat import. No new schema is needed.

Provider failures are distinct from no results: disabled/busy/unavailable 503,
rate limit 503 + Retry-After, timeout 504, malformed upstream data 502; sanitized
Problem Details only. One retry for transient network/408/5xx, bounded by the
operation timeout. Retry 429 only when Retry-After permits a <=1 second wait;
otherwise propagate an unavailable result with Retry-After. Four outbound
operations per instance maximum, no waiting queue. Bound bodies to 1 MiB, JSON
nesting to 32. Caller cancellation propagates. No caching until terms are reviewed.

Normalize title/name, release_date/first_air_date, description, poster reference
and limited type-specific metadata. No provider scores become community scores.
Missing dates/artwork are nullable; unsafe/oversized text and malformed required
fields fail closed. Adult search disabled; adult results/details excluded. Image
references use TMDB's image host and validated poster paths; backend never downloads
artwork. Provider external URLs are built from validated IDs only.

Terms checked 2026-10-29: official FAQ says free non-commercial use with attribution;
commercial use requires contact with TMDB. Required approved logo plus notice belong
in future frontend About/Credits; provider capability response exposes notice/link.
Full API terms URL is crawler-blocked; user must review accepted terms in TMDB's
account workflow, particularly commercial use, caching and image rights before launch.
Token activation/live verification needs an account-issued API Read Access Token.
Automated tests use fake HTTP responses and real PostgreSQL, no live credentials.
