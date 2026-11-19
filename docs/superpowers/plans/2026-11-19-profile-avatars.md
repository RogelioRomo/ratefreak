# Profile Avatars Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [x]`) syntax for tracking.

**Goal:** Add custom uploaded avatars and a fallback to existing user identity surfaces, using a dedicated filesystem folder.

**Architecture:** Introduce account-owned avatar metadata and a dedicated avatar service/storage adapter under the existing artwork root. Reuse bounded image decoding and storage primitives without moving catalog files. Extend existing profile moderation, export/erasure and checked image access to cover avatars.

**Tech Stack:** .NET/EF Core/PostgreSQL, SkiaSharp, Next.js/React/TypeScript/CSS Modules, TanStack Query, xUnit, Vitest and Python operator tests.

**Spec:** `docs/superpowers/specs/2026-11-19-profile-avatars-design.md`

## Global Constraints

- Static JPEG, PNG or WebP only; maximum input 5 MiB; retain existing 8192-per-axis and 20,000,000-pixel decoder bounds.
- Correct orientation, center-crop to square, re-encode WebP with maximum 512 x 512 output; never upscale small images.
- All avatar data, staging and leases live under `<Artwork:RootPath>/avatars/`; leave existing catalog files in place.
- Public/owner/report image routes use checked access, no-store and nosniff; bypass image optimization.
- Keep existing profile hide/restore, report deduplication, staff scope and account erasure policies.
- Additive schema changes; use disposable test databases. Persistent migration remains a separate reviewed operation.
- No social links, onboarding, remote-image import, animation, crop editor or new dependencies.
- Keep frontend object interfaces in separate files. Read applicable `web/AGENTS.md` and local Next.js guides before frontend code.

## Review Focus

- Avatar folder replaced with a symlink: upload, read, cleanup and backup refuse traversal.
- Uncertain save or session change: old/account-private previews cannot leak into the next account; refetch before retrying a possibly committed write.
- Avatar changes while staff inspect it: stale restore is rejected through ProfileUpdatedAt.
- Mixed old catalog files and new avatar files: backup/restore preserves both and rejects all other nested archive paths.
- Retired or hidden image URL: subsequent direct image requests fail, including while other public surfaces still hold stale DTOs.

---

## File Structure and Interfaces

- Create `src/RateFreak.Application/Identity/Avatars.cs`: `AvatarState(string? AvatarUrl, long Revision)`, `AvatarStatus` (`Success`, `Invalid`, `NotFound`, `Conflict`, `Unavailable`), `AvatarResult(AvatarStatus Status, AvatarState? Value = null)`, and `IAvatarService`.
- `IAvatarService`: `PutAsync(Guid actorId, long expectedRevision, Stream input, CancellationToken ct)`, `DeleteAsync(Guid actorId, long expectedRevision, CancellationToken ct)` return `Task<AvatarResult>`; `ReadAsync(Guid userId, Guid assetId, CancellationToken ct)`, `ReadOwnAsync(Guid actorId, Guid assetId, CancellationToken ct)`, `ReadForReportAsync(Guid actorId, Guid reportId, Guid assetId, CancellationToken ct)` return `Task<ArtworkRead?>` using the existing artwork read type.
- Create `src/RateFreak.Infrastructure/Authentication/AvatarAsset.cs`: one record per user, asset ID, storage key, content type, width, height, byte length; FK to `AccountUser` with cascade deletion.
- Modify `AccountUser.cs` and `Persistence/RateFreakDbContext.cs`: dedicated `AvatarRevision` starting at 0 and concurrency protected; configure avatar asset table and generate additive migration/snapshot.
- Create `src/RateFreak.Infrastructure/Artwork/FileAvatarStorage.cs`: dedicated adapter for existing storage primitives rooted at `Artwork:RootPath/avatars`; strict leaf keys and symlink checks.
- Create `src/RateFreak.Infrastructure/Artwork/AvatarService.cs` and `AvatarCleanupService.cs`; register in `DependencyInjection.cs`.
- Modify `ArtworkDecoder.cs` and its application interface with an avatar-specific decode entry point sharing existing validation/concurrency gating; preserve catalog decoding output.
- Create `src/RateFreak.Api/Endpoints/AvatarEndpoints.cs`; register in `Program.cs`.
- Add optional `AvatarUrl` fields to public identity DTOs and `AvatarUrl`/`AvatarRevision` to `AccountProfile`; update their actual projections in authentication, reviews, comments, follows, feeds, notifications and collections.
- Modify account export/erasure and operator maintenance to handle account-owned avatar files.
- Create `web/features/profiles/components/avatar.tsx`, `avatar-editor.tsx`, `avatar.module.css`, `api/avatars.ts` and separate `interfaces/avatar.ts`.
- Update existing profile, auth, review, comment, follow, feed, notification, collection and moderation interfaces/components; reuse the shared avatar component.

### Task 1: Isolated avatar storage and bounded decoding

**Files:** New storage adapter; existing decoder/interface; `tests/RateFreak.UnitTests/AvatarStorageTests.cs`, `AvatarDecoderTests.cs` (new), existing artwork storage/decoder tests.

