# Self-hosted Reference Installation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [x]`) syntax for tracking.

**Execution:** Completed locally on 2026-11-10. Native execution, review fixes and
[verification evidence](../../self-hosted-reference-verification.md) are recorded.
Implementation checkpoints were working-tree diffs. The owner subsequently
requested a local commit and merge into `main`; see Git history for integration.
Public publication remains outside this work.

**Goal:** Deliver a verified, portable single-host installation with production images, authenticated ingress and explicit install/upgrade/recovery tools.

**Architecture:** Optional Caddy forwards HTTPS traffic to Next.js, which authenticates ingress and constructs the existing API attribution headers. The API uses PostgreSQL and private persistent artwork/key/journal storage. One-shot tools provision and migrate explicitly; application restarts never migrate or regenerate secrets.

**Tech Stack:** Linux amd64, Docker Engine/Compose v2, Caddy, Node 24/pnpm 10.28.2/Next.js 16.4.3, .NET 10, PostgreSQL 17, Python 3 standard library for operator tooling/tests.

**Spec:** [Approved reference installation](../specs/2026-11-10-self-hosted-reference-installation-design.md).

## Global Constraints

- One instance of each application service; dedicated hostname at the site root.
- No Vercel account, developer SDK, developer user-secrets or mandatory provider account on the operator host.
- Application processes run without root; only bundled ingress publishes public ports.
- Separate migration/owner and least-privilege runtime database credentials.
- Independent 64-hex-character ingress/API secrets; no credentials in browser bundles, logs or command arguments.
- Private HTTP is an explicitly configured single-host boundary; cross-host links require TLS.
- Keep Secure cookies, CSRF, exact Origin, independent API authorization and existing route/body/time budgets.
- Journal: private directory, mode-0600 file, exclusive creation and file/parent fsync; preserve existing content.
- Independent journal replication and every operator's maximum 30-day database/artwork backup expiration remain operational obligations.
- No publishing, real-account deletion, existing-installation reset or project-database migration.
- Preserve current uncommitted documentation. Commits/checkpoints must contain only reviewed task files; do not broadly stage the workspace.

## Review Focus

- Proxy headers combined by the HTTP stack must fail closed rather than select a forged first/last address (Task 1).
- Reinitialization, interruption and wrong ownership must preserve all prior credentials/journal records (Task 2).
- Future migrations must grant runtime access to new tables without giving runtime DDL privileges (Task 4).
- Proxy timeouts/body handling must preserve five-minute export streaming and image uploads (Tasks 5–6).
- Restoring a stale snapshot must not replace the current journal or expose restored users before replay (Task 6).

## File responsibilities

Extend `web/server/api/{config,client-attribution}.ts` and its existing tests; update `web/server/interfaces/api-proxy-config.ts`. Shared proxy handlers remain responsible for routing/cookies/body limits.

Create `deploy/self-host/` for `compose.yaml`, optional `compose.caddy.yaml` and `compose.host-proxy.yaml`, `Caddyfile`, `instance.env.example`, `api.Dockerfile`, `web.Dockerfile`, `tools.Dockerfile`, `entrypoint.py`, `ratefreak.py`, `database.sql`, `test_operator.py`, `smoke.py` and `acceptance.py`. Entrypoint translates mounted secrets; operator CLI owns explicit lifecycle orchestration; SQL owns fresh database identities/grants; smoke/acceptance own disposable infrastructure proof. Add root `.dockerignore` and `docs/self-hosting.md`. Keep `deploy/compose.yaml` as the existing development database setup.

## Task 1: Portable authenticated ingress

**Files:** Modify the three web configuration/attribution files above and `web/README.md`; extend `config.test.ts`, `client-attribution.test.ts`, `proxy.test.ts`, `artwork-proxy.test.ts`, `account-data.test.ts`, `setup-proxy.test.ts`, `oauth-proxy.test.ts`.

**Interfaces:** Preserve `readApiProxyConfig(): ApiProxyConfig` and `clientAttributionHeaders(request: Request, config: ApiProxyConfig): Headers`. Add `clientIpMode: "trusted-proxy"`, optional `ingressSecret: string` and `allowPrivateApiHttp: boolean`. New environment keys: `RATEFREAK_INGRESS_SECRET`, `RATEFREAK_ALLOW_PRIVATE_API_HTTP=true`.

- [x] Write failing configuration/attribution tests for Production trusted-proxy mode, independent 64-hex secrets, exact HTTPS public origin, explicit private-HTTP opt-in, and rejection of missing/invalid/combined ingress keys or addresses. Assert IPv4/IPv6 success and no fallback to forwarded/Vercel/service headers.
- [x] Run `pnpm --dir web test server/api/config.test.ts server/api/client-attribution.test.ts`; confirm failures identify unsupported trusted-proxy behavior.
- [x] Extend configuration validation. Private HTTP opt-in must require trusted-proxy mode and a private/loopback IP or single-label service hostname; reject public IP literals/public DNS HTTP upstreams. Document that an operator-configured private hostname still requires correct private networking and cross-host TLS. Preserve existing Vercel/Development rules.
- [x] Add constant-time ingress key validation using Node crypto and strict single-literal address validation. Construct only the existing API headers; never forward ingress credentials. Errors continue through current sanitized proxy failure paths without an upstream request.
- [x] Add route tests for each listed handler family proving invalid ingress causes no fetch, valid ingress uses the validated client address, and ingress credentials are absent upstream/downstream. Include duplicate Headers.append values and two distinct clients.
- [x] Run focused tests, then full web tests, lint and typecheck. Review the diff and record a task checkpoint.

