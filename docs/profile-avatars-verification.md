# Profile avatar verification

Date: 2026-11-19. Implementation branch: `feat/profile-avatars`.

## Behavior

Account → Profile supports selecting, previewing, uploading, replacing and removing
an avatar independently of text edits. Static JPEG/PNG/WebP inputs are bounded to
5 MiB, 8192 pixels per axis and 20 million pixels; orientation is corrected before
center cropping to at most 512-square WebP. No original or metadata is retained.
The neutral person icon covers absent, unsupported and failed images. Public
profiles, reviews/comments, follows, feeds, notifications and collection owners
use the same avatar component. No social links or onboarding are added.

Files, upload stages and leases use `<Artwork:RootPath>/avatars/`. Existing catalog
images keep their current location. Database changes are additive in
`20261119165037_AddProfileAvatars`; no persistent project database migration was
performed. Existing accounts start with null avatars and revision zero.

Both writes check the editor's expected account UUID (`X-RateFreak-Account-Id`),
CSRF and avatar revision. The proxy explicitly forwards that account binding and
bounded multipart uploads. Old URLs cease serving after replacement/removal.
Public holds conceal avatar/name/bio together. Staff image inspection is tied to
retained profile reports or recorded direct-hide scope, with a distinct unavailable
state if records have expired. Hidden replacements advance the inspected profile
version. Independent text/session responses preserve newer avatar revisions.

## Automated checks

| Check | Result |
| --- | --- |
| .NET unit suite | 32 passed, zero skipped |
| PostgreSQL/API integration suite | 460 passed, zero skipped |
| Provider contracts | 196 passed, zero skipped |
| Frontend Vitest | 841 passed across 109 files |
| ESLint | Passed |
| TypeScript | Passed |
| Final frontend production build | Passed |
| Final API Release publish | Passed |
| EF model consistency | No pending model changes |
| Operator tests | 36 passed |
| UI mechanical detector | No findings on new avatar components/styles |

Commands used from the repository root:

```sh
dotnet test RateFreak.sln --no-restore -m:1 --settings /tmp/ratefreak-avatar.runsettings
pnpm --dir web test
pnpm --dir web lint
pnpm --dir web typecheck
pnpm --dir web build
dotnet publish src/RateFreak.Api -c Release --no-restore -m:1 -o /tmp/ratefreak-avatar-release
dotnet ef migrations has-pending-model-changes --project src/RateFreak.Infrastructure --startup-project src/RateFreak.Api
python3 -m unittest discover -s deploy/self-host -p 'test_*.py'
```

The temporary runsettings bound xUnit to two parallel threads. Tests use disposable
PostgreSQL containers, not the project database. Earlier fixed failures were
historical-schema test setup/current-model mismatches and public DTO field
allowlists; the test assertions now include avatar URLs without private fields.
The downgrade regression demonstrates refusal while avatar metadata exists.

## Independent review and regression evidence

One independent reviewer inspected the full tracked/untracked change. The final
fix pass covered:

- Wrong expected account ID: uploads and removal returned success before the fix,
  then 409 after binding the request to its authenticated principal.
- Direct profile inspection: a missing image reference reproduced before the fix;
  current visible/report/direct-hide inspection paths now support checked restore.
  Expired scope denies hidden reads/restoration until owner image removal.
- A live staged directory replaced with a symlink redirected lease deletion before
  the fix; disposal now revalidates the path. Cleanup refuses linked sibling leases
  and queries references only for the current batch of at most 1,000 files.
- Backup accepted an un-restorable reserved entry and catalog keys in the avatar
  namespace before the fix. Generated archives are now validated, hardlinked inputs
  become regular archive files, and references use separate namespace checks.
- A delayed text PATCH response reverted a newer avatar before the fix. Session
  updates now preserve higher avatar revisions and cancel/reconcile stale reads.
- Definitive upload rejection locked file selection before the fix. Validation,
  media/size and rate-limit rejections now permit a corrected selection; ambiguous
  outcomes still require a current-account check.

Tests also cover simultaneous same-revision uploads, actual current hidden version
before replacement, metadata-free output, distinguishable center-crop/orientation
pixels, public/owner/staff/old-URL access, cleanup, hidden
export, account deletion and journal replay. An editor transition test aborts and
conceals a pending upload when identity changes.

## Browser, filesystem and restore acceptance

Real Chromium ran with ordinary and staff accounts, the actual API/frontend and a
disposable PostgreSQL database. Initial development acceptance covered both themes
at 1440, 768, 390 and 320 pixels, with decoded images and no overflow/runtime errors.
The final confirmation used the built frontend and Release API in Production mode
behind a local HTTPS ingress with authenticated trusted-proxy/private-HTTP bridging.
Both themes were confirmed at 1440 and 320 pixels; keyboard focus, the expected
account header and secure cookies were exercised. The certificate was local and
trusted only by the acceptance browser.

The final journey covered upload, replacement, removal, public versus owner access,
staff report/direct inspection, hide, owner replacement remaining hidden, stale
restore refusal, current restore, account export and deletion. Export ZIP mapping
and image presence were checked without exposing filesystem keys. There were no
browser runtime errors. Development full-reload interruptions were replaced by this
production-mode confirmation, not counted as product acceptance.

A real custom-format `pg_dump` and folder-aware artwork archive captured a public
catalog image and an account avatar. The dump/files were restored into the separate
`avatar_restore` database and a separate artwork root. The fresh Release CLI replayed
the current independent erasure journal with exact database-name confirmation.
SQL verified the erased account and its avatar metadata were absent; the restored
avatar file was gone and the public catalog item remained ownerless with identical
image bytes. Operator archive tests additionally verify traversal/link/duplicate
refusals and flat archive compatibility. Restore used the test host identity;
production container UID/filesystem/infrastructure acceptance remains separate.

Temporary private evidence was under `/tmp/ratefreak-avatar-acceptance/` and the
`/tmp/ratefreak-avatar-*.log` files. Those paths are not durable release artifacts.
The application does not claim to revoke images already downloaded. Operators must
protect the storage root from hostile filesystem writers; these tests do not claim
arbitrary concurrent OS-level path replacement is race-proof.

## Implementation rulings

- Work stayed on a dedicated feature branch in the shared checkout rather than a
  second worktree, preserving the user's workspace/dependencies. Changes are visible
  in that checkout; no merge/push is implied.
- A scalar current avatar ID complements account-owned asset metadata for bounded
  identity projections. Mutations update both transactionally; divergence would
  make images unavailable and therefore remains covered by ownership/read checks.
- Task commits were deferred until integrated verification to keep evolving
  interfaces together. This leaves a larger final review/commit rather than
  independently committed intermediate tasks.
- Expected account binding and retained direct-hide inspection were added after
  review to preserve account ownership and staff scope. Expired inspection scope
  requires owner removal before image restoration instead of granting unrestricted
  hidden-image access.

No minor review findings remain deferred. Deployment/publication, production
provider callbacks and migration of the persistent local database are outside
this verification.