**Interfaces:** Produces `FileAvatarStorage` with the existing `IArtworkStorage` operations, and `IArtworkDecoder.DecodeAvatarAsync(Stream input, CancellationToken ct): Task<DecodedArtwork>`.

- [x] Add failing tests: avatar files/stages/leases stay in `avatars/`; catalog root files remain untouched; traversal, symlinked folder/files and invalid keys are refused. Decoder tests assert square <=512 output, correct orientation, no upscale, WebP encoding, metadata removal, malformed/animated rejection and exact input/dimension limits.
- [x] Run `dotnet test tests/RateFreak.UnitTests --filter 'FullyQualifiedName~Avatar'`; confirm failures represent missing behavior.
- [x] Implement the adapter and shared bounded decoder path; create directory only for enabled valid absolute root, preserving existing root ancestor/link checks and a shared decode semaphore.
- [x] Run avatar and existing artwork unit tests; verify no catalog output change.
- [x] Commit the coherent storage/decoding change.

### Task 2: Account-owned avatar API, moderation and public projections

**Files:** New application contracts, asset entity, service, endpoints; user/context/DI/Program; identity DTOs and corresponding query services; moderation projections; new `AvatarTests.cs`, `AvatarConcurrencyTests.cs`, `AvatarModerationTests.cs` under integration tests.

**Interfaces:** Consumes Task 1 storage/decoder. Produces the `IAvatarService` contract above and nullable public `avatarUrl` fields. Account DTO returns owner route and `avatarRevision`, including when hidden.

- [x] Add failing API tests for private writes, CSRF, multipart validation/limits, disabled storage, upload/replace/remove, stale revision 409 and failed replacement retaining original. Add hidden/direct-old-URL and stale moderation restore tests; assert every public identity projection uses fallback/null while hidden.
- [x] Run focused integration tests against disposable PostgreSQL and confirm missing behavior fails; do not accept database-related skips as proof.
- [x] Generate additive migration: avatar table and revision default 0; existing users have no avatar. Add upload `PUT /api/v1/users/me/avatar` with exactly `expectedRevision` and `file` multipart fields, and delete `DELETE /api/v1/users/me/avatar` with `{expectedRevision}`. Permit initial revision 0. Return `AvatarState`; map invalid/conflict/unavailable to 400/409/503 and enforce 413/415 at request parsing.
- [x] Add checked GET routes: `/api/v1/users/{userId}/avatar/{assetId}` public current visible image; `/api/v1/users/me/avatar/{assetId}` owner current image; `/api/v1/moderation/reports/{reportId}/avatar/{assetId}` only existing authorized report-scoped profile inspection. Use existing read/write budgets, CSRF filter, no-store/nosniff and no raw storage exposure.
- [x] Implement mutation under the existing artwork/moderation lock, rechecking live account/revision after decoding, promoting before commit and advancing both AvatarRevision and ProfileUpdatedAt. Delete previous file after commit with safe retry. Reject stale restore and preserve hidden state during replacement. Ensure stale full-row Identity updates cannot revert avatar revision/profile version.
- [x] Extend nullable avatar projections without per-entry queries. Public URLs are omitted when hidden; settings get the owner URL. Extend existing moderation copy/inspection data to cover the image while preserving role/report rules and no historical snapshots.
- [x] Run focused tests, migration upgrade/preservation on disposable DB and EF pending-model-change check; inspect generated migration for unintended alterations.
- [x] Commit the API/model/projection change.

### Task 3: Cleanup, account lifecycle and folder-aware backup/restore

**Files:** `AvatarCleanupService.cs`, `Authentication/AccountDataExport.cs`, `AccountErasure.cs`, `AccountDataMaintenance.cs` where needed; `deploy/self-host/maintenance.py`, `test_operator.py`; new integration `AvatarLifecycleTests.cs`, `AvatarCleanupTests.cs`.

**Interfaces:** Consumes `AvatarAsset` and folder adapter. Account ZIP adds `avatar.json` mapping and current image at `avatar/<assetId>.webp`; operator artwork archive supports existing flat files plus `avatars/<canonical-leaf>`.

- [x] Add failing tests for live references surviving cleanup, orphan/replaced files older than 24 hours being retried, leased uploads surviving concurrent cleanup, DB outage deleting nothing and symlink safety. Cover account export (including hidden avatar), missing-file export refusal, deletion and restore erasure replay.
- [x] Add operator tests for mixed catalog/avatar archive round trip, nested traversal, symlink/hardlink entries, duplicates, missing referenced avatar and unexpected nested directories. Keep key archives strictly flat. Include pre-avatar flat archive compatibility.
- [x] Run focused integration tests and `python3 -m unittest discover -s deploy/self-host -p 'test_operator.py'`; confirm new cases fail before changes.
- [x] Implement bounded avatar cleanup scanning only the dedicated folder, using the existing coordination protocol and lease/age policy. Extend export mapping/consistent file reads, after-commit deletion, maintenance and erasure replay with no preserved account avatar attribution/files.
- [x] Extend artwork-only backup validation/archive/extraction to explicitly allow `avatars/<canonical-leaf>` regular files; never use unrestricted recursive extraction. Keep existing flat archive support, private ownership, exclusive/no-follow writes and durability checks. Validate both catalog and avatar DB references; update backup format handling only if the actual compatibility contract requires it and document the decision.
- [x] Run lifecycle, cleanup and operator tests. Rehearse a mixed-folder backup/isolated restore followed by current-journal replay on disposable data; verify existing catalog files survive and erased account avatars do not remain accessible.
- [x] Commit lifecycle/operations changes.

