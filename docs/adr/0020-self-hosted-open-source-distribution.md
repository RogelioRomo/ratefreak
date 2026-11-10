# ADR 0020: Self-hosted open-source distribution

Status: Accepted product direction (2026-11-10, user clarification).
Supersedes the Vercel hosting direction in ADR 0001; its browser/API session and
authorization boundaries remain in effect.

RateFreak is intended for open-source release so people can run their own
installations on infrastructure they choose. Selecting or operating one centrally
hosted production service is not the release objective. Vercel must not be a
required dependency for a supported installation.

Keep Next.js as the browser-facing proxy, .NET as the authentication/business
authority and PostgreSQL as the database. Separately built services may run on
the same host or different hosts. Existing secure-cookie, CSRF, Origin, private
artwork and erasure-journal requirements continue to apply.

The release must supply a reproducible reference installation and document the
runtime, networking, storage, configuration, migration and recovery contracts for
other hosting arrangements. Docker Compose is a proposed reference packaging
approach, not yet an implemented or user-approved detailed design. Supporting
arbitrary infrastructure does not mean every topology has been verified.

The current Vercel-specific production client-address adapter and development-only
database Compose file do not establish portable self-hosting readiness. A trusted
reverse-proxy design must reject spoofed forwarding information and preserve abuse
budgets without requiring Vercel or weakening authentication protections.

Project release acceptance must prove fresh installation, upgrade, persistence,
HTTPS/proxy behavior and backup/restore on a documented reference topology.
Instance operators remain responsible for their real storage expiration, secrets,
provider credentials, staff attribution and deployment-specific checks. Retain
these requirements in operator documentation rather than treating one chosen
hosting account as a prerequisite for publishing the project.

License selection, distributable artifacts, notices and contributor/security
documentation remain release work; no license is selected by this decision.
