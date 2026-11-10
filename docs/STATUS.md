# Current project status and roadmap

Updated: 2026-11-09. Account lifecycle (`bd3d78e`), staff management (`30c57e7`)
and profile moderation (`a0a7aa0`) are committed and verified locally. Admin
revocation and staff-policy documentation are implemented and verified locally;
see Git history for the current integration commit.
No production deployment is recorded.
The application is implemented and verified locally; production readiness remains open.
This page is the current status and backlog, replacing next-step instructions in
older plans and resume notes. It records work and decisions, not deployment authorization.

## Documentation guide

| Document | Purpose |
| --- | --- |
| [Root README](../README.md) | Local setup, configuration and API contracts |
| [Web README](../web/README.md) | Frontend setup, proxy and browser behavior |
| [Frontend checklist](frontend-checklist.md) | Delivery milestones and dated frontend history |
| [Implementation history](implementation-checklist.md) | Dated backend verification and implementation history |
| [Release verification](frontend-release-verification.md) | Verification evidence and the authoritative production release gates |
| [Catalog operations](custom-catalog-operations.md) | Artwork storage, backups and guarded collection migration |
| [Account operations](account-data-operations.md) | Retention, durable erasure journal and restore replay |
| [Staff operations](staff-access-operations.md) | Staff permissions, named operator attribution and deployment acceptance |
| [Architecture decisions](adr/) | Accepted decisions; later superseding ADRs take precedence |

Plans under `superpowers/` and the original [backend plan](../ratefreak-plan.md)
are dated design/execution records. Their unchecked tasks, branch names, temporary
ports and statements such as “uncommitted” describe the time they were written.
Use this page and Git for current scope and commit state. Temporary local paths
are evidence references, not durable deployment artifacts or guaranteed running services.

## Implemented

- .NET API, PostgreSQL, migrations, health checks, OpenAPI and Next.js browser proxy.
- Local accounts, first-run administrator setup, saved-code password recovery,
  account settings and Google/GitHub sign-in with explicit account linking.
- Private ZIP account exports, fresh password/linked-provider confirmation, permanent
  account deletion with a retention-confirmation dialog, public catalog ownership
  detachment, bounded event cleanup and independent erasure journal/replay.
- Provider search, preview and import for TMDB movies/TV, MusicBrainz albums and
  IGDB games; shared catalog identities and metadata.
- Public catalog browsing/details, private libraries, tracking, half-star ratings,
  reviews, review comments, public profiles, profile editing and follows.
- Custom catalog items with private/public visibility, categories, one protected
  filesystem image, owner management and reversible archival.
- Collections as lists of existing catalog items, with ordering, visibility,
  revision checks and explicit removal of unavailable memberships.
- Reporting and staff moderation for reviews, comments, custom items, categories
  and profile text, with visibility controls and audit history. Profile hiding
  replaces public display names with handles and omits bios across public identity
  surfaces; owner corrections stay hidden until staff explicitly restore them.
- Staff management inside moderation: admin-only account search, staff/all-account
  filters, current role indicators, confirmed moderator grant/revoke with audit
  reasons, and per-account audit links. Admin removal is supported in the UI and
  `--revoke-admin`, with last-Admin protection, preserved Moderator/user access,
  and actor-attributed audit history. Granting Admin remains operator-managed.
- Catalog category-only browsing controls, separate existing/new category workflows,
  spacing improvements and silent routine background session checks.

Current decisions:

- Local email verification and email-delivered recovery are intentionally excluded.
  Saved codes and already-linked providers handle recovery ([ADR 0014](adr/0014-account-recovery.md)).
- Dynamic collection fields and free-standing collection entries are retired.
  Custom subjects are catalog items that lists reference ([ADR 0016](adr/0016-custom-catalog-and-list-collections.md)).
- Profile moderation reversibly hides both display name and bio, keeps the account
  usable, uses the existing lifetime report-deduplication rule, and stores no text
  snapshots ([ADR 0018](adr/0018-profile-text-moderation.md)).
