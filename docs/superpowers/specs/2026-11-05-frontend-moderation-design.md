# Phase 7 staff moderation design

Status: approved and implemented2026-11-05; verified native implementation follows reporting commit `459a918`.

## Intent

Let existing moderators/admins inspect reports, make explicit decisions, manage
review/comment visibility and read audit history using the existing API. Preserve
native execution, focused components, separate interfaces and TanStack Query.

## Approaches

1. **Complete staff handling workflow (recommended):** queue, report detail,
   resolution, direct target visibility and audit reads form one focused slice.
   Staff can inspect, act and verify without switching to API tooling.
2. **Read-only queue first:** smaller delivery but leaves submitted reports
   unactionable through the UI and delays verification of concurrency protections.

The user approved the first approach. Role administration and deployment
are separate; they do not need to be added to make report handling useful.

## Access and navigation

Provide an Account link to `/moderation` labeled Moderation tools for signed-in
accounts. Do not claim the account has staff privileges: account/session DTOs have
no role claims and there is no dedicated capability endpoint. Opening the page
makes the existing guarded queue request; a backend200 grants access to that
response, not a persistent promise of future authorization. Ordinary accounts see
an explicit Staff access required state on403. Anonymous visitors get safe login
continuation, with no queued action after login. Extend continuation validation
only for canonical moderation routes and their explicitly allowed filters.

All staff data is private: query keys include account ID and all filters. Disable
reads, conceal/inert data and disable writes during session uncertainty. Do not
add moderation to public-query namespaces. Account changes clear prior state.

On any staff401/403, immediately conceal all staff content, cancel staff reads
and pending writes and remove that account's staff cached data. Keep an external
access-denied latch until an explicit retry performs a fresh authorization read;
cache removal must not cause automatic refetch loops. An ordinary session refresh
alone does not restore staff permission. Existing backend role checks remain the
security authority; frontend hiding is an additional privacy measure.

## Routes and reading

`/moderation` shows a labeled Open/Resolved status filter and URL-owned `page`
1..10000,20 rows per page, explicit Apply and Previous/Next navigation. Default
Open/page1. Queue ordering is backend oldest-first, with actual total counts.
Rows show target type/ID, reason, reporter identity or Deleted account, date and a
link to report detail. Distinguish loading, empty, invalid filter, out-of-range,
rate-limit and failed reads; never manufacture sample reports.

`/moderation/reports/{id}` displays report reason/details, reporter and date,
resolution metadata and the current target if present. The target is current
content, not a snapshot of the original report. Label that distinction. Display
literal text and keep spoiler bodies behind explicit reveal controls. Report
details are staff text and must also render literally. Null targets are Deleted
content and still permit the backend-supported dismissal flow.

`/moderation/{reviews|comments}/{id}` is a direct target inspection page linked
from report detail. Show author, current visibility, spoiler control and the
current body; no invented parent/media links because the staff target DTO omits
them. Provide explicit Hide or Restore according to the inspected visibility.
Show a clear note that restoring a comment does not guarantee public availability
when its parent review is hidden. Do not infer public visibility from Hidden=false.

`/moderation/audit` shows URL-owned targetType/targetId filters and paging,20 rows,
backend newest-first order and actual counts. Target type is optional review,
comment or account; target ID is an optional canonical UUID. Entries show time,
action, actor ID or Operator, target, optional report link, literal reason,
before/after state and reviewed timestamp when present. Do not resolve actor IDs
through nonexistent lookup APIs or invent display names. Link account audit rows
as audit records only; no role-management action is included.

## Decisions and concurrency

Open report detail offers explicit Hide and resolve or Dismiss report, a labeled
required reason1..1000 trimmed characters and a local inline confirmation of the
selected decision and exact inspected target. Resolved reports are read-only.
Dismissal does not restore content. Hide and resolve can acknowledge already
hidden content. Deleted targets allow only dismissal and send a null version.

Direct target changes also require a reason and inline confirmation naming the
exact action/content. Known own-authored targets have no write controls. Deleted
targets cannot change visibility; backend403 still handles historical author
checks when a deleted target no longer exposes its author.

Send `expectedTargetUpdatedAt` exactly as returned by the API; do not round it,
parse/reserialize through JavaScript Date or generate a client timestamp. A
confirmation binds to the inspected target version, report ID/status, action and
reason. Any observed revision/status change or session uncertainty cancels consent.
Keep draft reason text across same-account refreshes but require fresh explicit
consent after a new inspection. Pending submissions disable repeat clicks.

409 means stale target or report conflict: keep reason text, block submission and
offer explicit Reload latest; do not guess which resource changed or silently
repeat the write. 404 marks the resource unavailable. Validation/rate-limit
rejections retain drafts for deliberate recovery. Unknown network/timeout/5xx or
aborted outcomes block resubmission and offer a full reload with draft-loss warning.
POST never retries automatically, even for apparently repeat-safe visibility.

Confirmed204 ends the editor/consent and records success independently of follow-up
reads. Invalidate relevant staff report/target/audit keys and public review,
comment, aggregate and private own-review caches as applicable using existing key
factories. Do not derive unknown parent/media IDs from the target DTO; invalidate
existing bounded feature namespaces when exact IDs are absent. Public404 recovery
must not resurrect hidden content from stale cached lists.

If post-success inspection fails, keep confirmed feedback but block another write
until a fresh inspection succeeds. Never turn a confirmed decision into a failed
save or replay it after read failure. Staff data may remain mounted on transient
read errors only under concealed/inert/disabled wrappers until revalidated;401/403
remove it. Sensitive report details/reasons must not enter URLs, browser storage,
mutation variables/history or logs.

## Transport and implementation boundaries

Feature-owned moderation interfaces, API functions, query keys/filter codecs,
authorization boundary, write hook, screens and focused editors. Thin App Router
pages. Reuse existing editorial styles, Select/Button and feedback components;
responsive flat lists and divided sections, wrapping UUIDs/text and16px inputs.

Enable only existing exact moderation GET routes (reports list/detail, target
reads, audit) and POST routes (resolve and visibility). Validate all list filters
before proxying; preserve existing Origin/CSRF/JSON/body/no-store/abort guards.
No wildcard moderation proxy, admin role routes or caller-controlled upstreams.

## Verification and limits

Write focused failing tests before implementing route/query contracts, private
identity boundaries, staff403 cache removal/latching, stale confirmations,
precision-preserved version tokens, deleted-target dismissal, independent204
feedback, unknown outcomes and spoiler/plain-text rendering. Run full frontend
suite, lint/typecheck/build and existing PostgreSQL moderation integration tests.
Use one fresh whole-change review before declaring completion.

Live browser checks use the running API and real persisted data without response
interception or synthetic fixtures. Verify real moderator success, ordinary-user
403, revoked access, concurrency409, hide/restore public reads, audit persistence,
CSRF/Origin guards, keyboard and both themes at320/768/1024/1440. Use dedicated
local test accounts and exact-identity cleanup; never promote existing real
accounts. If no dedicated staff account exists, provisioning one for these checks
requires explicit authorization because it changes privileged membership.

No backend/schema/dependency changes, external sign-in, role administration UI,
provider activation, automatic commit/merge/push or deployment in this slice.
Release verification remains separate and production ingress is still unverified.
