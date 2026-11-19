# Current project status and roadmap

Updated: 2026-11-19. Custom uploaded profile avatars with a person-icon fallback
are implemented, verified and merged locally into `main` at `019cd10`. Avatar
images/stages/leases use the dedicated `avatars/` folder
inside the configured artwork root. The additive migration
`20261119165037_AddProfileAvatars` is applied to the persistent local database
following user authorization, a validated private backup, isolated upgrade/rollback
rehearsal and unchanged original-data fingerprints for all 28 existing application
tables. Existing accounts use the fallback avatar; uploaded artwork is unchanged. Full backend and
frontend checks, production builds, real HTTPS ordinary/staff Chromium acceptance,
and real mixed-folder backup/isolated restore/Release erasure replay passed;
see [avatar evidence](profile-avatars-verification.md). Disposable acceptance
services/database are stopped after verification. Existing local API/frontend
servers remain stopped.

Home-page Following/Community feeds, explicit per-rating
sharing and notification bell/count are implemented, verified locally and merged
into `main`; see [feed evidence](home-feeds-verification.md). The additive sharing
migration `20261119013555_AddRatingSharingAndFeedIndexes` is applied to the
persistent local database after user authorization, a validated private backup,
isolated restore/rehearsal and preservation checks for all 28 existing application
tables and original rating fields. Existing ratings remain private. Local API and
frontend servers were already stopped and remain stopped.
Application-native source packaging and modified-library
replacement are verified; scroll-bar license provenance is documented through
exact artifact/source equivalence. MIT license selected on 2026-11-11 and added
to the repository. Account lifecycle (`bd3d78e`), staff management (`30c57e7`)
and profile moderation (`a0a7aa0`) are committed and verified locally. Admin
revocation and staff-policy documentation are implemented and verified locally;
see Git history for the current integration commit.
Open Library work-based books, shared metadata caching for every provider and
persistent public provider-artwork caching are implemented and locally verified.
Next.js/eslint-config-next are patched to 16.4.6 for the optimizer security fix.
The Books migration `20261118034056_AddBooks` is applied to the persistent local
database after an authorized private backup and data-preservation verification;
The local `.env` now contains the Open Library contact email and enabled setting;
the API must receive these environment variables at startup. Local live activation
was verified on 2026-11-18: enabled capability, real search and work preview all
returned HTTP 200 using the configured contact. Real Chromium search, ordinary
account import, duplicate import and saved catalog details were also verified
against a disposable local database. A second observed Archive cover redirect
format was fixed with regression tests; all 10 available search covers decoded
afterward. The example settings are in
`.env.example`; no real contact email is stored in Git. See [books/cache evidence](books-and-provider-cache-verification.md).
No production deployment is recorded.
The application is implemented and verified locally; self-hosted release readiness remains open.
The product is intended for open-source distribution so operators can host their
own instances. A project-owned production deployment is not the release objective
([ADR 0020](adr/0020-self-hosted-open-source-distribution.md)).
This page is the current status and backlog, replacing next-step instructions in
older plans and resume notes. It records work and decisions, not deployment authorization.

## Documentation guide

