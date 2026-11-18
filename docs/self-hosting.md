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
The verified [application-native source sidecar](native-source-package-verification.md)
must accompany its matching image when a binary release is authorized. It does
not replace base-OS or other applicable source-delivery obligations.

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
| `storage/artwork/` | API-only image files; never a static web directory |
| `storage/keys/` | Durable ASP.NET Data Protection keys |
| `storage/image-cache/` | Disposable public provider image cache, mounted into the web container; excluded from account/recovery backups |
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

Public TMDB, IGDB and Open Library images use that common bounded cache. MusicBrainz
currently supplies no artwork. The minimum image TTL is 24 hours; upstream cache
headers can make it longer. Capacity is 1 GiB with framework eviction. Different
width/quality/format variants can each need an initial fetch. Cold/evicted images
still require their source; retained expired images can be served stale during
failed background refresh. This is a disposable performance cache, not an artwork
backup or an unlimited offline guarantee.

The Open Library cover route only accepts a numeric CoverID, validates every
redirect to a known same-cover Archive path, pins public DNS results, and bounds
time, bytes and redirect count. It admits at most eight simultaneous upstream
cover operations with 64 waiting requests per web process; excess work returns
503/Retry-After, and the ten-second deadline includes queue time. Unknown redirect layouts fail with the normal
artwork fallback. The optimizer admits only that dedicated local source route;
uploaded/API/moderation artwork cannot be requested through it. External source
redirects remain disabled for other providers. The default optimizer also has
explicit source, quality, size and response limits. Monitor CPU, egress and disk;
this does not add a perimeter abuse-budget service.

The `/provider-artwork/` namespace is an **internal optimizer source**, not a
public endpoint. The bundled Caddy edge returns 404 for it before proxying; Next.js
dispatches authorized optimizer source requests internally. Every alternate
ingress must likewise reject the namespace (including normalized/encoded path
variants) before forwarding. Browsers use `/_next/image`, which remains available.
Keep the web service's direct port private. Otherwise callers could bypass the
image cache by requesting the source route directly.

For takedowns or immediate refresh, stop the web service and remove only the
contents of `storage/image-cache`, preserving its directory ownership/mode;
restart through the normal accepted lifecycle. There is no general per-image
invalidation API. Browser copies may last until their HTTP TTL expires. Database
metadata/import removal and permission revocation remain separate operator work;
disabling an adapter does not erase prior imports or cached images.
Purging alone does not prevent refetching a still-allowed source URL. When a
removal must prevent future serving, also remove/block the affected source and
its catalog references under the applicable provider/rights policy.

The image cache contains only public provider artwork and is excluded from the
coordinated database/artwork/key-recovery backups. Never put private uploads,
erasure records or secrets there. Existing protected uploaded artwork remains in
`storage/artwork`, behind current visibility/authorization checks and `no-store`.
The API's shared public metadata cache is process-local (five-minute searches,
one-hour details), and resets on API restart. Each API replica needs coordinated
provider budgets when sharing egress.

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
schema and matching API/web/tools image identities. It never includes the erasure journal.
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
python3 deploy/self-host/ratefreak.py --instance-dir "$RATEFREAK_RESTORE" restore --backup /protected/backups/ratefreak-before-upgrade --journal /protected/current-account-erasures.jsonl --confirm-restore
python3 deploy/self-host/ratefreak.py --instance-dir "$RATEFREAK_RESTORE" replay --database-name ratefreak --confirm-erasure-replay
```

Restore validates matching artifacts/hashes and rejects a populated target. It
imports the supplied current journal only into the verified empty journal prepared
for that new instance; it refuses to replace journal records. A `restore-pending`
marker blocks startup. A separate matching completion receipt is written only
after the full database/artwork/key restore succeeds. Replay refuses incomplete
restores and cannot reaccept an invalidated migration or changed image set. Replay uses the existing explicit database-name maintenance
command, checks journaled accounts are absent and referenced artwork exists, and
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
