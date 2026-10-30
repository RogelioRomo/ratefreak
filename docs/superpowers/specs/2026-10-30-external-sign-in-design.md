# GitHub and Google sign-in

## Intent and approved direction

Add GitHub and Google as additional sign-in options alongside existing local
email/password accounts. Keep ASP.NET Core Identity and existing application
cookies, ownership rules and logout-all-session behavior. No email delivery
service, verification mail or password-reset flow is included. Users must link
an external identity while they can still authenticate to their account; this
feature does not recover a forgotten-password account using matching email.

## Approaches

The user selected direct integration into existing Identity rather than a hosted
authentication service. Direct integration preserves existing account IDs and
session controls, with no additional authentication vendor. A hosted service
would add provisioning and account/session migration responsibilities. Retaining
only passwords would not provide the requested additional options.

## Provider identity and configuration

Use server-side authorization-code flows with state/correlation validation and
PKCE. Prefer supported ASP.NET Core authentication handlers: Google handler and
the built-in OAuth handler configured for GitHub. Secrets and access tokens must
never reach browser JSON, URLs under our control, persistent account records or
logs. Request only identity/email permissions; discard tokens after profile lookup.
Use fixed official HTTPS endpoints, bounded timeouts and response sizes for custom
GitHub profile/email requests. Never follow arbitrary profile URLs.

Both providers are disabled by default and enabled independently under
Authentication:External:Google / GitHub, with client ID and secret in server-side
secret configuration. Missing enabled credentials fail sanitized startup validation.
Return only provider names/enabled status in public discovery. Disabled starts
return 503; unknown provider names return 404. Never fall back to another provider.

Provider identity is (provider name, immutable provider subject), enforced uniquely
by Identity's existing user_logins table. Google subject and GitHub numeric user ID
are authoritative; display names, emails and GitHub login names are not identifiers.

## Browser/API contracts

GET /api/v1/auth/providers exposes configured availability without secrets.
POST /api/v1/auth/external/{provider}/start initiates sign-in and returns an OAuth
redirect. Require CSRF even for anonymous initiation; reject authenticated sign-in
starts (409) to avoid accidentally switching the current account.

POST /api/v1/users/me/logins/{provider}/start initiates linking for an authenticated
account with CSRF. Bind the original account ID and security stamp into protected
flow properties and validate them on completion against the current authenticated
session and database. A changed, revoked or missing session cannot finish linking.

Register middleware callback paths /api/v1/auth/external/google/callback and
/api/v1/auth/external/github/callback. An internal completion endpoint consumes a
short-lived external cookie after middleware validates the callback. Callback
errors/cancellation produce sanitized API errors, clear transient cookies and
never issue an application session. No caller-supplied return URL is accepted in
this first API slice; completion returns fixed API JSON, suitable for browser and
future frontend integration. Deployment must use configured canonical HTTPS origin
and trusted proxy configuration; local callbacks use explicit localhost origins.

For an already linked identity, completion signs in its account with the ordinary
nonpersistent eight-hour application session, subject to Identity lockout policy,
and returns the existing private account profile. Re-check account existence and
security state before issuing the cookie. Clear transient authentication state.

For a new identity, completion returns an onboarding-required response. Keep only
validated provider identity/email in a protected HttpOnly temporary cookie, expiring
after five minutes. It grants no normal account access. GET
/api/v1/auth/external/pending returns onboarding status and safe suggested profile
fields; POST /api/v1/auth/external/register accepts only handle and displayName,
requires CSRF, and atomically creates a passwordless account and its login mapping.
Use existing handle/displayName rules. New accounts receive normal application
sessions only after successful completion. Duplicate handles return 409 and allow
retry within the pending lifetime. Concurrent/replayed completion must never
create orphan accounts or more than one mapping; duplicate identity returns 409
and requires a fresh sign-in. Clear pending state on success or terminal failure.

## Email and linking policy

Current schema requires a unique bounded email. New external accounts therefore
require a provider-verified valid email: Google's verified email claim or GitHub's
verified primary email from its authenticated email endpoint (including private
email). Missing/unverified/invalid emails fail onboarding with a sanitized error;
do not request a manually supplied email, invent placeholders or silently trust a
public profile email. Preserve verified status for new external accounts only.

An email collision never links or signs into the existing account. Return a generic
conflict instructing the user to sign in to the existing account and explicitly
link the provider. Do not mark an existing local email verified or change it when
linking. Existing linked identities keep working if their provider email changes
or becomes unavailable; login depends on immutable provider identity. Linking
itself does not require an available email because the target account exists.

Explicit linking atomically attaches a login to the original authenticated account.
A login already attached to that account is repeat-safe; a login attached elsewhere
returns 409 and is never moved. Allow at most one identity per provider per account,
enforced by a new unique database index (user_id, login_provider), if absent.
No email-based automatic merging or transfer of libraries, reviews or other data.
GET /api/v1/users/me/logins returns provider names and whether a local password
exists, not provider subjects/tokens. Unlinking and setting passwords are deferred:
this slice cannot accidentally remove an account's last authentication method.

## Security and persistence

All matched authentication/link responses use no-store, including errors and
redirects. Preserve strict JSON validation, CSRF for mutations, bounded body sizes,
rate limits and existing private DTO projection. Give external starts, callbacks
and onboarding explicit abuse budgets; authenticated linking uses an account budget.
Correlation and temporary cookies are HttpOnly, short-lived, with appropriate
SameSite policies for top-level cross-site OAuth redirects and Secure in production.
Protect tickets using existing environment-scoped Data Protection keys. Limit
external claim lengths before creating tickets. Re-check pending expiry server-side.
New security-sensitive mutations must be transactional; translate known unique/FK
races into sanitized conflicts/failures, never generic successful login.

Migration is additive: existing duplicate provider/account pairs cause index
creation to fail transactionally rather than deleting mappings. Add a technical
challenge table containing only random flow IDs/expiry, consumed atomically before
login/link completion to reject copied-ticket replay across instances/restarts.
Clean expired challenges during new starts. Down removes transient challenges and
the new unique index, restoring the former user index. Existing password hashes,
users, login mappings and social/catalog data are preserved. In-flight flows must
restart after downgrade. This storage choice implements the approved replay rule.

## Verification and rollout

Use real PostgreSQL integration tests and fake fixed-origin provider HTTP responses
to exercise start/callback/onboarding/link flows without real credentials. Cover
state/correlation/PKCE, expired/tampered/replayed tickets, account/email collisions,
concurrency and rollback, missing/changed sessions during linking, lockout, strict
JSON/CSRF, privacy, no-store, disabled configuration and existing password flows.
Verify migration apply/reapply/downgrade preserves users and login mappings.
Run full solution tests, Release build, EF model consistency and dependency audit;
perform independent code review before claiming completion.

Live activation remains separate: owner registers OAuth apps, supplies credentials
and exact callback origins. Document setup without requesting credentials in chat.
No public deployment or remote push is included. Frontend buttons, arbitrary return
redirects, account unlinking, email delivery, password reset and account recovery
administration remain outside this slice.

## Primary references

- https://learn.microsoft.com/en-us/aspnet/core/security/authentication/social/google-logins?view=aspnetcore-10.0
- https://docs.github.com/en/apps/oauth-apps/building-oauth-apps/authorizing-oauth-apps
- https://docs.github.com/en/rest/users/emails

The user approved this written design. Detailed route names, limits and the
verified-email requirement originated as implementation defaults, then were approved
with the design. The one-time challenge table is an implementation refinement backed
by a reproduced cookie replay after logout.
