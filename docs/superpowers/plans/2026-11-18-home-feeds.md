# Home Feeds Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking. Execution method has not yet been selected; native execution is recommended for these closely connected tasks.

**Goal:** Make signed-in Home a Following/Community feed of public reviews and explicitly shared ratings, and replace the notification navigation text with a bell and unread-count badge.

**Architecture:** Derive one current entry per author/item pair from existing reviews, shared ratings, public catalog access and current follow edges. Add rating-sharing fields through an additive PostgreSQL migration, expose a bounded authenticated read API through the existing Next proxy, and use account-scoped TanStack Query state for Home. Keep notification counting and its existing single poller.

**Tech Stack:** .NET 10, EF Core/PostgreSQL 17, Next.js 16.4.6 App Router, React, TypeScript, TanStack Query, CSS modules, Tabler icons, xUnit and Vitest.

**Spec:** `docs/superpowers/specs/2026-11-18-home-feeds-design.md` (approved 2026-11-18; includes the user-requested bell addition).

## Global Constraints

- Existing ratings remain private; each rating is shared only after explicit author opt-in.
- A private score never appears beside a public review, including for its owner or staff.
- Following is the default on a plain `/` visit. Signed-out visitors retain the introduction.
- Community means this instance; Following uses current followees, including their eligible earlier activity.
- Return 20 entries per page, newest first, with stable tie ordering and explicit Load more.
- Public eligibility uses `CatalogAccess.Readable(null)`, never owner/staff exceptions.
- Preserve real zero-star scores, spoiler concealment and profile moderation handle fallbacks.
- Both feed tabs require authentication and all responses use no-store. No extra feed poller.
- No library events, collection discussions, recommendations, new notification kinds or provider changes.
- Reuse current artwork validation/optimization and existing review report/discussion destinations.
- Preserve existing aggregate count/average semantics and score-only PUT compatibility.
- Read `web/AGENTS.md` and the installed Next guides relevant to routing/search params/client boundaries before product-code edits. No new dependencies are needed.
- Schema checks and browser acceptance use disposable databases. Persistent-database activation follows a separate backed-up preservation procedure; no reset, publication or deployment is part of these tasks.

## Review Focus

1. Legacy score-only clients editing an already shared rating must preserve sharing and its original publication time; Task 1 pins this.
2. A shared rating on an item later hidden or made private must disappear even for its owner; Task 2 pins read-time public eligibility.
3. A cached multi-page feed after account change or an authorization/refresh error must be concealed; Task 4 pins cache isolation and resets.
4. Equal timestamps and entries losing one component during pagination must not create repeated cards; Tasks 2 and 4 pin stable keys and client deduplication.
5. Two mounted notification links must continue observing one poller; unavailable counts must not display a stale number; Tasks 4 and 5 check existing behavior and browser presentation.

## File and responsibility map

| Area | Files | Responsibility |
| --- | --- | --- |
| Rating state | `src/RateFreak.Domain/PersonalMedia/Rating.cs`; `src/RateFreak.Application/PersonalMedia/PersonalMedia.cs` | Explicit sharing fields and write/read contracts |
| Rating persistence/API | `src/RateFreak.Infrastructure/PersonalMedia/RatingService.cs`; `src/RateFreak.Infrastructure/Persistence/Configurations/PersonalMediaConfiguration.cs`; generated migration/snapshot; `src/RateFreak.Api/Endpoints/PersonalMediaEndpoints.cs` | Atomic opt-in/out, compatibility, schema constraints and HTTP validation |
| Account export | `src/RateFreak.Infrastructure/Authentication/AccountDataExport.cs` | Export sharing state/publication time |
| Feed backend (new) | `src/RateFreak.Application/Social/Feeds.cs`; `src/RateFreak.Infrastructure/Social/FeedService.cs`; `src/RateFreak.Infrastructure/Social/FeedCursor.cs`; `src/RateFreak.Api/Endpoints/FeedEndpoints.cs` | Bounded current-state projection, cursor paging and authenticated endpoint |
| Backend integration | `src/RateFreak.Infrastructure/DependencyInjection.cs`; `src/RateFreak.Api/Program.cs` | Service/endpoint registration and no-store metadata |
| Feed proxy (new) | `web/server/api/feed-policy.ts` | Exact route and query validation |
| Proxy integration | `web/server/api/route-policy.ts`; `web/server/api/proxy.ts` | Allow/forward only the new feed GET contract |
| Feed client (new) | `web/features/feeds/interfaces/feed.ts`; `query/filters.ts`; `api/feed.ts`; `hooks/use-feed.ts`; `query/keys.ts` under that feature | Shared types, URL state, read API, account-scoped queries and invalidation |
| Home UI (new) | `web/features/home/components/home-screen.tsx`; `web/features/feeds/components/feed-screen.tsx`, `feed-entry.tsx`, `feed.module.css` | Session-aware Home, shared tab/filter/list view and entry rendering |
| Home integration | `web/app/page.tsx` | Suspense boundary and HomeScreen entry point |
| Rating UI | `web/features/personal/interfaces/personal.ts`, `interfaces/component-props.ts`, `api/personal.ts`, `components/rating-form.tsx`, `personal-controls.tsx`, `title-activity.tsx`; `web/features/catalog/components/media-detail-screen.tsx` | Share checkbox, public-eligibility hint and prop plumbing |
| Feed invalidation | `web/features/personal/hooks/use-personal-write.ts`; `web/features/moderation/hooks/use-staff-write.ts`; `web/features/custom-catalog/components/item-editor.tsx`, `categories-screen.tsx` | Reset feed queries after local writes affecting eligible data/identity; follows and profile edits already use `usePersonalWrite` |
| Bell | `web/features/notifications/components/notification-link.tsx`; `notifications.module.css` | Icon and count badge, existing behavior preserved |
| Proof/docs | New focused tests below; existing personal/export/session/notification tests; `docs/home-feeds-verification.md`; `docs/STATUS.md`; `README.md` | Regression proof, API/usage contract and current status |

