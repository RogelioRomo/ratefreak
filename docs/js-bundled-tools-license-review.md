# Corepack and Yarn bundled dependency license review

Review date: 2026-11-21. Scope: the actual Corepack 0.36.0 `dist/lib/corepack.cjs` and Yarn Classic 1.22.22 `lib/cli.js` extracted from the shipped runtime archive. This review prepares source and notice material; it does not claim that the current image already distributes the additional notices or reproduce the upstream build.

Subsequent integration: [runtime delivery verification](runtime-redistribution-verification.md)
confirms the prepared notice bundle in new disposable proof images. The matching
runtime material artifact excludes optional full specification-data archives;
the broader archives described below remain local acquisition evidence.

The evidence directory is `.local/release-compliance/runtime-review-2026-11-21/js-bundles/` (abbreviated `J/` below). The original runtime, registry metadata, source archives, and frozen source locks are in the adjacent `web/` directory. These local, ignored evidence files must be deliberately included in any compliance handoff; committing this Markdown alone does not distribute them.

## Result and artifact mapping

271 distinct package/version archives have been collected: Yarn's frozen production dependency closure contains 249; Corepack contributes 23 candidates, of which 19 resolve to bundled packages and four are deliberately retained extra versions of the same package names. One package/version is shared. All 249 Yarn archive SHA-1 values match the original lockfile `resolved` hashes; recorded lock integrity values are additionally verified where present. All 23 Corepack candidates match the exact-version npm registry integrity. `J/inventory.json` records each package's artifact, license declaration, source URL, hash verification, and notice paths. No package retrieval failed.

| Artifact | What it provides | Distribution decision |
| --- | --- | --- |
| `J/THIRD-PARTY-NOTICES.txt` | 631,514 bytes of top-level licenses, package license/copyright/NOTICE/AUTHORS files, relevant README/source evidence, upstream release license supplements, CC0 and CC-BY full legal text, and explicit SPDX attribution | Prepared conservative notice material. Preserve it with the tools, including the explicit MIT/ISC declaration decisions below. It intentionally includes some extra package notices. |
| `J/packages/<name>@<version>/package.tgz` | Exact npm/Yarn registry source package for every inventory entry | Preserve as source evidence; most licenses here impose notice retention rather than a source offer. Do not label these as a complete upstream VCS/build-source replacement. Scoped names use `__` instead of `/`. |
| `J/packages/.../notices/` | All license, copying, notice, copyright, and authors files found anywhere inside each published package | Original bytes retained, including nested upstream notices. |
| `J/packages/.../supplemental/` | README and source files carrying grants, notices, or copyright headers when the package lacks a standalone license | Full original files retained; declarations with omitted standard terms are supplemented by the documented decisions below. |
| `J/packages/.../upstream-source.tar.gz` and `upstream-notices/` | Source at the package registry's `gitHead` for the 13 qualifying repositories whose packages omit a standalone license | Exact source evidence, including proof that several historical commits also omit the referenced text. |
| `J/upstream-supplements/` and `upstream-supplements.json` | Babel v6.26.0 and regenerator-runtime@0.11.1 license texts; full CC license texts; acquisition outcomes and hashes | Babel/regenerator texts supplement exact published packages. CC text copies were acquired from the SPDX license-list-data repository because direct automated downloads of the Creative Commons text endpoints returned 403. Official legal-code pages were independently reviewed. Failed acquisition records are retained; actual published grants are distinguished from source-acquisition failures. |
| `J/bundled-source-notice-extracts.txt`, `J/corepack.cjs`, `J/yarn-cli.js` | Copyright/license block comments and complete actual shipped bundle sources | Retain embedded third-party notices too. Package metadata alone does not cover vendored code comments. |
| `J/source-review/clipanion-npm-3.2.1-fc9187f56c.patch` | Corepack's original source patch against Clipanion | Preserve this with the source. The base Clipanion source archive alone does not describe the bundled patched code. |
| `J/selection.json`, `J/corepack-scope.json`, `J/SHA256SUMS` | Selection inputs, extra-version distinction, and checksums for 920 evidence files | Machine-readable audit mapping. SHA256SUMS excludes itself. |
| `J/collect.py`, `J/enrich.py`, `J/supplement-upstream.py`, `J/finalize.py`, `J/prepare-declarations.py`, `J/assemble.py` | Acquisition, integrity checks, supplemental extraction, and notice assembly | Evidence utilities, not production changes. Run in that sequence if rebuilding; `collect.py` resets the inventory before enrichment. |

## Corepack 0.36.0

Registry `gitHead` is `7a4c30b1aecece619a3e01db757f0eb947414b1f`; the source archive root, source `package.json`, frozen Yarn lock, build script, and Clipanion patch are pinned to that source. The shipped bundle retains `// node_modules/...` annotations. Inspecting nested `node_modules` segments matters: `which/node_modules/isexe` is present and would be missed by a first-path-only scan.

The 19 package resolutions are:

