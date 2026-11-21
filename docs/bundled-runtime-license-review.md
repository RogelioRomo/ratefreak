# Bundled runtime and SkiaSharp license review

Reviewed 2026-11-21 against the retained candidate and the disposable runtime
coverage proof images. This is a component review, not whole-image clearance.
No publication, production code or provider settings changed.

Subsequent integration: [runtime delivery verification](runtime-redistribution-verification.md)
records the completed framework/native notice packaging and decision binding in
new disposable proof images. The source review itself remains scoped to the
identified versions and payload hashes.

## Evidence and decisions

The exact image-extracted notices came from
`.local/release-compliance/runtime-fix-2026-11-21/{api,web,tools}-notices.tar.gz`.
Extracted public notices, source downloads, acquisition URLs, build recipes and
hash manifests are retained under
`.local/release-compliance/runtime-review-2026-11-21/native/`.
`files.json` records their byte sizes and SHA-256 hashes; `acquisition*.json`
records download results, including unsuccessful source-location guesses.
Hashes establish the bytes inspected, not upstream signature verification.

These rows are reusable review conclusions for the named source-inventory keys;
they are not a generated or accepted release `coverage.json`:

| Images | Scope / name / version | Source decision |
| --- | --- | --- |
| web | node / node / v24.22.2 | No mandatory source delivery identified for the reviewed Node binary aggregate under the complete exact LICENSE. Preserve that entire file, including embedded notices. npm, Corepack, Yarn and their separately inventoried runtime dependency closures need their own decisions. |
| api, tools | dotnet / Microsoft.NETCore.App / 10.0.14 | Reviewed source exception for the framework distribution, with retained image notices plus the exact source-commit notice supplement described below. OS libraries and EF/app closures remain separate. |
| api, tools | dotnet / Microsoft.AspNetCore.App / 10.0.14 | Reviewed source exception for the framework distribution, with the same complete notice supplement; Mono memcheck references are explicitly BSD for that file, not a GPL runtime obligation. |
| api | nuget / SkiaSharp / 4.155.0 | Managed wrapper MIT grant supports a source exception limited to the wrapper; retain its copyright and license. It does not cover native asset packages. |
| api | nuget / SkiaSharp.NativeAssets.Linux.NoDependencies / 4.155.0 | Reviewed source exception for all mapped Linux variants, including Bionic. Deliver complete Skia notice supplement and FTL/IJG acknowledgments. |
| api | nuget / SkiaSharp.NativeAssets.Win32 / 4.155.0 | Reviewed source exception covering all shipped DLL and PDB architectures with the same complete notice supplement. |
| api | nuget / SkiaSharp.NativeAssets.macOS / 4.155.0 | Reviewed source exception covering the shipped dylib with the same complete notice supplement. |

## Exact notice hashes

| Material | SHA-256 |
| --- | --- |
| web `/usr/local/LICENSE`, complete Node license, identical byte-for-byte to fetched v24.22.2 upstream LICENSE | `5888dbb9a1d2b18f2c3e6c5f6af1b39de658372b402a0577b002777f14c62ace` |
| api and tools `/usr/share/dotnet/LICENSE.txt` | `cfc21f5e8bd655ae997eec916138b707b1d290b83272c02a95c9f821b8c87310` |
| api and tools `/usr/share/dotnet/ThirdPartyNotices.txt` | `2dc8f8c5a39401e928b5784ab564eb8b3ceb99ead3df8f260e0cab7e0bbecc7a` |
| SkiaSharp wrapper and all three native asset packages `LICENSE.txt` | `89101e35a8c66fd4d6dffc1763259161d35cb564c169714ec227a768c89f2938` |
| All three exact native asset package `THIRD-PARTY-NOTICES.txt` files, identical bytes | `21504c46c4c58aa64c1055bd2dcbc5f9a136b4b8c412ed3cc6740e22c5b127f5` |

