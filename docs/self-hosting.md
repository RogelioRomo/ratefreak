# Self-hosting RateFreak

This reference package runs Next.js, the .NET API and PostgreSQL on one Linux
amd64 host using Docker Engine and the Docker Compose plugin (`docker compose`). Python 3 runs the operator commands;
Node/.NET SDKs are built inside images. It supports one replica of each service
and one HTTPS hostname at the site root. The acceptance host used Docker 29.9.2,
Compose 5.7.0 and Python 3.13.5; older Compose v2 releases have not been verified.
ARM, Windows-native containers, subpaths,
CDN chains and multiple replicas are not yet verified release configurations.

Use a checkout matching the application version. No Vercel account or shared
RateFreak credentials are required. OAuth and metadata providers default off;
local accounts and custom catalog entries work independently. This package does
not publish container images. Project code is MIT licensed; dependencies and
provider content retain separate terms. Read the [provider agreement checklist](provider-agreement-evidence.md)
before enabling providers and the [release notice/source evidence](release-compliance-verification.md)
before redistributing images. Application credits are available at `/credits`.
The 2026-11-24 follow-up records TMDB staff guidance permitting database metadata
refresh and retention of TMDB IDs after termination. The owner approved automatic
metadata/poster refresh beginning at five months and removal of expired provider
content at six months while keeping independent user records. This lifecycle is
implemented and [locally verified](tmdb-lifecycle-verification.md), including
current-policy restore protection. The
[November 26 handoff](provider-implementation-handoff.md) selects the same lifecycle
for IGDB and direct Open Library cover URLs; this checkout implements those
choices. The interval is our policy, and research limits remain recorded in the
[November 26 reviews](provider-agreement-evidence.md#current-disposition-2026-11-26). Short request-cache TTLs do not resolve
database retention. Follow the [current disposition](provider-agreement-evidence.md#current-disposition-2026-11-24)
before claiming an enabled instance meets provider terms; no runtime settings
were changed by the review.
The verified [application-native source sidecar](native-source-package-verification.md)
must accompany its matching image when a binary release is authorized. It does
not replace base-OS or other applicable source-delivery obligations.

The reference database disables PostgreSQL JIT compilation while an LLVM
security finding lacks a complete caller exclusion. SQL remains supported;
reassess the security and capacity evidence before enabling JIT.

The reference package limits glibc malloc to one arena in application, database
and maintenance services to exclude aligned secondary thread arenas
(CVE-2019-1010025). Removing or overriding `glibc.malloc.arena_max=1` requires
security reassessment. This may increase allocation contention; representative
capacity qualification remains outstanding. It does not resolve the separate
thread-stack guard/address-reuse findings. See the
[current security closure](security-closure-2026-11-26.md).

Server image optimization supports JPEG, PNG, WebP and GIF. HEIF/AVIF decoding
is disabled while the bundled libheif has unresolved security advisories;
provider images requiring that decoder cannot be optimized. This restriction
does not patch the bundled library or change the direct TMDB poster byte cache.
Server-side HTTP responses compressed with Zstandard are also rejected while
Node’s embedded decoder is affected by CVE-2026-124851. Gzip, deflate and
Brotli responses remain supported. Providers requiring Zstandard responses
cannot be used until a patched runtime is qualified.
See the [security follow-up](security-followup-2026-11-25.md).

## Install

Choose a private instance directory outside the repository. Keep the source
checkout for updates; keep instance files and backup copies out of Git.

```bash
export RATEFREAK_INSTANCE="$HOME/.local/share/ratefreak"
python3 deploy/self-host/ratefreak.py --instance-dir "$RATEFREAK_INSTANCE" init --origin https://ratings.example.com
python3 deploy/self-host/ratefreak.py --instance-dir "$RATEFREAK_INSTANCE" build
python3 deploy/self-host/ratefreak.py --instance-dir "$RATEFREAK_INSTANCE" prepare
python3 deploy/self-host/ratefreak.py --instance-dir "$RATEFREAK_INSTANCE" migrate
python3 deploy/self-host/ratefreak.py --instance-dir "$RATEFREAK_INSTANCE" start
```

Point the hostname's DNS at this host and make ports 80/443 reachable for bundled
Caddy. Its certificate state persists across recreation. For a LAN/test instance,
initialize with `--local-tls` and explicitly trust the Caddy local CA on clients;
do not disable TLS checks or production Secure cookies. Caddy's
[HTTPS documentation](https://caddyserver.com/docs/automatic-https) explains public
DNS/port and local certificate requirements. Custom public ports should use your
own proxy configuration.

`init` creates operator-owned mode-0700 configuration directories and mode-0600
secret files. It preserves existing values and refuses implicit origin changes,
symlinks or unsafe permissions. `prepare` is a deliberate privileged one-shot
container that provisions service-owned private directories and runtime copies
of secrets. API/web/tools subsequently run as UID/GID 10001; the PostgreSQL
reference image uses UID/GID 999. Repeated preparation preserves existing journal
records and credentials. Fix conflicting ownership deliberately after identifying
its cause; the tool will not recursively take ownership of unknown data.

The fresh-database bootstrap suppresses statement/error-duration logging in its
password-provisioning connection so failing SQL cannot expose generated credentials
in ordinary diagnostics. It does not disable normal application monitoring.

`migrate` is explicit and uses the release-built EF bundle and migration identity.
`start` requires its successful acceptance record and API schema readiness.
Application restart does not migrate, reset the database, replay erasures or
regenerate credentials. `deploy/compose.yaml` remains the separate development
PostgreSQL configuration; it is not this reference package.

A fresh installation contains no user accounts, catalog items, ratings, reviews
or uploaded artwork. Migrations create the five built-in media category
definitions and pending setup state; test acceptance fixtures are not populated
by installation/startup. See the [fresh-install verification](release-security-review-2026-11-23.md#fresh-install-data-verification).

Visit `/setup` over HTTPS. Read `secrets/setup` privately on the operator host and
enter it in first-run setup to create the initial Admin. Save the recovery code
privately. Setup never reopens on upgrade or staff removal; ordinary registration
remains unprivileged. After setup completes, stop writers and add `RATEFREAK_SETUP_SECRET_DISABLED: 'true'`
to the API environment in your private instance override; then run explicit
`migrate` and `start` to accept that configuration. The reference entrypoint will
remove the setup secret from the API process environment. Existing completed setup
also rejects it permanently. Keep secrets out of screenshots, support tickets and logs.

## Storage and account deletion

| Instance path | Purpose |
| --- | --- |
| `instance.json` | Validated public origin/local TLS choice |
| `secrets/` | Operator-owned source credentials; never image build input |
| `runtime/` | Per-service private secret copies prepared for container identities |
| `storage/artwork/` | API-only catalog artwork; never a static web directory |
| `storage/artwork/avatars/` | Account-owned avatar images and their staging/lease files, separate from catalog content |
| `storage/keys/` | Durable ASP.NET Data Protection keys |
| `storage/image-cache/` | Legacy disposable Next.js optimizer cache; cleared once during provider lifecycle preparation |
| `storage/tmdb-posters/` | Disposable managed TMDB poster cache with independent fetch timestamps; excluded from backups |
| `independent/tmdb/tmdb-policy.json` | Current protected TMDB termination/identity/poster blocks, independently replicated and excluded from database backups |
| `storage/igdb-covers/` | Disposable managed IGDB cover cache with independent fetch timestamps; excluded from backups |
| `independent/igdb/igdb-policy.json` | Current protected IGDB termination/game/cover blocks, independently replicated and excluded from database backups |
| `independent/account-erasures.jsonl` | Private durable erasure journal, excluded from ordinary backups |
| `prepared.json`, `migrated.json`, `deployment.json` | Lifecycle acceptance/configuration records; not substitutes for database health |

PostgreSQL and Caddy have project-scoped named volumes. Instance directory paths
produce distinct Compose project names. Do not move an instance casually:
its path determines that name and therefore which volumes are selected.
Record project/volume identity before an intentional move. Do not run `down -v`.

Protect the host/storage at rest, restrict host/Docker administrator access and
independently replicate the erasure journal with append preservation and failure
monitoring. A different directory or volume on the same disk does not establish
independent recovery. Database/artwork backups must expire within 30 days on all
stores, including snapshots, object versions, replicas and exported archives.
Keep journal history beyond all restorable backups plus an operational margin.

Deletion is unavailable by default even though the journal is provisioned. Once
independent protection/replication is actually configured and verified, stop the
app and record the operator acknowledgment:

```bash
python3 deploy/self-host/ratefreak.py --instance-dir "$RATEFREAK_INSTANCE" stop
python3 deploy/self-host/ratefreak.py --instance-dir "$RATEFREAK_INSTANCE" enable-deletion --confirm-independent-journal
python3 deploy/self-host/ratefreak.py --instance-dir "$RATEFREAK_INSTANCE" start
```

This flag records your configuration acknowledgment; it does not provision or
monitor remote replication. Continue checking it. Export/confirmation remain
usable when deletion is unavailable. Retention schedules and journal recovery
rules are in [account operations](account-data-operations.md).

## Existing reverse proxy and other infrastructure

Use `start --ingress external` for the bundled host-proxy override: Next.js is
published only on `127.0.0.1:3000`. The host proxy must terminate HTTPS for the
configured origin and overwrite both headers on every upstream request:

- `X-RateFreak-Ingress-Key`: the exact secret from the instance's `secrets/ingress`.
- `X-RateFreak-Ingress-IP`: one verified client IPv4/IPv6 literal.

Read the secret into protected proxy configuration; never let a browser supply
these values. The direct-edge example derives the address from the TCP peer.
Behind another proxy/CDN, explicitly trust only the intended sources and verify
real-client parsing and spoof rejection. Follow your proxy's deployment contract;
Caddy's [header controls](https://caddyserver.com/docs/caddyfile/directives/reverse_proxy)
are used by the bundled example. Preserve separate Set-Cookie headers, no-store,
upload limits, export streaming and disconnect cancellation. No public `/api`
path should bypass Next.js.

A containerized existing proxy can replace the bundled edge on the dedicated
Compose ingress network instead of publishing Next. Restrict remote proxy source
access and use TLS on upstream links across hosts. Keep API/database ports and
private health endpoints inaccessible publicly. Network membership and host-admin
access are part of the trust boundary.

The default package uses private single-host HTTP between Next and API, with
separate ingress/API keys. API `BrowserProxy__AllowPrivateHttp=true` plus the fixed
HTTPS `BrowserProxy__PublicOrigin` lets an authenticated Next service describe that
HTTPS browser hop for secure cookies. Unauthenticated requests and ordinary
forwarded-proto/host headers cannot activate it. Cross-host API links need TLS;
do not reuse this private-network exception for public HTTP.

For other orchestrators, build the same images and supply the configuration
contracts in the [API README](../README.md) and [web README](../web/README.md).
External databases require deliberately provisioned migration/runtime roles and
credentials, trusted TLS, equivalent grants for existing/future tables and reviewed
migrations. Do not run fresh-database bootstrap against an existing installation.
The default operator CLI provisions its own local PostgreSQL; custom topologies
must provide their own reviewed orchestration and repeat the acceptance checks.
To use the standard application environment contracts without the reference
secret-file loader, override the API entrypoint to `dotnet /app/RateFreak.Api.dll`
and the frontend entrypoint to `node server.js`. Supply all required private
configuration and preserve the non-root users, protected mounts and ingress trust
checks. The reference entrypoint otherwise loads credentials from its mounted
secret files and constructs the configured database connection.

Optional providers can be enabled using explicit API environment configuration in
a private mode-0600 `compose.override.yaml` in the instance directory. The CLI
appends this file consistently to lifecycle commands. Changing it invalidates
startup acceptance until an explicit stopped-writer migration/acceptance run.
They need your own keys/contact information and terms
acceptance; enabling Google/GitHub also requires callbacks on the exact frontend
origin. Secrets stay server-only. Compose file overrides must preserve service
permissions, private networking and the fixed-origin trust boundary.

## Upgrade

Record the existing source/image identities, migration history, named volumes and
configuration. Read release/migration notes and stop every writer before backup
and migration. Use matching API/web/tools artifacts from one release.

The reference database pin now selects PostgreSQL 17.12 on Debian 13 Trixie.
The PostgreSQL major version and data-directory layout remain at 17, but the
libc/ICU collation versions differ from Bookworm. Existing Bookworm installations
must use the fresh-instance logical restore procedure below before the usual
application upgrade; reusing the old volume can leave indexes ordered under
different collation rules. Before upgrading, also review the
[17.12 migration notes](https://www.postgresql.org/docs/release/17.12/):
they identify circumstances requiring index repair and recommend a new full
backup for users of PostgreSQL incremental backups. RateFreak's coordinated
backup uses `pg_dump`, rather than PostgreSQL incremental backups. A disposable
reference upgrade does not establish that every installation's indexes are
unaffected. Keep the previous image identity and a verified coordinated backup.

`migrate` and `start` reject mismatched database-default or explicit collation
versions. They never refresh collation metadata or rebuild indexes implicitly.
`start` requires application writers to be stopped before it starts PostgreSQL
for the check. Repeating `start` on a running stack therefore requires `stop`
first. Do not suppress a mismatch by refreshing version metadata alone.

For an application-only upgrade with unchanged, current database collations:

```bash
python3 deploy/self-host/ratefreak.py --instance-dir "$RATEFREAK_INSTANCE" stop
python3 deploy/self-host/ratefreak.py --instance-dir "$RATEFREAK_INSTANCE" backup --output /protected/backups/ratefreak-before-upgrade
# Obtain/review the new matching source release, then:
python3 deploy/self-host/ratefreak.py --instance-dir "$RATEFREAK_INSTANCE" build
python3 deploy/self-host/ratefreak.py --instance-dir "$RATEFREAK_INSTANCE" prepare
python3 deploy/self-host/ratefreak.py --instance-dir "$RATEFREAK_INSTANCE" migrate
python3 deploy/self-host/ratefreak.py --instance-dir "$RATEFREAK_INSTANCE" start
```

A failed migration invalidates the prior startup acceptance record and leaves the
app stopped. Do not start old artifacts against a newer schema or assume a guarded
downgrade can reconstruct discarded data. Prefer forward fixes or an isolated
matching-version restore. Rebuilding replaces local image tags; retain the previous
release artifacts/images before doing so if they are needed for recovery.

### Bookworm-to-Trixie database transition

Keep the old instance stopped and retain its source, complete application image
set, private configuration, named volumes and a coordinated backup. Initialize
a **new private instance directory** with the reviewed Trixie installation
package; do not mount the old database volume into it. Use the planned public
origin and reconcile provider/OAuth/ingress settings privately before cutover.

Restore and replay with the **old matching API/web/tools images first**. Backup
validation intentionally binds their exact IDs; a backup from the previous
application cannot be restored directly with the new Skia image set. For a
bundle-based installation, select the retained, validated old bundle in the new
instance before `prepare` and follow [backup and isolated restore](#backup-and-isolated-restore).
Supply the current independently protected erasure journal and TMDB/IGDB policies,
preserving their required ownership and mode. Do not take those records from
the snapshot or bypass the image/ownership checks. Source-built installations
must preserve and select the equivalent complete old image set with reviewed
operator tooling; rebuilding old tags does not establish the old identities.

The fresh Trixie database recreates indexes under its current collation rules.
Verify restored content, artwork, keys, current-policy replay, database-default
and explicit collations while traffic remains stopped. Then select the new
application image set and run `prepare`, `migrate` and acceptance before
`start`. This separates the database OS restore from the application update
without weakening either restore binding. Verify the new instance over HTTPS
before switching traffic, and retain the old stopped instance and backup for
the reviewed recovery plan. Never run both instances as writers or roll back
over newer user data.

### Downloaded release bundles

Download every file from the verified project release and retain its
`release-manifest.json`. Check provenance before running supplied code. The
manifest binds filenames, sizes, SHA-256 hashes, platform and all five image IDs;
checksums alone do not authenticate a publisher.

With writers stopped and a coordinated backup retained, use the matching source
installation bundle's operator tool:

```bash
python3 deploy/release/bundle.py verify /path/to/downloaded-release
python3 deploy/self-host/ratefreak.py --instance-dir "$RATEFREAK_INSTANCE" load-release --bundle /path/to/downloaded-release
python3 deploy/self-host/ratefreak.py --instance-dir "$RATEFREAK_INSTANCE" prepare
python3 deploy/self-host/ratefreak.py --instance-dir "$RATEFREAK_INSTANCE" migrate
python3 deploy/self-host/ratefreak.py --instance-dir "$RATEFREAK_INSTANCE" start
```

Loading validates all artifact hashes and image/archive identities before selecting
one complete versioned image set. A failed/partial load preserves the previous
selection; successful selection invalidates prior migration acceptance. Old images
are retained. Bundle-selected services resolve immutable image IDs, so another
instance cannot change their selection by moving a Docker tag. A subsequent
source `build` explicitly selects the complete local image set only after all
five builds succeed, and requires fresh migration acceptance. Image selection
does not change implicitly on start. New bundles also select the patched PostgreSQL
and Caddy identities; their package sources/licenses accompany the release. Do not
substitute any selected image in an instance override. Review PostgreSQL collation
compatibility and coordinated recovery before changing an installation. Retained
legacy three-image bundles keep their original separately referenced service scope.

See [release preparation and announcements](releasing.md) for versioning, required
matching source materials and optional administrator update checks. Update checks
are disabled by default; an available upstream release does not install itself.

## Provider configuration and image cache

Optional provider settings belong in the protected instance
`compose.override.yaml` (mode 0600), as other runtime overrides do. Example:

```yaml
services:
  api:
    environment:
      Providers__OpenLibrary__Enabled: 'true'
      Providers__OpenLibrary__Contact: 'replace-with-your-monitored-email@example.org'
```

Use a real monitored operator email before enabling the adapter; no API key is
needed. Follow the normal stop/build/**prepare**/migrate/start upgrade sequence.
Preparation creates `storage/image-cache` with UID/GID 10001 and mode 0700;
it preserves existing cache files and refuses conflicting ownership/modes or
symlinks. This cache is mounted at `/app/.next/cache/images`; it survives web
container recreation while the rest of the cache parent remains temporary.
Do not skip preparation and rely on Docker creating a root-owned bind source.

IGDB and TMDB images use separate managed caches, described below. Open Library
covers are delivered directly as validated HTTPS image URLs on
`covers.openlibrary.org`, using its large (`-L.jpg?default=false`) size and CSS
layout dimensions. They bypass the same-origin cover proxy and Next.js optimizer.
The old `/provider-artwork/openlibrary/` route and optimizer source admission are
removed; there is no optimizer fallback. MusicBrainz currently supplies no artwork.

Viewing book covers causes the browser to contact Open Library and any provider
redirect destinations directly, sharing its IP address and normal connection
metadata with those services. Images use `referrerPolicy="no-referrer"`.
Operators should disclose this external image delivery in their privacy notice.
If adding a Content Security Policy, permit `https://covers.openlibrary.org` in
`img-src` and review actual provider redirect destinations; the repository does
not ship a CSP. Source attribution remains linked beside provider details.
A missing or blocked image shows the normal accessible artwork fallback.

During the first provider-lifecycle preparation with this package, stop web
writers and run the normal explicit `prepare` command. It clears the contents of
`storage/image-cache` once because legacy optimizer variants cannot reliably be
identified by provider. It preserves that directory's ownership/mode and refuses
unexpected mounts, symlinks or special files. The operation does not touch
`storage/tmdb-posters`, `storage/igdb-covers` or protected uploads in
`storage/artwork`. Repeated preparation preserves caches and policies. Do not
restore legacy optimizer backups or re-enable their source routes. Previously
delivered browser copies cannot be recalled; direct covers thereafter follow the
provider's own browser cache headers.

The `/provider-artwork/` namespace remains blocked at the bundled ingress;
alternate ingress must preserve that denial. Provider artwork is excluded from
the coordinated database/artwork/key-recovery backup. Private uploads remain
behind current visibility/authorization checks and `no-store`.
The API's shared public metadata cache is process-local (five-minute searches,
one-hour details), and resets on API restart. Each API replica needs coordinated
provider budgets when sharing egress.

Open Library requests application registration in its
[API guidance](https://openlibrary.org/developers/api). Configuring a contact email
identifies requests; it does not submit the registration form. Record the actual
operator/application, intended use and deployment egress before making the form's
acknowledgments. Metadata caching guidance does not by itself approve the
direct browser cover delivery. See the
[registration and cover-scope review](provider-public-guidance-2026-11-18.md).

## TMDB refresh, expiry and removal

TMDB metadata refresh begins five UTC calendar months after its last successful
synchronization. Temporary failures retry without renewing that timestamp. At six
months, expired title/overview/date/metadata/poster references are cleared and an
"Unavailable title" placeholder preserves the internal item, TMDB ID, ratings,
reviews, library history and list memberships. Authoritative not-found responses
remove provider fields immediately. A later valid refresh can restore fields for
the same item unless the operator has blocked it. Hidden/archived state is retained.

Poster bytes have a separate clock based on an actual successful image fetch;
refreshing metadata alone does not make a cached poster fresh. TMDB posters use
`/tmdb-posters/<filename>` with `no-store`, a fixed HTTPS w500 upstream and a
bounded private cache. They bypass Next.js's shared optimizer; upstream cache
headers cannot extend their six-month maximum. A scheduled sweep covers unvisited
posters. Before expiry a temporary source outage may use the existing bytes; once
expired the handler must fetch fresh bytes or return the normal artwork fallback.
The endpoint rejects query strings, arbitrary source URLs and redirects. Direct
TMDB URLs through `/_next/image` are denied.

`prepare` provisions the dedicated poster directory as UID/GID 10001, mode 0700,
and an initial empty provider policy as mode 0600. Policy provisioning is recorded
outside ordinary backups. Repeated preparation validates it and does not replace
a missing policy with an empty one. API and web mount the policy directory read
only at `/independent`; the tools image mounts it writable. The existing account
erasure journal remains at host `independent/account-erasures.jsonl` and is mounted
separately at `/journal/account-erasures.jsonl` for its writers.

On this upgrade, preparation clears the legacy shared Next.js image cache once,
with web writers stopped, because old TMDB entries cannot be individually
identified. Legacy IGDB/Open Library optimizer entries also become cold;
managed provider caches remain separate. The receipt prevents repeat
preparation from clearing ordinary cached images again. Old browser copies cannot
be recalled; do not claim retroactive erasure of previously delivered content.

For a provider removal, stop writers and run the matching release's operator tool:

```bash
python3 deploy/self-host/ratefreak.py --instance-dir "$RATEFREAK_INSTANCE" stop
python3 deploy/self-host/ratefreak.py --instance-dir "$RATEFREAK_INSTANCE" tmdb-remove --database-name ratefreak --identity movie:123 --confirm-tmdb-removal
# Or, when the TMDB licence ends:
python3 deploy/self-host/ratefreak.py --instance-dir "$RATEFREAK_INSTANCE" tmdb-remove --database-name ratefreak --terminate --confirm-tmdb-removal
```

The operation durably blocks the identity/known poster filenames (or all TMDB
operations for termination) before clearing stored provider fields and the poster
cache. It refuses active writers and leaves startup blocked after partial failure;
repeat the same command to complete cleanup. Database-name validation prevents
accidentally applying it to a different configured database. It does not delete
user content. Disabling the TMDB adapter alone stops network requests but does not
mean licence termination and does not remove prior data immediately.

Imported poster source history supplies known filenames. Offline maintenance
cannot discover unknown poster filenames previously seen only in search/preview
or outside this installation: supply each known affected filename with repeated
`--poster filename.jpg` arguments alongside `--identity`. Review the actual removal
request and available source records; do not describe an ID-only command as
identifying every historical image. Filename blocks also prevent direct URL
refetching. Global termination blocks every TMDB poster regardless of filename.

Replicate the current policy independently of database backups and retain it for
all restorable snapshots. Never replace it with a backup-era or freshly empty
policy. Isolated restore requires `--tmdb-policy` as well as the current erasure
journal; use protected copies with the service ownership expected by the matching
tools image. Restore/replay applies the current blocks and expires old content
before reopening traffic. The poster cache is never restored. Missing, malformed
or unreadable policy denies TMDB operations; restore does not silently reconstruct
permissions. No automatic unblock is provided: a reviewed operator policy change
requires stopped writers and subsequent reconciliation/acceptance.

Metadata expiry reconciles at startup and on a one-minute timer, draining
100-row database batches independently of network refresh. Refresh selects at most
100 identities per pass, with at most one new pass per minute across API replicas;
slow requests can make a pass longer. A persisted provider cooldown stops the
batch after a rate limit or Retry-After response while expiry continues.
Poster startup/minute sweeps independently remove expired/blocked records; refresh
batches cover up to 100 retained posters with eight concurrent operations. The
poster cache allows 2,048 records / 1 GiB, with a 5 MiB source-body ceiling and
8-active/64-queued source work per web process. Each source operation has a
ten-second deadline including queue time. Multiple hosts and network-shared cache
filesystems are outside the verified reference topology.

Cache mutations use kernel advisory locks via the existing Python 3 runtime. A
live writer retains the checked lock descriptor; process death releases exclusion.
The regular `.tmdb-lock` file remains present, but its presence alone does not
mean the cache is locked. Runtime does not unlink it. Stopped-writer cache cleanup
can remove it together with discarded bytes and partial writes.

Monitor the private readiness check and sanitized `TMDB reconciliation failed`,
`TMDB refresh batch failed` and `TMDB poster cache reconciliation failed` messages,
cache disk usage, and independent policy replication. To inspect metadata work,
use a privileged read-only database session to query `tmdb_refresh_schedule` for
`last_batch_at` / `next_allowed_at`, and group `tmdb_lifecycle_states` by
`last_outcome` with counts and oldest `last_attempt_at` / `next_attempt_at`.
For an enabled, nonterminated provider, investigate due identities that remain
unchanged over successive passes, or `content_removed = false` identities whose
successful sync time plus six UTC calendar months is already past. Account for
explicit policy blocks and per-identity backoff; an intentionally terminated
provider should not be expected to refresh. Retention checks must use forward
calendar addition, not a fixed day count or inverse subtraction at month ends.
The cache directory's record count/bytes and recurring reconciliation errors reveal
poster cleanup problems; never delete or reset the independent policy as a remedy.

A stopped/failing host or inaccessible storage cannot enforce on-disk deletion;
the one-minute timer is a target cadence, not an instantaneous deletion guarantee
for arbitrary backlogs. Keep alerting and capacity appropriate to the catalog.
Calendar deadlines use UTC with month-end clamping, not a fixed 180-day
approximation. This lifecycle does not clear operator eligibility, attribution,
other providers' policy questions or the independent release security gate.


## IGDB refresh, expiry and removal

IGDB uses the selected TMDB-style lifecycle: refresh starts at five UTC calendar
months after successful synchronization; unrefreshed metadata expires at six.
This interval is RateFreak policy, not a provider-confirmed deadline. Temporary
failures retain the successful timestamp and retry with backoff and provider
cooldowns. Expiry sweeps run independently of network access. API disablement
stops requests but does not purge content; existing imports still expire.

Expired provider fields become an "Unavailable title" placeholder while the
catalog UUID, game source identifier, ratings, reviews, libraries, history and
collections remain. Confirmed provider not-found results and explicit operator
removals are durably blocked; age expiry alone can recover on a valid refresh.
Current policy also filters cached searches/details and prevents reimport.

Covers use `/igdb-covers/<imageid>.jpg`, `no-store` and a separate bounded cache.
Actual successful cover fetch time controls its five/six-month dates; metadata
refresh cannot renew cached bytes. Old remote optimizer URLs are denied, including
historical size/quality variants. Startup and minute sweeps remove expired or
blocked cached content. Stale, unexpired images may be served after a failed
refresh; expired images are withheld. Remote browser copies cannot be recalled.

`prepare` provisions the protected policy even when the provider is disabled.
Compose mounts `/independent-igdb/igdb-policy.json` writable to API/tools (the API
persists confirmed provider removals) and read-only to web, plus `/data/igdb-covers` for web/tools. Local processes use
`Providers__Igdb__PolicyPath`, `RATEFREAK_IGDB_POLICY_PATH` and
`RATEFREAK_IGDB_CACHE_PATH`. Preserve policy ownership UID/GID 10001, mode 0600,
and parent/cache directories mode 0700 in the reference containers. Missing,
unsafe or unreconciled policy withholds dependent catalog and cover requests.
Never recreate an empty policy to bypass a missing initialized policy.

With all application/maintenance writers stopped, run:

```bash
python3 deploy/self-host/ratefreak.py --instance-dir "$RATEFREAK_INSTANCE" igdb-remove --database-name ratefreak --identity game:123 --confirm-igdb-removal
# Include any known search-only or otherwise unknown historical cover image IDs:
python3 deploy/self-host/ratefreak.py --instance-dir "$RATEFREAK_INSTANCE" igdb-remove --database-name ratefreak --identity game:123 --poster co123.jpg --confirm-igdb-removal
# For applicable operator termination/removal of all IGDB content:
python3 deploy/self-host/ratefreak.py --instance-dir "$RATEFREAK_INSTANCE" igdb-remove --database-name ratefreak --terminate --confirm-igdb-removal
```

The command writes durable blocks before database cleanup, clears the IGDB cache,
and records completion. Interrupted removal blocks startup until the identical
intent is retried successfully. Known cover history is retained for removal;
unknown preview-only/historical filenames require explicit `--poster`. Global
termination blocks every IGDB cover. Blocking covers prevents server refetch,
including old optimizer URLs. It does not recall copies already delivered.

Replicate the current policy independently of database snapshots. Isolated
restore requires both `--tmdb-policy` and `--igdb-policy`, from protected current
files outside the snapshot and target. Replay reconciles both providers before
verification can reopen traffic. Monitor readiness and sanitized `IGDB
reconciliation failed`, `IGDB refresh batch failed` and `IGDB poster cache
reconciliation failed` messages. Inspect `igdb_refresh_schedule` and
`igdb_lifecycle_states` for scheduling/outcomes. Per provider, each minute batch
handles at most 100 items; multiple API processes coordinate through database
locks and the persisted cooldown. Size capacity to finish refreshes before expiry
and coordinate discovery/import budgets across replicas.


## Secret rotation

Stop all writers and record the named operator/change before rotating credentials.
Never regenerate the entire instance or remove key/journal/data storage to rotate
one secret. Generate fresh independent 64-hex values privately; retain the required
file owners/mode 0600. Preparation refuses mismatched existing copies rather than
silently replacing them, so rotate the explicitly identified copies together:

- Ingress: `secrets/ingress` and `runtime/web/ingress`; Caddy reads that runtime file.
- API attribution: `secrets/proxy`, `runtime/api/proxy`, `runtime/web/proxy` and
  `runtime/tools/proxy`.
- Database runtime: alter the actual database role password under a named privileged
  session, then update `secrets/runtime` and its `runtime/api`, `runtime/tools` and
  `runtime/postgres` copies. The PostgreSQL copy supports fresh bootstrap only.
- Migration/bootstrap identities: rotate the corresponding real database role
  and matching `secrets/migrator` or `secrets/postgres` plus the prepared copies.
  Changing a password file alone never changes an initialized database password.

Use protected stdin/configuration for database changes, not command arguments or
recordings containing passwords. Use an authorized privileged maintenance session
for service-owned files. Re-run `prepare` to validate consistency, then explicit
`migrate` and `start`; verify readiness and authorization before reopening traffic.
Data Protection key rotation is separate: preserve the existing key ring for active
sessions/recovery rather than replacing it with a new empty directory. Provider
credential rotation follows its API-only configuration and provider registration.

## Backup and isolated restore

The explicit `backup` command requires writers stopped, a ready instance and a
new destination. It takes a custom-format PostgreSQL dump, artwork archive and
private key-recovery archive, validates the dump inventory and records hashes,
schema and matching API/web/tools image identities. It never includes the erasure
journal, current TMDB/IGDB removal policies or disposable provider image caches.
Protect/encrypt the output and enforce expiration externally; files on the same
host are not disaster recovery. The command does not configure a backup schedule.

Restore into a **new isolated instance**, never over an existing installation.
Use the matching image/version and a separate test hostname or leave ingress off.
Supply a private mode-0600 copy from the current independent journal replica taken
with source writers stopped; never use an older journal bundled with a snapshot.

```bash
export RATEFREAK_RESTORE="$HOME/.local/share/ratefreak-restore"
python3 deploy/self-host/ratefreak.py --instance-dir "$RATEFREAK_RESTORE" init --origin https://restore.example.com
python3 deploy/self-host/ratefreak.py --instance-dir "$RATEFREAK_RESTORE" prepare
python3 deploy/self-host/ratefreak.py --instance-dir "$RATEFREAK_RESTORE" restore --backup /protected/backups/ratefreak-before-upgrade --journal /protected/current-account-erasures.jsonl --tmdb-policy /protected/current-tmdb-policy.json --igdb-policy /protected/current-igdb-policy.json --confirm-restore
python3 deploy/self-host/ratefreak.py --instance-dir "$RATEFREAK_RESTORE" replay --database-name ratefreak --confirm-erasure-replay
```

Restore validates matching artifacts/hashes and rejects a populated target. It
imports the supplied current journal only into the verified empty journal prepared
for that new instance; it refuses to replace journal records. It also requires
and installs the independently supplied current TMDB/IGDB policies; a database snapshot
cannot grant permission to serve blocked provider content. A `restore-pending`
marker blocks startup. A separate matching completion receipt is written only
after the full database/artwork/key restore succeeds. Replay refuses incomplete
restores and cannot reaccept an invalidated migration or changed image set. Replay uses the existing explicit database-name maintenance
command, reconciles TMDB and IGDB expiry/current blocks, checks journaled accounts are
absent and referenced artwork exists, and
only then clears the startup block. A failed/interrupted restore stays stopped;
inspect and recover into another fresh isolated target rather than retry blindly.

Before reopening traffic, additionally verify credentials/private content/artwork,
public ownership detachment, moderation holds, unrelated accounts, idempotency and
operator-specific expected rows. Keep the restored instance's journal independently
replicated and verify key/volume permissions. Record recovery time/point against
your objectives. `replay` can be repeated with writers stopped; it does not start
HTTP or apply migrations.

## Operations and troubleshooting

`status` shows the current Compose services; `stop` preserves data. Lifecycle
commands take a per-instance lock, and maintenance refuses restarting, paused or
active application/maintenance containers; all writers must be explicitly stopped. Use named host
accounts and record maintenance outcomes under the [staff operations policy](staff-access-operations.md).
Normal application actions use named staff accounts; grant Admin through the
existing offline API command under protected configuration, with another Admin
remaining before revocation. Do not invent direct SQL role changes.

Monitor readiness privately, journal failures/replication lag, backup age/expiration,
export resources, missing artwork, cleanup/retention failures and available disk.
Log metadata rather than credentials, request bodies, exported data or journal
contents. Persist/protect keys; a startup warning about an absent XML encryptor
means the filesystem/host storage protection remains your responsibility.

If the app cannot start, inspect lifecycle markers, actual schema readiness and
sanitized service logs. Missing secrets/permissions fail closed. Invalid ingress
returns sanitized failures before API calls. Failed migration/restore requires
stopped writers and a reviewed fix; never replace a journal with an empty file,
reset the database, change production cookie security or expose private storage
to make a check pass.

Verification commands and recorded reference results are linked from
[STATUS](STATUS.md). Provider licensing, actual backup expiration, external records,
load capacity and your own infrastructure still require operator acceptance.

## Profile avatar storage

With `Artwork__Enabled=true`, avatar uploads use `<Artwork__RootPath>/avatars/`
inside the existing API artwork volume. Generated `.webp` files, temporary `.stage`
files and `.lease` files all stay in that folder. Existing catalog images keep
their current paths. Do not expose either directory as static files or put them
in the web image optimizer/cache; the API checks current ownership, moderation
and asset identity and sends `no-store`/`nosniff`. Protect the root and avatar
folder from untrusted filesystem writers. Symlinked paths and linked leases are
refused. Replacing a directory concurrently is an operator integrity concern,
not a supported way to move live storage.

Apply `20261119165037_AddProfileAvatars` through the normal backup/migration
procedure before starting this version. It adds avatar metadata and nullable
identity/revision fields; existing accounts use the person-icon fallback. Its
downgrade refuses while avatar data exists. No persistent installation is migrated
by tests or by application startup.

Back up the entire artwork root. The maintenance tools allow canonical files in
the `avatars/` subdirectory while retaining flat catalog archive support; key
archives remain flat. The writer validates its own produced archive before a
success manifest, and catalog/avatar references are checked in their separate
namespaces. Use matching tools/images for restore as described above. A backup
taken before the avatar migration remains supported by the pre-upgrade inventory
check. Do not enable arbitrary recursive archive extraction.

Avatar replacement/removal and account deletion attempt file cleanup after commit.
The hourly avatar sweeper retries unreferenced files older than 24 hours in batches
of at most 1,000; database failures defer deletion. Restore/replay checks and the
30-day backup expiration policy also apply to avatars. See
[avatar verification](profile-avatars-verification.md).