| Package | Version | Declared license |
| --- | --- | --- |
| @isaacs/fs-minipass | 4.0.1 | ISC |
| @zkochan/cmd-shim | 6.0.0 | MIT |
| chownr | 3.0.0 | BlueOak-1.0.0 |
| clipanion | 3.2.1, with source patch | MIT |
| cmd-extension | 1.0.2 | MIT |
| debug | 4.4.3 | MIT |
| graceful-fs | 4.2.11 | ISC |
| is-windows | 1.0.2 | MIT |
| isexe | 4.0.0 | BlueOak-1.0.0 |
| minipass | 7.1.3 | BlueOak-1.0.0 |
| minizlib | 3.1.0 | MIT |
| ms | 2.1.3 | MIT |
| semver | 7.8.5 | ISC |
| supports-color | 11.0.0 | MIT |
| tar | 7.5.22 | BlueOak-1.0.0 |
| typanion | 3.14.0 | MIT |
| v8-compile-cache | 2.4.0 | MIT |
| which | 7.0.0 | ISC |
| yallist | 5.0.0 | BlueOak-1.0.0 |

The root manifest chooses semver 7 and which 7; which 7's locked dependency chooses isexe 4. The collection additionally retains semver 6.3.1, which 2.0.2/6.0.1, and isexe 2.0.0 from other lock branches rather than silently assigning them to shipped modules. They are marked extra in `corepack-scope.json`. Most exact grants are standalone files; Clipanion and Typanion contain complete MIT grants in their original README files, now reproduced in the assembled notices. No Corepack dependency license-text gap remains within this annotated scope. This is source/notice sufficiency for the annotated dependencies, not a reproducible-build assertion.

