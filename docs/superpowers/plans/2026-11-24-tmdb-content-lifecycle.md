# TMDB Content Lifecycle Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [x]`) syntax for tracking.

**Goal:** Refresh TMDB metadata/posters from five months, expire at six, preserve user content, and enforce removals through upgrades/restores.

**Architecture:** .NET worker reconciles stored metadata with PostgreSQL coordination. A dedicated Next.js poster handler/cache enforces actual-fetch deadlines. Offline maintenance and an independent policy file coordinate explicit removal/termination and restore protection. Frozen contracts make the three domains independently implementable; use the dispatching-parallel-agents workflow with disjoint file ownership, then integrated review.

**Tech Stack:** Existing .NET 10, EF Core/PostgreSQL, Next.js 16/Node 24, Python operator tools; no new production dependencies.

**Spec:** `docs/superpowers/specs/2026-11-24-tmdb-content-lifecycle-design.md` (approved).

## Global Constraints

- Automatic refresh starting at five months, retries before six months, and removal of expired provider content at six months with an "Unavailable title" placeholder.
- Internal catalog UUIDs and TMDB identity references survive removal.
- Use UTC calendar arithmetic: a successful synchronization at time T is due at T.AddMonths(5), and content expires at T.AddMonths(6).
- A failed attempt never advances T. Null legacy sync time uses item creation time.
- Preserve user content, categories, hidden/archived flags, other providers, uploads and avatars.
- No publication, persistent-instance migration, or persistent-data cleanup during development.
- Services fail closed for TMDB when policy is unavailable; do not manufacture permissive policy in runtime.

## Shared interfaces (binding across tasks)

- Policy path API option: `Providers:Tmdb:PolicyPath`; env `Providers__Tmdb__PolicyPath`.
- Web env: `RATEFREAK_TMDB_POLICY_PATH`, `RATEFREAK_TMDB_CACHE_PATH`.
- Production paths: `/independent/tmdb-policy.json`, `/data/tmdb-posters`.
- Exact JSON schema: `{"format":1,"terminated":false,"blockedIdentities":[],"blockedPosters":[]}`.
- `blockedIdentities`: unique canonical `movie:<positive-int32>` / `tv:<positive-int32>` strings, no leading zero. `blockedPosters`: unique canonical filenames `[A-Za-z0-9_-]+\.(jpg|jpeg|png|webp|avif)` (maximum 200 characters). Both arrays max 10,000, whole policy max 1 MiB. Reject unknown fields, duplicate JSON keys, malformed values, insecure/nonregular/symlink paths and non-0600 policy. Owner UID must match service UID on Unix. Require an absolute path. Read the current policy on operations, without stale permissive caching.
- Independent policy is controlled by offline maintenance; service readers never create it. Preparation creates initial empty policy only for new instances; existing missing policy is not silently repaired after initial provisioning (receipt outside backups).
- .NET maintenance entry point: `--tmdb-maintenance --database-name <exact-name> --reconcile`, or `--block <movie:N|tv:N>`, or `--terminate`. Optional repeated `--poster <canonical-filename>` supplies search/preview-only or externally identified historical source filenames for `--block`; imported poster history is collected automatically. Offline ID-only removal cannot discover unknown search-only filenames and must document this limit. Stop writers; validate DB identity. Block/termination durably updates policy before database purge, and adds affected known poster filenames. No automatic unblock/resume in this first slice; reviewed manual policy changes are documented.
- API replay of account erasures also calls TMDB offline reconcile (expiry/policy only, no network) before reporting success.
- Web route: `/tmdb-posters/<canonical-filename>` (no query), no-store/nosniff. TMDB original references remain canonical in API JSON; renderer translates them. TMDB is removed from Next remotePatterns; cannot reach the dedicated route through Next localPatterns.
- Poster store: separate prepared private directory, excluded from backups. Operator maintenance clears this cache with writers stopped. Upgrade must clear old shared Next cache once, using a lifecycle receipt to avoid deleting ordinary cache on every prepare.

## Review Focus

- Calendar month ends and null/future/tampered timestamps must not grant extra retention.
- A slow HTTP response racing with removal must not resurrect blocked provider fields/bytes.
- Multiple workers/large backlogs must not starve expiry or overload upstream.
- Restore with missing current policy must fail, and old snapshots must not undo a block.
- Old Next optimizer URLs and invalid encoded filenames must not bypass cache expiry/removal.

---

### Task 1: API metadata lifecycle and policy

**Files:** create `src/RateFreak.Infrastructure/Providers/Tmdb/TmdbContentPolicy.cs`, lifecycle state/service/worker files under that directory; modify TMDB options/adapter, domain catalog refresh methods, import service, dependency registration, EF configuration/migration; create `src/RateFreak.Api/Maintenance/TmdbMaintenance.cs`; modify Program/account replay; tests under backend projects only.

**Interfaces:** produces shared policy reader/writer and maintenance command above. Integrates lifecycle state keyed by `MediaExternalId.Id`, sync timestamps remain on mapping. Fresh scheduled details bypass response cache. Worker uses session advisory lock across HTTP and short row transactions to apply; shared lock across maintenance/import prevents resurrection. Batches 100 per minute, oldest due first; failures retry with bounded exponential delay (minimum 1 minute, maximum 24 hours), honoring larger Retry-After within 24 hours. Expiry updates all overdue rows in bounded database batches independent of refresh failures. Startup reconcile before serving; periodic reconciliation every minute. Unit-injectable TimeProvider.