- Staff permissions follow current database roles and existing report-scoped
  inspection. Operator identity requires protected named host/deployment records;
  the application's `operator` actor alone does not identify a person. See
  [ADR 0019](adr/0019-staff-access-and-operator-attribution.md). Deployed attribution,
  external-record retention and Admin removal/recovery acceptance remain open.
- Routine session refetches retain the last verified identity and UI. Initial
  discovery, explicit account transitions and session-check errors still gate
  private content; confirmed logout/account changes clear old private state.
  The API authorizes protected requests independently.

## Latest local evidence

| Check | Latest recorded result |
| --- | --- |
| Frontend tests, 2026-11-09 | 687 passed across 95 files for Admin revocation |
| Backend tests, 2026-11-09 | 600 passed, zero skipped: 27 unit, 419 PostgreSQL integration, 154 provider contracts; includes concurrent last-Admin protection, CLI argument checks, API permissions/CSRF, idempotency, audit attribution and retained registered-user/Moderator access |
| Frontend ESLint / TypeScript, 2026-11-09 | Passed |
| Frontend production build / API Release publish | Passed on 2026-11-09 for Admin revocation; no schema changes. EF model consistency previously passed for profile moderation |
| Local Google/GitHub acceptance | User confirmed the local provider flow on 2026-11-07; deployed HTTPS acceptance remains open |
| Account lifecycle browser verification, 2026-11-09 | Real local Chromium/API/Next/disposable PostgreSQL journey passed: ZIP, desktop/mobile dialog, cancel/focus, deletion, replayed session rejection and public/private content handling; earlier UI checks retain their original evidence limits |
| Original staff management browser verification, 2026-11-09 | Before Admin-removal controls: real Chromium/API/Next/disposable PostgreSQL passed read-only Admin, account search, moderator grant/revoke and audit reasons, cancel/focus, role refresh, moderator/admin denial isolation and layouts. Temporary evidence: `/tmp/ratefreak-staff-browser/results.json`; no persistent roles changed |
| Profile moderation browser verification, 2026-11-09 | Real Chromium/API/Next/disposable PostgreSQL passed report → staff hide/resolve → owner correction staying hidden → staff restore; public profile/review handle fallbacks, audit reasons, confirmation cancellation, logout concealment, and 1440/768/390/320px layouts. No browser runtime errors. Temporary evidence: `/tmp/ratefreak-profile-browser/results.json` and inspection screenshots. Test services/database removed after checks |
| Admin revocation browser verification, 2026-11-09 | Real Chromium/API/Next/disposable PostgreSQL passed UI removal/reason/confirmation, cancel/focus, account audit, retained Moderator/user access, current-session Admin denial, last-Admin 409 and refresh-before-retry, self-removal concealment, offline last-Admin refusal and 1440/768/390/320px layouts. No browser runtime errors. Temporary evidence: `/tmp/ratefreak-admin-revoke-browser/results.json` and screenshots; temporary services/database removed |
| Persistent local database | Setup migration applied 2026-11-07; authorized collection cutover 2026-11-08; account lifecycle migration 2026-11-09 with backup/preservation checks. Profile migration `20261110014234_AddProfileModeration` applied locally under the approved design on 2026-11-09 (PST), with a private mode-0600 backup and unchanged original row counts/content fingerprints across all 27 existing application tables. Evidence: `.local/localhost/profile-moderation-migration-verification.json` |
| Local runtime | Updated API on localhost:5080 and frontend on localhost:3000 verified healthy; Staff management page and anonymous rejection at the new Admin-removal route checked. No migration or persistent role changes for this feature |
| Git integration | Catalog/list rewrite `91a5cce`, account lifecycle `bd3d78e`, staff management `30c57e7`, profile moderation `a0a7aa0`; Admin revocation/policy integration follows in Git history |

Lower test totals than older snapshots reflect the removal of retired collection
field/entry behavior. Counts describe the executed suites; they do not establish
unchanged coverage. See [release evidence](frontend-release-verification.md) for
the scope and limitations of earlier browser/build checks.

