# Account recovery without email delivery

Status: approved by user, 2026-11-06. User selected recovery codes and wants
Google/GitHub to remain optional sign-in methods. No email verification or email
delivery is to be added.

## Intent and scope

Let local-password users recover access using a saved secret, without relying on
unverified email addresses or administrator judgment. Preserve existing account IDs,
owned data and optional provider identities. This slice covers backend contracts,
persistence, browser proxy, registration, account settings and reset UI.

Google/GitHub backend support already exists but is disabled by default; frontend
sign-in/linking and live provider activation were previously deferred. Preserve
that support. Completing browser OAuth sign-in/linking is the next planned slice after recovery; credentials and
actual activation require provider app configuration. Provider email assertions
remain governed by the existing external-account contract; this does not add
RateFreak email verification.

## Approaches

The user selected self-service saved recovery codes following a comparison with
admin-assisted resets and previously linked external sign-in. Codes avoid email
infrastructure and manual ownership decisions. The cost is that users must retain
the code. No administrator password override or security-question fallback is added.

## Proposed behavior

- New local registrations return one recovery code alongside the existing profile.
  Registration still does not sign the user in. Show a dedicated save-code step
  before navigating to login, with copy/download and clear loss-of-access guidance.
- Existing local users can create or replace their code in account settings after
  entering their current password. Replacement immediately invalidates the old
  code. Requiring the password protects against a stolen session taking over recovery.
- Forgotten-password UI accepts the public handle, recovery code and a new password.
  Existing email-based password login is unchanged. A successful reset consumes the
  code, returns a replacement to save, revokes every session and requires login.
- Code rotation and reset never merge accounts or change email, handle, roles or
  provider links. External-only accounts cannot acquire a local password using
  this flow; their recovery remains with an already linked provider.
- Without the password, saved code or previously linked sign-in method, access
  cannot be restored. Explain this before users dismiss their recovery code.
- Unverified email is contact/account data, not ownership proof or a recovery channel.

## Security and persistence

Builder defaults, proposed here for review:

- Generate a cryptographically random 128-bit secret, with a readable grouped
  encoding and unambiguous normalization. Store only a purpose-bound verifier;
  never persist or log the raw code. Code generation uses platform cryptography.
- Use real PostgreSQL transaction/row locking so consuming or replacing a code,
  changing the password and updating the security stamp are atomic. Concurrent
  replay permits at most one success. Keep provider-link/session operations safe
  under the existing account locking discipline.
- Add an additive migration for recovery storage. Existing users begin without a
  code and obtain one through authenticated password confirmation. Do not mint
  undisclosed codes during migration.
- Require CSRF/origin protection for all mutations, no-store on sensitive responses,
  bounded input and coordinated account/IP attempt limits. Unknown handles and
  invalid/consumed codes have the same public failure response. Failed recovery
  does not lock normal password login. Respect the existing multi-instance abuse
  release gate; do not claim process-local limits solve it.
- Keep plaintext codes/passwords out of URLs, analytics, logs, browser persistent
  storage and shared query/mutation caches. Retain a displayed code only in local
  component memory until dismissal/navigation. Do not automatically sign in after reset.
- Record recovery issuance/replacement/reset security events without secrets; keep
  them distinct from content-moderation audit records. Choose a minimal persistence
  contract during planning, documenting retention as an outstanding policy gate.

## Verification and release implications

Prove registration/enrollment, replacement, invalid/replayed codes, concurrent
consumption, password policy, all-session revocation, authorization, CSRF/origin,
rate limiting and no-store behavior with backend tests using real PostgreSQL for
state/concurrency. Verify frontend save/reset/settings flows, error handling and
sensitive-data lifetime; run lint, typecheck, relevant suites and production build.
Perform a dedicated local signed-in browser journey with guarded cleanup.

Update account/release docs to describe the no-email policy and lost-code outcome.
Do not mark deployment, retention/deletion, backups, provider rights or operational
release gates complete. No automatic commit, merge, push, deployment, provider
activation or elevation of real accounts is authorized by this design.

## Builder review resolutions

- Account creation and initial recovery-verifier persistence commit atomically.
- Authenticated replacement locks/reloads the user and rechecks the requesting
  session security stamp before password verification, following existing linking
  concurrency safeguards.
- Successful recovery clears password-login lockout atomically; failed recovery
  never changes login lockout state.
- If a successful reset response is lost, the user should try the new password and
  regenerate a code in settings. No automatic reset retries or server-side
  plaintext retention; explain this outcome in the reset UI.
- This slice adds recovery-specific per-account and per-IP limits within existing
  runtime infrastructure. Distributed/perimeter enforcement remains a production
  gate, not an implicit infrastructure expansion.
