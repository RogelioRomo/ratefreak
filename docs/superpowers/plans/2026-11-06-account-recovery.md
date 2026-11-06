# Account Recovery Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [x]`) syntax for tracking.

**Goal:** Recover local passwords using saved codes without email delivery.
**Architecture:** .NET owns secrets, transactions and authorization. Next proxies allowlisted routes and displays codes only in component memory. PostgreSQL serializes account recovery against linking/logout/profile changes.
**Tech Stack:** .NET 10 / Identity / EF Core / PostgreSQL; Next.js / React / TanStack Query.
**Spec:** docs/superpowers/specs/2026-11-06-account-recovery-design.md

## Global Constraints

- No email verification, email delivery, admin overrides or automatic login after reset.
- Cryptographically random 128-bit codes; store only purpose-bound verifiers.
- Additive migration; existing accounts enroll after confirming their password.
- Preserve accounts, provider links and content; optional browser OAuth is the next separate slice.
- No commit, merge, push, deployment, provider activation or real-account elevation.

## Review Focus

- Lost reset responses: new password works; regenerate code after login, never auto-retry.
- Concurrent reset/replacement/logout: one code consumer and no stale-session enrollment.
- External-only users: no local-password creation through recovery.
- Registration/reset secrets: absent from profile reads, persistent browser state, logs and query caches.
- Abuse: bounded inputs, account/IP budgets, no account-existence disclosures or login-lockout denial of service.

### Task 1: Atomic recovery API and persistence

**Files:** Application/Identity/AccountRecovery.cs; Infrastructure/Authentication/{AccountRecoveryService,RecoveryCode,AccountRecoveryEvent}.cs; AccountUser.cs; AccountService.cs; Persistence/RateFreakDbContext.cs and additive migration/snapshot; DependencyInjection.cs; Api/Endpoints/AccountRecoveryEndpoints.cs; Api/Security/ApiSecurity.cs; Program.cs; IntegrationTests/AccountRecoveryTests.cs.
**Interfaces:** Registration returns AccountProfile fields plus recoveryCode (preserve existing profile shape). POST auth/recovery-code accepts {password}, requires session/stamp+CSRF, returns {recoveryCode}. POST auth/reset-password accepts {handle,recoveryCode,password}, returns {recoveryCode}. GET auth/recovery-status requires session, returns {hasPassword,hasRecoveryCode}. IAccountRecoveryService.GenerateAsync(Guid,string,GenerateRecoveryCodeRequest,CancellationToken); ResetAsync(ResetPasswordRequest,CancellationToken); StatusAsync(Guid,CancellationToken).

- [x] Add integration tests for registration secret/hash, reset/password/login/session revocation, rotation, no-code legacy enrollment, provider-only rejection, generic failures, password validation, replay/concurrency, CSRF and no-store.
- [x] Run `dotnet test tests/RateFreak.IntegrationTests --filter FullyQualifiedName~AccountRecoveryTests` and observe missing behavior.
- [x] Implement contracts, 16 random bytes encoded as grouped hexadecimal, purpose/account-bound SHA-256 verifier with constant-time comparison, atomic row-locked mutations and security events; add migration.
- [x] Add recovery IP policy (10/minute), authenticated replacement (5/minute/account), and durable account attempt budget (5 failed attempts/15 minutes) checked under the user lock. Failed requests never affect normal login lockout. Successful reset clears lockout and revokes sessions. Registration verifier/event commit with account creation.
- [x] Add budget, concurrency, stale-stamp and rollback tests; run focused integration tests plus existing accounts/external/profile concurrency tests. Expected: all pass.

### Task 2: Proxy boundary

**Files:** web/server/api/route-policy.ts; web/server/api/proxy.test.ts.
**Interfaces:** Exact new routes from Task 1 only, no query parameters; existing origin/CSRF/cookie/no-store enforcement.

- [x] Add proxy tests for recovery POST/GET routing, methods, queries, origin rejection, forwarding and no-store.
- [x] Run focused Vitest and observe failure for new routes.
- [x] Extend allowlist; run proxy tests. Expected: pass.

### Task 3: Recovery forms and secret lifetime

**Files:** web/features/auth/interfaces/{registration-response,recovery-code-response,recovery-status,reset-password-request,generate-recovery-code-request,recovery-code-display-props,recovery-editor-props}.ts; api/accounts.ts; hooks/use-register.ts; components/{register-form,login-form,account-details}.tsx; focused recovery components/tests/CSS; web/app/reset-password/page.tsx.
**Interfaces:** register returns AccountProfile & {recoveryCode:string}; useRegister accepts a completion callback and discards sensitive data before returning from mutationFn. Recovery forms use useSensitiveMutation (void result, retry false) and local React state for returned code.

- [x] Add UI tests proving save step before login, enrolled/legacy/provider account settings, reset failure/success, dismissal, password/code clearing and no sensitive mutation-cache data.
- [x] Run focused Vitest and observe missing behavior.
- [x] Build accessible forms using current auth panels/fields/buttons; copy/download code and explicit saved acknowledgement before dismissal; provide password confirmation and lost-response guidance. Preserve current design and navigation.
- [x] Run focused tests, then lint/typecheck. Expected: pass.

### Task 4: Verification and documentation

**Files:** docs/adr/0014-account-recovery.md; README.md; docs/frontend-release-verification.md; docs/frontend-checklist.md; approved design/plan status.

- [x] Run full backend/frontend suites, lint/typecheck/build and diff check; inspect outputs.
- [x] Run dedicated local browser enrollment/reset/replay/login journey with guarded cleanup if services are available; record actual result and limitations.
- [x] Fresh review of security and scope, fix material findings with regression tests.
- [x] Record no-email policy, API contracts, saved-code loss/rotation and remaining release gates; mark completed tasks and evidence, no production claims.


## Execution evidence

Completed 2026-11-06 on `feat/account-recovery-codes`, with no automatic commit,
merge, push or deployment. Full suites: 600 frontend tests; 606 backend tests
(64 unit, 388 PostgreSQL integration, 154 provider contract), zero failures/skips.
Lint, typecheck, production build and diff check pass. Real Chromium recovery
journey and 16 state/width checks pass; dedicated processes/container removed.
Fresh review found unsaved-code loss on session-read errors and an incomplete
registration OpenAPI schema; both fixed with failing/passing regression evidence.
See `docs/frontend-release-verification.md` for policy, migration and release limits.


Follow-up: user explicitly requested applying the local database migration and
committing the changes. Migration applied and verified against the existing project
database, preserving counts (2 users, 8 media, 0 collections) and leaving existing
users unenrolled. No merge, push or deployment authorized.
