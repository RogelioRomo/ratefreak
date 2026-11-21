# Global npm runtime source and notice review

Reviewed 2026-11-21 against the retained web runtime. This covers npm 11.19.1
and its 143 versioned dependency manifests. Corepack and Yarn have a separate
[bundled-tool review](js-bundled-tools-license-review.md). These conclusions are
bound to the preserved bytes; they do not cover future package versions.

## Source and license decisions

The review retains every shipped npm package's original attribution metadata,
complete available license/notice texts and exact source tree. The 144 package
records and their notice paths are in local `web/npm-reviewed-packages.json`.
Their grants include MIT, ISC, BSD, Apache-2.0, BlueOak-1.0.0, CC-BY-3.0,
CC0-1.0 and npm's own Artistic-2.0. Dependencies retain their own grants; npm's
license does not replace them. Original source/header notices remain intact.

The npm [exact release license](https://github.com/npm/cli/blob/d63a975ce3c1219752519b7fb68bdc03faa767c0/LICENSE)
requires source instructions when distributing compiled standard versions without
source and provides conditions for modified source. The chosen mechanism supplies
the exact shipped source, its original terms and the comparison with the registry
release. It does not rely on calling the shipped tree byte-identical to the npm
registry archive. The source remains redistributable under its original licenses.

The original registry package's SHA-512 integrity passed. Of the 1,925 common
regular files compared, 38 differ only after line-ending/trailing-whitespace
normalization; there is also one additional empty `.npmrc`. This describes bytes,
not a proof that whitespace changes cannot affect behavior. The preserved patch
and exact shipped tree document the upstream packaging differences. RateFreak
does not claim to have authored those changes or apply its MIT license to npm.

Source retrieval materials:

- `shipped-runtime-sources.tar.gz` preserves the exact runtime package tree,
  including npm source, configuration, documentation, dependencies and notices.
- `npm-11.19.1.tgz` preserves the original
  [registry release](https://registry.npmjs.org/npm/-/npm-11.19.1.tgz).
- The npm source archive preserves its registry-identified
  [source commit](https://github.com/npm/cli/tree/d63a975ce3c1219752519b7fb68bdc03faa767c0).
- `npm-byte-comparison.json` and `npm-upstream-packaging-differences.patch`
  document the comparison and original/shipped filenames.

These are retained under
`.local/release-compliance/runtime-review-2026-11-21/web/` and included in the
combined local review-material artifact. Publication must provide the matching
source materials alongside the binary release. Local collection is not public
source delivery. To inspect/run the original npm source, extract the shipped
archive into an empty directory and use Node with
`usr/local/lib/node_modules/npm/bin/npm-cli.js`; its `lib/`, configuration and
documentation source are preserved. Dependency build forms retain their own
licenses and separate source/notice treatment.

## Missing standalone notice files resolved

Four shipped packages omit standalone license files. Their treatment is explicit:

| Package | Evidence and supplied material |
| --- | --- |
| `@npmcli/agent` 4.0.2 | Exact [source package metadata](https://github.com/npm/agent/blob/db7852869c74ceb8985d7c36b291a2e7a866741c/package.json) affirmatively declares ISC and names GitHub Inc. as author. The source commit also lacks a standalone license. Preserve that metadata and supply canonical ISC terms, without inventing a copyright holder or year. This is a declaration-based decision, not a newly fabricated grant. |
| `@sigstore/verify` 3.1.1 | Preserve the exact [repository Apache-2.0 license](https://github.com/sigstore/sigstore-js/blob/c1dc7d4778a450787fc72b083f2490ad02b714c6/LICENSE) omitted from the published package. |
| `spdx-exceptions` 2.5.0 | Preserve its original metadata and [upstream attribution README](https://github.com/kemitchell/spdx-exceptions.json/blob/3aa64bec339abc6a3eca00c3436aaa7e154b8799/README.md), including Linux Foundation/contributor attribution, source URI and complete CC-BY-3.0 terms. No changes to the identifiers are claimed. |
| `spdx-license-ids` 3.0.23 | Preserve original registry README/metadata and complete CC0 terms. The [exact README](https://github.com/jslicense/spdx-license-ids/blob/a2a975daf48bbc2afdc8e7c304405b44c656fd53/README.md) supplies its CC0 dedication reference. |

The review also preserves qrcode-terminal's older `licenses` metadata, its full
Apache text and relevant vendored source notices; an empty singular `license`
field does not establish an absence of licensing. BlueOak packages retain their
exact license texts. Canonical reference texts are clearly identified as such;
template placeholders are not asserted copyright information.

## Distribution mechanism

`deploy/compliance/runtime-supplements/NPM-RUNTIME-NOTICES.txt` contains the
144 component sections and source instructions. Its rule in `sources.json`
selects the web image explicitly and pins the reviewed npm manifest hash.
The collector packages the supplement and provenance in the image's runtime
notice archive. Changed or missing applicable inputs require review rather than
silently skipping the notice.

The matching runtime component decisions identify actual retained source
materials. Full source/notice delivery and final candidate validation remain
separate from this component review; no provider clearance or public release is
claimed here.
