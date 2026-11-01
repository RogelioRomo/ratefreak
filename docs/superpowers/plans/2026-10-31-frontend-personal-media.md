# Personal Media Implementation Plan

> Execute natively with superpowers:executing-plans; preserve existing feature checkout.

**Goal:** Private tracking library plus own ratings/reviews and public community reviews on catalog details.
**Architecture:** Two authenticated own-record reads in existing .NET services; exact restricted proxy routes, feature APIs/Query keys containing account identity, focused native forms and public review pagination.
**Tech Stack:** Existing .NET/EF/PostgreSQL and Next.js/React/TypeScript/CSS Modules/TanStack Query/Vitest.
**Spec:** docs/superpowers/specs/2026-10-31-frontend-personal-media-design.md (approved by user).

## Global constraints

Preserve approved editorial themes/icons, all uncommitted work, separate interfaces, existing auth/CSRF safeguards. No new dependencies, schema migrations, automatic commits/push/deploy or real-account verification writes. Reuse disposable backend test containers and synthetic browser writes.

## Review focus

- Private record reads and cached state must never cross account boundaries or expose hidden reviews.
- Status edits preserve optional dates; rating zero is not missing.
- Review conflicts/errors/refetches preserve drafts without silently replacing another review.
- New methods and query strings cannot widen the proxy beyond exact approved routes.
- Review-page navigation and background queries must not reset unrelated personal form drafts.

### Task 1: Own-record backend reads
Files: Application/PersonalMedia/PersonalMedia.cs, Infrastructure/PersonalMedia/{LibraryService,ReviewService}.cs, Api/Endpoints/PersonalMediaEndpoints.cs; tests/RateFreak.IntegrationTests/PersonalMediaTests.cs.
Interfaces: ILibraryService.FindAsync(userId,mediaId,ct); IReviewService.FindOwnAsync(userId,mediaId,ct), existing DTOs.
- [x] Add failing integration test: anonymous401, absent404, owner existing DTO/no-store, another user404, hidden own review404.
- [x] Run targeted test; expect missing route failures.
- [x] Implement filtered reads and exact authenticated/rate-limited endpoints; no business write changes.
- [x] Run PersonalMediaTests; expected green.

### Task 2: Transport, contracts, state and safe continuation
Files: server/api/personal-policy.ts and tests, proxy/route-policy/proxy tests; app/api route methods; features/personal/{interfaces,api,query,hooks}; auth/query/return-to tests.
Interfaces: typed library/rating/review contracts match C#; validatePersonalQuery(params,kind); own APIs convert only404 to null, preserve errors; keys contain account and media/filter IDs.
- [x] Failing tests for exact GET/PUT/PATCH/DELETE paths, mutation guards, duplicates/unknowns/status/type/pages and unchanged auth/provider rules.
- [x] Failing client/filter/continuation tests including score0, complete dates/body, signal propagation, canonical /library and /catalog/GUID only.
- [x] Implement approved boundary/state contracts and APIs; verify focused tests green.

### Task 3: Library and personal forms
Files: app/library/page.tsx; features/personal/components/library-screen,personal-controls,tracking-form,rating-form,review-form,public-reviews and CSS/props/tests; hooks/use-personal-write; catalog detail/navigation/home copy.
- [x] Add failing real QueryClient DOM tests for protected library, URL filters/pages/recovery and sessions.
- [x] Add failing form tests for saved dates,0rating, own-review absence/errors, literal/spoiler text, conflict/draft preservation, inline confirmations, pending guards and stale account/media writes.
- [x] Implement session-gated keyed private components with abortable writes, precise invalidation and no automatic retries.
- [x] Integrate public aggregate/review URL pagination without remounting forms; safe login continuation and signed-in Library link.
- [x] Run full frontend tests; expected green.

### Task 4: Verification/handoff
- [x] Full frontend tests/lint/typecheck/build, focused backend suite and diff check; expected green.
- [x] Batch both themes320/768/1024/1440 browser captures, keyboard/history/session/review-page drafts and synthetic writes; inspect console/overflow.
- [x] Restart API with new reads; live read-only status/contract checks, no real writes.
- [x] Fresh combined code/UI review, bounded fixes, design documentation comparison.
- [x] Record exact evidence/process state in checklist and README/PRODUCT; next collections metadata/visibility.
