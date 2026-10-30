# RateFreak web

Next.js App Router with TypeScript, ESLint, and CSS Modules. This app shares
one repository with the .NET API, but has its own dependencies and deployment.
Use Node.js 24 LTS and pnpm 10 (the exact pnpm version is pinned in package.json).

From the repository root:

```bash
pnpm --dir web install --frozen-lockfile
pnpm --dir web dev
```

Open http://localhost:3000. The placeholder homepage runs without the API or database.

## Checks and production

```bash
pnpm --dir web lint
pnpm --dir web typecheck
pnpm --dir web build
pnpm --dir web start
```

`start` serves the production build and requires `build` first. Commit
`pnpm-lock.yaml` when dependencies change. Keep frontend tooling and packages
in this directory; no root JavaScript workspace or build orchestrator is needed.
The generated pnpm workspace configuration records ignored optional dependency
build scripts; it does not combine the frontend and backend into a JS workspace.

## API boundary

API integration is not implemented yet. The intended browser-facing proxy must
forward session and antiforgery cookies, relay all Set-Cookie headers, forward
CSRF tokens, reject cross-origin mutations, and avoid caching private responses.
.NET remains responsible for accounts, authorization, and business rules.
Do not expose provider credentials through NEXT_PUBLIC_ variables.

See the [root README](../README.md) for backend setup and API contracts.
