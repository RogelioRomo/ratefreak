# SkiaSharp 4.157.0 native notice and source-closure review

Reviewed 2026-11-25. This is a focused redistribution/provenance review for the
stable SkiaSharp update, not release security clearance or a native rebuild.
No shared product files, previous evidence or persistent installation were changed.

## Exact inputs and pending image binding

The four official NuGet archives in `../packages/` contain SkiaSharp,
SkiaSharp.NativeAssets.Linux.NoDependencies, SkiaSharp.NativeAssets.Win32 and
SkiaSharp.NativeAssets.macOS, all version **4.157.0**. Their repository metadata
names wrapper commit `2a53a3c8641073bc914c63ee158bbf62b8a0bb92`, matching the
stable release source. Its Skia submodule is
`51cb594d773b5383a45065ea3c8cbec398ad2c36`. Acquisition metadata records exact
official URLs, archive hashes and signature-file presence; this note does not
claim an independent signature trust-validation run.

`skia-payload-mapping.json` records every native archive member and its expected
`/app/runtimes/...` path: **20 payloads**, comprising thirteen Linux ELF libraries,
three Windows DLLs, three Windows PDBs and one universal macOS dylib. The path set
matches 4.155.0, but **all twenty payload hashes change**. These are package-bound
expected hashes, not a claim that unbuilt images already contain them. Match the
actual API/tools publish outputs to this mapping before adopting the targets.

The current package LICENSE is byte-identical to the reviewed prior MIT text.
All three native-asset package THIRD-PARTY-NOTICES files are byte-identical to
each other, 112,187 bytes, SHA-256
`243f2276bde636931b1cf8517fb11c0451be857996fbe1a2c690a3c7d39502ac`.
The earlier notice was 139,775 bytes and included an unused libmicrohttpd section
that upstream has removed. Current original notices are preserved verbatim.
The absence/removal of a package notice is not used as proof of compiled scope.

## Source/build comparison

The full new wrapper and Skia source archives are retained under `upstream/archives/`,
with exact source URLs and hashes in `acquisition-archives.json`. Current DEPS,
GN settings, shared recipe and Linux/nodeps/Windows/macOS recipes are retained in
`upstream/` and the notice archive's `source-configuration/` directory.

The old and new DEPS have the same conservative codec/font/GPU dependency pins
except **Vulkan-Headers**: `74d8a6cb930c68ef617b202c3ff3c59d919e086b` becomes
`c46850864f4661461b0f6cb9922c058ffea4915e`. `deps-comparison.json` includes the
complete parsed mapping and sole change. The changed external header archive and
current LICENSE.md, full Apache-2.0 and full MIT texts were acquired. LICENSE.md
changes; the full Apache/MIT grant texts are byte-identical to their earlier
versions. No new license election or incompatible grant was identified.

The Skia-owned Vulkan header namespace is assessed separately from the external
DEPS entry. All **33 current internal header files** were freshly acquired from
the current Skia commit and match the earlier reviewed bytes; its distinct BSD
integration grant is preserved. `internal-vulkan-comparison.json` records that
comparison rather than silently reusing an old source label.

The recipes still explicitly disable DNG SDK, piex, tools, partition allocator,
external ICU and HarfBuzz for the reviewed targets. The complete recursive Skia
tree contains no libmicrohttpd, legacy giflib or mozzconf.h. Wuffs' exact GN
target still compiles only generated BASE/GIF/LZW modules and retains the full
Apache-2.0 generated-source grant and separate Skia BSD integration notice.
Current defaults enable libpng decode/encode and disable Rust PNG/JPEG paths.
The new WASM-only Dawn glue does not enter the Linux/Windows/macOS payload set.

Linux nodeps continues to set Fontconfig false. A new core dependency makes the
custom-directory font manager available when Fontconfig is absent; this is
Skia-owned code plus the same already-covered FreeType dependency. It does not
introduce a new external font engine or license choice. The current Windows
recipe changes toolchain selection/validation plumbing but retains the reviewed
static codec choices, `/MT` and conservative GPU settings. macOS's native minimum
is now 12.0; this is a compatibility fact for its embedded universal asset, not
a new promise that RateFreak supports macOS-native installation.

The upstream recipe removes earlier dependency-verification helper calls.
This review does not assume those removed helpers ran: actual Linux ELF NEEDED
and compiler metadata were read independently. Their absence from the recipe is
not treated as evidence that dependencies disappeared.

