# Focused native image-decoder assessment — 2026-11-25

This is a read-only technical investigation, not release clearance. Sources and
retrieval hashes are in acquisition*.json and input-hashes.json; actual retained
maintainer JSON records include complete affected ranges and descriptions.

## Actionable finding

Current Sharp0.35.5 / @img/sharp-libvips-linux-x641.3.4 embeds libheif1.23.5.
These are still the latest npm versions retrieved from the official registry.
The [libheif maintainer inventory](https://github.com/strukturag/libheif/security/advisories)
contains newer findings not represented by Sharp's package-level advisory list.
The focused high-priority set is GHSA-hhg3-fxg7-3gxw (file-open color-profile CPU),
GHSA-74mj-m37q-h3q9 (quadratic sequence lookup during file open), and
GHSA-fpr5-xpfr-vcq3/GHSA-jx54-6rm6-2cgj (AV1/libaom size-check bypass). The
retained assessment records affected1.23.5 and upstream fix commits. File-open
work or tiny AVIF files can exceed useful resource budgets; the5MiB response
limit and visible image dimensions do not establish exclusion.

The supported OpenLibrary proxy accepts image/avif (open-library-cover.ts:53).
Next's configured output format WebP does not prohibit AVIF input. The locked
native profile contains aom3.15.1, no dav1d/FFmpeg; the pinned packaging recipe
builds libaom before libheif with plugin loading disabled. Keep these findings
open until the decoder is fixed or blocked and the changed flow is verified.

## Smallest remediation candidate

[Sharp's block API](https://sharp.pixelplumbing.com/api-utility/#block) can disable
VipsForeignLoadHeif before server image requests. The standalone block-probe
passed: safe1x1 AVIF metadata succeeds before blocking and rejects after;
JPEG/PNG/WebP continue to decode and produce WebP. This is not yet production
Next instrumentation/worker proof. Every Node optimizer process must install
the policy before accepting requests and fail closed if installation fails.
A direct pinned Sharp dependency may be required for reliable pnpm resolution.
No native binary/source profile update is needed for this restriction; document
the loss of AVIF/HEIF optimization input. Custom native rebuilding is a larger
alternative requiring changed locks, source materials and artifact qualification.

## Completed bounded checks

Current Sharp0.35.5 and libvips8.18.7 are outside the affected version ranges of
all5 Sharp and12 libvips maintainer advisories retrieved. This does not clear
newer component-level reports. Current libarchive3.8.9 is beyond both retained
libarchive advisory fixes (3.8.8 and3.8.0).

Both Sharp and pinned Skia embed libpng1.6.58, within CVE-2026-46675's version
range. However that advisory requires png_read_end before starting image rows.
Exact libvips vipspng.c:210 documents never calling png_read_end; Skia's pinned
SkPngCodec.cpp uses the explicitly unaffected progressive png_process_data path.
This supports a caller-limited exclusion, not a claim that those copies are patched.

SkiaSharp4.155.0 has no published repository advisories; its exact submodule
is c2d78902a56e24827a7273a4ed3461ce18780680. No exhaustive Skia/Chromium or
recursive embedded-decoder security assessment was completed. Generic scanners
and repository-advisory absence cannot substitute for that remaining work.
