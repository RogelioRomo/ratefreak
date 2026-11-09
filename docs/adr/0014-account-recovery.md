# ADR 0014: Saved-code password recovery

Status: approved direction, 2026-11-06. Supersedes earlier local-account launch
requirements for RateFreak email verification and email-delivered recovery.

Keep local email/password registration and optional Google/GitHub sign-in. Do not
verify local email addresses or send recovery emails. An email value is not proof
of account ownership; never use email matching to link or recover accounts.

Issue a random 128-bit saved recovery code on local registration. Existing local
users enroll, and enrolled users rotate codes, by confirming their current password
in account settings. Show plaintext only in the issuing response/browser save step;
store only a purpose/account-bound SHA-256 verifier in separate recovery state.
Users save codes in a password manager or offline. Replacing a code invalidates its
predecessor immediately. There is no administrative reset or security-question path.

Recovery accepts handle, saved code and new password. Under the account row lock,
consume/replace the verifier, hash the new password with Identity, clear login
lockout and revoke all sessions atomically. Return a replacement code and require
normal login. Failed recovery never changes password-login lockout. Authenticated
rotation rechecks the requesting session stamp after locking/reloading the user.
Registration, initial verifier and issuance event commit atomically.

Recovery POSTs require antiforgery protection and the existing proxy Origin guard.
Reset attempts have a process-local 10/IP/minute budget and a durable shared
5-failures/account/15-minute budget; password-confirmed rotation has a separate
5/account/minute HTTP budget and shares the durable failure window. Generic failure
responses conceal unknown accounts, invalid codes and exhausted account budgets.
Account existence is not asserted by recovery responses. Inputs are bounded;
all auth responses use no-store. Production perimeter/IP budgets remain required.

Keep recovery secrets/passwords out of URLs, logs, browser persistent storage and
TanStack Query caches. Browser save state survives recoverable background session
checks and is discarded on acknowledgement/navigation. A lost reset response means
the user should try their new password and, if it works, generate another code in
settings; never automatically retry or persist plaintext for redisplay.

External-only users recover through their existing providers; recovery codes do
not create local passwords for them. Provider identities, roles, profiles and owned
data are unchanged. Google/GitHub browser sign-in/linking was implemented in a
subsequent slice, with local user acceptance recorded on 2026-11-07; production
provider acceptance remains separate. See [current status](../STATUS.md).

If password and code are both lost, only an already linked provider can restore
access. Make this limitation explicit before users dismiss their code. Retention/
erasure/export policy remains a separate launch gate. Minimal account recovery
security events record account ID, action and UTC timestamp, without credentials
or actor IP. Account deletion cascades these events; time-based retention policy
must be settled before launch. Events are distinct from content moderation audit.

Recovery changes use an additive migration, with no automatic application at API
startup. Existing accounts have no recovery verifier until they enroll. Deployment,
HTTPS, Data Protection, backups, abuse and provider-rights gates stay open.
