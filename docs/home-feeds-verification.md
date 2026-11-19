# Home feeds and rating-sharing verification

Date: 2026-11-18. Local feature branch `feat/home-feeds` implements the approved
[design](superpowers/specs/2026-11-18-home-feeds-design.md) and
[plan](superpowers/plans/2026-11-18-home-feeds.md).

## Delivered behavior

Signed-in Home has Following and Community views with media filters and 20-entry
cursor pages. Entries combine current public reviews and explicitly shared
ratings, preserving spoilers and zero scores. Signed-out Home keeps its existing
introduction. The notification navigation link is a bell with a count badge and
exact accessible count, using the existing shared poller.

Existing/new ratings remain private unless opted in. Score-only PUT clients
preserve the saved sharing choice; only an explicit checkbox edit changes it in
the browser. Deleted ratings clear their form's opt-in. Current public eligibility
is evaluated for every feed query; owner/staff access does not widen it. Locally
changed reviews/ratings, catalog/category visibility, follows, profile text and
moderation reset feed caches. Any loaded page's 401 refreshes the session; failed
reads conceal stale entries. Merged pages deduplicate and order current timestamps.

## Automated proof

The focused review regressions passed after each was reproduced against the
previous behavior. They cover rating recreation consent, other-tab consent
changes, custom-item/category cache invalidation, pairs moving between pages,
mixed-page 503/401 responses, and unknown catalog visibility flags. Backend
coverage includes rating-sharing lifecycle/concurrency, public eligibility,
profile hiding, account removal, exact cursor ties/binding, bounded excerpts,
authentication/query validation, private exports and an old-rating migration.

Final results: **830 frontend tests**, **27 unit**, **456 PostgreSQL/API
integration** and **196 provider-contract tests** passed; zero backend skips.
Frontend lint/typecheck, the final Next.js production build, API Release publish
and EF model/migration consistency passed. All integration tests use disposable
PostgreSQL; no database-free subset is presented as database proof.

The complete final integration set ran in complementary name-filtered partitions:
148 account/feed/rating tests and 307 remaining tests, followed by the newly added
feed budget/OpenAPI case (1 test). The commands used `dotnet test
 tests/RateFreak.IntegrationTests` with filters
`FullyQualifiedName~Account|FullyQualifiedName~Feed|FullyQualifiedName~Rating` and
`FullyQualifiedName!~Account&FullyQualifiedName!~Feed&FullyQualifiedName!~Rating`,
plus `FullyQualifiedName~Feed_uses_social_read_budget` after adding that last test.
A prior monolithic run was interrupted (exit 143), so the final result is reported
as partitioned proof, not a successful single invocation. Earlier disk-space and
historical-fixture failures were diagnosed and corrected before these runs.

## Real browser acceptance

Chromium used the actual local API/Next proxy and a disposable migrated database
on separate ports (5088/3008). Synthetic ordinary accounts exercised:

- A private zero score beside a public review stays out of the feed.
- Explicit UI sharing publishes that score in the combined entry; unsharing
  removes the score while keeping the review.
- Following/Community and media filters work; Load more appends 24 unique entries
  and updates the URL; Back resumes the first page.
- A genuine follower notification drives the author header's bell count.
- Count fixtures at 0/99/100/unavailable prove badge hiding/capping, exact accessible
  labels and one shared count request across desktop/mobile navigation mounts.
- Failed feed refreshes conceal old cards; retry recovers; logout replaces the
  feed with the signed-out introduction.
- Both themes at 1440/768/390/320px showed no horizontal overflow or browser runtime
  errors. Populated feeds, compact artwork fallbacks and mobile bell navigation
  were inspected visually.

The additional count boundary cases use API response fixtures; they do not claim
creation of 100 genuine notifications. No external metadata provider was needed.
Component regressions cover account-switch concealment and mixed-page errors;
this browser run does not claim a full staff-role UI workflow or cross-tab
acceptance. Existing moderation APIs/policies remain authoritative.

