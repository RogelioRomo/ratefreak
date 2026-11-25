# TMDB metadata and poster lifecycle

Status: approved by the owner on 2026-11-24. Builder review found no unresolved
planning questions. Implementation and local acceptance are recorded in
[verification](../../tmdb-lifecycle-verification.md).

## Intent and agreed behavior

RateFreak must keep TMDB metadata and posters current without losing independently
authored ratings, reviews, library history or list memberships. The owner approved
automatic refresh starting at five months, retries before six months, and removal
of expired provider content at six months with an "Unavailable title" placeholder.
Internal catalog UUIDs and TMDB identity references survive removal.

The owner also confirmed resizing/re-encoding is allowed. Treat this as an owner
decision, not as a statement made in the linked forum reply. The staff reply of
2026-10-15 supports refreshing database imports and retaining TMDB IDs after
termination: https://www.themoviedb.org/talk/6ac663597ca2844b5f25e72b.
Record the source and its limits in the provider evidence and update STATUS.

## Approaches

1. Automatic maintenance with explicit deadlines (selected in conversation).
   Covers rarely viewed imports and handles outages without depending on an
   operator remembering a schedule. Requires a worker and managed poster cache.
2. Refresh only when someone visits/imports a title. Less background traffic, but
   leaves unvisited database records and disk files indefinitely stale.
3. Operator-scheduled bulk refresh and purge. Simpler runtime but transfers the
   central correctness requirement to deployment scheduling.

Implement the first approach using the existing .NET/PostgreSQL and Next.js
application. The details below are engineering proposals, not previously agreed
choices by the owner.

## Existing implementation and scope

- `media_external_ids.last_synced_at` records import time. Repeat import returns
  the existing catalog UUID without fetching. The domain has no refresh method.
- Provider request caches hold searches for five minutes and details for one hour;
  those caches do not control database retention.
- TMDB/IGDB posters currently use Next.js's optimizer; Open Library has an internal
  source adapter. The optimizer can keep stale files after failed revalidation and
  has no supported per-image invalidation API.
- Library, feed, moderation and export titles are joined from `media_items`, so
  changing that shared item preserves identities and removes provider fields
  across those views without rewriting independent user content.

This work covers TMDB only. Do not impose its timing rules on other providers or
modify custom uploads, avatars, category behavior or moderation visibility.
Do not publish, migrate the persistent developer database, or delete existing
instance data as part of development. Use disposable databases/storage for proof.

## Metadata state and maintenance

Use UTC calendar arithmetic: a successful synchronization at time T is due at
T.AddMonths(5), and content expires at T.AddMonths(6). Use an injectable clock and
test month-end and leap-year boundaries. A failed attempt never advances T.
For legacy null timestamps use the existing item's creation time, not upgrade
time; null timestamps are not evidence of a fresh import.

Add explicit TMDB lifecycle state keyed by existing provider identity: retry time,
attempt/failure status and content-removed status. Preserve the previous successful
sync time after removal so an expired record cannot appear fresh. Use an additive
migration with a due-work index and no changes to other providers' behavior.

Run a bounded worker on startup and periodically, selecting oldest deadlines
first. Serialize work across API replicas with PostgreSQL coordination; do not
hold an open row transaction across upstream HTTP. Recheck version/state under
the write transaction before applying a response. Retry temporary failures with
bounded backoff and respect Retry-After, sharing the existing transport budget.
An individual failing record must not starve later records or expiry cleanup.

A valid authoritative response atomically replaces title, overview, date, provider
metadata and poster reference, advances successful synchronization and bumps the
item revision/updated timestamp. Preserve its UUID, type, category, user relations,
created time and all moderation flags. Repeat imports remain idempotent and do
not become an unbounded manual refresh endpoint. Use fresh upstream details for
scheduled synchronization, without renewing timestamps from an old cached result.

An authoritative 404 or filtered/unusable deleted record removes provider fields
immediately. Temporary transport/validation errors retain existing content only
until its original expiry. At expiry, set the title to "Unavailable title", clear
description, release date, image reference and provider metadata, and retain UUID,
type, category and TMDB IDs. Never erase user text, review comments or list entries.
Do not change hidden/archived status to represent expiry. A later valid response
can restore provider fields to the same identity unless explicitly blocked.

Expiry cleanup runs independently of provider enablement/network access. Disabling
the adapter stops upstream calls; it does not reset clocks or mean termination.
Run startup expiry reconciliation before readiness/traffic acceptance. A running
worker is not a strict instant-deletion guarantee: document its maximum sweep
interval, capacity and monitoring requirements, and test the startup/restore gate.
Do not claim compliance for an offline host whose storage cannot be maintained.