The migration's timestamp filename is generated by EF; use the name `AddRatingSharingAndFeedIndexes` and keep the existing migration chain intact.

### Task 1: Explicit rating sharing and private-by-default migration

**Files:** Rating/domain/contracts/persistence/API/export files above. Create `tests/RateFreak.IntegrationTests/RatingSharingTests.cs` and `RatingSharingMigrationTests.cs`; extend `AccountDataTests.cs` and existing personal-media tests where contracts change.

**Interfaces:**
- `Rating` adds `bool IsShared` and `DateTimeOffset? SharedAt`.
- `RatingRequest(decimal? Score, bool? IsShared = null)`; own `RatingDetails` gains `IsShared` and `SharedAt`.
- Change `IRatingService.PutAsync` to `Task<CatalogResult<RatingDetails>> PutAsync(Guid userId, Guid mediaId, decimal score, bool? isShared, CancellationToken ct)`; update internal callers together. HTTP callers remain compatible.
- Use existing `CatalogStatus.Success`, `NotFound` and `Invalid`; sharing validation errors use key `isShared`.

- [ ] **Step 1: Write failing PostgreSQL/API tests.** Name the cases `Existing_and_new_ratings_default_private`, `Explicit_sharing_is_atomic_and_score_zero_is_valid`, `Legacy_put_preserves_sharing_and_publication_time`, `Unsharing_then_resharing_creates_new_publication_time`, `Private_item_share_is_rejected_without_changing_score`, `Rating_sharing_requires_owner_session_and_csrf`. Assertions: migration preserves original score/IDs/times and sets false/null; score-only creates false/null; explicit true creates timestamp; repeated true/omitted edits preserve it; false clears it; invalid sharing returns 400 with no partial score change; anonymous 401 and missing CSRF 400. Test concurrent shared/legacy writes never violate state/time consistency.
- [ ] **Step 2: Run the new tests and confirm failure.** `dotnet test tests/RateFreak.IntegrationTests --filter 'FullyQualifiedName~RatingSharing'`. Expect missing contracts/schema or assertion failures, not unavailable Docker.
- [ ] **Step 3: Implement contracts, atomic write and migration.** Under the existing moderation lock, verify author and readable item, separately check public eligibility for an explicit true, then upsert score/sharing together. Omission preserves existing fields; false clears timestamp; true preserves a current shared timestamp or sets the current time. Retained consent on a subsequently private item may be preserved by omitted legacy updates, but the feed always suppresses it. Add a database check requiring exactly `(is_shared AND shared_at IS NOT NULL) OR (NOT is_shared AND shared_at IS NULL)`. Generate the additive migration using `dotnet ef migrations add AddRatingSharingAndFeedIndexes --project src/RateFreak.Infrastructure --startup-project src/RateFreak.Api`. Include candidate feed indexes in Task 2 if justified there, before any persistent application.
- [ ] **Step 4: Extend the private export and verify passes.** Export `IsShared` and `SharedAt` with each own rating; assert no new public data endpoint. Re-run the focused tests plus `dotnet test tests/RateFreak.IntegrationTests --filter 'FullyQualifiedName~PersonalMediaTests|FullyQualifiedName~AccountDataTests'`. Run the documented EF pending-model check; expected no pending changes.
- [ ] **Step 5: Commit this coherent change** with message `feat: add explicit rating sharing`. Stage only its source, generated migration/snapshot and tests.

