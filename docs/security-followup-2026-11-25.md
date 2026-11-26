# Security follow-up — Skia, database transition and decoder guards

**The release security gate remains held.** The owner requires a documented fix
or caller-level exclusion for every finding, including medium/low findings.
Vendor deferral, low severity and lack of a demonstrated exploit do not satisfy
that standard. No residual-risk acceptance or scanner suppression was added.

This follows the [earlier integration](security-integration-2026-11-25.md).
Its historical image identities remain unchanged. Current nonofficial source,
image, scan and assessment bindings are in the
[summary](release-evidence/2026-11-25-security-followup/summary.json) and
[ledger](release-evidence/2026-11-25-security-followup/advisory-ledger.json).

## Implemented changes

- Updated both SkiaSharp references to stable **4.157.0**. All four acquired
  official packages passed NuGet signature verification. All 20 native target
  paths/hashes were updated and checked during image builds. The complete
  native notice review retains the changed Vulkan header grant, compiler
  exceptions, FTL choice and IJG acknowledgment. See the
  [native source/notice review](skia-4.157-native-review-2026-11-25.md).
- For Skia CVE-2026-126912, the reviewed package pins Skia
  `51cb594d773b5383a45065ea3c8cbec398ad2c36`, which contains the fixed Chrome
  157.0.8103.78 Skia pin `fef60e46c522b87e5802ddf0a215a763dd5e0483` without
  missing ancestors. This is a source-lineage boundary, not an independent
  exploit reproduction or bit-for-bit native rebuild. The
  [acquisition/compare receipt](release-evidence/2026-11-25-security-followup/skia/fix-boundary.json)
  preserves exact upstream references and response hashes.
- Selected the pinned PostgreSQL **17.12 Trixie** image. Its Kerberos
  `1.21.3-5+deb13u2` addresses the two previously open shared ASN.1 findings,
  CVE-2026-107364 and CVE-2026-119079. Other Kerberos findings retain their
  separate caller assessments.
- Added a fail-closed collation preflight before migration and startup, with
  writers stopped. Bookworm-to-Trixie libc/ICU versions really differ; reusing
  an old data volume is not accepted merely because PostgreSQL stays on 17.
  The [operator guide](self-hosting.md) documents logical backup/restore into a
  fresh instance, preserving the backup's original application image binding
  before accepting new application images. No automatic metadata refresh or
  reindex shortcut is performed.
- Disabled Node's four public Zstandard decompression entrypoints during
  server registration. Node 24.22.3 embeds zstd 1.5.7 independently of the fixed
  OS package; built-in fetch could decode a peer-selected zstd response through
  that copy (CVE-2026-124851). The policy changes the real CommonJS exports and
  synchronizes builtin ESM exports before application networking starts.
  Gzip, deflate and Brotli remain supported. Upstreams requiring zstd responses
  fail until a patched runtime is qualified. The vulnerable native copy remains
  installed; this is a supported-caller mitigation, not a vendor patch.
- Explicitly disabled PostgreSQL JIT compilation in the reference Compose
  command. LLVM CVE-2026-13573 lacked a complete SQL-derived symbol-name bound.
  A real default instance demonstrated JIT compilation; the changed configuration
  reports JIT off, compiler unavailable, no JIT in a forced-cost query and no
  LLVM/provider mapping in a separately tested backend. Fresh HTTPS application
  acceptance passed on the existing qualified application images. Enabling JIT requires
  reassessment. The library remains installed.
- Retained the verified HEIF/AVIF decoder restriction after Next initializes
  Sharp. JPEG, PNG, WebP and GIF optimization remain supported.

## Exact candidate and validation

Source snapshot: `ede1b617bd58161e1404b1bc6fb7edd92a211102`, clean isolated
checkout, version `0.1.0-dev.1`, **nonofficial**. The final report and evidence
were added after the snapshot. The subsequent `postgres -c jit=off` Compose
configuration delta was separately tested on these same images and is bound by
[its input hashes](release-evidence/2026-11-25-security-followup/jit-policy/input-hashes.json).
The frozen installation archive still describes the earlier snapshot; it must
be regenerated to include that delta at final artifact qualification. The
application release version and publication authorization remain undecided.

| Image | Exact local image identity |
| --- | --- |
| API | `sha256:3984e4af4f632a888cdb60337103b44bc8c88a9450e89631761fef1b87e822e0` |
| Web | `sha256:c04ceb1759155f4b84d00b0bf51f24e0280a174affb8197430f3934316952e1e` |
| Tools | `sha256:4a3f1d57fecb70da5aadc05d16eb604545b0de59a502b6753511298e3804f772` |

