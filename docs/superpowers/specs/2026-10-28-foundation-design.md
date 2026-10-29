# Phase 0 and Phase 1 design

Source: ratefreak-backend-plan.md, phases 0–1. The user approved the thin Next.js
proxy direction; implementation details below are builder decisions.

Goal: an independently runnable .NET 10 API and PostgreSQL foundation, with
repeatable migrations and test infrastructure. No frontend, identity endpoints,
business entities, providers, generic frameworks or public deployment.

Use four layered projects, Minimal APIs, EF Core/Npgsql, PostgreSQL 17,
Problem Details, Development OpenAPI, liveness/readiness and validated configuration.
Document decisions in docs/adr. Tests cover invalid configuration, health endpoints,
OpenAPI exposure, 404/500 errors, and real PostgreSQL migration application.

Approaches: Minimal APIs versus controllers; choose Minimal APIs for the small
endpoint surface. The user chose future Next.js proxying over direct browser-to-API
calls. Business rules are provisional and do not enter the foundation schema.

Constraints: no secrets in source, no automatic migration on startup, no permissive
CORS, no HTTP/EF dependency in Domain. Initial migration is an empty baseline.

Environment constraints: Git metadata is unavailable, Docker socket is denied,
and api.nuget.org DNS resolution fails. Use cached compatible production packages
for compilation; record unverified test and migration execution honestly. Updating
and auditing pinned packages remains required before production deployment.
