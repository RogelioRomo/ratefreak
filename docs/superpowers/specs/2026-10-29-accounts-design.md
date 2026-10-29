# Phase 2 first slice: accounts

Approved scope: email/password registration and login, unique public handles,
logout and /users/me, with cookie sessions, CSRF, rate limits, migrations and tests.
Source: ratefreak-backend-plan.md Phase 2 and docs/adr/0004-local-accounts.md.

Goals: safe account storage and a real authenticated API slice backed by PostgreSQL.
Non-goals: frontend/proxy code, catalog, providers, profile writes, email delivery,
JWT/OIDC, social features, public production launch. Production policies are in ADR 0004.

Approach chosen: Identity stores in Infrastructure and HTTP/security in Api;
plain Application contracts and validation keep framework concerns at the edges.
Alternative: built-in MapIdentityApi exposes additional token/recovery routes and
uses email as username, so custom thin endpoints better match the approved handle
and proxy/cookie requirements. No new authentication protocol is implemented.

Migration extends the deployed baseline; do not replace it or migrate at startup.
Database integrity includes normalized unique email and handle, explicit identity
relationships, UUID keys, UTC timestamps and consistent snake_case names.

Work in place: .git is an empty protected placeholder, so worktrees/commits cannot
be created. Preserve the user's existing database/container, secrets and services.
