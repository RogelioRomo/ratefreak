# ADR 0008: IGDB game identity and app authentication

Status: Accepted, 2026-10-30. User requested IGDB after follows and explicitly
selected main games, remakes and remasters; exclude DLC/bundles/editions.

Add video_game with one item per positive IGDB games ID, namespace game. Use modern
game_type values0/8/9 and absent/null version_parent. No title/platform merging.
Reuse existing import transaction, ownership, CSRF, library/rating/review/collection
contracts. Widen only the catalog CHECK; locked guarded Down refuses games.

Use fixed IGDB and Twitch endpoints, distinct named clients, no redirects, safe
header logging and bounded JSON. App-only client_credentials tokens in singleton
memory with monotonic expiry/early renewal; bounded401 replay, no user Twitch login.
One active operation, no queue,250ms games pacing including retries. Preserve full
upstream cooldown duration; reject every new call throughout it. Limits are process
local and need coordination before multiple instances/share credentials.

Only normalized core fields, cover ID and bounded platform/genre names are stored.
No imported provider scores. Preserve release timestamp metadata without inventing
a calendar day. IGDB public pages use slugs: numeric URLs can point to another
game, so optional externalUrl stays null in this slice.

A separate provider-specific import stack would duplicate security/transaction
logic. A general OAuth framework or SDK adds no present value. Standalone platform,
DLC/edition entities, account linking and broad caching are deferred.

Configuration stays disabled until the user supplies server-side Twitch app
credentials. No live activation/public deployment inferred. See IGDB design/plan,
README and implementation checklist for contracts, primary references and evidence.
