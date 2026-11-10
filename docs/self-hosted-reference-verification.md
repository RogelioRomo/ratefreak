# Self-hosted reference verification

Verified locally on 2026-11-10 for the approved
[reference design](superpowers/specs/2026-11-10-self-hosted-reference-installation-design.md).
Implementation was prepared on `feat/self-hosted-reference` based on `e7e1385`.
The project owner subsequently requested a local commit and merge into `main`;
see Git history for the integration commit. No public publication or deployment
is part of that integration.

## Implemented

The [self-hosting guide](self-hosting.md) covers source-built API/web/tools images,
private Compose services, optional Caddy HTTPS, existing-proxy integration,
private configuration and secret provisioning, persistent storage, least-privilege
PostgreSQL identities, explicit migrations, upgrades, backup and isolated restore.
Vercel is optional; local accounts/custom catalog work with external providers off.

Production application processes use UID/GID 10001. Privileged preparation copies
operator secrets into per-service private files and exclusively provisions the
journal. PostgreSQL uses separate migration/runtime roles. Startup acceptance pins
all three application/tool image identities and the instance override hash.
Lifecycle commands serialize per instance and reject active/restarting writers.
Incomplete restores cannot be accepted by replay; normal replay cannot manufacture
migration acceptance. Unknown container modes fail closed.

## Executed verification

| Check | Result and scope |
| --- | --- |
| Frontend suite | 719 passed across 96 files; ESLint and TypeScript passed |
| Backend suite | 603 passed, zero skipped: 27 unit, 422 PostgreSQL integration, 154 provider contracts |
| Operator/tooling suite | 30 passed; initialization, private permissions, symlinks, credential preservation, archive corruption/traversal, release/restore guards, locks, writer states and command modes |
| Production images | API, Next standalone and tools images built; non-root runtime, no SDK/developer data, writable temporary storage and unknown-mode refusal checked |
| Fresh reference instance | New PostgreSQL volume; packaged preparation/migration; real HTTPS setup, local Admin/ordinary accounts and providers disabled |
| HTTPS account/catalog behavior | Secure/HttpOnly cookies, CSRF/foreign-Origin denial, forged ingress-header overwrite, image upload/decode/privacy and private ZIP export passed |
| Browser | Real Chromium through Caddy/Next/API, with the local CA imported into NSS and TLS checks enabled; login/private account, 1440/390px, no overflow or runtime exceptions |
| Database privileges | Current/future table DML succeeds as runtime; CREATE/ALTER/DROP refused; API/web/database not publicly published in the reference topology |
| Recreation/upgrade | Removed/recreated only test application containers; session survived using persisted keys; no-op release upgrade preserved user/item counts |
| Additive schema upgrade | Packaged bundle advanced a separate database from `AddAccountDataLifecycle` to `AddProfileModeration`; seeded category content fingerprint unchanged |
| Failed migration | Explicit injected failure invalidated startup acceptance; startup and ordinary replay refused; existing rows preserved |
| Concurrent maintenance | A live one-shot tools container and a separately held instance lock both prevented maintenance |
| Bootstrap credential diagnostics | Deliberate duplicate-role SQL failure initially exposed a generated test password in PostgreSQL statement logs; connection-scoped logging suppression fixed it, and a fresh bootstrap/failure scan found no generated credentials in diagnostics |
| Partial application replacement | Temporarily changed the frontend image tag only; startup refused the mismatched complete release, then the original tag was restored |
| Staff operations | Real HTTPS Moderator grant/revoke, current-session denial, Admin removal/recovery, last-Admin refusal and idempotent offline appointment passed; operator audit counts verified |
| Coordinated recovery | Database/artwork/key backup, current independent journal supplied separately, restored deleted account observed before replay, blocked startup, wrong-database refusal and repeated replay passed |
| Data after replay | Owner/private item erased; public ownership detached; held public item stayed hidden; profile report pseudonym stayed stable; unrelated Admin and staff audit retained |
| Restored HTTPS | Admin login, public artwork 200, private/held artwork 404 passed; an unmatched Host produced an empty edge response without proxying the app |
| Real ingress IP budget | 35 requests with varying forged forwarding/ingress headers produced 28 successful CSRF responses and 7 rate-limit responses; spoofing did not create new budgets |
| Incomplete restore | Injected failure during key extraction after database/artwork recovery produced no completion receipt; replay and startup refused, keeping the resurrected account isolated |
| Long export edge budget | Separate real Caddy/Next stack with a controlled HTTP upstream: TLS verified, client disconnect propagated, delayed export returned 504 at 300.01 seconds |