The [pinned Corepack license](https://raw.githubusercontent.com/nodejs/corepack/7a4c30b1aecece619a3e01db757f0eb947414b1f/LICENSE.md) is MIT and requires its copyright/permission notice to accompany copies. Its short top-level license does not contain these 19 dependency notices. Include the collected third-party notices as additional material.

## Yarn Classic 1.22.22

Registry `gitHead` is `740c38c3a962c30ddb344a919bbfb7065620714b`. That source's `package.json` and `yarn.lock` define the collected 249-package production closure, including optional dependency edges. Every traversed `name@range` descriptor resolves in the frozen lock; no selector is unresolved. Multiple versions remain distinct. Source imports were checked against the selected package names; Node built-ins and strings in comments/examples were excluded. Runtime additions from Babel are covered by the declared `babel-runtime` closure; the bundle's Apache-licensed tslib helper is also covered (`tslib` 1.9.3). The actual bundle embeds core-js version 2.5.7, matching the source lock.

The webpack bundle has 1,055 numbered modules but no module-to-package build manifest or source map. Therefore the inventory is a conservative frozen production dependency closure, not an exact per-module attribution proof. Build-time loader packages declared in production dependencies can be overincluded. No package from this closure was discarded merely because its module provenance was difficult to prove. The original build script and `.babelrc` are retained inside the pinned Yarn source archive. A reproducible build or upstream module manifest would be needed to reduce the inventory safely.

The [Yarn v1.22.22 license](https://raw.githubusercontent.com/yarnpkg/yarn/v1.22.22/LICENSE) is BSD-2-Clause. Its binary redistribution condition requires its copyright, conditions, and disclaimer in accompanying material. Dependency license declarations additionally include MIT, ISC, Apache-2.0, BSD variants, Unlicense, CC0-1.0 and CC-BY-3.0; these are not replaced by Yarn's BSD license. Apache LICENSE/NOTICE files and original embedded copyright headers are retained. No general source-offer requirement was established merely by those declarations. `json-schema`'s package-level AFL/BSD references are distinguished from its runtime files' explicit MIT grant below.

### SPDX data decision

`spdx-exceptions@2.1.0` is retained as CC-BY-3.0. Its original README attributes the extracted SPDX specification content to the Linux Foundation and its contributors, and separately describes the package author's extraction work. Preserve both statements. The assembled notice adds source, title/version, copyright attribution, and the [CC BY 3.0 license link](https://creativecommons.org/licenses/by/3.0/legalcode), plus the full license text. This follows the actual historical package's attribution rather than inferring that identifiers need no permission. `spdx-license-ids@3.0.0` declares CC0-1.0 in its README; that README and the [CC0 legal text](https://creativecommons.org/publicdomain/zero/1.0/legalcode) are also retained. Legal-code texts were obtained from [SPDX license-list-data](https://github.com/spdx/license-list-data/tree/main/text) with local SHA-256 hashes; these general legal texts are not claimed to be package-specific copyright notices.

### Ten explicit declaration-based notice decisions

The original published MIT/ISC declarations are treated as affirmative grants under those named standard licenses. An omitted standalone LICENSE file alone does not negate an explicit grant. The collection supplies the canonical permission, conditions, and disclaimer, and reproduces original package metadata, registry metadata where collected, and any README/header attribution unchanged. It does not invent copyright holders or years, identify maintainers as owners, or copy attribution from a later package version. These are recorded compliance decisions based on the exact evidence, rather than assertions that missing historical files were found.

`J/declaration-decisions.json` maps all ten packages to their evidence and `packages/<name>@<version>/prepared-license-notice.txt`. Those prepared notices are included in `THIRD-PARTY-NOTICES.txt`. Canonical MIT and ISC texts and provenance are retained in `J/canonical-terms/`. The canonical templates' copyright placeholder lines are omitted from the prepared notices; actual supplied attribution is reproduced instead. The [Open Source Initiative's MIT text](https://opensource.org/license/mit) was checked independently. This addresses standard terms omitted from upstream packaging without introducing a fictitious copyright notice.

| Package | Exact evidence | Notice/source decision |
| --- | --- | --- |
| buffer-alloc 1.2.0 | Published MIT declaration; source gitHead `a0d55382c805f3b117a055e0be203f13f2519cc5` | Supply MIT terms; preserve full original package and registry metadata. Exact source also omits a standalone license. |
| buffer-alloc-unsafe 1.1.0 | Published MIT declaration; gitHead `93c66c6a1e0f812a55128036004d6ff8a561fd98` | Same explicit-grant MIT decision, retaining this package's own attribution. |
| buffer-fill 1.0.0 | Published MIT declaration; gitHead `a2badd1de771c99285e385dccdebd63093f0285a` | Same explicit-grant MIT decision, retaining this package's own attribution. |
| hash-for-dep 1.2.3 | Published ISC declaration; gitHead `37f17801b424b622eb8a26b8f4858579b91456d6` | Supply ISC terms, preserve complete original metadata. Do not silently substitute MIT. |
| heimdalljs 0.2.5 | Published MIT declaration; gitHead `1b1a25fb73bc6c2609e498578c6738086c28ace1` | Supply MIT terms and preserve complete original metadata. |
| inquirer 6.2.0 | Explicit MIT declaration in exact original npm source; no registry gitHead | Supply MIT terms and original metadata. Failed attempts to fetch guessed upstream release tags do not override the published MIT grant. VCS commit remains unbound, but exact shipped dependency package source is retained. |
| json-schema 0.2.3 | Runtime `lib/validate.js` and `lib/links.js` explicitly name MIT and state `Copyright (c) 2007 Kris Zyp SitePen (www.sitepen.com)`; gitHead `07ae2c618b5f581dbc108e065f4f95dcf0a1d85f` | Elect MIT for these explicitly labeled runtime JS files, supply MIT terms, preserve the actual header copyright and all original package-level alternative-license statements. Do not reclassify schema/specification data files under MIT. See boundary below. |
| math-random 1.0.1 | Explicit MIT declaration and README link; gitHead `14f9d4cb231e26156e1295d4d55b624dfdb54d3c` | Supply MIT terms and reproduce the original metadata and README attribution. |
| path-parse 1.0.5 | Explicit MIT declaration and README attribution to Javier Blanco; no registry gitHead | Supply MIT terms and preserve the original README and metadata. Exact npm source is bound by original lock hash even though guessed VCS tags were unavailable. |
| v8-compile-cache 2.0.0 | Explicit MIT declaration; original gitHead `3700d11a6b311b77f8904ec55465eb002311bb04` source collected | Supply MIT terms and original metadata; no later-version copyright is substituted. Corepack's 2.4.0 is a separate entry with its own complete license. |

`json-schema` boundary: its package README/metadata refers to AFL/BSD alternatives, while the actual runtime JavaScript contains the more specific MIT grant. This review relies on that file-specific grant for runtime JS and retains the conflicting package-level statements as evidence. It does not approve redistributing the entire upstream specification/schema-data archive under MIT. Source tarballs are collected audit evidence; if the release distributes those optional full source archives rather than only the runtime bundle and notices, classify their specification data separately under the original package-level alternatives. No automatic copyleft/source-offer conclusion is inferred from ambiguous `licenses` metadata. The precise webpack module map remains unproved, so preserve the conservative complete notices and actual bundle source rather than dropping this entry.

Other omitted standalone files were concretely resolved: browserify-zlib carries the full grant in source; isarray, Clipanion, and Typanion carry full README grants; is-gzip has the MIT text in its exact gitHead source; Babel has its v6.26.0 upstream license; regenerator-runtime has its [versioned upstream MIT license](https://raw.githubusercontent.com/facebook/regenerator/regenerator-runtime@0.11.1/LICENSE). All are included in the assembled material.

## Verification and release boundary

The collection scripts completed with 271 successful archives, zero unresolved Yarn lock descriptors, verified original Yarn SHA-1 / available lock integrity and Corepack exact-version registry integrity. The assembled notice includes original full text rather than a list of SPDX identifiers. Checksums cover the collected original archives and notice material. Exact Yarn webpack module membership, the file-specific versus package-wide `json-schema` license boundary, and delivery of the prepared notices into the actual image/release remain separate from this completed acquisition work. No production code, image, publication, or external contact was changed by this review.