| Document | Purpose |
| --- | --- |
| [Root README](../README.md) | Local setup, configuration and API contracts |
| [Contributing](../CONTRIBUTING.md) | Contributor setup, architecture boundaries, applicable checks and pull-request expectations |
| [Security policy](../SECURITY.md) | Private-reporting instructions/setup status, report scope and self-hosted security responsibilities |
| [Web README](../web/README.md) | Frontend setup, proxy and browser behavior |
| [Frontend checklist](frontend-checklist.md) | Delivery milestones and dated frontend history |
| [Implementation history](implementation-checklist.md) | Dated backend verification and implementation history |
| [Release verification](frontend-release-verification.md) | Verification evidence and the authoritative production release gates |
| [Catalog operations](custom-catalog-operations.md) | Artwork storage, backups and guarded collection migration |
| [Home feeds](home-feeds-verification.md) | Rating sharing, Following/Community, notification bell and verification limits |
| [Notifications](notifications-verification.md) | Inbox behavior, API, retention and local acceptance evidence |
| [Account operations](account-data-operations.md) | Retention, durable erasure journal and restore replay |
| [Local restore verification](account-restore-verification.md) | Disposable backup/restore and Release erasure replay evidence |
| [Self-hosted release plan](self-hosted-release-plan.md) | Portable installation, packaging and open-source release work |
| [Dependency redistribution review](dependency-redistribution-review.md) | Package/image license findings, inventory scope and notice/source follow-up |
| [Provider terms review](provider-terms-review.md) | Provider attribution, service eligibility and unresolved content-rights checks |
| [Credits/compliance evidence](release-compliance-verification.md) | Implemented provider credits, packaged notices, native-source evidence and remaining obligations |
| [Native source package verification](native-source-package-verification.md) | Image-bound source sidecar, offline rebuild/replacement proof and remaining OS/publication scope |
| [Provider agreement evidence](provider-agreement-evidence.md) | Retrieved official agreements, access limits and operator acceptance requirements |
| [Provider clarification drafts](provider-agreement-questions.md) | Unsent provider-specific questions about retention, image optimization, termination and operator eligibility |
| [Open Library/MusicBrainz guidance](provider-public-guidance-2026-11-18.md) | Current registration, cover display, Archive terms scope, core-data and hosted-service findings |
| [Wikidata trial](wikidata-evaluation-2026-11-18.md) | Live movie/TV search and field coverage; sparse posters and classification/label/date handling |
| [Wikidata usage review](wikidata-usage-review-2026-11-18.md) | CC0 structured-data reuse versus hosted API policies and separate image/text licenses |
| [Books/cache verification](books-and-provider-cache-verification.md) | Open Library work imports, all-provider metadata cache, persistent image cache and verification limits |
| [Self-hosting guide](self-hosting.md) | Reference installation, configuration, upgrades and recovery |
| [Reference verification](self-hosted-reference-verification.md) | Packaging, HTTPS, maintenance and recovery evidence/limits |
| [Staff operations](staff-access-operations.md) | Staff permissions, named operator attribution and deployment acceptance |
| [Architecture decisions](adr/) | Accepted decisions; later superseding ADRs take precedence |

Plans under `superpowers/` and the original [backend plan](../ratefreak-plan.md)
are dated design/execution records. Their unchecked tasks, branch names, temporary
ports and statements such as “uncommitted” describe the time they were written.
Use this page and Git for current scope and commit state. Temporary local paths
are evidence references, not durable deployment artifacts or guaranteed running services.

## Implemented

- Custom uploaded profile avatars with a person-icon fallback, independent
  upload/replace/remove controls, public identity display and a separate `avatars/`
  filesystem folder. Writes bind to expected account/revision with CSRF; checked
  URLs respect current visibility, replacement/removal and retained staff inspection
  scope. Profile hiding covers avatar/name/bio together. Account export, deletion,
  bounded cleanup and mixed-folder backup/restore/replay are verified. Merged
  locally into `main` at `019cd10`; the persistent local migration is applied after
  backup/restore rehearsal and data-preservation checks.
  [Evidence](profile-avatars-verification.md), [ADR 0021](adr/0021-profile-avatars.md).

- Signed-in Home Following/Community feeds with public reviews and explicitly
  shared ratings, media filters, bounded cursor pages, spoilers and existing
  review/report links. Ratings are private by default, including existing rows;
  editing a score does not overwrite sharing consent from another tab. Feed reads
  recheck public visibility and local changes clear cached feed pages. Navigation
  now uses a bell with an unread-count badge. The additive sharing migration is
  applied to the persistent local database after validated backup/restore and
  unchanged original-data fingerprints; existing ratings remain private.
  [Feed evidence and limitations](home-feeds-verification.md).

