# Current project status and roadmap

Updated: 2026-11-09. Git baseline: `bd3d78e` (account lifecycle committed on local `main`).
Staff management is implemented and verified on `feat/staff-management`.
It has not been deployed.
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
- Reporting and staff moderation for reviews, comments, custom items and categories,
  with visibility controls and audit history. Profile reporting is still pending.
- Staff management inside moderation: admin-only account search, staff/all-account
  filters, current role indicators, confirmed moderator grant/revoke with audit
  reasons, and per-account audit links. Admin roles remain operator-managed.
- Catalog category-only browsing controls, separate existing/new category workflows,
  spacing improvements and silent routine background session checks.

Current decisions:

- Local email verification and email-delivered recovery are intentionally excluded.
  Saved codes and already-linked providers handle recovery ([ADR 0014](adr/0014-account-recovery.md)).
- Dynamic collection fields and free-standing collection entries are retired.
  Custom subjects are catalog items that lists reference ([ADR 0016](adr/0016-custom-catalog-and-list-collections.md)).
- Routine session refetches retain the last verified identity and UI. Initial
  discovery, explicit account transitions and session-check errors still gate
  private content; confirmed logout/account changes clear old private state.
  The API authorizes protected requests independently.

## Latest local evidence

| Check | Latest recorded result |
| --- | --- |
| Frontend tests, 2026-11-09 | 675 passed across 92 files for staff management |
| Backend tests, 2026-11-09 | 585 passed, zero skipped: 27 unit, 404 PostgreSQL integration, 154 provider contracts; executed suites separately after restoring existing dependency paths from the prior platform |
| Frontend ESLint / TypeScript, 2026-11-09 | Passed |
| Frontend production build / API Release publish | Passed on 2026-11-09 for staff management; EF model consistency previously passed for account lifecycle, with no schema changes in this slice |
| Local Google/GitHub acceptance | User confirmed the local provider flow on 2026-11-07; deployed HTTPS acceptance remains open |
| Account lifecycle browser verification, 2026-11-09 | Real local Chromium/API/Next/disposable PostgreSQL journey passed: ZIP, desktop/mobile dialog, cancel/focus, deletion, replayed session rejection and public/private content handling; earlier UI checks retain their original evidence limits |
| Staff management browser verification, 2026-11-09 | Real Chromium/API/Next/disposable PostgreSQL passed: read-only Admin, searched account directory, grant/revoke and audit reasons, cancel/focus, current-role refresh, moderator/admin denial isolation, revoked-session rejection, and layouts at 1440/768/390/320px. No browser runtime errors. Temporary evidence: `/tmp/ratefreak-staff-browser/results.json` and screenshots; no persistent roles changed by these checks |
| Persistent local database | Setup migration applied 2026-11-07; authorized test-data collection cutover completed 2026-11-08; account lifecycle migration applied 2026-11-09 after explicit user authorization, with a private backup and unchanged data verified across 25 existing tables |
| Git integration | Catalog/list rewrite and 2026-11-09 UI/session changes committed together as `91a5cce` |

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
- [ ] Add profile reporting/moderation, recorded as pre-launch work in the backend
  history. Decide the handling of reported profile text and staff access.
- [ ] Finalize staff-access and operator-attribution policies. Account erasure and
  bounded moderation retention are defined in ADR 0017; deployed acceptance remains open.
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

Suggested next work: address profile moderation and staff-access/operator policies,
then verify account lifecycle setup, backup expiration and a staging deployment
against the release gates. No production deployment is recorded by this consolidation.