### Task 2: Current-state Following and Community feed API

**Files:** New backend feed files and registration above; create `tests/RateFreak.IntegrationTests/FeedTests.cs`, `FeedPrivacyTests.cs`, `FeedPaginationTests.cs`. Use the existing `AccountDatabase`/`ApiFactory` fixtures and disposable PostgreSQL.

**Interfaces:** In `Application/Social/Feeds.cs`, define:

```csharp
public sealed record FeedQuery(string Tab, string? Type, string? Cursor);
public sealed record FeedReview(Guid Id, string Excerpt, bool IsSpoiler);
public sealed record FeedEntry(ReviewAuthor Author, MediaSummary Media,
    decimal? Score, FeedReview? Review, DateTimeOffset PublishedAt);
public sealed record FeedPage(IReadOnlyList<FeedEntry> Items,
    string? NextCursor, bool HasFollowees);
public sealed record FeedReadResult(FeedPage? Page,
    Dictionary<string, string[]>? Errors = null);
public interface IFeedService {
    Task<FeedReadResult> ListAsync(Guid viewerId, FeedQuery query, CancellationToken ct);
}
```

GET `/api/v1/feed?tab=following|community&type=<catalog-type>&cursor=<opaque>` defaults to Following; missing type means All. No client-controlled page size. Return `FeedPage` on success and validation problem on invalid input. Use `ReviewAuthor` and `MediaSummary` from existing Application namespaces.

- [ ] **Step 1: Write failing feed tests.** Following excludes non-followees; Community includes viewer and other authors; old public reviews appear; private scores are null even beside own public reviews; shared rating/review merge into exactly one pair; rating zero is retained; edits do not bump time; adding a review is publication; hidden review leaves only an independently shared score. Assert excerpts are at most 280 Unicode scalar values and full bodies/private fields are absent from serialized payloads.
- [ ] **Step 2: Add privacy/paging tests and confirm failures.** Tests cover item/category hidden/private/archive transitions for an owner/staff viewer, deleted author, profile handle fallback, current unfollow, component deletions, deterministic ties across 21+ entries, unsupported/repeated/unknown parameters, invalid/oversized/mismatched cursors, anonymous 401/no-store, social-read 429/Retry-After and route OpenAPI presence. Run `dotnet test tests/RateFreak.IntegrationTests --filter 'FullyQualifiedName~Feed'`.
- [ ] **Step 3: Implement a bounded PostgreSQL projection.** Join eligible reviews and shared ratings by author/item without multiplying rows; evaluate public access and current follow edges inside the query. Compute publication from eligible components, sort by time descending, author UUID ascending, item UUID ascending; seek using that exact ordering, take 21 and return 20 plus continuation. Apply the review excerpt bound in SQL before materializing. PostgreSQL `left(body, 280)` is suitable for Unicode scalar characters. Use one SQL snapshot for candidate components and current visibility, parameterize values and propagate cancellation. No N+1 calls, event log or provider fetches.
- [ ] **Step 4: Implement versioned cursor parsing.** Encode version, viewer, tab, type and last time/author/item position using bounded base64url JSON. Max encoded length 1024; validate before decoding/querying, reject invalid values and binding mismatches. Keep timestamp precision through round-trip. Add necessary reviewed indexes to the unapplied additive migration from Task 1 (or generate a second additive migration if Task 1 was already applied anywhere). Inspect representative PostgreSQL query plans; do not claim a scalable feed from small fixtures alone.
- [ ] **Step 5: Wire the endpoint/service/no-store and verify passes.** Apply auth, social-read budget and `FeedNoStore` metadata, including 400/401/429 paths. Run the focused feed tests and Task 1 tests. Expected all pass with no backend skips. Commit as `feat: add authenticated following and community feeds`.

### Task 3: Feed proxy, URL state and account-scoped client

**Files:** Feed proxy/client files above; create `web/server/api/feed-policy.test.ts`, `web/features/feeds/query/filters.test.ts`, `web/features/feeds/hooks/use-feed.test.tsx`; extend existing proxy tests.

