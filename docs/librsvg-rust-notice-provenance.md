# librsvg Rust dependency notice provenance

Reviewed 2026-11-17. Scope: the 13 packages flagged without license text in
`.local/release-compliance/rust/verified-vendor`. The local vendor set contains
target-specific and development dependencies as well as Linux runtime/build
dependencies; its membership does not prove inclusion in a distributed binary.
Actual target/build inclusion is a separate parent verification task.

Downloaded notices and their exact source URLs, SHA-256 hashes, package mappings
and qualifications are indexed in
`.local/release-compliance/rust/supplement-texts/sources.json`. These are local
release evidence inputs, not checked-in notice manifests. No dependency source,
parent manifest or application code was changed by this task.

## Resolved exact-source cases

| Package | Evidence and source |
| --- | --- |
| selectors 0.40.0 | Cargo manifest declares MPL-2.0; exact-source `lib.rs`, `build.rs` and other source files carry the MPL notice pointing to Mozilla. Its [published source tree](https://api.github.com/repos/servo/stylo/git/trees/67faaab3ff7aa66780ec1d0f51ca47e177b812d3?recursive=1) has no applicable root LICENSE. The [official full MPL 2.0 text](https://www.mozilla.org/media/MPL/2.0/index.815ca599c9df.txt) was obtained, SHA-256 `fab3dd6bdab226f1c08630b1dd917e11fcb4ec5e1e020e2c16f83a0a13863e85`. Preserve the original source notices and provide this license; obtaining license text alone does not establish source-offer compliance. |
| anes 0.1.6 | [Root MIT license](https://raw.githubusercontent.com/zrzka/anes-rs/378ac7461ecac29fde9e10df7b08359d1315a9ad/LICENSE-MIT) exists at the exact `.cargo_vcs_info.json` commit. MIT is one of its declared alternatives. |
| defmt-parser 1.0.0 | [Workspace-root MIT license](https://raw.githubusercontent.com/knurling-rs/defmt/4a8cdb44891ed57b8ff5a023b6bec7137c48708f/LICENSE-MIT) exists at the exact VCS commit; package path is `parser`. |
| mutants 0.0.4 | [Workspace-root LICENSE](https://raw.githubusercontent.com/sourcefrog/cargo-mutants/14011d08c42a7cd368698fe28f77eb4cd0b65bf0/LICENSE) exists at the exact VCS commit; package path is `mutants_attrs`. |
| r-efi 5.3.0 | [AUTHORS](https://raw.githubusercontent.com/r-efi/r-efi/97b55bed1c2c91dcbf787674849f05337ff80b33/AUTHORS) contains a full MIT grant and copyright, as well as other license-option statements. Downloaded bytes match the AUTHORS file already in the verified crate. The filename-based detector missed it. |
| r-efi 6.0.0 | [AUTHORS](https://raw.githubusercontent.com/r-efi/r-efi/7e1b0322d31d625f81a5656096330934f9cd835d/AUTHORS) similarly matches the packaged AUTHORS. Select MIT without pretending the LGPL alternative applies simultaneously. |
| difflib 0.4.0 | [Pinned upstream LICENSE](https://raw.githubusercontent.com/DimaKudosh/difflib/f035fb8e656f27119e23eca9d5b996df14f3885e/LICENSE). This repository has no version tags and the crate no VCS metadata, so exact equivalence was checked: all seven packaged source/test/example/original-manifest files match upstream Git blob hashes at the pinned source tree. Generated Cargo metadata/checksum files are excluded from this comparison. |

## Qualified notices and unresolved cases

| Package | Obtained evidence | Qualification |
| --- | --- | --- |
| winapi-i686-pc-windows-gnu 0.4.0 | [Architecture MIT license](https://raw.githubusercontent.com/retep998/winapi-rs/796a8e6c2971dc2ff1bcff166e6671284f9b5b6b/i686/LICENSE-MIT) from pinned upstream 0.3.9 source tag | Crate has no VCS metadata; source comparison against this later upstream tree differed. This is authoritative family/subpackage notice text, but exact-version source linkage remains unconfirmed. Windows target package; do not describe it as proven Linux runtime content. |
| winapi-x86_64-pc-windows-gnu 0.4.0 | [Architecture MIT license](https://raw.githubusercontent.com/retep998/winapi-rs/796a8e6c2971dc2ff1bcff166e6671284f9b5b6b/x86_64/LICENSE-MIT) | Same scope/qualification as above. Both architecture notice files have identical bytes. |
| malloc_buf 0.0.6 | [Later pinned upstream LICENSE](https://raw.githubusercontent.com/SSheldon/malloc_buf/d9a3e539642bd90e07df458d226b19cdfa606863/LICENSE) | Exact [release source tag](https://github.com/SSheldon/malloc_buf/tree/a7811e5f4c6f9685dd57ad9bfe34e9bf6b0ba4f9) has no license file. Later text is retained with qualification; exact-release applicability was not established. |
| block 0.1.6 | MIT declaration and author metadata preserved from verified crate | No license file found in [release tree](https://github.com/SSheldon/rust-block/tree/47178790cfc9d4a8b092051d8b413b78bd31254a) or inspected current tree. |
| objc-foundation 0.1.1 | MIT declaration and author metadata preserved from verified crate | No license file in [release tree](https://github.com/SSheldon/rust-objc-foundation/tree/0c157a59b7931430ed5ac7839381470b0c516d3c) or inspected current tree. |
| objc_id 0.1.1 | MIT declaration and author metadata preserved from verified crate | No license file in [release tree](https://github.com/SSheldon/rust-objc-id/tree/6527cdf28fef2b155c67c1681f548ecfd794003c), also the inspected current tree. |

The four SSheldon crates belong to the Objective-C/macOS dependency family in
this vendor superset. Their metadata declarations are not substituted for full
license text or invented copyright notices. Target exclusion from the actual
Linux binary must be demonstrated from the executed build graph. Even when not
compiled, a source bundle containing them should retain their original metadata
and report these notice limitations rather than claiming every crate has a full
exact-source license notice. This investigation did not contact maintainers or
establish new license grants.

## Verification and next integration

- SHA-256 of every downloaded/indexed file was recomputed and matched the local
  index. HTTP errors were not saved as license text.
- Exact VCS commit identities came from verified crate `.cargo_vcs_info.json`;
  unversioned crates used official release tags or explicitly qualified pinned trees.
- MIT was selected only where offered by the crate declaration; original files
  retain alternative-license statements. The full MPL text applies to selectors.
- Integrate these texts into the final Rust notice bundle with package scope and
  original copyright/source notices. Extend filename detection to recognize
  r-efi's AUTHORS, or retain its explicit mapping.
- Record actual Linux compiled dependencies before describing target/dev packages
  as runtime obligations. Preserve qualified cases in any complete source-vendor
  redistribution evidence; this task does not mark them all resolved.