## Task 2: Safe instance configuration and storage initialization

**Files:** Create `deploy/self-host/{ratefreak.py,entrypoint.py,instance.env.example,test_operator.py}`.

**Interfaces:** Operator host command: `python3 deploy/self-host/ratefreak.py --instance-dir PATH init --origin https://HOST [--local-tls]`. Subsequent subcommands consume the same instance directory. CLI returns 0 on success, nonzero with sanitized output on failure. No secrets are supplied as CLI arguments. Operator host needs Docker/Compose and Python 3, not SDKs.

- [x] Write unittest cases using temporary directories for exclusive creation, repeat initialization, interrupted/missing files, symlinks, malformed secrets, unsafe permissions and journal records preserved byte-for-byte. Assert emitted output contains no secret values.
- [x] Run `python3 -m unittest discover -s deploy/self-host -p 'test_operator.py'`; observe failures before implementation.
- [x] Implement validated root-origin configuration and per-secret private files: ingress, API attribution, setup, PostgreSQL bootstrap/migration/runtime passwords. Reject paths containing newline/control characters and require independently generated secrets. Stage/fsync new configuration and use exclusive installation; repair only missing known outputs without changing existing values.
- [x] Implement a one-shot storage-init mode run by the tools image: create app-owned artwork/key directories and journal at fixed container paths; validate existing ownership/modes/symlinks. New journal uses O_EXCL/fsync including the parent. Initialization stops on conflicting existing data. Use stable application UID/GID 10001; root is restricted to this explicit provisioning operation.
- [x] Implement entrypoint secret loading into existing environment contracts without printing values, using a connection-string encoder rather than string concatenation. Distinguish API/runtime and migration credentials. Missing required secrets stops startup; normal entrypoint performs no initialization/migration.
- [x] Rerun unit tests. Inspect generated files/modes and verify repeated init retains bytes. Record checkpoint.

## Task 3: Reproducible production images

**Files:** Create `.dockerignore`, `deploy/self-host/{api.Dockerfile,web.Dockerfile,tools.Dockerfile}`; modify `web/next.config.ts` for standalone output.

**Interfaces:** Build targets `ratefreak-api:local`, `ratefreak-web:local`, `ratefreak-tools:local`. API listens privately on 8080; Next listens on 3000. API/web UID/GID 10001. Tools contains PostgreSQL 17 client utilities, Python, the matching API Release artifacts and a release-built EF bundle at `/app/efbundle`.

- [x] Add image checks to `smoke.py`: runtime user non-root, expected files present, no `.env`/`.local`/developer config, required temporary/cache paths writable, no compiler SDK in application images.
- [x] Run checks against missing images and record expected build-required failure.
- [x] Build API using pinned release inputs and matching runtime; provide explicit Python support for the entrypoint. Build tools with migration bundle in SDK stage and compatible glibc runtime. Keep migration credentials runtime-only.
- [x] Read installed Next output/self-hosting docs; add standalone output and copy standalone, public and static assets into Node 24 runtime. Install with the pinned pnpm version and frozen lockfile. Set production/listener config at runtime.
- [x] Build all images with `docker build -f deploy/self-host/<name>.Dockerfile -t ratefreak-<name>:local .`; validate smoke checks and real SkiaSharp upload/decode. Build a second instance with different origin/secrets using identical web image bytes.
- [x] Run web verification affected by standalone output and API Release publish. Record build identities and checkpoint; no registry push.

## Task 4: Database roles, explicit migrations and Compose orchestration

**Files:** Create `deploy/self-host/{compose.yaml,database.sql,smoke.py}`; extend operator CLI and tests.

**Interfaces:** `ratefreak.py --instance-dir PATH migrate`, `start`, `stop`, `status`. Base Compose services: `postgres`, `api`, `web`, one-shot `storage-init`, `migrate`, `tools`. Persistent mounts separate PostgreSQL/artwork/keys/journal; secrets mounted only where required. Add explicit instance-local acceptance marker recording successfully migrated release/schema; marker supplements API readiness and is not a substitute for database validation.

