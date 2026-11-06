# ADR 0015: One-time installation setup

Status: user approved secret-protected browser setup, 2026-11-06. Extends ADR 0011:
ordinary registration still never grants privileges; the initial installation setup
is a distinct one-time, deployment-authorized operation.

A fresh database has one pending `installation_state` row (fixed primary key 1 and
check constraint). Until completion, the API blocks all business/account/OAuth
requests, even direct calls and callbacks, before authentication handlers. Only
setup status, setup submission, CSRF and health checks are available. Missing state,
configuration or database access never reopens or bypasses setup.

The deployer supplies `Installation:SetupSecret` to the .NET API through user-secrets
or `Installation__SetupSecret` in the deployment secret store. Generate 32 random
bytes as exactly 64 hexadecimal characters. The secret is not a Next.js setting,
account password or provider credential. It is typed into a masked setup field,
compared as fixed-length decoded bytes in constant time, and is neither logged nor
persisted by the application. Missing/malformed configuration blocks setup with
operator guidance. Completed installations no longer require that secret.

GET `/api/v1/setup/status` returns only `{required,configured}`. POST `/api/v1/setup`
accepts `{setupSecret,email,password,handle,displayName}` with CSRF and proxy Origin
protection, bounded JSON, and 5 attempts/IP/15 minutes. Status reads allow 60/IP/minute.
These IP limits remain process-local; production perimeter budgets are still needed.
All setup and pre-setup blocked responses are no-store. Completed submissions return
409; incorrect secret/input 400; unavailable setup 503. Secrets are never echoed.

Creation takes the shared moderation advisory transaction lock before the singleton
row lock. In one transaction create the local account, saved recovery verifier/event,
Admin role/membership, operator-attributed `grant_admin` audit event and permanent
completion marker. Recheck pending state under the lock. Exactly one concurrent
request can succeed. Any persistence failure rolls everything back. Setup returns
the existing local-registration DTO including its one-time recovery code, with no
login. Normal registration remains an unprivileged operation.

The browser checks setup before mounting SessionProvider or normal navigation.
Pending installations use `/setup` with the existing account design and theme
controls. Unknown status shows loading/retry and no normal content. The recovery
save step survives background completion checks and requires acknowledgement before
continuing to login. Credentials never enter shared query/mutation cache or browser
persistent storage. Uncertain writes require checking status; if setup succeeded
but its response was lost, sign in and regenerate a recovery code in settings.

Installation status has a separate nonpersistent query client so session transitions
cannot remove it. After confirmed completion, stop automatic status reads and retain
normal app state on subsequent read errors; completion cannot be revoked. Before
completion, errors remain fail closed. The root Suspense boundary supports dynamic
pathname resolution with Next Cache Components.

Upgrade policy: the additive migration marks databases with any existing accounts
completed without promoting anyone or altering their data. An existing installation
without an Admin uses the existing offline `--appoint-admin` maintenance command.
Deleting/demoting the first admin, removing the secret, restarting or redeploying
never reopens setup. InitialAdminId is historical attribution without a cascading FK.
There is no HTTP reset/reopen operation. Schema rollback/database replacement is an
operator recovery operation requiring a reviewed plan, never a way to rerun setup.

Apply migrations before starting the new API. No startup migrations, real-account
promotion, provider activation or production deployment are implied by this feature.