**Interfaces:**
- TypeScript `FeedEntry`, `FeedReview`, `FeedPage` mirror Task 2 JSON and existing `ReviewAuthor`/`MediaSummary` types.
- `FeedFilters = { tab: "following" | "community"; type: MediaType | ""; cursor: string | null }`.
- `readFeedFilters(params: URLSearchParams): FeedFilters | null` and `feedParams(filters: FeedFilters): URLSearchParams` in `query/filters.ts`.
- `getFeed(filters: FeedFilters, signal?: AbortSignal): Promise<FeedPage>` in `api/feed.ts`.
- `feedKeys.root = ["feeds"]`; `feedKeys.page(accountId, tab, type, cursor)` identifies one bounded page query. `useFeed(accountId: string, filters: FeedFilters, active: boolean)` reads that page. FeedScreen owns append/navigation state, retaining page query keys rather than copying response bodies into an independent permanent store.
- `resetFeeds(client: QueryClient): Promise<void>` in `query/keys.ts` cancels old reads, removes their visible pages and invalidates/reset affected queries so local privacy changes cannot leave stale scores on screen.

- [ ] **Step 1: Write failing query/proxy tests.** Defaults and all six catalog types; duplicate, unknown, blank-invalid, overlong cursor and unsupported type/tab rejection; exact GET-only route; query canonicalization; mutation remains blocked. Assert cookie forwarding, abort propagation and no-store remain intact.
- [ ] **Step 2: Write failing query lifecycle tests.** Different accounts/tabs/types never share pages; no reads while session is unknown/transitioning; same-pair entries deduplicate during append; refresh clears cursors/pages; 401 invokes session refresh without exposing old pages; other refresh errors conceal cached entries. Run `pnpm --dir web test server/api/feed-policy.test.ts features/feeds` and the affected existing proxy tests.
- [ ] **Step 3: Implement proxy policy and API/query modules.** Add `feedRoute`/`feedQuery` exports to the feed policy, register exact path/method and parameter validation before general proxy query rejection. Use existing `apiRequest` and no-store proxy response behavior. Account-scope every feed query; use existing session cache clearing rather than adding feeds to the public-root allowlist. Guard append against concurrent/stale responses.
- [ ] **Step 4: Verify the focused tests pass** and commit as `feat: connect feed queries through the browser proxy`.

### Task 4: Home feed, rating controls and notification bell

**Files:** Home/feed/rating UI and notification files in the map; update `web/features/personal/components/personal.test.tsx` and `web/components/layout/app-shell.test.tsx`; create `web/features/home/components/home-screen.test.tsx`, `web/features/feeds/components/feed-screen.test.tsx` and `feed-entry.test.tsx`. Extend existing session/notification tests only where their observable expectations change.

**Interfaces:**
- `HomeScreen()` chooses signed-out `HomeIntro` or signed-in `FeedScreen`; use a Suspense boundary from `app/page.tsx`.
- `FeedScreen({ accountId }: { accountId: string })` reads URL filters and Task 3 queries.
- `FeedEntryCard({ entry }: { entry: FeedEntry })` renders one pair; key by author/media IDs. Review identity/body changes reset spoiler reveal.
- Propagate `canShareRating: boolean` from the loaded `MediaDetails` through `TitleActivity`, `PersonalControls`, private forms and `RatingForm`. Compute it with public visibility, public non-hidden category, non-hidden item and non-archived item, defaulting unknown fields to false. Server validation remains authoritative.
- Own-rating TS DTO gains `isShared: boolean`, `sharedAt: string | null`. Extend `saveRating(id, score, signal?, isShared?)` without moving the existing signal argument; omit undefined sharing from the JSON body.
- `NotificationLink()` keeps its existing count hook/destination/labels; renders `IconBell` and an upper-right count badge.