## Query-plan scope

In the disposable database, PostgreSQL EXPLAIN ANALYZE with 10,000 additional public
reviews and 100 shared ratings returned 21 candidate rows in 49.234 ms for
Following and 49.904 ms for Community. The query uses a full join and computed
publication ordering, so bounded output does not imply constant work or efficient
index seeking over arbitrarily large catalogs. These are local synthetic plans,
not a representative concurrent production load test or scaling guarantee.

## Review decisions and execution limits

An independent read-only reviewer checked the whole initial change. The single
fix pass addressed the consent, cache, ordering, authorization and accessibility
findings with reproduced regressions. The required unknown-flag fail-closed
behavior was also verified through the actual catalog detail screen.

Implementation decisions: query current state instead of storing event snapshots;
send only deliberately changed rating consent; refresh authorization based on all
loaded pages; isolate historical migration fixtures where the current EF model
or other tests' retained data cannot represent the old schema safely.

The feature worktree was relocated from `/tmp` into the repository's ignored
`.local/worktrees/home-feeds` after the temporary filesystem filled during the
existing 256 MiB export-limit test. Historical collection-reset seeding was changed
to insert the old rating shape, and the album downgrade test now owns its own
PostgreSQL container to avoid shared-test ordering. These are fixture corrections,
not changes to account export or provider behavior.

## Activation and release boundary

Migration `20261119013555_AddRatingSharingAndFeedIndexes` adds private-default
sharing fields, a state/time consistency constraint and partial feed indexes. Its
upgrade preserves an old rating's ID, score and original timestamps while setting
false/null sharing state. The user subsequently authorized its application to the
persistent local database; see the local migration record below.
Disposable browser services/database were removed after acceptance. No existing
running instance, production deployment or release artifact has been updated. Provider agreements and self-hosted release gates remain open.


## Local integration

The user selected local merge on 2026-11-18. `main` was fast-forwarded from
`3fc0127` to feature tip `d7bf8c8`. Verification on the merged checkout passed
`pnpm --dir web test` (830 tests) and a complete single invocation of
`dotnet test RateFreak.sln` (27 unit, 456 PostgreSQL/API integration and 196
provider-contract tests; zero skips). This post-merge full run supplements the
partitioned feature-branch proof above. Original untracked planning documents
and selected local evidence were preserved under `.local/home-feeds-merge/`.
The temporary feature worktree/local branch were cleaned up after verification.
At merge time, no remote push, persistent-database migration or application
restart was performed. The subsequent authorized schema upgrade is recorded below.


## Authorized persistent local migration

On 2026-11-18, the user explicitly requested application of
`20261119013555_AddRatingSharingAndFeedIndexes`. The target was the existing
loopback PostgreSQL `ratefreak` database in `ratefreak-postgres-1`, previously at
`20261118195946_AddNotifications`. API port 5080 and frontend port 3000 were
already stopped and no database clients were connected; no servers were started.

A private mode-0600 custom-format backup was created, inspected and restored into
an isolated database. All original table fingerprints matched. The upgrade was
then rehearsed on that restore, preserving all 28 original application tables and
every original rating field; the temporary database was removed. After SQL review,
the same transaction-wrapped migration was applied to the persistent database.

Post-upgrade original-column fingerprints/counts match for all 28 application
tables; the existing rating is private with a null sharing timestamp. Both new
partial indexes and the validated state/time constraint exist, and `is_shared`
is non-null with false default. Migration history advanced by exactly one entry.
A repeat application changed no original data. Database and optional artwork
backups are private local artifacts, excluded from Git, and follow the existing
maximum-30-day retention policy.

Private operational evidence: `.local/localhost/rating-sharing-migration/verification.json`.
The backup and restore/rehearsal logs reside alongside it. This is verified local
schema application, not live authenticated/browser acceptance on a running
persistent installation or production release clearance.