## Poster fetching, storage and expiry

Keep Next.js optimization for IGDB/Open Library. Route TMDB artwork through a
dedicated same-origin handler with its own bounded cache, including search and
preview artwork. Remove direct TMDB optimizer permissions so bookmarked optimizer
URLs cannot continue serving old TMDB cache entries. Use a public route outside
the edge-blocked `/provider-artwork/` internal namespace; document the distinction.

The handler accepts only canonical TMDB poster filenames and derives a fixed
`https://image.tmdb.org/t/p/w500/` upstream URL. It does not accept arbitrary URLs,
query parameters, redirects or incoming cookies/authorization/forwarded headers.
Reuse the established pinned-public-DNS, timeout, byte and queue bounds. Fetch the
already-sized w500 image; additional re-encoding is unnecessary for this change.

Store bytes with their actual successful fetch time and content type, using atomic
replacement in a separate prepared UID-10001 directory. Never infer freshness
from filesystem access time, renewed metadata, restarts or an upstream max-age.
Reject malformed records, unsafe paths/symlinks and unsupported responses. Bound
entry count and disk bytes; coalesce concurrent misses within a process and make
cross-process file updates atomic. Missing cache storage fails safely.

Posters have their own five/six-month clock. A scheduled cache sweep refreshes
retained posters at five months and deletes expired/blocked files, including those
not recently viewed. A request must independently check expiry and current removal
policy before a hit; no stale-on-error serving beyond six months. Fresh fetches
may repopulate an expired, nonblocked poster. A metadata refresh with an unchanged
poster URL does not reset the poster clock. A metadata refresh with a changed URL
uses the new source; stale cached source files still undergo their own cleanup.

Return poster responses with `Cache-Control: no-store` and `nosniff`, bypassing
Next.js optimization and downstream persistent caching for TMDB. Existing artwork
fallbacks handle missing/expired images. Do not download images through browsers
directly from TMDB. Previously delivered browser copies cannot be recalled; record
this limitation rather than claiming retroactive erasure.

## Explicit removal, termination and restores

Provide an offline operator action for specified TMDB movie/TV IDs and for all
TMDB content on termination. Stop writers before running it. Persist blocks in a
protected independent provider-policy file, outside database/account backups, before
clearing metadata or deleting files. Include blocked poster filenames so direct
requests cannot refetch removed images. A global termination block prevents new
TMDB imports, searches, previews, refresh and poster delivery until an explicit
operator action removes it. A record block prevents reimport/refresh of that ID.
This is separate from simply disabling the provider adapter.

API and web must consult current policy; a missing/corrupt/unreadable required
policy fails closed for TMDB operations. A removal result cannot claim success
until database cleanup and poster-cache cleanup complete; interrupted cleanup is
idempotently resumable. The policy contains identities/control data, not titles,
synopses or credentials. Bound and validate its schema and size. Operator writes
are atomic and durable; services read it but do not silently replace it.

Extend reference preparation/mounts and offline commands to provision the policy
and disposable poster directory with restrictive permissions. Require the current
policy during isolated restore, apply its blocks and expire old provider content
before accepting restore replay/start. Preserve sync timestamps in backups; never
refresh them during restore. Poster caches remain excluded from coordinated backups.
Purge the legacy shared image cache during this upgrade with all web writers
stopped, because its opaque historical TMDB entries cannot be safely identified.
Document that this also makes other providers' image caches cold.

## Verification and completion

Use deterministic clock and fake HTTP tests for five/six-month boundaries,
successful refresh, transient errors, Retry-After, malformed responses, 404,
disabled providers and restart reconciliation. PostgreSQL integration tests prove
same UUID and user-data preservation, race safety, migration behavior, removal
idempotency and restoring an older snapshot with the current policy.

Poster tests prove actual fetch-age tracking, unchanged-URL refresh, scheduled
cleanup of unvisited files, hard expiry during outages, cache capacity, policy
blocks, concurrent writes, unsafe path/URL rejection and transport bounds. Verify
old Next.js TMDB optimizer URLs are denied and protected uploads remain excluded.
Run relevant backend/frontend/operator tests, lint, typecheck, production builds
and model consistency; use disposable reference acceptance for upgrade/restore and
ordinary-account artwork display. Record exact evidence and remaining release
gates. Other providers' policy questions and the security gate remain independent.

Implementation planning may choose internal names, batch sizes, retry intervals,
cache format, locking primitives and command spelling within these constraints.
It must record those choices and an appropriate acceptance scope before coding.
