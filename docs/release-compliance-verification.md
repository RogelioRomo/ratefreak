# Credits and release compliance evidence

Recorded: 2026-11-13. This change adds credits and notice packaging. Public
publication remains gated by the unresolved obligations below.

**Follow-up 2026-11-17:** [native source-package verification](native-source-package-verification.md)
supersedes the initial application-native and scroll-bar provenance gaps recorded
here. The source sidecar, offline rebuild, source-modified replacement and observed
Rust notices are verified. OS/remaining-image source delivery and provider agreement
requirements remain open; no publication has occurred.

## Provider credit

`/credits` is linked from the shared footer and credits TMDB, MusicBrainz and IGDB.
It includes TMDB's unmodified approved compact logo and required disclaimer,
linked software notices, project MIT license and the two font licenses. The
[logo provenance](../web/public/branding/README.md) records its official origin/hash.
Imported catalog details credit known persisted provider identities using static
trusted destinations. Credits survive provider disablement, deduplicate identities
and omit unknown/custom-item associations. Discovery/preview credit is retained.

Real Chromium checked 1440px and 390px in both light/dark themes: logo loaded at
its natural aspect ratio, no horizontal overflow or page exceptions, and software
notices returned HTTP 200. Setup/session API responses were controlled fixtures;
these checks establish frontend rendering, not deployed provider/API acceptance.
Screenshots/results are local evidence under `.impeccable/review/credits/`.
An independent UI reviewer opened all four captures and returned **ship**;
catalog credit was independently checked through source and focused tests.

## Package and image notices

[Collection instructions](../deploy/compliance/README.md) describe reproducible
generation from installed packages. The generator collects primary license and
nested notice texts, records hashes/repositories and uses explicitly pinned
supplements where packages omitted their texts. It refuses missing required
dependencies, missing primary license text with `--check`, and changed supplement
hashes. This is a text-presence check, not a legal conclusion.

The local web graph has 63 packages; the API restored graph has 35 packages and
tools adds the EF package for 36. This is a conservative package notice superset,
not a proof that every package ships in every runtime. Native payloads and OS
versions are separately captured from actual runtime images. API/tools carry
`/app/notices`; web carries `/app/public/notices` and serves it to users.
Existing OS/runtime notices and project LICENSE accompany package notices.

Candidate builds use separate `:credits-review` tags; no running installation or
public registry was changed. [Image evidence](release-evidence/2026-11-13-credits/summary.json)
records exact candidate image IDs, bundle hashes and native/runtime inventories.
These IDs identify this local build, not future artifacts from mutable package
repositories or a later source revision.

## Native source evidence

The packaged web image contains `libvips-cpp.so.8.18.7` through
`@img/sharp-libvips-linux-x64` 1.3.4. It includes LGPL-3.0-or-later components;
the wrapper's Apache license does not replace their terms. Both the LGPL and GPL
texts and upstream native notice table are bundled, together with license texts
collected from the source archives.

The selected upstream packaging release's build recipe and version declarations
match the installed component versions. All 33 source archive/patch URLs selected
from that recipe were downloaded (about 156 MiB), hashed and retained locally in
`.local/release-compliance/native-sources/`. The committed
[acquisition manifest](../deploy/compliance/native/source-acquisition.json)
records URLs/hashes and extracted license-file hashes. Files were read from
archives without extracting arbitrary paths. Native recipe/version-file provenance
is recorded in [sources.json](../deploy/compliance/native/sources.json).

API/tools retain SkiaSharp's complete package third-party notices and fingerprint
the native payloads, including non-host architecture files inherited from NuGet.
Host `ldd` output is evidence for dependency inspection, not execution testing of
every architecture. No claim of added ARM support is made.

## Remaining release requirements

- Establish a complete corresponding-source delivery mechanism for the exact
  distributed LGPL/other source-bearing native and OS components. Retrieved
  archives alone are not that mechanism. Verify nested Rust crate sources,
  platform/toolchain/build inputs, applicable patches and replacement/relinking
  requirements against the actual binaries. Preserve needed installation
  information where the distribution/license conditions require it.
  The application-native portion now has the verified sidecar/rebuild/replacement
  follow-up above; OS and remaining-image coverage retain their separate checks.
- The `react-remove-scroll-bar` notice provenance task was addressed on 2026-11-17
  through exact npm runtime-file equivalence and the maintainer's license-only
  source ancestry. The original distribution omission and unavailable gitHead
  remain documented historical facts, not claims of inspected source.
- Complete a final artifact audit (including embedded/vendored runtime and browser
  components), current vulnerability review and repeat checks for the final release
  revision and every supported architecture. OS copyright preservation is not
  exhaustive source or image redistribution clearance.
- Resolve the [provider agreement findings](provider-agreement-evidence.md) and
  each operator's commercial eligibility/acceptance. No agreement was accepted
  and no provider credentials were used in this work.

## Verification

- Frontend: 722 tests across 97 files passed; ESLint and TypeScript passed.
- Local frontend production build passed outside the sandbox after sandboxed
  compiler output parsing failed; all three candidate production images built.
- Notice collector: seven focused tests passed, covering nested notices, missing
  required dependencies/primary texts, stable manifests, partial NuGet caches, embedded-license substitution and supplement tampering.
- Existing operator suite: 30 tests passed. Independent code review reproduced
  two collector gate gaps; regression tests, fixes and reviewer recheck resolved both.
- Browser: four Credits captures, both themes, desktop/mobile; independent UI
  review passed. No deployed OAuth/import or account lifecycle check was rerun.
- Image checks validate project license, package bundle hashes, empty missing-text
  lists, runtime evidence hashes and bundled native-source manifest presence.

No database/schema, roles, provider configuration, public deployment or publication
changed. The broader release gate remains open.