### Task 4: Avatar editor, fallback and existing identity surfaces

**Files:** New frontend avatar/API/interface/components and focused tests; existing profile/auth and public identity interfaces/components; moderation profile inspection/report controls.

**Interfaces:** Consumes Task 2 routes and optional DTO avatar fields. API module exports `uploadAvatar(file: File, expectedRevision: number, expectedAccountId: string, signal: AbortSignal): Promise<AvatarState>` and `removeAvatar(expectedRevision: number, expectedAccountId: string, signal: AbortSignal): Promise<AvatarState>`. Shared `Avatar` accepts a nullable checked local URL, size and optional accessible label.

- [x] Read relevant local Next.js guides and applicable frontend skill before code. Add failing tests for no-image/error fallback, validated local-only image URLs, accessible decorative images, preview/object-URL cleanup, input reset and fresh preview after selecting the same file again.
- [x] Add editor tests for upload/replace/remove, invalid/oversized file, explicit save, centered preview, pending state, 409 refetch, 413/415/429/503 errors and uncertain-save blocking until refetch. Test logout/account switching during upload so prior private state cannot leak or update the new account.
- [x] Add projection UI tests for profile/reviews/comments/follows/feeds/notifications/collections; moderation inspection shows authorized current avatar and updated hide/restore copy. Hidden profile report behavior remains consistent with the combined moderation design.
- [x] Run focused Vitest tests to establish failures.
- [x] Implement multipart fetch with existing CSRF/error conventions without JSON Content-Type; reuse existing private write/session guards. Display circular images via non-optimized checked URLs with person-icon fallback and no-referrer. Preview matches center crop; do not upscale output or add crop controls.
- [x] After successful writes, refresh session/avatar state and invalidate all affected identity queries, including feed pages. Preserve display-name/bio edits; do not auto-submit them. Scope drafts/uncertain state to account and release object URLs on change/unmount.
- [x] Run focused tests, then `pnpm --dir web lint` and `pnpm --dir web typecheck`.
- [x] Commit frontend changes.

### Task 5: Final acceptance and documentation

**Files:** `docs/STATUS.md`, `docs/self-hosting.md`, `docs/account-data-operations.md`, `README.md`, new `docs/profile-avatars-verification.md`.

**Interfaces:** Complete feature from Tasks 1–4; no further API or storage changes unless acceptance exposes a defect.

- [x] Run complete unit/integration/provider suites with required services, full frontend Vitest suite, lint/typecheck, production build, API Release publish and operator tests. Record actual commands/results and any limits, without inferring previous test counts.
- [x] Browser-check ordinary upload/replace/remove, public views, moderation hide → owner replacement → stale restore refusal → current restore, hidden direct-URL denial, export and deletion against disposable data. Check keyboard/focus, fallback/errors, Light/Dark and 1440/768/390/320px layouts. Remove temporary services/data after proof.
- [x] Document configured root and `avatars/` layout, folder permissions, shared volume, backup/restore compatibility, cleanup/retention and migration procedure. Record no-store/refetch guarantees and inability to revoke downloaded copies.
- [x] Update STATUS only with verified implementation and actual persistent migration state. Do not apply the migration to the existing persistent database as part of disposable verification.
- [x] Request whole-diff review, resolve supported findings and rerun relevant checks; report evidence and remaining limitations.
- [x] Commit final docs/verification updates and report completion without publishing/deploying artifacts.

## Execution status

All five tasks are implemented and verified. Task commits were combined after
integrated proof; see the execution rulings and
[final verification](../../profile-avatars-verification.md). Persistent migration,
merge, push and publication are separate operations.

## Execution rulings after independent review

- Bind both avatar writes to `X-RateFreak-Account-Id`, checked against the authenticated principal and explicitly forwarded by the browser proxy. This closes the gap before another tab's cookie change is observed.
- Direct profile inspection uses public images when visible, retained report scope when available and recorded direct-hide scope otherwise. Expired inspection scope blocks image restoration until owner removal.
- Preserve the higher avatar revision when independent profile responses update session state; cancel older session reads and refresh after confirmed avatar changes.
- Revalidate the lease path at disposal, refuse linked sibling leases, bound avatar cleanup queries to the file batch, validate produced backup archives, and validate catalog/avatar keys independently.
- Retain existing catalog file layout and shared volume; no persistent migration or file relocation was performed during implementation.
