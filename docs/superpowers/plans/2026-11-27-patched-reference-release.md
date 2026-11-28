# Patched reference release implementation plan

> **For agentic workers:** Use the already selected parallel-agent workflow for independent file groups; the coordinator integrates and verifies them.

**Goal:** Deliver a fully patched five-image 0.1.0 candidate without changing a persistent installation or publishing it.

**Architecture:** Preserve the three application images and their existing manifest/source schema. Add an optional strictly validated `reference_images` map containing exactly `postgres` and `caddy`. New candidates and source builds deliver/select both reference images; legacy three-image fixtures remain readable. Keep reference source/license coverage explicit and required when reference images are present.

**Tech stack:** Existing Python release/operator tooling, Docker/Compose, .NET, Next.js, PostgreSQL17/Trixie and Caddy2 with a Debian13 final runtime.

**Authorization:** Owner selected the complete patched set on 2026-11-27 after learning it distributes the existing five services rather than adding services.

## Constraints

- Work in `.local/stable-qualification-2026-11-27/successor-source`; preserve original f54048c artifacts and source/tag.
- No persistent instance migration, service restart, remote push, tag publication, report/message submission or release publication.
- Keep version0.1.0; record a new source revision and separately retained artifact identities. Do not overwrite existing local immutable version tags.
- Pin upstream bases, apply compatible OS package updates with refreshed runtime build stages, preserve allocator/decoder/JIT/architecture conditions and reevaluate CVEs.
- Every reference archive, source decision and notice must bind to its exact image/platform/source revision. New dependencies cannot inherit the original three-image-only legal review.
- Existing original-candidate source/layer/security/acceptance evidence retains historical scope.

## Task 1: Validate and select reference images

Files: `deploy/self-host/release_bundle.py`, `ratefreak.py`, `compose.yaml`, `compose.caddy.yaml`, related focused operator/bundle tests.

Contract: manifest optional `reference_images` has `postgres`/`caddy` each `{id,tag,file}`, with tags `ratefreak-ROLE:VERSION` and all archive files hashed in `files`. `images` remains exactly API/web/tools. `selected_reference_images(root)` returns selected immutable IDs for bundles; source selection captures both reference roles. Compose receives `RATEFREAK_POSTGRES_IMAGE`/`RATEFREAK_CADDY_IMAGE` and rejects overrides replacing a selected image. Loader checks all five conflicts, archive hashes, IDs, platforms and labels before returning a selection; legacy manifests remain valid.

- [x] Observe failing tests for missing/tampered/mixed references, immutable tag conflicts, loading failure and Compose substitution.
- [x] Implement minimal validation/selection/build behavior and run focused tests.

## Task 2: Build patched references and collect actual evidence

Files: new `deploy/self-host/postgres.Dockerfile`, `caddy.Dockerfile`; new `deploy/release/reference_inventory.py` and its focused tests.

Contract: Dockerfiles have `runtime` stage, pinned existing vendor bases, package upgrade commands and same OCI version/revision/source labels as application images; preserve normal upstream entrypoints/users. Collector API `collect(output: Path, images: dict[str,str]) -> dict` writes `postgres-notices.tar.gz`/`caddy-notices.tar.gz` and returns `{format:1,images:{ROLE:{image_id,components:[{scope,name,version}],...}}}`. Components cover installed OS package/source versions and exact copied/native/Go module identities. Collect actual package copyright/notice text and executable fingerprints; missing supplemental notices/source coverage must stay blocking. No Docker socket mounts or executable extraction into public evidence.

- [x] Use real controlled binary/package inputs to verify inventory parsing and reject malformed/missing identities.
- [x] Build trial patched service images and prove expected package fixes, preserving runtime behavior.

## Task 3: Candidate preparation and source validation

Files: `deploy/release/prepare.py`, `deploy/self-host/release_manifest.py`, new `deploy/release/reference_sources.py`, focused tests and release/self-hosting documentation.

Contract: `built-images.json` and application `source-inventory.json` stay three-role. Add `built-reference-images.json` and `reference-inventory.json`. Prepare builds/archives both reference roles, collects their notices/inventory, and stores `reference_images` in the candidate. Finalization loads and reuses exact archives. Reference source input `reference-coverage.json` maps exact inventory components to hashed materials or documented reviewed exceptions and original notices; validation requires it if references exist. Final source archive includes it. Final acceptance receipts must name the selected reference IDs. Build commands refresh runtime package stages; existing immutable local release tags cannot be replaced accidentally.

- [x] Observe focused missing/mismatched reference proof failures before implementation.
- [x] Implement candidate/source/manifest integration and verify legacy and five-image paths.

## Task 4: Repair and exercise supported upgrade acceptance

Files: `deploy/release/acceptance.py`, new focused harness tests.

- [x] Provision current storage with candidate tools before old baseline migration/seed, while keeping baseline tools for historical migrations.
- [x] Compare original table columns and exact original row multisets; allow only explicit new lifecycle tables/columns, migration-history append and one Songs seed, checked separately.
- [x] Preserve original artwork/key bytes; bind fresh/upgrade/restore/browser/capacity proofs to exact application/reference IDs.

## Integration and qualification

- [x] Independent diff review; run relevant operator/release/compliance tests, then the full existing release verifier once for the final source revision.
- [x] Commit/tag in isolated successor namespace, freeze immutable application/reference archives, inventories and matching installation source.
- [x] Acquire matching new OS/Go source inputs and full notices, verify all-layer/privacy and source coverage; apply risk policy to refreshed scans and actual native conditions.
- [x] Run fresh HTTPS, pinned upgrade, browser, recovery/restart, nonempty erasure/TMDB/IGDB restore replay and defined capacity checks against exact selected images; clean disposable resources.
- [x] Finalize exact bytes without rebuilding, validate final bundle, update STATUS with actual completion and remaining external notification/publication gates.

Final local outcome: successor9eea2c7 passed2128tests/15checks, all880component source coverage, source/security/operational qualification and independent final readback.21artifact verified bundle retained; no publication/persistent changes. Main implementation integration is uncommitted and hash-bound to frozen source. Notification receipt and GitHub synchronization/publication remain external gates.
