# ADR 0010: Additional GitHub and Google sign-in

Status: accepted, 2026-10-30. User approved direct integration and written design.

Use server-side ASP.NET Core OAuth handlers and existing Identity/session cookies.
Keep password registration/login. Avoid adding an email-delivery dependency or a
hosted authentication service. The original deferral of password recovery was
superseded by [ADR 0014](0014-account-recovery.md): saved-code recovery is
implemented and local email verification/recovery mail are intentionally excluded.
Browser integration and local provider acceptance were completed later; deployed
acceptance remains open. See [current status](../STATUS.md).

Provider identity is immutable (provider, subject), never email or username. Existing
users explicitly link while authenticated; matching email never signs into or merges
an account. New external accounts require a provider-verified email, then choose a
unique RateFreak handle/display name. They have no local password. Linking preserves
the target email, password, account ID and all owned data. One identity per provider
per account. Do not add unlinking until last-method protection and recovery policy
are designed.

OAuth state/correlation and PKCE protect callbacks. Short-lived external cookies
convey validated identities without granting normal access. A server-side challenge
is consumed atomically once before login/link completion; copied external cookies
cannot bypass logout by replaying completion. Challenge rows contain only ID/expiry,
are removed on completion and cleaned after expiry during new starts. They live in
PostgreSQL so replay protection is consistent across API instances/restarts.
Pending onboarding lasts at most five minutes; registration uniqueness/transaction
guards reject repeated/competing claims without orphan accounts.

Linking validates original account/session security stamp and holds the user row lock
through attachment. Reload tracked user state under the lock because EF identity
resolution otherwise preserves values read during prior session validation.

Providers are independently disabled by default. Activation requires app registration,
credentials and a canonical public origin. No live provider activation or deployment
is implied by implementation. Recovery for an external-only account depends on its
provider (or another explicitly linked provider); no administrative recovery shortcut
or email-based account takeover path is introduced.
