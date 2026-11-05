# Frontend release verification

Date: 2026-11-05. Local verification passes; public-launch approval remains pending.
This record covers the current checkout, including uncommitted staff moderation.
It does not assert that these changes are committed, deployed or production verified.

## Evidence from this pass

| Check | Result |
| --- | --- |
| Frontend tests | 588 passed across 84 files |
| Full backend suite | 591 passed, zero skipped: 64 unit, 373 real PostgreSQL integration, 154 provider contract |
| ESLint and TypeScript | Both exited 0 |
| Next production build | Exited 0; all application and proxy routes generated |
| API liveness and readiness | `/health/live` and `/health/ready` returned 200 |
| Real catalog through Next proxy | `/api/v1/media` returned 200 |
| Live Chromium navigation | 11 routes rendered without application errors |
| Responsive checks | All 11 routes fit at 320, 768, 1024 and 1440px |
| Keyboard | Tab selected Skip to content; Enter focused main |
| Authorization and URL guards | Anonymous account/staff API reads 401; admin proxy 404; invalid/duplicate catalog page 400 |
| Cache boundary | All five authorization/filter probes returned `Cache-Control: no-store` |
| Browser runtime exceptions | None observed |

Browser routes: Home, catalog list, existing catalog detail, provider discovery,
collections, login, registration, library, account, moderation queue and audit.
Account redirected to login; private library and moderation showed login guidance.
This was an anonymous cross-feature smoke test, not a new signed-in write test.
Prior signed-in flow evidence, concurrency, role revocation, responsive theme
captures and guarded cleanup are recorded in the frontend checklist.

Browser traffic used Chromium → Next on port 3100 → the running Development API
on port 5080 → local PostgreSQL. There was no response interception, bundled demo
data or synthetic API response. This pass made no account/content writes and
created no identities, privileges or catalog records. Existing catalog content
was read only. Backend integration tests use separate disposable real PostgreSQL
containers; unit/contract tests retain their existing mocked boundaries.

Temporary reproducibility artifacts are `/tmp/release-browser.mjs`,
`/tmp/release-browser-results.json` and `/tmp/release-health-*.json`; these are
local evidence, not committed automation. They require the local running services
and Chromium CDP on port 9222. They contain no credentials.

## Outstanding release gates

These remain open under the existing [backend launch requirements](implementation-checklist.md)
and [deployment direction](adr/0001-deployment-and-authentication.md).

| Gate | Evidence needed before launch |
| --- | --- |
| Local account ownership and recovery | An accepted policy for unverified emails, lost passwords and account retention/deletion; external sign-in is optional and does not settle local-password recovery |
| Real production ingress | Deployed HTTPS origins, exact write Origin, matching server secrets and verified Vercel address headers, or a separately verified adapter for another host |
| Production sessions | Real `__Host-` Secure/HttpOnly cookies, independent Set-Cookie forwarding/deletion, CSRF rejection, all-device logout and no private response caching through the actual ingress |
| Durable Data Protection | Restricted persistent key storage, encryption/protected storage, restart and replica continuity, and controlled key recovery |
| Database recovery | Protected backups and a successful isolated restore drill with migration/version checks; no production rollback without a reviewed recovery plan |
| Abuse controls | Coordinated/perimeter budgets for multiple API instances; current rate limiters are process-local |
| Provider rights and attribution | Approved metadata/artwork storage and usage terms, required notices/logos, and commercial licensing where applicable |
| Operational checks | Private health monitoring, sanitized logging, secret rotation, representative load testing and deployed client-disconnect propagation |
| Deployed end-to-end acceptance | Dedicated ordinary/staff accounts exercising the complete journey on the actual candidate release, with recorded cleanup |

Do not bridge Development and Production by weakening Secure cookies, enabling
loopback attribution in production, trusting arbitrary forwarding headers or
resetting abuse limits. The production build proves compilation; local HTTP
Development checks do not prove the deployment gates above.

## Repeating local checks

From the repository root, with installed dependencies and Docker available:

```bash
pnpm --dir web lint
pnpm --dir web typecheck
pnpm --dir web test
pnpm --dir web build
dotnet restore RateFreak.sln
dotnet test RateFreak.sln --no-restore -m:1
git diff --check
```

Run live browser checks against the API using the private local configuration
described in [web setup](../web/README.md). Never copy secret values into this
record. A Next build can disrupt an already-running dev server's generated files;
restart the dev server before further browser checks if necessary.
