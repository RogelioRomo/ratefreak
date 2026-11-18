# Contributing to RateFreak

RateFreak is an MIT-licensed application for self-hosted media catalogs and
communities. Bug reports, documentation improvements and focused pull requests
are welcome. Read [current status](docs/STATUS.md) before starting: older plans
and verification notes record their original scope and may have been superseded.

For a suspected vulnerability, follow [SECURITY.md](SECURITY.md) instead of opening
a public report with exploit details, credentials or private user data.

## Set up a development checkout

You need:

- A stable .NET 10 SDK compatible with [global.json](global.json).
- Node.js 24 and pnpm 10; [web/package.json](web/package.json) pins the pnpm version.
- Python 3 for notice collection and operator/compliance tooling.
- Docker Engine or Desktop with Compose and access to its daemon. PostgreSQL
  integration tests use disposable containers and may pull images.

From the repository root, restore dependencies and tools:

```bash
dotnet restore RateFreak.sln
dotnet tool restore
pnpm --dir web install --frozen-lockfile
```

Follow the [backend setup](README.md#1-prerequisites) for local PostgreSQL, API
configuration and explicit migrations, then the
[web accounts setup](web/README.md#local-accounts-setup) for matching server-only
proxy secrets and local origins. Complete the
[first-run setup](README.md#first-run-administrator-setup) for a fresh database.

The API does not load `.env` automatically, and it does not migrate the database
at startup. Use your own development secrets and a database you can safely change.
Do not apply experiments, reset commands or test migrations to someone else's
database. Provider adapters and external sign-in are optional; most development
and automated tests do not require real provider credentials or live imports.

The development Compose file starts PostgreSQL only. For the supported production
topology, use the separate [self-hosting guide](docs/self-hosting.md). Development
loopback settings are not a production proxy configuration.

## Make a focused change

Start a branch from the current default branch. For substantial changes, explain
the problem and proposed approach in an issue before investing in implementation.
Keep unrelated refactors and dependency upgrades separate from a bug fix.

The main boundaries are:

- `src/RateFreak.Domain` and `src/RateFreak.Application`: domain rules and contracts.
- `src/RateFreak.Infrastructure`: persistence, accounts and provider integrations.
- `src/RateFreak.Api`: HTTP endpoints and security enforcement.
- `web/`: Next.js UI and the same-origin browser proxy. Authentication,
  authorization and business rules remain in .NET.
- `tests/`: .NET unit, provider-contract and PostgreSQL/API integration tests.
- `deploy/`: development/self-hosted packaging and operator/compliance tools.
- `docs/`: current decisions, operations, plans and dated verification evidence.

Follow nearby conventions and applicable `AGENTS.md` instructions. Before changing
Next.js behavior, consult the version-matched documentation under
`web/node_modules/next/dist/docs/`, as described in [web/AGENTS.md](web/AGENTS.md).
Use focused regression coverage for behavior changes; documentation-only changes
need accurate commands and working links rather than application tests.

Schema changes need an inspected EF migration and tests against disposable
PostgreSQL. Check model consistency, explain data preservation and upgrade impact,
and document operator steps. Do not rewrite a migration already used by an
installation or assume a destructive reset is an acceptable upgrade path.

Keep secrets, `.env`/`.env.local`, user-secrets, account exports, database dumps,
uploads, Data Protection keys and private evidence out of commits and reports.
Use synthetic fixtures. Redact cookies, authorization headers, recovery codes,
proxy/setup secrets and connection strings from shared logs or screenshots.

## Run the checks relevant to your change

Run commands from the repository root. Start with focused verification, then run
the applicable checks below before requesting review. Report any checks you could
not run and why; a database-free subset does not establish PostgreSQL behavior.

For backend behavior:

```bash
dotnet build RateFreak.sln
dotnet test RateFreak.sln
```

The full suite includes unit, provider-contract and real PostgreSQL tests.
Docker must be available; database tests fail rather than silently skip when it
is unavailable. They use their own containers, not your development database.

For frontend behavior:

```bash
pnpm --dir web lint
pnpm --dir web typecheck
pnpm --dir web test
pnpm --dir web build
```

For operator or compliance tooling, run the affected suite:

```bash
python3 -m unittest discover -s deploy/self-host -p 'test_*.py'
python3 -m unittest discover -s deploy/compliance -p 'test_*.py'
```

For schema changes, also run:

```bash
dotnet ef migrations has-pending-model-changes --project src/RateFreak.Infrastructure --startup-project src/RateFreak.Api
```

For UI changes, check relevant browser flows at desktop and mobile widths,
keyboard navigation and both themes. Automated tests do not replace testing
changed authentication, proxy, private-artwork or operator flows at their actual
boundary. Use disposable accounts/databases for acceptance work.

There is no repository CI workflow configured at the time this guide was added.
Include local verification results in the pull request. Test totals in older
documents are dated evidence, not a fixed target or proof of your change.

## Dependencies, providers and licensing

Include the pnpm lockfile when frontend dependencies change. Review exact-version
licenses, security advisories and shipped native/runtime components when changing
dependencies or images. `pnpm dev` and `pnpm build` generate ignored notice output;
do not hand-edit that output. Follow the
[notice/source tooling guide](deploy/compliance/README.md) for changed artifacts.

Provider metadata and artwork do not inherit the project's MIT license. Keep
credentials operator-owned, preserve source attribution and consult the
[provider agreement review](docs/provider-agreement-evidence.md) before changing
stored fields, caching, artwork handling or distribution. The user has chosen to
retain TMDB; its cache/database-retention clarification is pending. Do not treat
live API success as content-rights or release clearance.

Contributions to project code are made under the existing [MIT license](LICENSE).
Include the origin and applicable license for third-party code, fonts, images or
fixtures you add; do not include provider catalogs or copyrighted artwork as
unreviewed release/test payloads.

## Submit a report or pull request

Use [GitHub issues](https://github.com/RogelioRomo/ratefreak/issues) for ordinary
bugs and feature proposals. Include the commit/tag, relevant runtime versions,
steps to reproduce, expected/actual behavior and sanitized diagnostics. Identify
whether the issue occurs in development or the supported self-hosted topology.

A pull request should explain the concrete problem and resulting behavior, link
related issues, list the checks actually run and describe migrations/configuration
changes. Include screenshots for visible UI changes and update relevant docs.
State remaining limitations without presenting unrun checks as passing.

The maintainer reviews integration and release decisions. Opening a pull request
does not publish images, deploy an instance or authorize a production migration.
