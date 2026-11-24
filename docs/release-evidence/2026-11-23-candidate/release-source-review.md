# Candidate source and notice delivery review

Reviewed 2026-11-23 for Linux amd64 development candidate `0.1.0-dev.1`, source
revision `f19a90784abff9ea5b0d787d7fc3a4cb85ac2a4b`. `coverage.json` supplies the
exact API/web/tools image identities. These are local candidate materials, not
provider permission, an authorized publication or a stable-release designation.

## Delivery mechanism

Distribute the complete matching source-material bundle alongside the image and
installation archives. `coverage.json` maps every inventoried component/version
to actual source materials or a named, conditional source exception. The
installation archive supplies committed RateFreak source, Dockerfiles, maintenance
scripts and build instructions; its `SOURCE_REVISION` identifies the build tree.
Do not distribute the binaries while withholding their matching source materials.
Dependencies and fonts retain their own grants; RateFreak's MIT license does not
relicense them. Preserve original copyright, permission and disclaimer notices,
Apache notices and modification marking, and applicable non-endorsement terms.

## Exact-source material decisions

Every installed OS binary package is mapped through its exact source package and
distribution version to the prepared OS source archive. The unchanged archive's
historical image IDs identify its original collection; `os-image-bindings.json`
and this candidate's coverage map establish applicability to the current images.
All current source package/version pairs match that archive. Source descriptors,
original upstream inputs, distribution patches and build instructions are retained.
Its README describes restoring original filenames and `dpkg-source -x` extraction.
This conservative collection supplies sources without assuming an OS-wide license
exception. No additional installed OS package/version occurs only in lower layers.

All 28 named web native components and the LGPL sharp-libvips package map to the
new image-bound native sidecar, validated against the complete locked 406-input
source/build profile and exact shipped binary hash. Preserve the LGPL/GPL texts,
embedded notices and the documented modified-library replacement mechanism.
The earlier offline rebuild/replacement proof retains its original scope; it
was not repeated merely because the containing image identity changed.

npm's exact shipped JavaScript source, original registry source, explicit source
comparison/patch and source instructions accompany its runtime decision. Other
runtime components retain their individual reviewed decisions from
`runtime-review.md`; their exact names, versions, copied-file hashes and notice
supplement target checks match the current images. This is not an npm-wide or
Node-wide permissive assumption.

## Conditional package and framework source exceptions

Each application package has its exact manifest metadata and complete collected
license/notice hashes recorded in `application-source-decisions.json`. The
application notice bundles are byte-identical to the previously inspected
package bundles; every declared license has an accompanying text. MIT, ISC,
BSD-3-Clause, 0BSD and PostgreSQL grants permit the reviewed object/source
distribution while preserving their actual notices and other grant conditions;
they do not require an additional corresponding-source archive. Apache-2.0
portions retain complete licenses, supplied NOTICE content and source attribution;
there is no new modification to those upstream packages in this candidate.

Next 16.4.6's collected embedded licenses were separately inspected rather than
treated as MIT merely because its package declares MIT. The collected set
contains MIT, ISC, BSD, Apache, Blue Oak 1.0.0 and CC0 grants. Preserve those
complete texts. Its two MPL-2.0 texts belong to `dist/compiled/@vercel/og` and
its satori subdirectory. Every distributed web image layer was inspected: none
contains an OG/satori payload, yoga/resvg WASM or a TTF file. Those MPL texts
are conservative build-package notices, not evidence of shipping MPL code in
this candidate. A future OG/image-generation feature requires a new payload
review and any applicable preferred-source delivery; this exception cannot be
transferred to such a build without reinspection. No SWC native binary is shipped
under `/app`; its package notice is also a conservative inventory entry.

caniuse-lite's CC-BY-4.0 text, package identity and attribution remain in the
complete web notice bundle; retain them with the distribution. This license
does not introduce a corresponding-source requirement. No claim of a mandatory
visible product-page attribution is inferred solely from build-time use.

The .NET package notice supersets include generic references to LGPL rights and
a Mono/Valgrind discussion. The supplied Mono notice expressly grants BSD terms
for memcheck.h; the GPL reference describes the rest of Valgrind. The generic
LGPL reverse-engineering reservation in Roslyn/SolutionPersistence notices is
not a declaration of a separately shipped LGPL library. Complete package texts
are retained, including bundled Apache/BSD notices; no notice has been reduced
to its opening MIT paragraph.

The reviewed Node 24.22.2, .NET 10.0.14 and SkiaSharp 4.155.0 decisions in
`runtime-review.md` apply only because their actual payload fingerprints and
the complete applicable source-commit and native notice supplements match.
The delivery copies of the .NET/Skia archives normalize local tar ownership;
`portable-notice-provenance.json` proves unchanged member contents. Preserve
all original image notices plus those complete supplements, including FTL/IJG
acknowledgments and separately licensed Skia headers. Source exceptions are
conditional on that delivery, not based on the wrapper package's MIT label.

The migration executable was parsed without executing it. Its 104 embedded
entries and 36 dependency-library records contain no uninventoried NuGet package
version. Its embedded Skia binary matches the reviewed Linux x64 payload; its
embedded apphost identifies .NET commit
`0427b9abbead3fa64c6655eb4290c537397131fa`, matching the framework review.
The generated migration program and RateFreak assemblies have their source in
the installation archive. This evidence covers the bundle closure rather than
assuming that a development-tool dependency cannot ship.

## Sharp addon and fonts

The sharp 0.35.5 native addon is a separate payload from libvips. Exact registry
archives for sharp and its Linux x64 addon share source commit
`51a990faa26ade5586a4934ac9673c98d8893326`; the registry addon payload matches
the shipped binary. The sharp source archive retains C++ sources and binding.gyp.
Node-addon-api is a declared header build dependency. Its declared minimum
8.9.2 source and original MIT collaborator notice are supplied conservatively in
`sharp-addon-notices.txt`, with acquisition metadata and original archive. The
exact header resolution used by the upstream prebuild is not asserted; this is
not a byte-for-byte addon reproduction claim. Its MIT grant imposes notice
retention rather than mandatory source delivery. The source/notice material is
supplied separately with the matching binaries, including the addon Apache
license, rather than being covered by libvips's LGPL declaration.

DM Sans and Fraunces are the two unchanged WOFF2 files in the committed source
and web public directory. Their exact hashes, families and accompanying original
SIL OFL 1.1 copyright/license texts are recorded. Preserve OFL licensing and
attribution; do not sell the fonts alone or misrepresent modifications. Neither
included notice declares a Reserved Font Name. No font modification is made by
this candidate. Their original download/subsetting history is not independently
reconstructed by this artifact inspection; retain that provenance limitation.

## Limits

Per-component mapping validation proves recorded coverage and integrity, not
automated legal approval. All-layer inventories and source/artifact privacy
checks retain their recorded scope. Inherited base-image cryptographic test
content and PostgreSQL's public default snakeoil key are explicitly distinguished
from local private values; the tools entrypoint does not run that PostgreSQL
server or select its TLS certificate. No configured private value was found.

Provider clearance, fresh/upgrade/browser acceptance for these exact images,
current vulnerability review, operator-specific infrastructure acceptance and
publication authorization remain independent gates. A stable version/source
change requires refreshed candidate identity and relevant verification.
