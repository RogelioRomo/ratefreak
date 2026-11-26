# Security integration and remaining clearance — 2026-11-25

**The release security gate remains held.** This follow-up integrates the Debian
13 trial into the shared checkout, updates the reference PostgreSQL image, and
mitigates a reachable native image-decoder exposure. Installation, upgrade,
recovery and current-image checks passed for the nonofficial proof described
below. Remaining findings are preserved rather than suppressed.

The changes remain uncommitted in the shared checkout. Builds use a clean,
isolated local source snapshot at
`8cc1462a32c826b98d7267b5173cd9c8b9329a45`, version `0.1.0-dev.1`, with
`official=false`. The subsequent evidence/status documentation is outside that
snapshot. Nothing was pushed or published, and the persistent developer
database, its existing image selection and provider settings were not changed.

## Changes and rationale

- Web/tools final stages use the previously tested pinned Debian 13 base;
  tools use ICU76 and the signed `trixie-pgdg` repository. Node 24.22.3,
  .NET 10.0.15 and PostgreSQL client major 17 remain unchanged.
- The reference PostgreSQL pin selects the official 17.12 Bookworm image,
  including gosu 1.20 and its corrected account-file parser dependency. The
  database major version and data-directory layout remain unchanged.
  [PostgreSQL's migration notes](https://www.postgresql.org/docs/release/17.12/)
  retain installation-specific index-repair and incremental-backup caveats;
  the disposable upgrade does not establish every existing index is unaffected.
- Runtime APT stages were rebuilt without cached package-install layers.
  This picked up a listed libzstd security fix and newer Ubuntu expat, libssh
  and SQLite packages. A cached Docker layer containing `apt-get upgrade`
  does not establish that current packages were installed.
- Startup disables Sharp's `VipsForeignLoadHeif` loader. The shipped libheif
  1.23.5 matches newer maintainer advisories, and the supported Open Library
  proxy previously accepted AVIF for native decoding. Outputting WebP and
  limiting input size do not remove the affected decoder paths. The retained
  upstream package metadata offered no newer compatible prebuilt at review time.
  HEIF/AVIF server decoding is restricted; JPEG, PNG, WebP and GIF optimization
  remains supported. The native library is still present and is not claimed
  patched. The direct TMDB poster byte-cache path is unchanged.

The decoder policy must run **after** Next initializes its own loader allowlist:
Next 16.4.6 explicitly re-enables HEIF on first optimizer use. Independent review
found this bypass in the first implementation. The corrected implementation
initializes the exact Node runtime optimizer module, applies the block, then
starts the TMDB scheduler. Next's awaited instrumentation hook completes before
requests are served. Failure to install the policy propagates out of startup.
The pinned internal Next module path, same Sharp instance and process coverage
must be requalified when changing Next or the server/worker topology.

## Exact proof and acceptance

Image identities, scanner/config digest bindings and source identity are retained
in [the summary](release-evidence/2026-11-25-security-integration/summary.json)
and [scan bindings](release-evidence/2026-11-25-security-integration/scan-bindings.json).
These identities describe this local proof, not a stable release.

| Check | Recorded result |
| --- | --- |
| Decoder regression | Real AVIF decoding succeeded before the mitigation; the regression failed. The corrected test also failed against the first implementation's lazy Next initialization, then passed after the fix. |
| Frontend suite | 1,018 tests passed; TypeScript and ESLint passed. The suite was run outside the sandbox after sandboxed filesystem/child-process checks failed. |
| Operator/release/compliance | 72 + 26 + 20 tests passed. Release tests needed an unsandboxed rerun for Node's nested Git process. |
| Production builds | API, web and tools built from the same clean nonofficial source revision. |
| Compiled startup and optimizer | Exact web-image instrumentation and real Next optimizer share the same Sharp object; AVIF metadata/optimization reject after lazy optimizer access; JPEG/PNG/WebP/GIF still optimize. No network or application volumes were used by this probe. |
| Fresh HTTPS | Setup, secure cookies, CSRF/Origin rejection, ingress-header replacement, ordinary accounts, protected artwork/privacy, export and logout passed. |
| Upgrade | Bookworm/TMDB proof images to the corrected image set, with PostgreSQL 17.11 → 17.12; database fingerprints and artwork preserved. |
| Coordinated recovery | Database/artwork/key restore preserved all 33 table fingerprints. An independent current TMDB policy and erasure journal were supplied; startup guard and repeated replay passed. The journal fixture was empty, so this is not a new nonempty-erasure proof. |
| Chromium | Trusted HTTPS ordinary/staff journeys passed; administrator/update states checked in both themes at 1440/768/390/320 widths. |
| Source/notices | 502 image/component entries validated across 43 material files; four new OS source versions have 14 complete, hash-verified descriptor inputs. All 406 locked native inputs were verified and rebound to the current web image. Application notice manifests match the prior trial. |
| Distributed-layer/privacy audit | All 43 layers inspected. Seven configured private values matched no content/configuration; no suspicious application-data paths. The one generic private-key-pattern hit is unchanged upstream libgnutls binary content, byte-identical to the previously reviewed payload. This is not proof of exhaustive secret absence. |
| Disposable resources | Acceptance projects' containers and volumes removed; no persistent installation was upgraded. |

Backend code and schema were unchanged; the historical 778-test backend result
was not presented as a new full backend-suite run. A first recovery fixture
incorrectly assigned the current TMDB policy to the host user; restore refused
it. The corrected fixture preserved service ownership and passed without
weakening the product's ownership checks.

## Current scan and scoped assessments

Trivy 0.76.2 used its pinned scanner image and the database updated
`2026-11-25T13:07:24Z`. No ignore file or ignore-unfixed option was added.
Raw reports remain locally retained; their hashes and compact, reconciled
findings are in the public evidence directory.

| Image | Raw package/advisory matches | Critical / high | Listed fixed-version matches |
| --- | ---: | ---: | ---: |
| API | 124 | 0 / 0 | 0 |
| Web | 343 | 0 / 77 | 0 |
| Tools | 382 | 0 / 80 | 0 |
| Reference PostgreSQL 17.12 | 615 | 4 / 141 | 61 |
| Reference Caddy | 21 | 0 / 2 | 18 |

Counts are repeated package/advisory associations, not independent exploitable
bugs. The November 23 database produced different counts; it is not a valid
unchanged-database comparison with this scan. Zero non-OS matches in the three
application-image scans does not clear embedded native libraries.

The [ledger](release-evidence/2026-11-25-security-integration/advisory-ledger.json)
retains every match, source context, version, vendor evidence and disposition.
It conservatively leaves matches open unless the recorded scope supports a
decision; additional consumer-specific assessments must not be generalized to
all callers of a package.

- The trial's 20 conditional advisory assessments were carried forward with
  current-image runtime/feature probes and eight hostile-archive checks. They
  cover 167 matches; vendor affected-package status is not erased.
- [Four OpenSSL consumer assessments](release-evidence/2026-11-25-security-integration/openssl/review.md)
  address the PEM existing-stack, CMS encryption, DANE and CMS/PKCS7 revocation
  conditions for the reviewed Node/.NET/libpq callers. Final-image runtime and
  crypto-shim/libpq hashes were rebound to those inputs. This is not a blanket
  exclusion for API curl or every other native consumer. Revocation checking
  exists; it must not be described as universally absent.
- [Caddy's focused assessment](release-evidence/2026-11-25-security-integration/service/assessment.md)
  and [OS-helper assessment](release-evidence/2026-11-25-security-integration/service/caddy-os-assessment.md)
  account for all 21 matches within the supported reference configuration.
  Evidence includes the actual trust-store helper chain, OpenSSL caller
  conditions, ordinary zlib build versus the affected contrib code, disabled
  admin API and absent request-body/tracing/HTML/OpenPGP handlers. The newer
  official Caddy digest has the same Caddy binary; changing that digest alone
  would not patch Go. The existing reference pin is retained.
- Gosu's 60 Go standard-library findings concern operations absent from its
  supported local identity-switch/exec workflow; the Windows finding is
  platform-excluded. Updating PostgreSQL/gosu fixes the separate account-file
  parser advisory. These decisions do not clear PostgreSQL's OS libraries.
- [The native review](release-evidence/2026-11-25-security-integration/native/review.md)
  checks maintainer advisories beyond the package-manager scans. The libpng
  1.6.58 finding CVE-2026-46675 concerns a particular `png_read_end` sequence;
  inspected libvips and pinned Skia readers use different paths. The HEIF
  exposure is mitigated by the verified loader restriction, rather than waived
  from a version number or MIME check.

## Work still preventing security clearance

1. **Finish PostgreSQL shared-library exposure decisions.**
   [The focused PostgreSQL review](release-evidence/2026-11-25-security-integration/postgres/assessment.md)
   proves that GSS negotiation can enter Kerberos before `pg_hba` selection.
   SCRAM configuration and absence of a keytab do not alone exclude shared
   ASN.1 findings, particularly CVE-2026-119079 and CVE-2026-107364. The reference
   database has no published port, but this does not prove the parser absent.
   Establish exact affected-function/credential ordering or remediate the
   relevant library/build before closing those findings.
   An isolated [Trixie comparison](release-evidence/2026-11-25-security-integration/next-remediation-options.json)
   supplies a concrete alternative: official PostgreSQL 17.12 Trixie contains
   Kerberos `1.21.3-5+deb13u2`, matching Debian's fixed version for both IDs,
   and neither appears in its same-database scan. Raw matches fall from 615 to
   439. That image still needs residual assessment and installation/recovery
   qualification; its glibc 2.41 database collation behavior must not be treated
   as an automatically safe replacement for an existing Bookworm data volume.
   The reference pin remains the qualified 17.12 Bookworm image.
2. **Finish remaining OS source/caller assessments.** SQL/XML is genuinely
   compiled into PostgreSQL; current application queries do not use it, but
   arbitrary SQL/XML, extensions or operator changes require separate decisions.
   Custom logical backups default to gzip; that narrowly excludes some zstd
   workflows, not imported zstd archives or crafted WAL. The tools-image zstd
   fix does not patch the database image's copy. Other residual source families
   remain in the ledger. Temporary Debian IDs are not automatically missing
   evidence: some have detailed GHSA links/fixes in the retained raw tracker.
3. **Resolve remaining Skia and embedded-component scope.** Pinned
   SkiaSharp 4.155.0 maps to Skia `c2d78902a56e24827a7273a4ed3461ce18780680`.
   The November Chrome advisory CVE-2026-126912 identifies a Skia buffer overflow,
   but the retained public issue/fix searches did not establish whether the
   affected path reaches RateFreak's CPU raster uploads. Other identified
   GPU/glyph/path fixes do not resolve that unknown. A follow-up source-ancestry
   check establishes that stable SkiaSharp 4.157.0's Skia pin
   `51cb594d773b5383a45065ea3c8cbec398ad2c36` contains
   `fef60e46c522b87e5802ddf0a215a763dd5e0483`, the Skia revision pinned by
   Chrome's fixed `157.0.8103.78` release. The current 4.155.0 pin diverges and
   does not contain that source boundary. This identifies a concrete update
   candidate; it does not reproduce the private bug, prove a RateFreak exploit,
   or qualify the replacement NuGet native binaries. Review the new payloads,
   matching notices/provenance and relevant acceptance before adopting it.
   Complete the remaining recursive-codec and embedded Node component review,
   including the independent embedded zstd copy. The shipped Next tar module
   contains zstd API references; a lexical search is not a reachability proof.
4. **Qualify any further changes and the final stable candidate.** Maintain
   matching source/notices, inventories, all-layer/private-data checks and
   relevant install/upgrade/recovery proofs. The final stable version/revision
   and operator-specific host/container-engine checks remain separate gates.

Large private artifacts, full scanner reports, signed source indexes and vendor
downloads are retained under `.local/security-clearance-2026-11-25/`. Public
hash manifests identify them without copying private fixture configuration or
credentials into the repository. Prior candidate records retain their original
identities and scope.
