# Dependency license and native source packaging plan

> Execute inline using the existing approved release-compliance design.

**Goal:** Resolve the scroll-bar license provenance through artifact/source evidence
and produce a verified, distributable native-source sidecar for the candidate web
image, with explicit remaining OS/source/relinking scope.
**Spec:** `docs/superpowers/specs/2026-11-13-credits-and-release-compliance-design.md`.
**Architecture:** Extend existing Python compliance tooling; no product UI, API,
provider activation, data migration or publication. Sources remain separate from
runtime images; manifests bind the sidecar to image/native hashes.

- [x] Verify exact npm archives/source equivalence and preserve a reproducible
  provenance record; update the pinned supplement without changing runtime code.
- [x] Acquire the full pinned sharp-libvips packaging tree and locked Rust crate
  sources; compare original/edited lockfiles and verify offline dependency resolution.
- [x] Test a source-sidecar builder for required source/hash validation, path safety,
  deterministic output, image/component mismatch and incomplete coverage refusal.
- [x] Build and inspect the real source sidecar; preserve build/replacement instructions
  and prove replacement of the native library in an isolated read-only container.
- [x] Record actual rebuild/source-delivery limits and OS sources separately; do not
  conflate an acquired source bundle with complete redistribution clearance.
- [x] Run relevant tests/notices and image checks, independent review, update STATUS
  and commit the resulting reviewable change. No public release or deployment.

Completed 2026-11-17. See `docs/native-source-package-verification.md` for actual
image/archive fingerprints, test results and remaining OS/publication scope.