## Remaining product and policy work

These items are pending; their order below is a suggested sequence, not an approved
implementation specification.

- [x] Define account deletion/export policy: preserve public catalog entries without
  account attribution, erase personal/private content, recovery events 90 days while
  active, resolved moderation records 365 days, backup copies 30 days ([ADR 0017](adr/0017-account-data-lifecycle.md)).
- [x] Implement and verify account export/deletion, final retention confirmation
  dialog, independent erasure journal/replay and cleanup ([design](superpowers/specs/2026-11-09-account-data-lifecycle-design.md)).
  Migration `20261109171007_AddAccountDataLifecycle` is tested on disposable databases
  and applied to the project’s persistent local database on 2026-11-09 after user
  authorization. All row counts and content fingerprints across 25 existing tables
  were unchanged. The erasure journal still requires provisioning/configuration;
  other installations require their own reviewed migration/setup.
- [x] Add profile reporting/moderation with approved handling of display name/bio,
  current staff access, owner correction/restoration, erasure integration and
  reviewed additive migration ([design](superpowers/specs/2026-11-09-profile-moderation-design.md)).
- [x] Define staff-access and operator-attribution policies in
  [ADR 0019](adr/0019-staff-access-and-operator-attribution.md) and the
  [staff operations runbook](staff-access-operations.md), using named host/deployment
  records without a new application identity field. Operational acceptance remains
  open: named attribution, external record protection/365-day expiration, and a
  deployed Admin removal/recovery acceptance. Admin removal/recovery is now locally
  tested through Staff management and offline commands; deployment verification
  and named external records remain required.
- [x] Add Admin revocation in Staff management and the offline operator command,
  protecting the last Admin and retaining Moderator/registered-user access
  ([implementation record](superpowers/plans/2026-11-09-admin-revocation.md)).
- [x] Add administrator UI for moderator-role management within existing moderation
  ([design](superpowers/specs/2026-11-09-staff-management-design.md)). Account lookup,
  current roles, reasoned grant/revoke and audit links are verified locally;
  offline administrator appointment remains the way to grant Admin.

Further features remain separate scope decisions:

- [ ] Provider-backed books.
- [ ] Provider catalog correction, metadata refresh and provenance policy; custom
  item owners already have editing controls.
- [ ] Optional activity feeds, collection discussions and notifications.
- [ ] Account suspensions and appeals.
- [ ] Optional avatars/social links and other profile expansion.
- [ ] Persistent metadata-provider activation and any additional live import
  acceptance desired for the target environment. Automated import tests and earlier
  provider-specific local evidence already exist; do not treat all live imports as untested.

## Production readiness

The frontend delivery milestone still open is Phase 7 production verification.
Use the [release-gate table](frontend-release-verification.md#outstanding-release-gates)
as the detailed acceptance checklist. Remaining categories are:

- [ ] Target hosting and deployed HTTPS/Origin/proxy configuration; the accepted
  direction is Vercel for Next.js and a separately hosted API.
- [ ] Real production cookies, CSRF, logout/recovery and Google/GitHub callbacks.
- [ ] Durable protected Data Protection keys and persistent PostgreSQL/artwork storage.
- [ ] Coordinated database/artwork backups expiring within 30 days, a protected independent
  erasure journal, and a successful isolated restore drill that replays erasures.
- [ ] Multi-instance/perimeter abuse budgets, monitoring, sanitized logs, secret
  rotation, representative load tests and disconnect propagation.
- [ ] Provider licensing, metadata/artwork rights and required notices/attribution.
- [ ] External-client compatibility and reviewed migrations for the target database;
  the completed local test-data cutover does not authorize another reset.
- [ ] End-to-end acceptance using ordinary/staff accounts on the deployed candidate.
- [ ] Fresh production/Release builds and browser checks for the final source revision.

Suggested next work: verify account lifecycle setup, backup expiration and restore
replay, then prepare staging with staff-operations acceptance evidence
against the release gates. No production deployment is recorded by this consolidation.
