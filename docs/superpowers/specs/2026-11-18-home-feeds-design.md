# Home-page Following and Community feeds

Date: 2026-11-18. Status: approved by the user on 2026-11-18. The user also
requested the notification bell/count change below when approving this design.

## Intent and agreed product decisions

Make Home useful for signed-in readers by showing public opinions across the
catalog. The user chose a home-page feed with Following and Community tabs,
Following selected by default, rather than a separate feed destination. Signed-out
visitors keep the existing introduction and catalog links.

The user also explicitly chose per-rating sharing: users decide whether to share
each rating, and all existing ratings remain private. Today rating reads expose
the caller's own score or an anonymous aggregate, while reviews already expose
their authors. Feed work must preserve that distinction until the author opts in.

## Approaches

- **Home with both tabs (selected):** makes following useful and gives new users
  a Community destination before they have connections. Both tabs reuse one list
  presentation and filtering behavior.
- **Home with Following alone:** smaller scope, but users without follows have no
  activity to browse. Considered in the conversation; the user chose both tabs.
- **Separate feed page:** preserves the current introduction for signed-in users,
  but adds a destination and leaves Home less useful. The user requested Home.

Implementation recommendation: query current reviews, explicitly shared ratings,
catalog access and follow edges. An event log would add historical content,
deduplication, erasure and moderation lifecycle obligations that this feed does
not need. This query-based choice is a proposal by the builder, not a separate
user requirement.

## Home behavior

- Signed-in Home replaces the introduction with the feed. Signed-out Home retains
  the current introduction. Initial session discovery shows a loading state;
  session failures show a retry state. Do not briefly expose another account's
  Following feed or render a signed-out introduction during a routine refetch.
- Following is the default on a plain `/` visit. Community is activity across
  this instance, not across other installations. Following includes only current
  followees; Community includes all eligible authors, including the viewer.
- Store the active tab, media filter and pagination in URL query parameters so
  refresh, Back and links preserve the view. Changing tabs/filters resets paging.
- Media filters: All, Movies, Television, Music, Games, Books and Custom. All
  includes eligible public custom catalog items. No library tracking events,
  imports, follows, collection edits or collection discussions in this release.
- Show newest activity first, with deterministic ordering for timestamp ties.
  Fetch bounded pages (20 entries) and provide an explicit Load more control.
  No automatic infinite scroll, ranking algorithm or notification badge for feeds.
- Empty Following explains following and links to Community without switching
  tabs automatically. Distinguish no follows, no activity and no filter matches
  where useful. Empty Community links to the catalog to write a review/share a
  rating. Loading, failure, retry and end-of-list states remain distinct.

## Entries and ordering

- One current entry per author/catalog-item pair, combining an eligible public
  review and an explicitly shared rating. A private score never appears beside a
  public review. Rating-only, review-only and combined entries are all supported.
- Include author handle/display name, item title/type, cover when available,
  nullable shared score, review ID and a bounded plain-text excerpt, spoiler flag
  and activity time. Preserve a real zero-star rating. Do not deliver full review
  bodies or unnecessary metadata in the feed response.
- Link authors to profiles, items to catalog details and reviews to the existing
  review/discussion page. Rating-only entries have no invented discussion link.
  Reviews reuse existing reporting affordances and permission rules.
- Spoiler excerpts are concealed until explicitly revealed. Reveal state must
  reset when identity or the entry's review changes; never mix it between cards.
  Plain text remains escaped and covers have meaningful accessible text/fallbacks.
- A review's publication time is its existing CreatedAt; a rating's publication
  time is when its author explicitly shares it. Entry time is the later eligible
  publication time. Publishing a review after sharing a rating can move the same
  entry forward; editing text or the shared score does not bump publication time.
- Unsharing/deleting a rating removes the score and leaves an eligible review.
  Deleting/hiding a review removes its text, review ID and review-derived time;
  an independently shared rating may remain. With neither component, remove the
  entry. Sharing again after explicitly unsharing is a new publication time.
- Existing public reviews are eligible immediately. Existing ratings are not
  backfilled as public. The feed shows current state, not a permanent history.
  Following someone includes their eligible older activity too.

## Rating sharing and compatibility

- Add an explicit unchecked-by-default "Share this rating" control to rating
  editing. Explain that sharing makes the score attributable to this user in
  instance feeds. Saving a rating without checking it keeps it private. Existing
  shared ratings load their saved checked state; authors can uncheck and save.
- Persist an explicit sharing state and nullable publication timestamp on the
  rating, with an additive migration defaulting existing rows to private/null.
  Preserve sharing time during ordinary shared-score edits. Share/unshare and
  score updates are one authorized, CSRF-protected transaction under existing
  catalog/moderation write locking.
- Sharing is allowed only for catalog items readable publicly, even when the
  author/staff can privately read the item. Reject an explicit attempt to share a
  private, archived or moderated item with a useful validation response. Keep
  private-item rating functionality usable with an explanation in the form.
- Extend own-rating responses with sharing state. Extend the existing PUT input
  with an optional sharing field: omission preserves the saved state on update
  and defaults new rows to private. Existing score-only clients cannot silently
  publish or unpublish a rating. Explicit false unshares it.