- [x] Write meaningful failing tests: five/six calendar month boundary, month-end leap year; same UUID/user rows survive refresh/expiry; disabled adapter still expires; 404 removes; malformed response doesn't renew; null legacy timestamps; policy malformed/missing/blocked identity; slow response/removal race; fairness and retry behavior.
- [x] Run focused tests, record RED; implement minimal behavior and additive indexed migration.
- [x] Run backend suites against disposable PostgreSQL and model consistency; record GREEN and any limitations. Do not change operator/web files or docs, and do not commit while parallel work is active.

### Task 2: Dedicated TMDB poster lifecycle

**Files:** create `web/server/artwork/tmdb-*` modules/tests and public route `web/app/tmdb-posters/[filename]/route.ts`, instrumentation bootstrap if needed; modify shared provider image policy, artwork renderer and their tests. Own web/ files only; read AGENTS.md and local Next docs before coding.

**Interfaces:** consumes shared policy schema and envs; produces dedicated route above, clock-injectable cache/transport with deterministic tests. Use w500 origin bytes, max upstream body 5 MiB, no redirects, pinned public DNS, 10-second total deadline, 8 operations/64 queued work, coalesced per-source misses. Own persistent cache must bound bytes (1 GiB) and entries (2048), atomic file writes and secure path checks, no stale source/header-based renewed time. Scheduler at startup and every minute sweeps expired/blocked files and refreshes a bounded batch (100) due at five months; no six-month stale serving. Calendar arithmetic must match .NET clamping. Read current policy before delivering a hit and after upstream fetch before persistence/response; reject termination changes racing a fetch.

- [x] Write failing tests for exact calendar boundaries, unchanged filename refresh, fake outages, unvisited cleanup, malformed cache, capacity, policy blocks/missing policy, concurrency/cancellation, unsafe source and cache paths, SSRF bounds, no optimizer bypass.
- [x] Run focused Vitest cases to see RED, implement dedicated store/route/scheduler and renderer source translation.
- [x] Run web tests/lint/typecheck and production build as feasible; record GREEN and limitations. Do not change backend/operator/docs; no commits while parallel work is active.

### Task 3: Reference operator preparation, removal and restore

**Files:** `deploy/self-host/entrypoint.py`, `ratefreak.py`, `maintenance.py`, `compose.yaml`, operator tests; new focused helper module if needed. Own deploy/self-host files only.

**Interfaces:** consumes API command/schema/env and web route/cache contracts above. Preparation creates directory/policy with UID/GID10001 and 0700/0600; mounts policy read-only to API/web but read-write tools. Provision receipt outside backup identifies missing policy on repeat prepare. CLI `tmdb-remove --database-name NAME (--identity movie:N|tv:N | --terminate) --confirm-tmdb-removal` stops/refuses live writers, runs tools maintenance, clears cache with safe paths, records completion only on success. Expose `tmdb-reconcile` as needed; tools replay allowlist supports API maintenance. Restore now requires `--tmdb-policy PATH`, validates and copies independent current policy before replay. Verify replay checks blocked and expired provider fields are removed. Cache initialization/legacy cache upgrade receipt never automatically recreates permissive policy for existing installs.

- [x] Write failing Python tests for first/repeat preparation, missing policy, unsafe ownership/symlinks/schema, exact command wiring, stopped-writer requirement, interrupted cleanup and restore requiring current policy.
- [x] Run focused unittest RED then implement minimal preparation/lifecycle wiring/mounts.
- [x] Run operator/release/compliance tests; record GREEN and limitations. Do not edit application/web/docs; no commits while parallel work is active.

### Task 4: Integration, documentation and release evidence

**Files:** plan/spec/STATUS/provider docs/self-hosting/reference verification as needed; integration scripts/evidence in `.local/tmdb-lifecycle/` scratch.

**Interfaces:** consumes all three task deliverables. Root owns integration fixes only after coordinating with owners. API/backups and web must share exactly the same policy snapshot/schema. No runtime policy synthesized for tests without explicit fixture provisioning.

- [x] Review task reports and diff against all approved design requirements; address cross-domain contract mismatches with focused regression tests.
- [x] Run full backend/frontend/operator checks, lint/typecheck, production builds and EF pending-model check. Use escalation for Docker/network sandbox failures, never use persistent .env or instance volumes.
- [x] Build disposable candidate images and run fresh/upgrade/restore, old-policy restoration, five/six-month synthetic-clock proof and ordinary-account poster/fallback acceptance. Keep report honest about exact source/image scope.
- [x] Dispatch independent code review; fix Critical/Important findings with focused tests, rerun affected checks. No publication/integration without later authorization.
- [x] Update approved design status, STATUS/provider docs/operator runbook with actual shipped behavior, limits and evidence; preserve historical records. Report source path/branch, checks and any unmet release scope.

Completion: all four tasks implemented and locally verified. See [verification](../../tmdb-lifecycle-verification.md). Integration/publication remain separate.
