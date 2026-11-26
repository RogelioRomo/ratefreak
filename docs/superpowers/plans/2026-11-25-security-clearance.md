# Release security clearance implementation plan

**Goal:** Integrate the previously verified Debian 13 web/tools runtime change and produce current, image-bound security evidence without changing the persistent developer instance or publishing a release.

**Approach:** Carry forward the exact three-file trial patch approved in the conversation. Build from a clean isolated local source snapshot based on current main, retaining nonofficial identity. Assess findings against actual shipped components and supported workflows; historical exclusions do not automatically transfer. Do not suppress findings or describe an incomplete assessment as clearance.

**References:** `docs/debian-runtime-trial-2026-11-23.md`, `docs/debian-advisory-assessment-2026-11-23.md`, `docs/release-os-review-2026-11-23.md`.

## Tasks

- [x] Integrate the retained trial patch (web/tools Debian base, ICU76, PGDG suite). Run existing operator/release/compliance tests; inspect the exact diff. Also update the reference PostgreSQL pin to 17.12; no schema change.
- [x] Build current API/web/tools images in an isolated committed clone. Retain source revision, image identities, source inventory and build logs. Refresh vulnerability database and scan those images plus the pinned PostgreSQL/Caddy reference images. Preserve raw reports and scanner/database metadata.
- [ ] Reconcile findings with authoritative vendor guidance, prioritizing reachable network/image parsers and bundled Node/OpenSSL/native components. Record fixes, conditional exclusions and unresolved findings with evidence and reassessment limits. Refresh source/notice coverage for changed runtime payloads.
- [x] Run fresh HTTPS installation, supported upgrade and coordinated backup/isolated restore acceptance on changed artifacts. Repeat affected checks after any further runtime change.
- [x] Review implementation/evidence, update STATUS and record exactly what passed and what remains. Public release stays held unless all required security work is resolved. No publication or persistent-instance migration.

## Findings and execution record

- A reachable libheif decoder exposure required a product mitigation. Pin the already-shipped Sharp version directly and block HEIF/AVIF decoding after initializing Next's optimizer. Independent review caught Next undoing the first block on lazy initialization; real optimizer regression was observed RED then GREEN and the corrected compiled production-image probe passed. JPEG/PNG/WebP/GIF remain supported.
- 1,018 frontend tests, 118 operator/release/compliance tests, lint and TypeScript passed. Fresh/upgrade HTTPS, PostgreSQL 17.11-to-17.12, Chromium and coordinated restore acceptance passed. All 33 restored table fingerprints were preserved; the independent journal fixture was empty. Source coverage validates 502 components, 43 materials and 406 native inputs.
- At the initial checkpoint, clearance remained incomplete: PostgreSQL GSS/shared-ASN.1, remaining OS/caller decisions, Skia126912 fix/caller evidence and recursive embedded coverage. See `docs/security-integration-2026-11-25.md` and its retained evidence. No blanket residual-risk acceptance was made.

## Validation focus

Debian base and PGDG suite must agree; copied .NET must load ICU/native dependencies; migration tools must preserve existing data/artwork; Node's embedded OpenSSL needs independent version/caller review; tools' root workflows cannot inherit web/API privilege assumptions. Source/license evidence must describe new bytes and separate lower-layer payloads from the merged filesystem.

## Execution

The user authorized proceeding with the explained security-clearance approach. Execute inline; keep isolated build/test data under `.local/security-clearance-2026-11-25/`. Final changes remain reviewable in the shared checkout.

## Follow-up checkpoint — 2026-11-25

- User selected the strict standard: every finding, including medium/low, needs a documented fix or caller-level exclusion. No residual-risk acceptance.
- Stable SkiaSharp 4.157.0/native notices integrated; official signatures and 20 native target hashes checked. PostgreSQL 17.12 Trixie selected after logical-restore/collation proof; startup/migration guard added without touching persistent data.
- Reachable Node embedded-zstd decoding mitigated before networking; real warm-fetch and preloaded-tar proof passed through compiled startup. HEIF/AVIF restriction remains verified.
- Candidate ede1b617bd58161e1404b1bc6fb7edd92a211102 passed 778 backend, 1,026 frontend, 123 tooling tests and affected image/source/layer/HTTPS/upgrade/browser/restore proofs. PostgreSQL JIT was then disabled and independently verified using real SQL and fresh HTTPS acceptance on those images. The final frozen installation package must include that later config delta.
- Eight medium/low IDs remain documented proof gaps (three glibc mitigation properties, five C++ compiled-caller/header conditions). Security clearance remains held. See docs/security-followup-2026-11-25.md and its current ledger. The reconciliation task above remains incomplete until those gaps are resolved; no unassessed row is silently treated as cleared.
