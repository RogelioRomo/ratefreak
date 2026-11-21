# Release versioning verification

Work in progress, 2026-11-20, on `feat/release-versioning` in the isolated worktree.
This records local implementation evidence, not public-release clearance.

## Verified so far

- Backend: 751 tests passed, zero skipped (59 unit, 496 PostgreSQL/API integration,
  196 provider contracts). New tests cover SemVer, cached/coalesced/conditional
  checks, disabled requests, stale failures, rate limits and administrator boundaries.
- Frontend: 940 tests passed before the subsequent focused browser-proxy regression
  test was added. Lint/typecheck and the elevated local production build passed.
- Operator tests: 43 passed. Release tooling tests: 11 passed. Existing compliance
  tests: 18 passed. These counts will be refreshed after final fixes/review.
- Development API/web/tools production images built under separate `release-proof`
  tags. They remain nonofficial and retain the pre-feature source revision as
  development metadata; they are not claimed to be a tagged official candidate.
- Disposable HTTPS baseline-to-development-image rehearsal preserved all recorded
  database fingerprints and rejected old migration acceptance. The following admin
  route check found a missing browser-proxy allowlist entry; its regression test
  failed before the fix and passed after. The final rehearsal is pending.

Local logs/artifacts are under `/tmp/ratefreak-*` and
`.local/release-versioning/` in the main checkout. Only synthetic fixture data is
used. No persistent developer database or existing service configuration was changed.

## Remaining verification and release boundaries

Final HTTPS/browser and image-loading acceptance, independent code review and
fresh final-source checks are pending. No hosted GitHub Actions run, real upstream
release notification, public binary/source publication or persistent instance
upgrade is claimed. Source-delivery/provider and operator-specific release gates
remain independent. Missing image-bound source coverage explicitly blocks completed
candidate finalization; retained review archives are not an installable release.
