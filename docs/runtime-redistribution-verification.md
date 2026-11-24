# Runtime component review and notice delivery

Verified 2026-11-21 in the working tree. This completes the selected copied
runtime and bundled-component review for the inspected payloads. It does not
qualify the final project revision, close provider permissions or publish a release.

## Result

- Recorded source/material decisions for all **158 copied-runtime inventory
  components**: 154 in web and four in tools. This includes npm, Corepack, Yarn,
  their copied shims, Node aliases, gosu and upstream entrypoint scripts.
- Recorded **ten additional framework/native decisions**: two .NET frameworks
  and three SkiaSharp native packages in both API and tools. The Skia review
  covers all 20 shipped runtime files, including other architectures and symbols.
- Prepared five complete notice supplements with exact upstream grants,
  attributions and source instructions. No missing copyright holders or years
  were invented; declaration-based MIT/ISC decisions preserve actual metadata
  and supply standard terms without asserting template attribution.
- Delivered and verified those notices in three disposable derived images.
  All **606 runtime notice-file hashes** passed, including the original notices.
  OS inventories and application-native path/hash pairs match the retained base
  candidates. No persistent deployment, database or provider configuration changed.

The source decisions distinguish mandatory source materials from reviewed
no-source-required grants. They do not infer that every dependency has npm's,
Node's, or the project's top-level license.

| Component scope | Review and mechanism |
| --- | --- |
| npm 11.19.1 and 143 dependency manifests | [npm review](npm-runtime-license-review.md): exact shipped source, registry source, documented upstream whitespace/configuration differences, all supplied attribution and complete supplemented notices. Source/instructions remain under original licenses. |
| Corepack 0.36.0 and Yarn 1.22.22 | [Bundled-tool review](js-bundled-tools-license-review.md): 271 locked package archives retained as audit evidence; complete conservative notices include explicit SPDX attribution and declaration-based terms. Exact Yarn module membership is not required to discard any collected notices; the larger notice set is retained. Optional full specification-data archives are excluded from the distributable material below. |
| gosu 1.19 and Docker entrypoint scripts | [Copied-tools review](copied-tools-license-review.md): exact binary/source matches, Go/module/copied-runc notices, Lucent/Vita Nuova attribution and MIT script grants. |
| Node, .NET and SkiaSharp | [Bundled-runtime review](bundled-runtime-license-review.md): complete Node license, original plus exact-source .NET notices, Skia enabled codec/font/GPU/compiler closure, FreeType FTL election and acknowledgement, IJG acknowledgement, Wuffs and separately licensed header grants. |

## Enforced delivery

All three production Dockerfiles copy `deploy/compliance/runtime-supplements`
and pass an explicit `--image-family` to the collector. `sources.json` selects
the applicable notice rules and pins exact target-file and notice hashes.
Every applicable target must exist and match; a missing architecture file or
changed framework version marker cannot silently disable a supplement.
Unrelated images' rules are skipped by declared family, not file absence.

The runtime notice archive contains the applicable supplement files and their
provenance index. Existing application and OS/runtime notices remain intact.
These notice additions do not change application dependencies, runtime binaries,
schema or product behavior.

The proof images are tagged `ratefreak-{api,web,tools}:runtime-license-proof`.
They derive from the retained `11f4049` candidates and replace only collection
tooling/notice evidence, with an uncommitted audit label. They are not full
production rebuilds from current source and must not be published as the final
release. [Machine-readable proof](release-evidence/2026-11-21-os-sources/runtime-license-review.json)
records their exact IDs and current tooling hashes.

## Prepared material artifact

Local artifact:

`.local/release-compliance/runtime-review-2026-11-21/runtime-review-materials.tar.gz`

Size: **8,686,299 bytes**. SHA-256:
`c6a7f9a6ffc1550ee2313fd529d504bc4379ae5c6fdc74186aa9df9d2dd9b619`.

Its 18 manifest-listed files were read back and hash-verified. It includes exact
shipped runtime source, original npm registry source, source comparison/patch,
the complete notices and review records, specific runtime decisions and complete
.NET/Skia notice supplements. Optional upstream test/specification archives with
broader licensing scope remain local audit evidence rather than being silently
added to distribution. Full source-license provenance and original archives are
retained under `.local/release-compliance/runtime-review-2026-11-21/`.

[Runtime decision records](release-evidence/2026-11-21-os-sources/runtime-decisions.json)
cover the runtime inventory subset, not all release components. Their coverage
and material hashes were validated against the proof inventory. Additional
framework/native records are in the material artifact; copied payloads and
actual notice delivery were rechecked before binding those decisions to the
derived images. This artifact supplements the separately prepared
[OS/native source materials](os-source-delivery-verification.md); it is not a
complete release source bundle or a public delivery event.

## Verification and remaining release work

**20 compliance, 26 release and 52 operator tests passed**. Regression tests
exercise actual archive contents, changed/missing targets, changed notice bytes,
explicit image selection, old evidence rejection and archived-inventory
reconciliation. Independent implementation review's missing-target applicability
finding was reproduced, fixed and rechecked. The final three derived image
builds and archive checks passed with collection networking disabled.

The next release task is to assemble full image/source coverage from the final
source revision, repeat the relevant source/artifact and privacy checks, and run
fresh-install/upgrade/browser acceptance for those exact production images.
The reviewed decisions transfer only when the payload hashes and versions still
match and all applicable notices accompany them. Provider clarification,
operator-specific acceptance and authorized publication remain separate gates.

## Current candidate follow-up — 2026-11-23

The [current development candidate](release-candidate-verification-2026-11-23.md)
now has full inventoried source coverage assembly, all-layer artifact/privacy
inspection and exact-image fresh-install/upgrade/browser acceptance. Its
719 image/component mappings preserve the reviewed material/notice conditions;
the native sidecar is newly bound to the current web image. Delivery copies of
the .NET/Skia notice archives normalize local ownership metadata while preserving
member bytes. The separately reviewed sharp addon now has its header collaborator
notice and supporting materials alongside the binaries.

This supersedes the immediate next-task instruction above for that exact local
development candidate. It does not qualify an unselected final stable identity,
resolve the documented font/header provenance limits, establish current
vulnerability/provider clearance, or authorize publication.