- [ ] **Step 1: Write failing behavior tests.** Home session branches; Following default and explicit Community; filter/tab changes reset paging; Back/refresh restores URL view; distinct empty/error/loading/end states; load-more deduplication; failed refresh conceals prior cards; profile/catalog/discussion links; spoiler conceal/reveal and escaped text; zero/private-score cases. A review+private rating displays review text with no score.
- [ ] **Step 2: Add rating/write regression assertions and run red.** Unchecked default for new/old private ratings; checked saved shared ratings; explicit share/unshare request bodies; invalid server response preserves draft; private-item opt-in disabled with an explanation while unsharing remains available; score-only compatibility tests remain valid where sharing was omitted. Run `pnpm --dir web test features/home features/feeds features/personal/components/personal.test.tsx components/layout/app-shell.test.tsx`.
- [ ] **Step 3: Implement the shared list and controls.** Use existing UI primitives, `MediaArtwork`, reporting and theme tokens. Use accessible navigation links with current-state indications for tab URLs; native labeled media select; explicit Refresh and Load more buttons. Load more records an append transition and navigates to its next cursor, adding that page's query key to the current account/tab/type list. Other URL navigation (including Back or a pasted URL) starts a list at that requested slice. Reset list and cursor on filters/Refresh; reset retained page keys when their queries are removed/invalidated. Read visible entries from those query results, deduplicate pairs and conceal the list on session/read errors. Retain verified identity during routine refetch. Thus paging is linkable without reconstructing an unbounded history on refresh, and removed query data cannot survive in copied card state.
- [ ] **Step 4: Implement rating sharing and invalidate dependent views.** Explain public attribution at the checkbox; always send the explicit checkbox state from the updated UI. Update feed queries after personal writes, follows, catalog/category/moderation writes and profile edits. Use `resetFeeds` for local changes so loaded later pages do not retain removed data. Locate the existing mutation owner for each write path; do not add a second network write or poller.
- [ ] **Step 5: Replace notification text with the bell/count badge.** Use Tabler `IconBell`, `aria-hidden` icon and visible badge, existing exact-count aria-label and `aria-current`, a title for pointer discoverability, and a 44px minimum focusable target. Anchor a circular pill to the bell's upper-right, cap visible count at `99+`, hide for zero/unknown/error. Preserve error suppression of cached counts and the existing shared single poller. Check desktop and mobile menu layouts in both themes; do not add a second count hook owner. This low-impact presentation change needs browser verification and existing hook coverage, not a new test suite mirroring the markup.
- [ ] **Step 6: Run focused proof and commit.** Run the tests from Step 2 plus `pnpm --dir web test features/auth/components/silent-session.test.tsx features/auth/components/session-provider.test.tsx features/notifications/hooks/use-notifications.test.tsx features/notifications/components/notifications-screen.test.tsx`; expected pass. Commit as `feat: add home feeds and notification bell`.

### Task 5: Final acceptance and current project documentation

**Files:** `docs/home-feeds-verification.md`, `docs/STATUS.md`, `README.md`; focused fixes only if verification finds failures.

**Interfaces:** Final source/migration and API/Next candidate from Tasks 1–4; ordinary reader accounts plus an authorized staff fixture in a disposable database.

- [ ] **Step 1: Run the applicable complete checks.** From root: `dotnet build RateFreak.sln`; `dotnet test RateFreak.sln`; `dotnet ef migrations has-pending-model-changes --project src/RateFreak.Infrastructure --startup-project src/RateFreak.Api`; `pnpm --dir web lint`; `pnpm --dir web typecheck`; `pnpm --dir web test`; `pnpm --dir web build`; `dotnet publish src/RateFreak.Api -c Release`. Record counts, failures and limitations. Never substitute a database-free subset for real integration proof.
- [ ] **Step 2: Exercise the real browser/API flow.** Use a disposable migrated database and isolated ports; do not replace the user's running services. With two ordinary readers: public review, private zero rating absent from feed, opt-in shared score, merged review, unshare, delete, follow/unfollow, Community/Following/media filters and enough fixtures to page. Check refresh, Back, stale failed read, session error, logout, account switch/cross-tab, moderation hiding/restoration and account deletion. Verify signed-out Home remains the introduction. Inspect network payloads for private-score/full-body leaks.
- [ ] **Step 3: Inspect UI and bell acceptance.** Both themes at 1440/768/390/320px; keyboard navigation, visible focus, no horizontal overflow/runtime errors, readable spoiler controls and artwork fallback. Bell at 0/1/99/100+ unread and count failure, read/delete/clear updates, active link, mobile menu dismissal, unclipped upper-right badge, exact accessible count and one shared polling stream. Restore/clean only the disposable fixtures/services.
- [ ] **Step 4: Record evidence and document behavior.** Add the feed contract, sharing/aggregate distinction and compatibility behavior to README. In STATUS mark only verified work complete and state migration activation status accurately. Document ordinary/staff/browser checks, counts and known limits in `home-feeds-verification.md`; keep secrets/private captures out of Git. Note that provider/release gates remain separate.
- [ ] **Step 5: Review the complete diff and commit evidence.** Verify `git diff --check`, review visibility/query/identity boundaries against the spec, and request the required implementation review before integrating. Correct substantive findings and rerun only affected proof. Commit docs as `docs: record home feed verification`. Do not claim the persistent local database or running instance is activated without its separate operational verification.

## Execution recommendation

Use native execution (`superpowers:executing-plans`): the tasks share contracts,
privacy rules and cache behavior, so keeping that context in one implementer is
useful. Run focused proof task by task and an independent full-change review
before integration. Subagent-driven execution remains available if the user
prefers per-task independent review. This recommendation does not authorize
spawning implementation agents before that choice.