- [x] Write smoke assertions: no base published ports, missing init/migration prevents `start`, failed migration leaves app stopped, restart leaves secret/journal hashes unchanged, separate instance project names/paths do not collide.
- [x] Bootstrap fresh PostgreSQL using secret-file inputs; create `ratefreak_migrator` as schema owner and `ratefreak_runtime` without superuser/create/DDL rights. SQL grants current/future table/sequence DML and schema usage. Default privileges must be owned by the migrator executing future migrations. Database listens only on its private network.
- [x] Implement `migrate` to refuse active API/writers, run the matching bundle with migration credentials and record success only after schema verification. External database mode uses an explicit configuration override and never bootstraps/takes ownership automatically.
- [x] Implement `start` to require initialized storage/secrets and matching successful migration/readiness; launch services without automatic DDL. HTTP/job services stay stopped after failure. `stop` preserves all volumes; no command calls `down -v`.
- [x] Test fresh setup/retention/erasure using runtime credentials. Create a disposable new table through migrator and verify runtime DML succeeds while CREATE/ALTER/DROP is denied. Test existing schema upgrade and deliberately invalid migration in disposable databases.
- [x] Run CLI unit tests and disposable Compose smoke checks; record checkpoint and actual Compose version used.

## Task 5: Optional HTTPS edge and existing-proxy contract

**Files:** Create `compose.caddy.yaml`, `compose.host-proxy.yaml`, `Caddyfile`; extend CLI and smoke tests; begin `docs/self-hosting.md`.

**Interfaces:** `start --ingress caddy` (default) or `start --ingress external`. Caddy override exposes 80/443 and certificate volume; host-proxy override publishes Next on `127.0.0.1:3000` only. Containerized external proxy joins ingress network instead. Direct-edge header contract is Task 1's ingress key/IP, replaced on every request.

- [x] Write failing smoke cases for forged ingress/X-Forwarded/Vercel headers, unauthenticated direct-web proxy requests, non-public API/database ports, unknown Host and ordinary/large/streaming routes.
- [x] Configure direct Caddy to derive client IP from socket peer and overwrite ingress headers, preserving original HTTPS public host and existing response headers. Load ingress secret through protected config; no admin API/public sensitive logs. Do not trust inbound forwarding chains by default.
- [x] Add local-TLS acceptance mode using a locally trusted test CA; never disable browser/TLS verification. Existing-proxy guide defines header overwrite, source restriction, TLS for remote upstreams and explicitly configured trusted proxy chains.
- [x] Verify all routes pass through Next, cookie headers remain separate, no-store survives, uploads fit existing 5 MiB file constraints and export streams retain the five-minute application budget without edge buffering/disconnect regressions.
- [x] Run real HTTPS browser checks in an isolated instance; record origin/certificate trust/headers, distinct client-budget behavior and API denial without changing persistent project data. Record checkpoint.

## Task 6: Packaged backup, restore and acceptance

**Files:** Extend CLI, tools image and tests; create `deploy/self-host/acceptance.py`; complete `docs/self-hosting.md`; update operational runbooks with package command references.

**Interfaces:** CLI `backup --output PATH`, `restore --backup PATH --confirm-restore`, `replay --database-name EXACT --confirm-erasure-replay`. Restore is allowed only into a fresh isolated instance with writers stopped and current independent journal supplied. Existing-instance destructive restore is excluded from automated tooling. Backups carry schema/release, hashes, archive inventory and key-recovery requirements; journal is excluded.

- [x] Write tests for archive traversal/symlinks, altered hash/revision, existing target data, missing/current-journal handling and failed replay. Assert every refusal preserves existing files and never starts app services.
- [x] Implement coordinated backup with writers stopped using packaged pg_dump/artwork archive tools, protected output permissions and verified catalog/hash metadata. Preserve separate key recovery material privately; never imply a separate directory is an independent backup.
- [x] Implement isolated restore into fresh targets only, require matching application/schema/key identity and the operator's current journal, then run the existing Release replay command. Failed restore/replay keeps the target stopped. Verify absent private data/artwork, retained public/moderation state and repeat replay before allowing `start`.
- [x] Run clean-install, container-recreation, existing-version upgrade and deliberate failure acceptance. Seed/delete only disposable accounts; prove no developer SDK/config is needed on the operator host. Verify staff appointment/removal/recovery and retention via current runbooks.
- [x] Run relevant full frontend/backend suites, lint/typecheck/builds and image/Compose checks. Record versions, source revision, topology, resource observations and cleanup. Do not broaden platform claims beyond tested amd64.
- [x] Document install, optional providers/external database, secret rotation, permissions, maintenance, upgrade, recovery, 30-day expiration, independent journal replication, named operator records and troubleshooting. Update README/STATUS/release gates with actual evidence and remaining limits. Perform final diff/secret review and checkpoint.

## Execution boundaries and review

Implement tasks sequentially because tools/Compose depend on earlier contracts.
Recommend native execution in this session with one independent final review;
the shared initializer/secret/migration interfaces benefit from continuity.
Task checkpoints may be reviewable diffs without commits until integration is
authorized. Existing documentation changes must remain intact.

Each failure triggers focused diagnosis before edits/retries. Never weaken a
trust boundary to satisfy a smoke check. Infrastructure commands requiring daemon
or network access use the approval mechanism. Publishing, license selection and
registry ownership are outside this implementation plan.