- .NET API, PostgreSQL, migrations, health checks, OpenAPI and Next.js browser proxy.
- Local accounts, first-run administrator setup, saved-code password recovery,
  account settings and Google/GitHub sign-in with explicit account linking.
- Private ZIP account exports, fresh password/linked-provider confirmation, permanent
  account deletion with a retention-confirmation dialog, public catalog ownership
  detachment, bounded event cleanup and independent erasure journal/replay.
- Provider search, preview and import for TMDB movies/TV, MusicBrainz albums and
  IGDB games and Open Library books; shared catalog identities and locally persisted
  metadata. Books use work identities rather than separate ISBN/edition entries.
- Shared bounded API metadata caching for every provider (five-minute searches,
  one-hour details), request coalescing and independent caller cancellation.
  Public provider artwork uses Next.js's persistent, bounded image cache; private
  uploads/moderation artwork retain access checks and no-store. Open Library's
  observed Archive redirects use a narrowly validated CoverID adapter.
- Private in-app notifications for new followers and comments on your reviews, with
  unread badges, periodic refresh, read controls, individual deletion and confirmed
  clear-inbox. Current-access filtering, export and account-erasure integration are
  implemented; [behavior and verification](notifications-verification.md). The
  additive migration was applied to the persistent local database on 2026-11-18
  after a validated private backup/restore and preservation checks.
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
- Permanent Credits page for TMDB, MusicBrainz, IGDB and Open Library, approved TMDB logo/notice,
  footer navigation and provider credits on imported catalog details. Production
  images collect package license/notice texts, project LICENSE, OS/runtime copyright
  evidence and native source/build provenance; this does not close source-distribution
  or provider-agreement obligations.
- Verified Linux amd64 libvips source sidecar with locked Rust sources and an
  offline source rebuild. A deliberately modified aggregate loads under the normal
  web runtime identity and passes raster/SVG processing. Native Rust notices cover
  all 162 observed compiled registry dependencies. The sidecar remains local and
  separate from OS/API/tools source-delivery requirements.
- Contributor guidance and security policy with setup/check commands, data and
  credential handling, contribution licensing and private-reporting instructions.
  The owner enabled GitHub private vulnerability reporting on 2026-11-18;
  the public API confirmed `enabled=true`. Notification delivery was not tested.

Current decisions:

- Local email verification and email-delivered recovery are intentionally excluded.
  Saved codes and already-linked providers handle recovery ([ADR 0014](adr/0014-account-recovery.md)).
- Dynamic collection fields and free-standing collection entries are retired.
  Custom subjects are catalog items that lists reference ([ADR 0016](adr/0016-custom-catalog-and-list-collections.md)).
