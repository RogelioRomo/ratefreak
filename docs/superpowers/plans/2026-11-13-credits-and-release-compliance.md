# Credits and release compliance implementation plan

> **For agentic workers:** Use superpowers:executing-plans to implement task by task.

**Goal:** Credit every provider and package auditable dependency notice evidence.
**Architecture:** Static Credits page and identity-based catalog credit; build-time
notice collection and per-image evidence. No backend/schema change.
**Tech stack:** Next.js/TypeScript/CSS Modules, Python 3, Docker, NuGet/npm metadata.
**Spec:** [Approved design](../specs/2026-11-13-credits-and-release-compliance-design.md).

## Global constraints

- Use exact unmodified TMDB branding and notice, all three providers, existing themes.
- Do not expose credentials, accept agreements, publish or migrate data.
- Missing notice/source evidence must remain visible and must not imply clearance.

## Review focus

- Imported titles still credit providers when integrations are disabled.
- Multiple identities deduplicate credits; unknown identities do not crash rendering.
- Custom items never receive a guessed provider credit.
- Native binaries need their own notices/source evidence beyond wrapper licenses.
- Exact final artifact contents determine verification scope.

### Task 1: Provider credits

Files: `web/app/credits/page.tsx`, `web/features/credits/`, shared shell/footer,
catalog detail, official logo/provenance under `web/public/branding/`.

- [x] Add failing credits/attribution tests for navigation, notice/logo, all known
  providers, duplicate/unknown identities and custom items.
- [x] Implement linked provider sections and contextual credit using persisted identities.
- [x] Run focused tests, lint/typecheck/build; inspect desktop/mobile in both themes.

### Task 2: Dependency notices and native evidence

Files: `deploy/compliance/`, self-host Dockerfiles, `web/public/notices/`, docs.

- [x] Test notice collection for real package texts, nested notices, deterministic
  manifests and refusal to silently ignore missing evidence.
- [x] Implement build-time collection from installed web/NuGet packages; record
  paths, versions, license metadata and hashes without machine paths/secrets.
- [x] Preserve project/font/native/runtime notices and collect OS package metadata.
- [x] Inspect native source/build provenance and unresolved compliance obligations.
- [x] Build/inspect artifacts with notice evidence; record scope and failures.

### Task 3: Agreements and status

Files: provider evidence/review, operator guidance, dependency review and STATUS.

- [x] Research full official agreement text and section-level implementation impact.
- [x] Document operator acceptance/eligibility requirements and unresolved clauses.
- [x] Link implemented changes and exact verification; keep remaining gates open.
- [x] Check documentation links, diff whitespace and final Git state.

Execution record: completed 2026-11-13. Evidence and unresolved legal/source
obligations are recorded in `docs/release-compliance-verification.md`; completing
these implementation steps does not mark the public-release gates closed.
