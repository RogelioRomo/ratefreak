# RateFreak Foundation Implementation Plan

> For agentic workers: use superpowers:executing-plans to implement task by task.

**Goal:** Build Phase 1 only and record Phase 0 decisions.
**Architecture:** Four layers, one API and PostgreSQL. Explicit migrations and no business tables.
**Tech Stack:** .NET 10, EF Core/Npgsql 10, PostgreSQL 17, xUnit v3, Testcontainers.
**Spec:** ratefreak-plan.md and ../specs/2026-10-28-foundation-design.md.

## Global Constraints

No frontend/providers/auth routes. Domain has no framework dependencies. No secrets
or automatic startup migrations. Preserve provisional product decisions as provisional.

## Review Focus

Missing/malformed connection strings must fail startup without leaking credentials.
Unreachable database must fail readiness while preserving liveness.
Pending migrations must fail readiness until explicit migration application.
Unexpected failures must return sanitized Problem Details with a trace ID.
Production must not expose OpenAPI or exception details.

### Task 1: Scaffold and test contracts

Files: RateFreak.sln, src/*/*.csproj, tests/*/*.csproj, global.json, Directory.Build.props.
- [x] Create projects/references and pinned package references.
- [x] Write configuration and HTTP contract tests before application behavior.
- [x] Attempt test execution; record missing packages/environment blockers.

### Task 2: Persistence and HTTP foundation

Files: Infrastructure/Persistence/*, Infrastructure/DependencyInjection.cs,
Api/Program.cs, Api/Middleware/*, Api/Endpoints/*.
Interface: AddInfrastructure(IServiceCollection, IConfiguration); RateFreakDbContext.
- [x] Implement connection validation, scoped context and empty baseline migration.
- [x] Implement health checks, sanitized errors, trace IDs and Development OpenAPI.
- [x] Build production projects and run available behavioral verification.

### Task 3: Repeatable local setup and proof

Files: deploy/compose.yaml, .env.example, .config/dotnet-tools.json, README.md,
tests/RateFreak.IntegrationTests/PostgresTests.cs, docs/implementation-checklist.md.
- [x] Add Compose storage/health/secrets inputs and explicit migration commands.
- [x] Add Testcontainers migration/idempotency/readiness test.
- [x] Verify build, test attempt, Compose parsing, and startup behavior (gaps recorded).
- [x] Review changes and record verification gaps; no commit without usable Git metadata.
