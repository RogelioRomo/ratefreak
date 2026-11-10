# ADR 0001: Standalone API and browser session boundary

Status: Accepted direction (2026-10-28). Authentication implementation is Phase 2.

The Vercel hosting direction below is superseded by
[ADR 0020](0020-self-hosted-open-source-distribution.md). RateFreak targets
self-hosted open-source distribution; the browser/API security boundary remains.

Deploy the .NET API independently from a Next.js frontend hosted on Vercel.
The browser talks to a thin Next.js backend-for-frontend; Next.js forwards
requests to .NET. .NET owns accounts, authorization and all business data.
Use ASP.NET Core Identity and secure HttpOnly browser session cookies.
Do not add JWT issuance or browser token storage solely because hosting differs.

The future proxy must correctly relay Set-Cookie, expire cookies on logout,
forward only the intended session cookie, avoid caching authenticated responses,
and enforce CSRF protection for browser mutations. Define cookie attributes,
session revocation, Data Protection key persistence, and the proxy trust boundary
before Phase 2 ships. .NET must authorize every request regardless of its source.
No permissive CORS policy is needed for server-to-server calls. Never trust a
client-supplied user ID header. Authentication routes are absent in Phase 1.

Alternatives: direct browser calls to sibling custom domains require explicit
credentialed CORS and CSRF protection; external OIDC adds an identity service and
is deferred until there is a concrete federation/mobile requirement.