| Check | Result and limits |
| --- | --- |
| Backend | 778 passed, zero skips, after the Skia update. Backend source is unchanged by the subsequent web-only zstd guard. The complete log preserves all suite counters; one reused TRX filename retained only the last suite. |
| Frontend | 1,026 passed; 13 focused real decoder tests passed. The zstd regression first failed against unguarded startup. TypeScript and lint passed. |
| Tooling | 77 operator, 26 release and 20 compliance tests passed. Release tests required an authorized retry after sandbox `spawnSync git EPERM`; the initial failure is retained. |
| Compiled web startup | Exact image rejects zstd after warming real fetch; all four decoder APIs reject through CommonJS and ESM. Gzip/deflate/Brotli work. A separately preloaded real Next tar parser also rejects zstd after registration while plain/gzip tar work. The existing real Next optimizer proof still rejects HEIF and accepts four raster formats. Safe synthetic inputs only, in disposable network-disabled containers. |
| Source/notices | 502 components, 47 material files and 406 web native inputs validated. All 20 new Skia payload hashes checked. Existing source-delivery/provenance limits remain; no bit-for-bit native reconstruction claim. |
| Distributed layers | All 43 layers and image identity chains inspected, including hidden/lower files. No configured private-value matches or suspicious application paths. The previously documented generic private-key pattern in the unchanged libgnutls binary remains a reviewed heuristic hit. This is not proof that every possible secret is absent. |
| HTTPS and browser | Fresh installation and prior-image application upgrade on Trixie passed. Secure sessions, origin/CSRF rejection, setup/recovery, ordinary/staff journeys and two themes at four widths passed. |
| Backup/restore | Candidate database/artwork/key restore preserved all 33 table fingerprints; independent current policy/journal replay and repeated replay passed. The fixture journal was empty. Disposable containers/volumes were removed; protected host proof directories remain retained. |
| Database OS transition | Separate Bookworm-to-fresh-Trixie logical restore passed using the original exact application images; separately, old-to-new application upgrade on Trixie passed. These are two bounded proofs, not a claim of one uninterrupted final stable-release migration rehearsal. |

The [verification record](release-evidence/2026-11-25-security-followup/verification.json)
binds logs and counters. The
[decoder probe](release-evidence/2026-11-25-security-followup/decoder-image-probe.json),
[restore proof](release-evidence/2026-11-25-security-followup/maintenance-acceptance.json),
[source validation](release-evidence/2026-11-25-security-followup/source-validation.json)
and [layer audit](release-evidence/2026-11-25-security-followup/all-layer-audit.json)
bind the current images. Exact native byte comparisons retain the differences
between historical and current images; the changed tools migration bundle is
not described as byte-identical.

## Remaining security work

Current scans contain **1,309 package associations across 565 source/advisory
records**. After the scoped assessments, **eight distinct IDs remain open**,
represented by 22 image/source records and 67 associations. All 22 are documented
proof gaps, not scanner suppressions or demonstrated RateFreak exploits. The
remaining vendor severities are medium/low; your selected standard still blocks
release. Zero unresolved scanner rows for an image does not establish complete
manual/native security coverage.

| Finding | Remaining proof required |
| --- | --- |
| CVE-2019-1010022 | Native thread/stack mitigation property; no applied fix or complete exclusion. |
| CVE-2019-1010024 | Native thread/stack mitigation property; no applied fix or complete exclusion. |
| CVE-2019-1010025 | Native thread/stack mitigation property; no applied fix or complete exclusion. |
| CVE-2026-102010 | GNU pb_ds heap erase_if; application source exclusion does not complete OS compiled-header provenance. |
| CVE-2026-114690 | GNU parallel search; complete OS header-instantiation closure not established. |
| CVE-2026-114691 | `vector<bool>` count overflow; complete OS caller/count-bound proof not established. |
| CVE-2026-115455 | Wide-character bitset extraction; PostgreSQL-image C++ consumer scope not completely excluded. |
| CVE-2026-125709 | `streambuf` I/O-failure handling; remaining OS/ICU caller closure not established. |

The [remaining-ID inventory](release-evidence/2026-11-25-security-followup/remaining-findings.json)
maps each ID to images, severity and package associations. Complete conditional
assessments remain in the ledger; counts alone do not establish exploitability.

The three glibc mitigation findings cannot be dismissed solely because vendors
dispute their security classification. The reviewed native thread/stack behavior
exists; no complete supported-caller exclusion or applied fix has been
established. Likewise, source searches of application dependencies do not prove
which affected C++ header templates the OS/native consumers used by supported
flows compiled.
Those gaps remain open under the selected release standard.

Exclusions elsewhere are conditional on the supplied reference installation:
fixed startup/health/maintenance commands, protected configuration and backups,
current schema/query callers and no added plugins, exotic locales, arbitrary
SQL, custom init scripts or alternative authentication/replication workflows.
They acknowledge actual library presence, root maintenance/startup, real GSS
negotiation, direct chown execution and noninteractive SQL caller details.
Internal networking, SCRAM alone and package counts are not blanket waivers.

Any changed fix or runtime selection needs matching image/source/notice,
security and affected acceptance evidence. Even after this security gate is
resolved, provider requirements, final stable artifact qualification,
load/capacity evidence, maintainer security-report notification delivery and
publication remain separate [release gates](STATUS.md#remaining-release-gates).

The persistent developer installation was not migrated or reconfigured.
Application processes remain stopped; only the existing persistent PostgreSQL
container remains running. Current source integration status is recorded in
[STATUS](STATUS.md); nothing was pushed or published. Large raw proofs and sources remain under
`.local/security-runtime-guard-2026-11-25/`,
`.local/security-skia-update-2026-11-25/`,
`.local/security-residual-os-2026-11-25/` and
`.local/postgres-security-transition-2026-11-25/`.
