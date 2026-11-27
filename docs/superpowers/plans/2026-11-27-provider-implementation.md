# Provider implementation plan

**Goal:** Implement the settled IGDB lifecycle and direct Open Library covers.

**Spec:** [Provider implementation handoff](../../provider-implementation-handoff.md).

**Architecture:** Preserve TMDB behavior. Give IGDB its own lifecycle, durable removal policy and dedicated artwork cache using the existing TMDB implementation as the reference. Open Library covers use validated direct browser URLs.

**Tech stack:** .NET/EF/PostgreSQL, Next.js/React, Python operator tools.

## Constraints and review focus

- Refresh at five UTC calendar months; expire at six without renewing successful timestamps on failure.
- Preserve identities and user content; API disablement does not purge.
- Block removed content after isolated restore using current independent policies.
- Actual artwork bytes have independent ages; obsolete optimizer URLs cannot refetch removed content.
- Keep uploads and TMDB working, and optional providers disabled by default.
- No production database migration, publication or provider correspondence is part of this implementation.

## Tasks

- [x] Backend: add IGDB policy/lifecycle/worker, import and catalog gates, maintenance command, additive migration, and calendar/retry/removal/preservation/pacing tests in `src/` and `tests/`.
- [x] IGDB artwork: add dedicated route, policy, managed cache and sweeps under `web/server/artwork/`; close optimizer access and test expiry/removal/refetch denial.
- [x] Open Library: update `web/shared/artwork/provider-images.ts` and artwork renderer to direct validated URLs, remove obsolete proxy access, and test all consumers and fallbacks.
- [x] Operator tools: provision independent IGDB policy/cache; add offline removal, pending-operation gates, isolated current-policy restore/replay and tests in `deploy/self-host/`.
- [x] Documentation and verification: update operator/privacy instructions, disposal guidance, handoff/STATUS and evidence. Run backend/web/operator suites, model consistency, production builds and artifact/isolated-install checks feasible in this environment; record precise limits.

Each behavior change starts with a failing focused test. Run relevant checks after each task, then combined verification and an independent review before completion.

## Execution decisions

The user requested continuing implementation of the settled handoff. Proceed in this checkout without another design approval. Independent backend, IGDB artwork and Open Library tasks use focused parallel agents; the primary agent owns operator tooling and integration. Keep an explicit record of verification and release gates rather than treating implementation as release qualification.