The long-stream test isolates transport behavior; it does not replace the real API
ZIP/export checks or establish representative load capacity. The additive upgrade
fixture proves the recorded schema transition, not every historical installation.
Private automated results and hashes are under `.local/selfhost-release-proof/`,
with other fixture records under `.local/selfhost-*` and temporary test logs/browser
artifacts under `/tmp/ratefreak-selfhost-*`. These local paths are not public release
artifacts. Disposable services/database volumes were removed after verification;
the original project database/container and real roles were unchanged.

## Artifact scope

Exact image identities and backup hashes for the complete recovery journey are
recorded in `.local/selfhost-release-proof/results.json`. Final defensive command
dispatcher hardening additionally requires an explicit maintenance action rather
than allowing an empty tools command to fall through to HTTP startup. Its regression
tests cover missing/unknown actions and preserve the recognized maintenance commands;
final image builds/runtime checks cover that update. Image identities describe their individual verification runs; Git history records
the integrated source revision. No public image release is claimed.

Test host: Linux amd64, Docker 29.9.2, Compose 5.7.0, Python 3.13.5. Images use
pinned Node 24, .NET 10, PostgreSQL 17 and Caddy 2 base manifests. Compatibility
with older Compose v2 releases, ARM, Windows-native containers, subpaths, remote
proxy/CDN chains and multiple replicas has not been established.

## Review and implementation decisions

Independent review identified incomplete-restore acceptance, writer-state races,
tools-only release acceptance and missing-artwork backup success. All four were
fixed with observed failing/passing regression tests. Archive fsync was also added
before backup success. No review finding remains deferred.

Implementation decisions, in order:

1. Kept the approved working tree on a feature branch rather than copying the
   uncommitted design/dependencies into a worktree. This preserves review context;
   it provides branch isolation rather than filesystem isolation.
2. Added explicit privileged `prepare` and UID-10001 runtime secret copies because
   bind-mounted operator-owned mode-0600 files are unreadable by the non-root app.
   Other user/permission models need deliberate adaptation.
3. Prepared separate UID-999 PostgreSQL secret copies for its initialization
   identity; these paths are specific to the pinned reference image.
4. Used positive 100ms Caddy flushing rather than negative low-latency mode, which
   suppresses early-disconnect cancellation. The real transport test verifies this
   choice at the cost of bounded flushing latency.
5. Left deletion unconfigured until explicit operator acknowledgment of independent
   journal protection/replication. The acknowledgment is not replication evidence;
   incorrect operator configuration can still violate the retention policy.
6. Added the authenticated fixed-HTTPS-origin API bridge for the private HTTP hop
   because ASP.NET antiforgery correctly refuses secure cookies on unrecognized
   HTTP requests. Public/remote plaintext links remain outside the supported trust
   boundary; no ordinary forwarded-protocol header enables the bridge.
7. Pinned tested base manifests and retained local source-build tags; security
   updates require explicit rebuild/reverification, not silent floating-base changes.
8. Allowed private instance overrides with acceptance hashes. Operators remain
   responsible for their own topology/configuration; only the reference was verified.
9. Added complete restore receipts, all-image acceptance and maintenance locking
   after review. Interrupted operations fail closed and may require a new isolated
   restore target, favoring preserved data over automatic repair.

## Remaining project and operator work

Before public release, choose the open-source license, review dependencies and
provider redistribution/attribution obligations, establish contributor/security
guidance and release automation/versioned distribution. No registry publication
or project-owned hosting choice is implied by this verification.

Operators must configure and verify off-host journal replication, encrypted or
otherwise protected durable storage/keys, actual maximum 30-day expiration for
every database/artwork backup copy, named operator records/365-day expiration,
monitoring, recovery objectives and capacity. Provider callbacks/credentials and
any topology outside the reference require their own acceptance. The tests do
not certify those properties for an installation we do not operate.