- Profile moderation reversibly hides avatar, display name and bio together
  ([ADR 0021](adr/0021-profile-avatars.md)), keeps the account usable, uses the existing lifetime report-deduplication rule, and stores no text
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
| Profile avatars, 2026-11-19 | 32 unit, 460 PostgreSQL/API integration and 196 provider contracts passed; zero backend skips. 841 frontend and 36 operator tests passed; lint/typecheck, final production build/API Release publish and model consistency passed. Chromium ordinary/staff upload/replace/remove, visibility, current-version restore, export/deletion, keyboard, both themes and responsive widths passed; final confirmation used local Production HTTPS. Real mixed-folder PostgreSQL/artwork backup, isolated restore and Release erasure replay preserved public catalog bytes and erased the account avatar. Independent review findings addressed with regression proof. Persistent local migration subsequently applied after private backup/restore and original-data preservation checks; [scope and limits](profile-avatars-verification.md) |
| Home feeds/rating sharing, 2026-11-18 | 830 frontend, 27 unit, 456 PostgreSQL/API integration and 196 provider contracts passed; zero backend skips. Integration proof used complementary partitions plus the final budget/OpenAPI case. Lint/typecheck, final production build, API Release publish and model consistency passed. Real Chromium ordinary-account sharing/unsharing, 24-entry paging/Back, both tabs, media filtering, failed-refresh concealment, logout and notification bell/count passed. Both themes at 1440/768/390/320px had no overflow/runtime errors. All independent review findings addressed with regression proof. Query plans with 10,000 additional reviews were inspected; no production-scale load claim. Disposable services/database removed; persistent local database unchanged. [Evidence and limits](home-feeds-verification.md) |
| In-app notifications, 2026-11-18 | 27 unit, 446 PostgreSQL/API integration, 196 provider contracts and 808 frontend tests passed; zero backend skips. Lint/typecheck and production build passed. Two-account real Chromium/API/Next/disposable database acceptance passed, including deletion/clear, later-arrival preservation in PostgreSQL, delayed navigation mark-read and cross-tab concealment. Both themes at 1440/768/390/320px had no overflow/runtime errors. Temporary services/database removed; the persistent database migration was subsequently applied with backup/restore validation and unchanged fingerprints for all 27 original application tables. [evidence and limits](notifications-verification.md) |
| Books and shared provider caches, 2026-11-17 | 27 unit, 431 PostgreSQL/API integration and 196 provider contracts passed; 775 frontend tests, lint/typecheck, patched Next.js 16.4.6 production build/API Release publish, 32 operator and 18 compliance tests passed. The final non-root/read-only web candidate demonstrated MISS/HIT, six-request coalescing, cache retention across recreation and warm/stale outage behavior. Reference Caddy denied direct source paths while optimized covers worked. Independent-review resource-bound findings were reproduced and corrected. Real Open Library work/search/cover shapes and Chromium API-fixture desktop/mobile journeys in both themes were checked; see [scope and limits](books-and-provider-cache-verification.md) |
| Native source packaging, 2026-11-17 | 18 compliance and 30 operator tests passed. Fresh web candidate built with complete collected texts for 162 observed Rust packages. A 406-input, approximately 192 MiB source sidecar was created twice with identical SHA-256 and bound to the final image ID. Full native build passed with network disabled; a source-marked replacement passed exact path/hash, PNG and SVG-to-WebP checks under UID 10001/read-only/no-network. Scroll-bar provenance resolved through the qualified artifact/source chain. See [evidence and limits](native-source-package-verification.md) |
| Credits and notice packaging, 2026-11-13 | 722 frontend tests, lint/typecheck, frontend production build and seven notice-collector tests passed. All three candidate images built; package text/hash checks passed for API 35/web 63/tools 36 packages, with runtime/native evidence. Chromium desktop/mobile in both themes and independent UI review passed. All 33 selected libvips source archive/patch URLs acquired and hashed; corresponding-source/relinking and agreement acceptance remain separate gates. See [scope/limits](release-compliance-verification.md) |
| Dependency/provider review, 2026-11-13 | Source/package metadata and official provider/license sources reviewed; [dependency findings](dependency-redistribution-review.md), [inventory](dependency-inventory.md) and [provider findings](provider-terms-review.md) recorded. Missing TMDB logo/About-Credits attribution, final artifact notice/source compliance and inaccessible full provider agreements remain open. This was not a final image scan, vulnerability audit or legal clearance |
| Self-hosted reference, 2026-11-10 | Complete private Compose/API/web/tools package and optional Caddy verified on Linux amd64. 719 frontend, 603 backend, 30 operator tests passed; real HTTPS/Chromium, DB permissions, migration/recreation, staff operations, coordinated restore/replay and incomplete-restore refusal passed. Controlled long export edge test: cancellation propagated, 504 at 300.01s. See [reference evidence](self-hosted-reference-verification.md) |
| Isolated account restore drill, 2026-11-10 | PostgreSQL 17 database/artwork backup restored into a separate disposable database; fresh Release CLI replay, exact-database refusal, second replay, private erasure/artwork cleanup, public detachment and moderation preservation passed. Focused integration tests: 13 passed, zero skipped. Existing project data unchanged. See [scope and limits](account-restore-verification.md) |
| Frontend tests, 2026-11-09 | 687 passed across 95 files for Admin revocation |
| Backend tests, 2026-11-09 | 600 passed, zero skipped: 27 unit, 419 PostgreSQL integration, 154 provider contracts; includes concurrent last-Admin protection, CLI argument checks, API permissions/CSRF, idempotency, audit attribution and retained registered-user/Moderator access |
| Frontend ESLint / TypeScript, 2026-11-09 | Passed |
| Frontend production build / API Release publish | Passed on 2026-11-09 for Admin revocation; no schema changes. EF model consistency previously passed for profile moderation |
| Local Google/GitHub acceptance | User confirmed the local provider flow on 2026-11-07; deployed HTTPS acceptance remains open |
| Account lifecycle browser verification, 2026-11-09 | Real local Chromium/API/Next/disposable PostgreSQL journey passed: ZIP, desktop/mobile dialog, cancel/focus, deletion, replayed session rejection and public/private content handling; earlier UI checks retain their original evidence limits |
| Original staff management browser verification, 2026-11-09 | Before Admin-removal controls: real Chromium/API/Next/disposable PostgreSQL passed read-only Admin, account search, moderator grant/revoke and audit reasons, cancel/focus, role refresh, moderator/admin denial isolation and layouts. Temporary evidence: `/tmp/ratefreak-staff-browser/results.json`; no persistent roles changed |
| Profile moderation browser verification, 2026-11-09 | Real Chromium/API/Next/disposable PostgreSQL passed report → staff hide/resolve → owner correction staying hidden → staff restore; public profile/review handle fallbacks, audit reasons, confirmation cancellation, logout concealment, and 1440/768/390/320px layouts. No browser runtime errors. Temporary evidence: `/tmp/ratefreak-profile-browser/results.json` and inspection screenshots. Test services/database removed after checks |
| Admin revocation browser verification, 2026-11-09 | Real Chromium/API/Next/disposable PostgreSQL passed UI removal/reason/confirmation, cancel/focus, account audit, retained Moderator/user access, current-session Admin denial, last-Admin 409 and refresh-before-retry, self-removal concealment, offline last-Admin refusal and 1440/768/390/320px layouts. No browser runtime errors. Temporary evidence: `/tmp/ratefreak-admin-revoke-browser/results.json` and screenshots; temporary services/database removed |
| Persistent local database | Avatar migration `20261119165037_AddProfileAvatars` applied on 2026-11-19 after explicit authorization, private mode-0600 database/artwork backups, validated isolated restore and upgrade/rollback/reapply rehearsal. All original columns/counts/fingerprints match for the 28 original application tables; original migration-history rows and artwork also match. Existing users have null avatar IDs/revision zero, the new metadata table is empty and repeat application is idempotent. Evidence: `.local/localhost/profile-avatar-migration/verification.json`. Rating-sharing migration `20261119013555_AddRatingSharingAndFeedIndexes` applied on 2026-11-18 after explicit authorization, a private mode-0600 backup, validated isolated restore and upgrade rehearsal. Original-column fingerprints/counts match for all 28 existing application tables; existing ratings remain private. Both new indexes and the validated state/time constraint exist; repeat application is idempotent. Evidence: `.local/localhost/rating-sharing-migration/verification.json`. Notifications migration `20261118195946_AddNotifications` applied on 2026-11-18 after explicit authorization, a private mode-0600 backup, an isolated restore with matching table fingerprints and preservation checks for all 27 existing application tables. New notification table empty; `.local/localhost/notifications-migration/verification.json`. Books migration `20261118034056_AddBooks` applied on 2026-11-17 (PST) after explicit authorization, a validated private mode-0600 backup, and unchanged fingerprints/counts for all 26 other application tables and original category rows. Local evidence: `.local/localhost/books-migration/verification.json`. Setup migration applied 2026-11-07; authorized collection cutover 2026-11-08; account lifecycle migration 2026-11-09 with backup/preservation checks. Profile migration `20261110014234_AddProfileModeration` applied locally under the approved design on 2026-11-09 (PST), with a private mode-0600 backup and unchanged original row counts/content fingerprints across all 27 existing application tables. Evidence: `.local/localhost/profile-moderation-migration-verification.json` |
| Open Library live activation, 2026-11-18 | API started with local `.env`; readiness, enabled capability, real Fantastic Mr Fox search and `OL45804W` preview returned HTTP 200. Author enrichment and cover reference present; repeated identical preview returned in 4 ms. Local evidence: `.local/localhost/openlibrary-live/verification.json`. Read-only checks; no authenticated import/browser acceptance claimed |
| Open Library live browser acceptance, 2026-11-18 | Real provider/API/Chromium with a disposable migrated database: ordinary reader import 201, repeat import 200/same ID, anonymous rejection 401 with valid CSRF, authenticated missing-CSRF rejection 400, saved metadata/attribution and decoded cover. Exactly one book/work identity and zero personal-library rows. Catalog at 1440/390px in Light/Dark and discovery at both widths had no horizontal overflow or runtime errors. Fixed the second observed Archive cover format; 34 focused tests and all 794 frontend tests passed, plus lint, typecheck and production build. All 10 available search covers decoded after the fix. Sanitized evidence: `.local/localhost/openlibrary-browser/`. Existing database untouched; temporary database/services removed. This is local development acceptance, not a production image/deployment qualification |
| Provider agreement follow-up, 2026-11-18 | Full TMDB API/site terms retrieved and hashed; six-month cache ceiling and termination purge raise a retention/lifecycle scope question for permanent database imports, pending user-led support clarification. No violation is established. Twitch/IGDB storage guidance rechecked; scope clarification still needed. Open Library registration/direct-cover guidance and Archive terms reviewed; MusicBrainz core-field/service distinction reconfirmed. Unsent clarification drafts prepared. No agreements accepted, providers contacted or runtime/data changes made; [current disposition](provider-agreement-evidence.md#current-disposition-2026-11-18) |
| Wikidata feasibility trial, 2026-11-18 | Read-only live searches and batch metadata for six films/six series. All selected works resolved across tested search routes and had names, descriptions, dates and IMDb IDs; general search ranked five first. Direct type filters improved ranking but needed an anime subclass addition. Five records had a general image; only one had poster data. CC0 structured-data imports supported; API and image licenses remain separate. [Evidence and limits](wikidata-evaluation-2026-11-18.md). No adapter, migration, provider-setting or database changes |
| Local runtime | During the authorized avatar migration on 2026-11-19, API localhost:5080 and frontend localhost:3000 were already stopped, with no other database clients. They remain stopped after this schema-only operation. During the authorized rating-sharing migration on 2026-11-18, API localhost:5080 and frontend localhost:3000 were already stopped with no database clients; both remain stopped after this schema-only operation. Earlier runtime evidence: API on localhost:5080 restarted and readiness verified on 2026-11-18 after the authorized notifications migration, retaining `.env` provider configuration. Notification list/count endpoints reject anonymous reads with 401. Frontend on localhost:3000 was verified in earlier work; not started or rechecked during this activation check |
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

- [x] Provider-backed books through Open Library, using shared work identities;
  [design](superpowers/specs/2026-11-17-books-and-artwork-cache-design.md),
  [verification/activation limits](books-and-provider-cache-verification.md).
- [ ] Provider catalog correction, metadata refresh and provenance policy; custom
  item owners already have editing controls.
- [x] In-app notifications for new followers and comments on your reviews, including
  read controls, individual deletion and clear-inbox; [evidence](notifications-verification.md).
- [x] Home-page Following and Community feeds of public reviews and individually
  shared ratings, with private-by-default migration and notification bell/count;
  [verification](home-feeds-verification.md). The persistent schema migration is
  applied; local API/frontend startup remains separate.
- [ ] Optional collection discussions.
- [ ] Account suspensions and appeals.
- [x] Custom uploaded profile avatars with a fallback and dedicated `avatars/`
  folder, implemented, verified and merged locally into `main` (2026-11-19).
  [Design](superpowers/specs/2026-11-19-profile-avatars-design.md),
  [implementation plan](superpowers/plans/2026-11-19-profile-avatars.md) and
  [verification](profile-avatars-verification.md). The persistent local migration is
  applied; other installations require their own reviewed upgrade. Social links and other profile expansion are outside this feature's
  scope.
- [ ] Persistent metadata-provider activation and any additional live import
  acceptance desired for the target environment. Automated import tests and earlier
  provider-specific local evidence already exist; do not treat all live imports as untested.

First-use/onboarding work was explicitly declined on 2026-11-19 and is not a
planned product priority.

## Self-hosted release readiness

The frontend delivery milestone still open is Phase 7 production verification,
applied to a documented self-hosted reference installation. Instance-specific
operational checks remain the responsibility of each operator.
Use the [release-gate table](frontend-release-verification.md#outstanding-release-gates)
as the detailed acceptance checklist. Remaining categories are:

- [x] Portable production API/web/tools images, private reference Compose and
  optional Caddy; [installation/upgrade/recovery guide](self-hosting.md) and local
  [acceptance evidence](self-hosted-reference-verification.md). The original
  `deploy/compose.yaml` remains the development database setup.
- [x] Hosting-independent authenticated ingress/client-address handling, explicit
  authenticated HTTPS bridging for private HTTP, real HTTPS/Origin checks and
  spoofing/budget rejection on the reference installation. Other topologies need
  their own verified proxy configuration.
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
- [x] Open-source license selection: [MIT](../LICENSE), selected 2026-11-11.
- [ ] Remaining redistribution/provider notices, release/versioning automation and
  versioned artifacts. Contributor/security guidance and private-reporting
  activation are complete; maintainer notification delivery remains untested.
  Self-hosting/upgrade instructions exist.

The [reference-installation design](superpowers/specs/2026-11-10-self-hosted-reference-installation-design.md)
is implemented and verified locally. Provider credits, package notices, the scoped
application-native source sidecar and modified-library replacement are verified.
The scroll-bar notice now has documented artifact/source equivalence without a
dependency change. The redistribution/provider release gate remains open.
On 2026-11-18, the user chose to keep TMDB and contact its support team about
whether the caching terms cover permanent database imports. That clarification
is pending; no support message was sent by the agent. The
[Wikidata trial](wikidata-evaluation-2026-11-18.md) remains feasibility evidence,
not a replacement decision. No provider migration, automatic expiry or
fetching-only implementation is authorized by that decision. Do not treat the
scope question as a confirmed violation or the pending clarification as clearance.
Independent release work can continue: final artifact/source-delivery review,
release automation and verification of the supported
installation. [Contributor](../CONTRIBUTING.md) and [security](../SECURITY.md)
guidance are added; GitHub private reporting is enabled and API-verified.
Provider-dependent release work remains: resolve refresh/removal requirements and the remaining
[provider agreement questions](provider-agreement-questions.md) (including IGDB
scope and Open Library cover optimization), and record each operator's eligibility.
TMDB contract retrieval is complete; provider-enabled release clearance is not.
Complete the base-OS/remaining image source-delivery and final artifact audit,
then deliver the matching source sidecar alongside any authorized binary publication.
The Books/cache candidate changes source and Next.js package versions; final
release artifacts and image-bound source/evidence must match that candidate.
See the [native source evidence](native-source-package-verification.md),
[credits/compliance evidence](release-compliance-verification.md),
[dependency review](dependency-redistribution-review.md) and [provider review](provider-terms-review.md).
Maintainer notification verification and versioned release automation remain under the
[release plan](self-hosted-release-plan.md).
Each operator still verifies their own journal protection, backup expiration,
staff records and infrastructure acceptance. No public deployment or publication
is recorded.
