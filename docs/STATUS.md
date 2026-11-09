# Current project status and roadmap

Updated: 2026-11-09. Source baseline: local `main`, commit `91a5cce`.
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
| [Operations runbook](custom-catalog-operations.md) | Artwork storage, backups and guarded collection migration |
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
| Frontend tests, 2026-11-09 | 644 passed across 87 files |
| Backend tests, 2026-11-09 | 554 passed, zero skipped: 27 unit, 373 PostgreSQL integration, 154 provider contracts |
| Frontend ESLint / TypeScript, 2026-11-09 | Passed |
| Frontend production build, API Release publish and EF model consistency | Passed on 2026-11-08; not rerun after the 2026-11-09 UI/session changes |
| Local Google/GitHub acceptance | User confirmed the local provider flow on 2026-11-07; deployed HTTPS acceptance remains open |
| Custom catalog/list browser verification | Recorded on 2026-11-08; no fresh browser verification for the 2026-11-09 changes because Chromium lacked `libnspr4.so` |
| Persistent local database | Setup migration applied 2026-11-07; authorized test-data collection cutover and four migrations completed 2026-11-08 |
| Git integration | Catalog/list rewrite and 2026-11-09 UI/session changes committed together as `91a5cce` |

Lower test totals than older snapshots reflect the removal of retired collection
field/entry behavior. Counts describe the executed suites; they do not establish
unchanged coverage. See [release evidence](frontend-release-verification.md) for
the scope and limitations of earlier browser/build checks.

## Remaining product and policy work

These items are pending; their order below is a suggested sequence, not an approved
implementation specification.

- [ ] Define account deletion, export and retention behavior, including authored
  content, shared catalog references, artwork, recovery events and moderation audit
  history; implement the resulting user/operator flows.
- [ ] Add profile reporting/moderation, recorded as pre-launch work in the backend
  history. Decide the handling of reported profile text and staff access.
- [ ] Finalize staff-access, operator-attribution and audit-retention policies.
- [ ] Decide whether an administrator UI for moderator-role management is needed;
  backend grant/revoke and offline administrator appointment already exist.

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
- [ ] Coordinated database/artwork backups and a successful isolated restore drill.
- [ ] Multi-instance/perimeter abuse budgets, monitoring, sanitized logs, secret
  rotation, representative load tests and disconnect propagation.
- [ ] Provider licensing, metadata/artwork rights and required notices/attribution.
- [ ] External-client compatibility and reviewed migrations for the target database;
  the completed local test-data cutover does not authorize another reset.
- [ ] End-to-end acceptance using ordinary/staff accounts on the deployed candidate.
- [ ] Fresh production/Release builds and browser checks for the final source revision.

Suggested next work: settle the account data lifecycle, implement the resulting
deletion/export flows, address profile moderation, then verify a staging deployment
against the release gates. No production deployment is recorded by this consolidation.