- Anonymous rating count/average retain existing semantics; opting out of feed
  attribution does not opt out of the aggregate. Extend private account exports
  with the sharing state/timestamp. Account deletion already cascades ratings and
  reviews; verify this also removes their derived feed entries.

## Visibility, moderation and stale state

- Feed eligibility always uses public catalog access, equivalent to
  CatalogAccess.Readable(null), rather than viewer-specific owner/staff access.
  Private/custom-owner exceptions never widen feed eligibility. Archived items,
  hidden items/categories and non-public items/categories are excluded.
- Exclude hidden reviews and deleted authors. Profile-text moderation uses the
  existing handle fallback and never returns a hidden display name or bio.
- Reevaluate current visibility, sharing and follow edges on every read. Private
  library data never participates in the query. No separate event snapshots are
  stored that could survive account erasure or content removal.
- Both tab endpoints require authentication in this version. Signed-out visitors
  browse public reviews through the existing catalog/review destinations.
  Responses, including authorization/error responses, use no-store through the
  API and Next proxy. Apply existing social-read rate budgets and cancellation.
- Scope client query caches to account, tab and filters; integrate the existing
  session transition clearing/concealment behavior. Cancel reads and conceal the
  feed on logout, account switches, cross-tab identity transitions and session
  errors. Retain verified identity during routine successful session refetches.
- Refresh on returning to Home/window focus and after local share/unshare,
  rating/review edits/deletes, follow/unfollow and relevant moderation changes.
  Also offer a Refresh action; no background feed polling or live-push guarantee.
  Changes by other clients appear on the next read. Do not retain stale entries
  visibly after authorization/refresh errors; show a retry state instead.

## Technical boundaries and builder choices

Follow the existing .NET Application/Infrastructure/API boundaries, EF/PostgreSQL
patterns, Next proxy, TanStack Query and frontend component/style conventions.
Proposed read contract: GET `/api/v1/feed` with validated tab (`following` or
`community`), optional supported media type and bounded paging cursor. Return
bounded entries and an optional next cursor; no total count is required.

The builder chooses exact DTO names, sharing column names, suitable indexes,
query construction and cursor encoding after checking existing conventions.
Keep filtering/grouping/paging in PostgreSQL with no per-entry queries or
unbounded materialization. Validate cursor size, structure and filter/tab
binding. Order by publication time and stable author/item keys; page queries use
current eligibility, not a retained snapshot. Client append deduplicates by the
author/item pair, and Refresh resets pagination. Concurrent deletes/unsharing may
change later pages; this is a live view, not an immutable export.

An additive migration is expected for rating sharing and justified feed indexes.
Verify it on disposable databases. Applying it to the persistent local database
is separate operational work with the repository's backup/preservation process;
do not reset existing data. No provider access or release publication is required.

This is a feature spanning API, database and frontend. Its implementation plan
lives at `../plans/2026-11-18-home-feeds.md`. Keep STATUS.md current when
implementation and verification are complete.

## Notification navigation addition

Replace the visible Notifications text in the existing navigation link with a
Tabler bell icon. Put a small circular unread-count badge at the icon's upper
right, showing 1–99 and `99+` above 99; hide the badge for zero or unavailable
counts. This is a count-bearing badge, rather than a numberless status dot.
Keep the `/notifications` destination, full exact unread count in the accessible
label, unavailable-count label, active-page indication, keyboard focus and a
minimum 44px pointer target. Use the same component in desktop and mobile
navigation, both themes, without clipping. Continue observing the existing
shared count query; do not introduce another poller or change inbox behavior.

## Verification and acceptance

- PostgreSQL integration: sharing defaults/backfill; legacy score-only PUTs;
  explicit opt-in/out; stable timestamps; zero score; pair merging and pagination;
  tab and media filters; follow/unfollow; public versus private-owner access;
  hidden reviews/profile text/items/categories; account erasure; no-store,
  authentication, CSRF on writes and bounded invalid parameters.
- Cover the privacy boundary directly: an existing private score must not appear
  in either tab or beside an author's public review, even for that author or staff.
  Verify spoiler handling and that excerpts are bounded before serialization.
- Frontend behavior: signed-out introduction, session loading/errors/transitions,
  tab/filter URL and Back behavior, empty/error/retry/load-more states, rating
  sharing controls and write failures, identity-scoped caches and invalidation,
  private-score omission, spoilers and accessible navigation.
- Browser acceptance with ordinary accounts against a disposable migrated
  database: follow, public review, private rating, share/unshare, review-only and
  combined cards, media filtering, paging, logout/account switch and moderation.
  Check desktop/mobile in both themes, keyboard controls, overflow and runtime
  errors. Use local fixtures where providers are irrelevant to the behavior.
- Run relevant backend/frontend suites, lint/typecheck and production builds.
  Record actual results and limitations; no deployment or live-provider clearance
  follows from this feature's acceptance.

## Deferred work

Collection publishing/discussions, reactions, direct feed comments, avatars,
recommendations, trending/popular ranking, cross-instance aggregation, feed
notifications, background polling and library-status sharing remain outside scope.
