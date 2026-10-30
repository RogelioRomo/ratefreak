# External sign-in implementation plan

> **For agentic workers:** Use superpowers:executing-plans to implement task by task.

**Goal:** Add optional Google/GitHub login and explicit linking alongside passwords.
**Architecture:** ASP.NET OAuth handlers validate provider callbacks and issue an
external-only transient cookie. Application-facing endpoints consume validated
identities; a transactional Identity service owns registration/linking and login.
**Tech stack:** .NET 10, ASP.NET Core Identity/OAuth, EF Core/PostgreSQL, xUnit.
**Spec:** docs/superpowers/specs/2026-10-30-external-sign-in-design.md (approved).

## Global constraints

- Preserve existing password flows and application sessions; no automatic email linking.
- Providers disabled by default; no real credentials, email delivery or deployment.
- Immutable provider IDs, verified email for onboarding, explicit authenticated linking.
- Transactional registration, bounded transient state, CSRF, no-store and rate budgets.

## Review focus

- Session revoked/switched during linking must not attach to a different account.
- Provider subject already claimed must not create orphan users under concurrency.
- Missing/private/changed provider email must not break established identity login.
- Callback/ticket tampering, replay and expiry must not create application sessions.
- Provider failures and callback query values must not expose tokens or secrets.

### Task 1: Provider negotiation and callback validation

Files: Api/Security/ExternalAuthentication.cs, ExternalProviderOptions.cs,
ExternalIdentityReader.cs; Endpoints/ExternalAccountEndpoints.cs; integration tests.
Produces external principal with bounded provider/subject/email/verified claims;
protected properties carry purpose and linking account/stamp. Uses existing
IdentityConstants.ExternalScheme with five-minute expiry.

- [x] Write HTTP tests for discovery, disabled/unknown starts, CSRF and real OAuth redirects.
- [x] Run focused tests and observe missing routes fail.
- [x] Add provider configuration/handlers, fixed callback origins, PKCE and bounded lookups.
- [x] Test callbacks with fake upstream HTTP, invalid state, errors and provider email variants.
- [x] Run focused tests; commit the coherent feature with Task 2 after verification.

### Task 2: Onboarding, linked login and linking

Files: Application/Identity/ExternalAccounts.cs;
Infrastructure/Authentication/ExternalAccountService.cs; API endpoints; DbContext
unique login index and generated migration; integration tests.
Interface IExternalAccountService: FindLoginAsync(provider, subject),
RegisterAsync(identity, handle, displayName), LinkAsync(userId, stamp, identity),
ListAsync(userId). Results carry profile/status without tokens/subjects in public DTOs.

- [x] Write lifecycle and attack tests before service implementation; verify RED.
- [x] Implement pending state from external cookie, five-minute expiry, sanitized responses.
- [x] Implement transactional account creation/mapping; unique collisions return 409.
- [x] Implement session/stamp-bound repeat-safe linking and linked login lockout checks.
- [x] Add tests for email collisions, stale sessions, uniqueness races, malformed JSON,
  expiry/replay, isolation, lockout, shared budgets, no-store and password compatibility.
- [x] Generate/inspect migration and test preservation on apply/reapply/downgrade.
- [x] Run focused suite, full solution and commit.

### Task 3: Review, rollout documentation and verification

Files: README.md, .env.example, docs/adr/0010-external-sign-in.md,
docs/implementation-checklist.md.

- [x] Document exact routes, setup/callback URLs, cookie/proxy requirements and recovery limits.
- [x] Independent whole-branch security/correctness review; fix required findings with regressions.
- [x] Full tests, Release build, model consistency and dependency audit.
- [x] Record actual evidence, provider-disabled status and remaining activation requirements.
- [x] Commit and leave branch ready for user-directed merge; no push.

## Execution ledger

Native execution in existing checkout on dedicated feature branch, consistent with
prior slices. User approved the design and explicitly requested proceeding.

- Pre-flight: Task 2 consumes Task 1's validated principal and protected purpose;
  purpose/provider/actor/stamp fields are shared through the external cookie.
- Initial 7 missing-route HTTP tests failed; expanded initial 18 callback/lifecycle
  tests also failed before implementation. Basic flows then passed 18/18.
- Ruling: add PostgreSQL one-time challenge IDs/expiry — copied external-cookie
  replay reproduced sign-in after logout; atomic consumption now rejects it.
  Cost: an extra small table/write on starts, cleanup on subsequent starts.
- Ruling: native execution and one final reviewer in the existing checkout on a
  dedicated branch, matching earlier slices; no additional worktree created.
- Regression: stamp changed between session validation and link SQL returned 200;
  EF identity resolution retained tracked state. Reload under row lock returns 401.
- Regressions: framework error-description logging leaked sentinel and optional
  GitHub email HTTP failure blocked identity login; category filtering and treating
  email lookup as optional resolved both. Verified focused suite 44/44.
- Review: independent static review found case-sensitive callback guard bypass;
  disabled-provider and exhausted-budget mixed-case tests failed before fix.
  Guard now uses PathString matching, identical to OAuth middleware.
- Review suggestion implemented: same subject/different verified emails in concurrent
  onboarding directly exercises provider-mapping conflict rollback.
- Tasks 1–3 complete: full suite 522/522 (64 unit, 154 provider, 304 integration),
  zero failures/skips, Release build zero warnings/errors, EF model consistency,
  local migration apply/reapply and fresh all-project transitive audit pass.
- Final review finding fixed: mixed-case guard regression RED→GREEN in full suite.
  No remaining required findings or deferred review minors.
- Local 5081 smoke passed health/readiness/Swagger/OpenAPI/discovery/error checks;
  temporary server stopped, no accounts created, providers remain disabled.
