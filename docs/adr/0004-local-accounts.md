# ADR 0004: Local accounts and cookie sessions

Status: Accepted direction, 2026-10-29. User approved email/password sign-in with
an independent unique public handle and authorized implementation of this slice.
The following detailed policies are implementation defaults, not separately
confirmed product requirements.

Use ASP.NET Core Identity with UUID IDs, normalized unique email and handle
indexes, and explicit PostgreSQL mappings. Identity entities/stores stay in
Infrastructure; Application exposes account contracts and operations. No custom
password hashing, bearer issuance, repository framework or frontend implementation.

Handles: 3–30 ASCII letters/digits/underscores; preserve display casing and enforce
case-insensitive uniqueness. Display names: 1–80 trimmed characters. Emails are
trimmed, valid addresses with both original and normalized forms up to 254
characters, and unique after Identity normalization.
Passwords: 12–128 characters, upper/lowercase and digit, at least four unique
characters. Five failed attempts lock an account for 15 minutes. Failed logins
use one generic 401 response. Registration does not sign the user in.

Sessions: eight-hour non-sliding browser cookies, HttpOnly, host-only, SameSite=Lax,
Secure outside Development; __Host- prefix in production. Logout invalidates all
sessions for the account by changing its security stamp, validated on every request.
This is simple and immediate, at the cost of a database lookup per authenticated
request; per-device session storage can be designed if requested later.

All auth mutations require ASP.NET antiforgery cookie + X-CSRF-TOKEN header.
GET /api/v1/auth/csrf issues the request token and HttpOnly antiforgery cookie.
Refresh the token after login/logout because it is bound to the current identity.
Auth responses and /users/me use Cache-Control: no-store. No permissive CORS.
The future Next.js proxy must relay both cookies, Set-Cookie changes and the header,
and enforce browser request origin before forwarding. .NET owns authorization.

Registration: 5/IP/hour; login: 10/IP/minute; CSRF issuance: 30/IP/minute. No queues.
IP means the actual connection address; do not trust arbitrary forwarding headers.
The future proxy needs its own per-browser abuse protection since .NET initially
sees the proxy's IP. Request bodies are capped at 64 KiB and passwords before hashing.

Persist Data Protection keys under .local/keys in Development (ignored by Git).
Outside Development require an absolute Authentication:DataProtectionKeysPath,
mounted durably and access restricted to the app. Production requires HTTPS and
key encryption/storage hardening before launch. Tests use isolated temporary keys.

Email verification, reset/recovery, public profile editing, admin/moderation and
catalog routes are not implemented in this slice. Email ownership is unverified;
do not publicly launch these local-account endpoints until verification/recovery
and deployment hardening are implemented. Existing provisional catalog decisions
remain unchanged. OIDC and direct browser calls remain alternatives for future needs.
