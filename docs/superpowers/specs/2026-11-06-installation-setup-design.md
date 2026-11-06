# One-time secret-protected installation setup

Status: user approved secret-protected recommendation on 2026-11-06. This document
records that flow and implementation defaults for review and execution.

## Intent

A newly deployed RateFreak installation must create its first account as Admin
before the application can be used. Once complete, setup never reopens and ordinary
registration always creates ordinary accounts. The API owns this decision.

## Approaches and decision

User selected browser setup protected by a deployment secret. Unprotected first
visitor registration would let strangers claim a fresh public deployment. The
existing offline admin-appointment command remains operator maintenance, not the
first-run browser experience. No automatic promotion based on email or user count.

## Persistent state and migration

Add a singleton installation-state row with completion timestamp and optional
initial-admin UUID. An additive migration creates pending state for an empty users
table and completed legacy state if any accounts already exist. This prevents
upgrades of existing databases from opening a new admin-creation route, without
promoting any existing user. This default preserves this project's existing users.
A missing singleton row or database error fails closed; it is never interpreted as
fresh setup. Completion is independent of account deletion, role changes, secrets,
restarts, API replicas and redeployments. No HTTP reset/reopen operation exists.

## API and secret

- GET /api/v1/setup/status returns only {required, configured}; configured indicates
  whether an installation secret is usable while setup is pending. No secrets or
  account information. Completed installs return required=false regardless of secret.
- POST /api/v1/setup accepts setupSecret plus the same local registration fields.
  Requires CSRF, bounded JSON, rate limiting and the existing proxy Origin boundary.
- Configure Installation:SetupSecret server-side with exactly 64 hex characters
  generated using platform cryptography. Never send it to Next/client configuration,
  return it in responses, log it, place it in URLs or store it in browser storage.
  Compare fixed-length decoded secret bytes in constant time. Missing/malformed
  configuration leaves setup blocked with operator guidance, never ordinary signup.
- Under the singleton row lock, recheck pending state and verify the secret; create
  the user, recovery verifier/issuance event, Admin role/membership, admin audit event
  and completion marker in one transaction. Use the existing moderation advisory
  lock before account/state locks to coordinate with offline role maintenance.
  Any failure rolls back all state. Concurrent submissions have exactly one winner.
- Return the normal local-registration response including the one-time recovery code;
  do not automatically log in. Later setup POSTs are refused even with a valid secret.
- Until setup completes, API middleware blocks application/account/OAuth endpoints,
  including normal registration. Allow only setup status/submission, CSRF, and
  health liveness/readiness. Guard runs before authentication/OAuth handling; no
  unknown-path or direct-API bypass. Every setup/blocked response uses no-store.
  Setup IP budgets remain process-local, preserving the production perimeter gate.
- Health remains usable for deployment readiness. Non-API development documentation
  and static requests are unaffected; they cannot bypass the guarded business APIs.

## Browser flow

Wrap the existing application in a setup-status boundary: loading/error states
never render normal content. Pending installs show a standalone setup page at
/setup, without normal navigation/sign-in/signup; navigation to other app paths
returns to setup. Use the existing editorial auth panel, inputs, themes and buttons.
The form asks for the deployment secret, display name, handle, email and password.
Explain first-admin privileges and saved-code recovery without email verification.
Use password input for the secret and keep all submitted credentials out of query
mutation state, persistent browser storage and errors.

After creation, keep the recovery save step visible until acknowledged; background
status checks must not discard it. Then clear the secret/code from component memory,
refresh setup/session state and continue to login. Completed installs redirect /setup
to the home page. Stale setup tabs refresh status after a completed/conflict response.
Preserve fail-closed loading/retry and unavailable configuration guidance.

## Validation and scope

Real PostgreSQL tests cover empty/legacy migration, first-admin authorization,
ordinary registration denial before setup and no privilege afterward, correct/invalid/
missing secrets, CSRF/no-store/budgets, concurrent setup, rollback, persistent closure,
missing state, configuration changes and external callback bypass. UI/proxy tests
cover gating, secret lifetime, save-code acknowledgement, stale tabs and retry.
Run full backend/frontend suites, lint/typecheck/build and dedicated live-browser
fresh-install verification with guarded disposable database cleanup.

Document secret provisioning, fresh/legacy behavior, existing offline admin
maintenance and outstanding production gates. No real user is promoted, and no
existing database migration, commit, merge, push, deployment or provider activation
is included unless separately requested. Google/GitHub remain optional later browser
integration; setup's first admin is a local-password account with a recovery code.


## Builder/review resolutions

- Extract transaction-free local creation, keep setup boundary outside session handling,
  and constrain singleton ID to 1 without an account-deletion FK cascade.
- Existing integration fixtures explicitly represent completed installations; fresh
  setup tests use separate disposable databases with the real guard.
- After confirmed permanent completion, disable automatic setup-status refetches
  and preserve normal application state even if a manual status check fails.
  Before completion, unknown/error state remains fail closed; unsaved recovery
  display has precedence over status changes.
- Root boundary uses Suspense for Next Cache Components dynamic pathname resolution.
