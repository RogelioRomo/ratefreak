# Installation Setup Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [x]`) syntax for tracking.

**Goal:** Secret-protected first-run creation of exactly one initial Admin.
**Architecture:** Persistent PostgreSQL singleton, API-wide setup guard, one transaction for local account/recovery/admin/audit/completion; Next installation boundary outside session handling.
**Tech Stack:** Existing .NET/Identity/EF/PostgreSQL and Next/React/TanStack Query.
**Spec:** docs/superpowers/specs/2026-11-06-installation-setup-design.md (approved user flow, builder-reviewed defaults).

## Global Constraints

- 64 hexadecimal character server-only Installation:SetupSecret; no email verification/delivery.
- Pending fresh database blocks business APIs and ordinary registration; legacy databases never auto-promote users.
- Completion survives deletion/demotion/restart/replicas and never reopens through HTTP.
- Secrets/code/passwords remain out of logs/URLs/persistent browser storage/query mutation state.
- No real account promotion, existing database migration, commit, merge, push or deployment without separate instruction.

## Review Focus

- Two simultaneous setup requests must create exactly one user/admin/recovery/audit/completion.
- Existing accounts without Admin must remain usable without exposing public bootstrap.
- Missing singleton/secret/database must fail closed, including direct APIs/OAuth callbacks.
- Failed storage must roll back all user/role/recovery/marker changes.
- Background status reads and stale tabs must preserve unsaved recovery codes and never expose normal content early.

### Task 1: API, persistence and migration

**Files:** Application/Installation/Installation.cs; Infrastructure/Installation/{InstallationState,InstallationService,InstallationOptions}.cs; Authentication/LocalAccountRegistration.cs and AccountService.cs; Persistence/RateFreakDbContext.cs/migration/snapshot; DependencyInjection.cs; Api/{Middleware/InstallationGuard,Endpoints/InstallationEndpoints}.cs; Program.cs; Security/ApiSecurity.cs; IntegrationTests/{InstallationTests,AccountDatabase,ApiFactory}.cs.
**Interfaces:** IInstallationService.StatusAsync(CancellationToken) -> InstallationStatus(bool Required,bool Configured); CompleteAsync(SetupRequest,CancellationToken) -> SetupResult(SetupOutcome,LocalRegistrationResponse?,errors?). GET setup/status; POST setup.

- [x] Add real PostgreSQL tests for initial state, guards, secrets, first admin/recovery, ordinary registration afterward, permanent closure, CSRF, concurrency and legacy/missing-state behavior; observe RED.
- [x] Extract transaction-free local creation helper reused under existing registration transaction and setup transaction, preserving response/error behavior.
- [x] Add fixed-key constrained singleton and additive migration: pending if users empty, completed otherwise; no FK cascade to initial admin. Schema downgrade is an operator-only action requiring a reviewed recovery plan.
- [x] Implement secret configuration/constant-time verification, shared moderation lock then singleton lock, atomic admin role/audit/completion and all API guards before OAuth/session handling. Only setup/status, setup POST, CSRF and health are allowed during pending state.
- [x] Explicitly initialize existing AccountDatabase fixtures as completed; DB-free endpoint tests use a test-owned completed-status implementation, with separate real-guard tests.
- [x] Prove generic failures, rate limits, rollback, restart/secret changes, migration legacy behavior and missing-state fail closure. Focused tests and existing auth tests pass.

### Task 2: Browser boundary and setup form

**Files:** web/features/setup/{interfaces,api,components}/*; app/setup/page.tsx; app/layout.tsx; server/api/route-policy.ts and proxy tests; focused setup UI tests.
**Interfaces:** setup/status query -> {required,configured}; POST setup accepts registration fields plus setupSecret and returns LocalRegistrationResponse. Setup form callback receives recovery code only, mutation returns void.

- [x] Add failing UI/proxy tests for pending/completed/unavailable gates, methods/origin/query guards, successful save-code flow, sensitive-cache exclusion and stale/setup-background checks.
- [x] Build installation boundary outside SessionProvider. Pending shows standalone themed auth-panel setup form, no normal navigation. Normal children mount only after confirmed completion; /setup redirects home for completed installs.
- [x] Preserve unsaved recovery-code step above status rendering, require acknowledgement then refresh installation/session state and navigate to login. No automatic login. Handle concurrent completion by refreshing status; uncertain writes suggest login/recovery, never auto-retry.
- [x] Keep interfaces separate, inherit account design and reuse recovery save component; tests/typecheck/lint pass.

### Task 3: Whole-feature verification and operating docs

**Files:** README.md, web/README.md, .env.example, docs/adr/0015-installation-setup.md, docs/frontend-release-verification.md, docs/frontend-checklist.md, this plan/spec.

- [x] Full backend/frontend suites, lint/typecheck/build/diff checks.
- [x] Fresh security/concurrency review, fix material issues with regression tests.
- [x] Real browser fresh-install journey through dedicated Next/API/disposable PostgreSQL, first-admin and ordinary-user checks, responsive captures, cleanup.
- [x] Document deployment-secret generation/provisioning, fresh/legacy behavior, existing offline admin maintenance, schema migration and release limits. Record results and leave changes uncommitted.


## Execution evidence

Completed 2026-11-06 on `feat/installation-setup`, leaving changes uncommitted and
existing accounts/database unchanged. Final full suites: 616 frontend tests across
87 files; 618 backend tests (64 unit, 400 integration, 154 provider contract), zero
failed/skipped. Lint, typecheck, production build and diff check pass. Fresh review
material finding (draft loss after completed-status refetch failure) fixed with a
failing/passing regression; no deferred findings. Unreachable-database wrapper
handling fixed after an observed regression, then full suite passes. Real Chromium
first-admin/ordinary-user journey and 16 responsive Light/Dark checks pass. Dedicated
processes/database removed and temporary test deployment secret deleted. See release
verification and ADR 0015 for operating instructions and remaining release limits.