## Reused and refreshed notice grants

All 97 previous notice inputs were hash-verified before copying into the new
notice directory. `notice-reuse-comparison.json` identifies **92 unchanged prior
paths** and five refreshed prior paths (README, current package notice, payload
mapping, ELF metadata and Vulkan-Headers LICENSE.md). Newly retained provenance
and GCC 16.2 texts are additional files, not changes to old evidence.

The unchanged exact dependency pins support retaining their already-reviewed
complete grants: Expat MIT; FreeType's explicit **FTL alternative** plus separate
BDF/PCF/hash, HarfBuzz-derived and gzip grants; libjpeg-turbo IJG/BSD/zlib grants;
libpng; libwebp BSD and PATENTS; zlib; Wuffs Apache-2.0; conservative allocator,
SPIRV-Cross/Khronos and header notices. This is a source/build-supported compiled
closure, not an assumption that every license in the superseding package notice
governs compiled code.

Both required acknowledgments remain in the README and text supplement:

- This software is based in part on the work of the FreeType Team.
- This software is based in part on the work of the Independent JPEG Group.

The FTL election was already part of the repository's reviewed distribution
policy; no new owner license choice is required by the observed update.

## Compiler/runtime scope

`skia-elf-toolchains.json` records readelf output from all thirteen new Linux
payloads. Ordinary Linux comments still identify LLVM 20.1.8. Bionic identifies
Android LLVM commit `d8003a456d14a3deb8054cdaa529ffbf02d9b262`. All ordinary
glibc/musl variants retain dynamic libstdc++ links; Bionic has no dynamic C++
runtime dependency and its recipe still requests static libc++. The full exact
LLVM compilation-exception texts retained by the earlier review remain matched
to these observed compiler families.

The conservative GCC release grant set is extended with GCC 16.2.0 because that
version appears in one payload's metadata. Newly fetched COPYING.RUNTIME and
COPYING3 are byte-identical to the previous conservative GCC release grants.
The Runtime Library Exception 3.1 conclusion remains limited to eligible
compilation by the ordinary disclosed Clang/GCC recipes. It is not a general
GPL exemption and does not clear a separately shipped OS libstdc++/libgcc binary.
Compiler version/comment observations do not reproduce the compiler or establish
the exact provenance of every inlined instruction or Windows toolchain component.

## Deliverables and disposition

- Investigator's assembled `SKIA-REDISTRIBUTION-NOTICES.txt`: **622,818 bytes**, SHA-256
  `4bb6f3dc4b519ba01e689c691c69ac35ad27a324c8a5bf090b759558cbddac8a`.
- Shipped text supplement: **622,674 bytes**, SHA-256
  `bc3e2ea64ac6341c786ab4d0e1407de17c6330e08c66eb64dcc9c63a6050547e`.
  Only trailing whitespace in the generated composite was normalized. All
  non-whitespace tokens match; the archive preserves original grant texts
  byte-for-byte. The runtime supplement index pins the shipped copy.
- `skia-redistribution-notices.tar.gz`: full notice/provenance directory,
  **888,450 bytes**, SHA-256
  `5c4703f11ecaf305fc298574075fe534fcc2b4174499eb45a0b88da897794363`.
  All **126 manifest payloads** were read back and hash/size verified.
- `source-exception-decisions.json`: current-version dispositions for the three
  native-asset package keys, with pending actual-image path/hash binding.
- `skia-payload-mapping.json`, `skia-elf-toolchains.json`, acquisition manifests,
  source/build diffs, dependency comparison and `input-hashes.json`: evidence.

The evidence supports retaining **no mandatory aggregate native-source delivery
identified for this conservative configured closure**, conditional on complete
current notice delivery, the existing FTL election and exact matching payloads.
This disposition must not be copied to changed assets/builds without review.
Separately shipped OS/native libraries retain their own requirements. Native
source archives are retained for evidence, but the notice archive is not a full
rebuild/source kit. No native bit-for-bit rebuild, exhaustive binary-to-source
correspondence or stronger existing source/header/font provenance is claimed.

The main task independently verified the official package signatures. Remaining
acceptance for this review is actual image/publish payload binding, runtime
compatibility and corresponding source coverage. This focused review does not
clear the overall release security gate.
