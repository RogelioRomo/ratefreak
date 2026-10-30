# Phase 6c: IGDB video games

User requested continuing implementation with IGDB after follows and confirmed
main games, remakes and remasters. Each IGDB games ID is a separate canonical
`video_game`; platform/edition entities, DLC, bundles and other game types excluded.
Use game_type (0/8/9) and require absent/null version_parent. No title-based merge.

## Approach

Add an IMediaProvider adapter to the existing registry/import transaction path;
a separate games pipeline would duplicate auth/CSRF/concurrency contracts.
No generic provider framework or SDK dependency. Fixed origins, separate named
`igdb` and `igdb-auth` HTTP clients with redirects disabled, redacted headers.
Tokens acquired using Twitch client_credentials in form bodies, never URLs.
App tokens cached in singleton state with monotonic expiry (60-second safety
margin, at most 55 minutes cached); invalidate on 401, renew once and replay once.
No user Twitch login/session/scopes. Disabled by default; enabled requires safe
ASCII alphanumeric client ID/secret of 1..256 characters. Timeout 500..15000ms,
default 6000ms, covers token acquisition, pacing, retries and response bodies.

Singleton exclusive operation slot, no queue. Games request starts at least
250ms apart including retries across adapter instances. Cooldown shared for token
and games 429/503 (default 1 second, honor complete delta/date Retry-After internally;
exposed seconds capped 86400). Reject during cooldown without HTTP. At most two
games attempts, one token request per attempt. Retry first network/408/5xx failure
once (only 503 with delay <=1s); permanent/redirect failures do not retry, except
401 triggers one token renewal. Token failures do not retry within the operation.
Credentials/bodies never logged or included in error/capability responses.

Search: `provider=igdb&type=video_game`, existing 1..500 page, 20 results,
fixed fields/filter and offset; quoted query with escaped backslashes/quotes.
Details: positive Int32 ID canonicalized, namespace `game`, external URL null.
IGDB public routes use slugs, so never invent a link from the numeric API ID.
No slug import in this slice; no provider URL fetching. Existing default search stays TMDB.
Details query filters ID; validate returned ID even when excluding type/edition.
Empty array -> missing, more than one detail -> invalid. Search <=20 rows and no
repeated IDs. JSON: <=1MiB, depth32, reject duplicate properties and invalid Unicode.

Map required id/name/game_type; title trimmed 1..300 no controls; optional summary
<=20000 with no NUL and only newline/tab controls. Metadata: gameType (number),
optional firstReleaseTimestamp (Unix seconds within DateTimeOffset range), optional
platforms/genres arrays of <=20 unique positive ID/name pairs with names 1..100.
Canonical releaseDate stays null: first_release_date alone does not establish
full calendar precision. No invented release day. Optional cover.image_id strictly
1..100 ASCII alphanumeric/underscore/hyphen -> fixed HTTPS IGDB CDN cover URL.
No raw external scores, arbitrary fields, URL/HTML or user-supplied metadata import.
Metadata serialized <=32768 characters. Partial malformed supported rows -> 502,
valid excluded types/editions -> omitted/404. Missing optional fields -> null/absent.

All games use catalog/library/rating/review/collection contracts without special
ownership changes. AddVideoGames only widens media_items type check; Down refuses
while any games exist. Preserve all prior canonical/provider records.

## Verification and scope

Provider contracts exercise authentication caching/expiry/renewal, fixed origins,
query escaping, response bounds/types/identity, exclusion, retry/cooldown/pacing,
caller cancellation/deadline/busy release and client isolation. HTTP/PostgreSQL
prove validation-before-provider, disabled503, auth/CSRF, authoritative/repeat and
concurrent imports, game personal/collection lifecycle, migration refusal/reapply.
No live API calls/credential provisioning or public deployment needed to implement.
Apply/reapply local additive migration, read-only smoke, full suite/Release build,
EF model consistency, dependency audit, independent review, documentation/commit.

## Primary references (checked 2026-10-30)

- https://api-docs.igdb.com/ — requests, 4/sec and <=8 inflight, games fields,
  enum-to-table migration (values unchanged), edition filter, images/pagination.
- https://dev.twitch.tv/docs/authentication/getting-tokens-oauth/#client-credentials-grant-flow
  — server app tokens using form-body credentials. No maintained user OAuth session.
- https://dev.twitch.tv/docs/authentication/validate-tokens/ — hourly validation
  requirement applies to applications maintaining OAuth sessions; this slice only
  acquires app tokens for server metadata access, with no Twitch user sessions.

Builder choices: conservative one-operation concurrency; no guessed calendar day;
optional external URL omitted; defer expanded datasets and broad caching.
Review correction: numeric public URLs can identify an unrelated slug/game;
reproduced with API ID1942 and regression-tested null identity/HTTP URL.
