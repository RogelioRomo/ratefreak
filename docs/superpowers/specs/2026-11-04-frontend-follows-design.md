# Frontend follows and public connections

Scope selected by the user's request to continue after public profiles. Native
execution continues with the existing Next.js App Router, CSS Modules and
TanStack Query. Preserve previous uncommitted profile work.

On `/users/{handle}`, add explicit Follow and Unfollow actions for verified
signed-in viewers other than the profile owner. The API exposes no point lookup
for viewer relationship state, so do not infer it by scanning paginated lists or
claim a current state. Explain that the actions set the relationship and are safe
to repeat. Anonymous visitors get a login continuation; login never writes.
Session checks conceal/inert controls and abort writes; identity changes reset
feedback. Known204 means saved even if subsequent list reads fail. Unknown
outcomes display uncertainty and allow only deliberate repeat-safe user actions.
No automatic mutation retries or optimistic relationship guesses.

Show Followers/Following links and one public list on the profile. URL parameters
`connections=followers|following` and `page=1..10000` own selection/paging; omitted
values mean followers/page1. Page size20. Reject unknown/duplicate/invalid params
before fetching; changing direction resets page1; Back/Forward restores selection.
Use exact public DTO fields, profile links, totalCount and oldest-first API order.
Include empty/missing/loading/429/retry/out-of-range states. List reads remain
available anonymously; all HTTP responses remain no-store. No graph-derived access
claims, private data, invented counts, feeds or notifications.

Proxy permits only GET users/{guid}/followers|following with bounded paging and
PUT/DELETE users/{guid}/follow through existing Origin/CSRF/JSON/body guards.
Writes send `{}` for the existing JSON boundary. No backend/dependency changes.

Approaches: explicit setters plus lists fit the current API. A toggle would need
an additional relationship-status contract; scanning pages is unbounded and gives
incomplete results. Builder choice: keep both deliberate actions with honest copy.
Reuse editorial typography, flat lists, theme tokens and visible keyboard focus.
Verify focused regressions, full frontend checks, backend FollowTests and real
browser/API workflows with exact temporary-account cleanup. No commit or deploy.