The API application notice manifest already records both SkiaSharp license and
third-party text hashes for each native package. The absence of a separate
Skia-named archive member does not mean the aggregate notice text is absent.
The runtime collector's native-binary list captures only 13 Skia files; direct
read-only image inspection captured all **20**, including Windows DLLs and PDBs.
`skia-payload-mapping.json` matches every image path/hash to bytes in the exact
4.155.0 NuGet cache. Package nuspec repository metadata names source commit
`d25e7fa628a2576e50ecc42004cc4fd03c6657f5`.

## Node: full bundled license reviewed

The retained 2,946-line [exact Node LICENSE](https://raw.githubusercontent.com/nodejs/node/v24.22.2/LICENSE)
contains the Node/Joyent MIT grants and separate sections for Acorn, c-ares,
merve, ittapi, amaro, SWC, ICU, libuv, LIEF, llhttp, Corepack, Undici,
postject, OpenSSL, Punycode.js, V8, SipHash, zlib, simdjson, simdutf, ada,
minimatch, npm, GYP, inspector_protocol, Jinja2, MarkupSafe, cpplint,
gypi_to_gn, GoogleTest, nghttp2, large_pages, caja, Brotli, zstd,
HdrHistogram, node-heapdump, rimraf, uvwasi, ngtcp2, nghttp3, node-fs-extra,
on-exit-leak-free and sonic-boom. Keeping only its opening MIT paragraph would
lose separately required notices.

The grants in those sections permit binary redistribution subject to copyright,
permission, disclaimer and attribution retention; BSD-style portions also restrict
endorsement. Apache portions require the license and applicable NOTICE
attributions and marking changed files; zlib portions restrict origin
misrepresentation and require preserving notices in redistributed source.
ICU includes Unicode, IBM, dictionary and other data notices. Retain the entire
file as the practical notice-preservation rule rather than selecting one label.
See [Apache section 4](https://www.apache.org/licenses/LICENSE-2.0).

Two GPL references inside ICU concern **aclocal.m4/pkg.m4 and config.guess**,
not a claim that ICU's runtime is GPL. Their notices expressly permit the
surrounding program's distribution terms when included in a program with an
Autoconf-generated configuration script, and identify ICU's configure script as
satisfying that condition. This supports the narrow binary source exception
above; it is not a general GPL exception for unrelated programs or a conclusion
about separately shipped build scripts. No Node corresponding-source archive
was needed to satisfy a source condition found in these reviewed grants.

## .NET: reviewed source exception and complete notice supplement

The shipped composite notice includes MIT, BSD-style, Apache references,
Unicode, IETF/RFC, W3C, LLVM/libunwind, public-domain algorithms, ISO SGML and
other notices. Preserve the complete composite plus project license; respect
binary copyright/disclaimer retention and non-endorsement clauses. The ISO
paragraph grants copying for conforming ISO 8879 SGML systems/applications;
it should not be relabeled unrestricted MIT. The reviewed text contains no
identified general copyleft source-delivery requirement, but does not constitute
a native library build inventory.

Fresh image reads of both
`/usr/share/dotnet/shared/{Microsoft.NETCore.App,Microsoft.AspNetCore.App}/10.0.14/.version`
report source commit `0427b9abbead3fa64c6655eb4290c537397131fa` and version
10.0.14. This commit resolves in the [dotnet VMR](https://github.com/dotnet/dotnet/commit/0427b9abbead3fa64c6655eb4290c537397131fa).
Exact VMR runtime and ASP.NET source notices were downloaded, in addition to
[the runtime version-tag notice](https://raw.githubusercontent.com/dotnet/runtime/v10.0.14/THIRD-PARTY-NOTICES.TXT)
and [ASP.NET version-tag notice](https://raw.githubusercontent.com/dotnet/aspnetcore/v10.0.14/THIRD-PARTY-NOTICES.txt).
The VMR runtime notice equals the fetched runtime-tag bytes; neither source notice
is the shipped composite. For example, the image has Unicode 1991–2020 and lacks
the source runtime notice's zlib-ng/OpenTelemetry/LinuxTracepoints sections.
The composite additionally includes SDK/tooling and ASP.NET content.

The exact source-commit runtime and ASP.NET licenses are byte-identical to the
image MIT license, hash `cfc21f5e8bd655ae997eec916138b707b1d290b83272c02a95c9f821b8c87310`.
The VMR source manifest separately identifies runtime commit
`6aa72daa24f66c01c3d7b6a32519e9d21396deec` and ASP.NET commit
`0ef4bbfa3291b306a21e5001cb0491277bdd35bc`; this records the component source
provenance rather than relying only on the framework version string.

Full comparison of the shipped composite and both exact source notices identified
MIT, BSD variants, Apache, Unicode, zlib, public-domain/CC0 and the additional
notice-preserving grants described here. The runtime's LLVM section uses the
University of Illinois grant, and libunwind-based code has its permissive grant.
ASP.NET's only GPL references occur inside the Mono notice's discussion of
Valgrind: **memcheck.h itself is explicitly BSD-style** and the GPL statement
applies to the rest of Valgrind. That statement does not require .NET runtime
source delivery. ISO SGML and IETF document-use restrictions are retained;
neither introduces a requirement to distribute framework source. The BSD/zlib
source-preservation conditions apply when source is distributed; they are not
mandatory source-publication clauses. No restrictive source-delivery condition
was identified for either reviewed framework aggregate.

The composite mismatch alone is therefore not a source-delivery blocker. A
truthful conservative notice supplement can keep all original notices and add
both exact source-commit license/notice sets, without claiming every named
SDK/test/platform component is embedded. This review prepared
`native/dotnet-redistribution-notices.tar.gz`, **35,299 bytes**, SHA-256
`d12315b23cf44f820a24fd4be0f82b6732e884d1d65299439aeb5e65f20426bb`.
It contains the unchanged image license/composite, full source-commit runtime
and ASP.NET licenses/notices, the full Apache-2.0 text, provenance README and
per-file hash manifest. Source runtime notice hash is
`66f1d4e44973185519bb4aa8a9718eb22fc7af2cc532e3ae9cfc4c127ee7fc54`;
ASP.NET source notice hash is
`52af3d603b6d069e635f8afd08ec44ec01559255b5f3afd161ae9e6fe7e7aa54`.
The complete bundle must accompany matching image artifacts or their associated
distribution documentation before final notice acceptance; preparing it locally
is not a public delivery event.

The decision is **no mandatory framework source delivery identified**, provided
the complete supplement accompanies the API/tools distributions and the original
in-image notices remain. This is an engineering license review inference from
full exact-commit terms, not an assertion of bit-for-bit upstream reproduction.
No .NET source archive was required by a condition found in this review.
The existing exact OS source mappings cover separately distributed OS libraries;
they do not depend on a system-library exception asserted for the whole image.

A read-only `ldd` inventory of the API framework `.so` files is retained in
`dotnet-api-native-ldd.txt`. Resolved external libraries are ordinary OS runtime
libraries; the optional tracing provider reports `liblttng-ust.so.0` missing,
which is not evidence that that absent library is shipped. This check does not
enumerate `dlopen` targets such as ICU/OpenSSL, which remain covered as shipped
OS packages. The framework notice supplement does not grant an exception for
the EF bundle, application NuGets, or new runtime components outside this scope.

## SkiaSharp: package MIT label is insufficient

The exact source commit's [submodule definition](https://raw.githubusercontent.com/mono/SkiaSharp/d25e7fa628a2576e50ecc42004cc4fd03c6657f5/.gitmodules)
points to mono/skia. Git tree metadata pins it to
`c2d78902a56e24827a7273a4ed3461ce18780680`. Acquired source archives:

| Archive | Size | SHA-256 |
| --- | ---: | --- |
| `SkiaSharp-d25e7fa.tar.gz` | 283094782 | `f215ea78cfa56d5bba137b0ddedc7ea84523465193a5e522683b7085d0dee1ce` |
| `skia-c2d7890.tar.gz` | 79870718 | `76cf954aa2738f89add9b79c08448d5a6ba8e5d5a1967532a0df1319d6830707` |

These archives were successfully parsed as tar archives. They preserve the
wrapper build recipes and Skia source/DEPS, **not a complete recursive dependency
source set or a verified rebuild**. GitHub-generated archives do not populate
submodules or DEPS downloads automatically.

The identical package notices list the following distinct obligations:

| Notice components | Redistribution treatment |
| --- | --- |
| ANGLE, Skia, libwebp, jsoncpp, SPIR-V Headers | Preserve applicable BSD/MIT copyright, conditions and disclaimers; respect endorsement restrictions. |
| HarfBuzz, Expat, ICU, imgui | Preserve copyright and permission notices, including ICU's separately incorporated notices. |
| etc1, piex, sfntly, SPIR-V Tools | Apache license copy, applicable attribution/NOTICE retention, and changed-file markings. |
| libpng, SDL, zlib | Preserve notices where required; do not misrepresent origin; mark altered source. The zlib notice identifies mozzconf.h separately as tri-licensed. |
| FreeType | Choose the FTL alternative rather than assuming GPL applies. Include distribution-documentation acknowledgment of the FreeType Team, retain FTL for source distribution and document source changes. [Upstream license choice](https://freetype.org/license.html). |
| libjpeg-turbo | IJG acknowledgment in product documentation, applicable TurboJPEG BSD text, and existing source notices/change markings when distributing source. The included roll-up is not a replacement for all referenced original license files. |
| Adobe DNG SDK | Preserve copyright/notices in human-readable distributed software; restrictions on documentation changes, trademarks and other agreement terms remain applicable. No general source-publication term identified in this grant. |
| gif | Embedded notice offers MPL 1.1/GPL 2+/LGPL 2.1+ alternatives. If actually shipped, select and meet a specific alternative; a MIT-only exception is unsupported. |
| libmicrohttpd | Notice specifies LGPL 2.1, with an eCos alternative only without HTTPS/SSL. No evidence supports choosing that alternative for an actual embedded build. |

If MPL-covered gif code is present, MPL 1.1 sections 3.2 and 3.6 require covered
source availability and executable-distribution source directions; the electronic
availability periods are at least twelve months after initial availability or
six months after a subsequent version. The scope is covered files, not automatic
publication of the entire application. See [MPL 1.1](https://www.mozilla.org/en-US/MPL/1.1/).
If LGPL libmicrohttpd is actually present, the retained full LGPL 2.1 text's
sections 4 and 6 require the applicable library source/combined-work conditions,
including a valid relinking or shared-library route as applicable; an ordinary
upstream homepage does not establish compliance. The exact package notice itself
is retained primary evidence for those terms.

The package notice is a **superset**, not a precise binary closure. The exact
[Skia DEPS](https://raw.githubusercontent.com/mono/skia/c2d78902a56e24827a7273a4ed3461ce18780680/DEPS)
and recursive tree contain no libmicrohttpd or legacy gif source. The shared
wrapper `GenerateGnBuild` function unconditionally adds
`skia_use_dng_sdk=false`, `skia_use_piex=false`, `skia_enable_tools=false` and
an official-build flag for release. Linux, Windows and macOS recipes all disable
ICU and HarfBuzz. These exclusions follow named source targets, not the
absence of exported symbols from stripped binaries.

The pinned [shared recipe](https://github.com/mono/SkiaSharp/blob/d25e7fa628a2576e50ecc42004cc4fd03c6657f5/scripts/infra/native/shared/native-shared.cake),
[Linux recipe](https://github.com/mono/SkiaSharp/blob/d25e7fa628a2576e50ecc42004cc4fd03c6657f5/native/linux/build.cake),
[Windows recipe](https://github.com/mono/SkiaSharp/blob/d25e7fa628a2576e50ecc42004cc4fd03c6657f5/native/windows/build.cake)
and [macOS recipe](https://github.com/mono/SkiaSharp/blob/d25e7fa628a2576e50ecc42004cc4fd03c6657f5/native/macos/build.cake)
establish the target configuration used for this review. The conservative
compiled dependency closure is now reviewed:

| Component / exact source pin | Scope and license decision |
| --- | --- |
| Expat `c7ffbf3879f6aef7a7b020ef84ddb4ee00222b19` | Explicit static Expat across recipes; MIT, including the newer exact nested `expat/COPYING` copyright text. |
| FreeType `0a0221a1347e2f1e07c395263540026e9a0aa7c7` | Linux uses static FreeType; Windows defaults to native font manager and FreeType false, macOS uses platform fonts. Conservative supplement covers FreeType regardless: select FTL, preserve full FTL and separately licensed BDF/PCF/hash X11, HarfBuzz-derived Old MIT, gzip zlib source/header grants. |
| libjpeg-turbo `9217719d3a58633923b096af4c1d50d304768a64` | JPEG encode/decode enabled; IJG/BSD and SIMD zlib grants reviewed. Exact relocated `src/turbojpeg.c`, `simd/nasm/jsimdext.inc`, README.ijg and full license acquired. |
| libpng `3061454d980de7d53608f594194cfac722721d2a` | PNG encode/decode enabled; full exact libpng grant retained. `contrib/gregbook` GPL example materials are outside the explicit library GN source list. |
| libwebp `4fa21912338357f89e4fd51cf2368325b59e9bd9` | Decode/encode enabled; BSD grant, separate PATENTS and integration notices retained. |
| zlib `89921383f1f51331f48a2dc843403779770ea3a9` | Enabled; exact permissive zlib license reviewed. The stale package notice's separately tri-licensed mozzconf.h is not in this pinned source tree. |
| Wuffs `e3f919ccfe3ef542cfc983a82146070258fb57f8` | Exact GN target compiles only BASE/GIF/LZW from generated `wuffs-v0.3.c`, version 0.3.3. [Generated file](https://raw.githubusercontent.com/google/wuffs-mirror-release-c/e3f919ccfe3ef542cfc983a82146070258fb57f8/release/c/wuffs-v0.3.c) has Apache-2.0 grant; Skia's integration BSD notice is separate. Both preserved, including the full generated file. |
| VulkanMemoryAllocator `eb744ea7a2b17040121b4bbb4d6f9e8a77e3cae7`, D3D12MemoryAllocator `169895d529dfce00390a20e69c2f516066fe7a3b` | Conservative GPU closure: Vulkan enabled Linux/Windows, D3D default enabled Windows, Metal macOS. MIT grants and D3D's additional NOTICES retained. |
| SPIRV-Cross `b8fcf307f1f347089e3c46eb4451d27f32ebc8d3` | GPU shader translation; Apache-2.0 plus distinct KhronosFreeUse header grant retained. |
| Vulkan-Headers `74d8a6cb930c68ef617b202c3ff3c59d919e086b` | Conservative DEPS/header scope; Apache-2.0/MIT grants retained. Actual Skia GN Vulkan target also uses checked-in `include/third_party/vulkan`; exact internal header files and separate BSD integration grant included. |
| Brotli `028fb5a23661f123017c060daa546b55cf4bde29`, HarfBuzz `36cb489cb02ce4b92099669ba9f9bea348eff93f` | Conservative additional DEPS coverage, not assertion that disabled HarfBuzz or non-Canvaskit WOFF2/Brotli paths are enabled. MIT/Old MIT notices retained. |
| Skia internals, skcms, skottie/jsonreader/skresources and integration code | Covered by exact Skia BSD grant and retained distinct skcms/integration notices. Tools, Dawn, DNG/piex, partition allocator, external ICU and legacy gif/libmicrohttpd do not enter these configured targets. |

Linux NoDependencies removes Fontconfig, not all compiled dependencies. Linux
Bionic variants still use `target_os='linux'`, so Android-target-only
cpu-features sources do not follow merely from the Bionic libc choice.
The exact GN source lists and pinned dependency licenses support the aggregate
source exception; a full bit-for-bit native rebuild was not performed.

`skia-elf-toolchains.json` records ELF comment/compiler and NEEDED metadata for
all thirteen Linux `.so` variants. The glibc/musl variants dynamically reference
libstdc++; Bionic has no dynamic C++ runtime reference and its recipe explicitly
statically links libc++. Comments identify LLVM 20.1.8 for ordinary Linux builds
and Android LLVM revision `d8003a456d14a3deb8054cdaa529ffbf02d9b262` for Bionic.
Exact LLVM libc++/libc++abi/compiler-rt licenses were acquired, including the
[LLVM compilation exception](https://raw.githubusercontent.com/llvm/llvm-project/llvmorg-20.1.8/libcxx/LICENSE.TXT).
The GCC runtime material's GPL does not establish a general static-library
exemption: retained **Runtime Library Exception 3.1** permits independent-module
target code generated through eligible compilation. The ordinary Clang/GCC
recipes and binary compiler records support that specific permission; they show
no proprietary manipulation of GCC intermediate representation. This decision
does not exempt a separately shipped GCC runtime binary from its own terms.

The reviewed decision for all three native asset package keys is **no mandatory
source delivery identified for this conservative compiled closure**, with complete
notice preservation and explicit FTL election. It is not a declaration that every
entry in the old package notice is permissively licensed. The source files and
build configuration distinguish unused copyleft alternatives from the actual
permitted binary-distribution grants.

Prepared supplement:
`native/skia-redistribution-notices.tar.gz`, **679,410 bytes**, SHA-256
`0d37f37d576bd3ecd2f7763a4a4ca9ccc6513465e4366159b4ddd2cb0ece0e12`.
All **97 payload hashes** were verified from the finished archive. It retains
the original package notices, exact dependency/header/compiler notices and
provenance. Its accompanying README states both required acknowledgments:

> This software is based in part on the work of the FreeType Team.
>
> This software is based in part on the work of the Independent JPEG Group.

Full original FreeType copyright wording and dates remain unchanged in its FTL
and source notices; the acknowledgment invents no copyright date or holder.
`skia-reviewed-decisions.json` records the three package-key decisions tied to
the candidate image and this notice-artifact hash. Deliver the complete supplement
with matching native artifacts before final notice acceptance.

Twelve exact dependency source archives were additionally acquired and parsed;
`acquisition-skia-dependency-sources.json` records their pins and hashes.
The JPEG source archive timed out, but its complete applicable license and
separate header grants were acquired. This is an optional source-acquisition
limitation, not an unfulfilled source-delivery condition found in the reviewed
licenses. The retained source inputs are not advertised as a complete rebuild
kit, and no source sidecar needs to be represented as such for these exceptions.

## Remaining scope

This review establishes bounded Node, .NET framework, SkiaSharp wrapper and
native asset source exceptions with concrete notice preservation. The .NET
notice supplement is prepared and must accompany the matching distributions.
The Skia native conservative closure is reviewed with its full notice
supplement prepared. The acquired wrapper/Skia archives are useful exact source
inputs, not a completed native source-delivery sidecar. Separate OS package sources,
web native sidecar, npm/Corepack/Yarn closures, EF executable, fonts, browser
bundles and full OCI-layer review remain governed by their existing audits.
Reconcile all findings to final release image hashes before publication.
