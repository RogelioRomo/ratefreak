# ADR 0003: Minimal layered foundation

Status: Implementation decision, 2026-10-28.

Use a modular monolith with Minimal APIs and feature-oriented code inside four
layers: Api -> Application -> Domain; Infrastructure -> Application/Domain.
Api references Infrastructure only for composition. Domain has no packages.
One PostgreSQL 17 database and a scoped EF Core DbContext. Future table and column
names use explicit snake_case mappings; UUID keys and UTC timestamptz timestamps.
No repository abstraction, mediator, broker, or empty feature folder hierarchy.

The first migration is an intentionally empty baseline: it establishes EF migration
history without inventing Phase 2 entities. Apply migrations explicitly using EF
CLI; never EnsureCreated or migrate on application startup. Readiness checks both
connectivity and pending migrations; liveness is independent of PostgreSQL.

Use built-in OpenAPI in Development only; Problem Details with trace IDs and no
exception details in responses. JSON console logs include scopes and request IDs.
ConnectionStrings:Default is required at startup and validated without connecting.
No auth, provider, frontend or speculative domain behavior in this phase.

Controllers remain a valid alternative but add little for the initial endpoints.
Separate API/database deployment and a PostgreSQL-only local Compose file keep
local debugging simple. Containerizing the API and ingress configuration are deferred.
