# Runtime security update inputs — 2026-11-23

Follow-up: the [remaining OS review](release-os-review-2026-11-23.md) reconciles
all retained findings with vendor records and records scoped exclusions. The
security release gate remains held; that review also identifies separate bundled
Node/OpenSSL work not represented by the previous zero non-OS scanner result.

This update retains .NET 10, Node 24 and PostgreSQL 17 compatibility while
replacing outdated build/runtime inputs and avoiding unnecessary shipped tools.
It does not migrate an existing instance, alter application schema or authorize
publication. Exact image acceptance and source coverage must describe the new
payloads rather than reuse an earlier candidate's identity.

## Runtime composition

- API/build tools use pinned official .NET SDK 10.0.404 and ASP.NET runtime
  10.0.15 images. Both framework version markers identify VMR commit
  `8e0f8ed2312954727f8919d7d1c2f78b483b4ee7`.
- Web builds use pinned official Node 24.22.3. The final Debian Bookworm image
  copies only `/usr/local/bin/node` and the complete `/usr/local/LICENSE` from
  that build stage, alongside the application. npm, Corepack, Yarn and their
  inherited runtime layers are not selected for the final image.
- Tools use a pinned Debian Bookworm base, PostgreSQL 17 client packages from
  the authenticated upstream APT repository, and the patched copied .NET
  runtime. Maintenance retains psql, pg_dump and pg_restore; a PostgreSQL server,
  its Docker entrypoint and gosu are unnecessary and are not copied into this
  final image. The separate database service is unchanged.
- Runtime APT steps refresh installed packages before application installation.
  The final package inventories, not the mutable repository name, identify the
  installed distribution versions. This does not imply every vendor-unfixed
  advisory is resolved.

## Repository authentication and notices

`deploy/self-host/postgresql-signing-key.asc` is the public key obtained from
<https://www.postgresql.org/media/keys/ACCC4CF8.asc>. Its SHA-256 is
`0144068502a1eddd2a0280ede10ef607d1ec592ce819940991203941564e8e76`;
the primary OpenPGP v4 fingerprint is
`B97B0AFCAA1A47F044F244A07FCC7D46ACCC4CF8`, checked against
[PostgreSQL's installation guidance](https://www.postgresql.org/download/linux/debian/).
The source file uses HTTPS and explicit Signed-By; no unsigned-repository or
certificate-verification bypass is used.

The patched .NET image license/composite and full runtime/ASP.NET licenses and
notices from the exact new VMR commit are byte-identical to their previously
reviewed counterparts. The complete supplemental grant texts are retained;
provenance labels, framework version-marker paths and target hashes are updated.
The new Node composite license is byte-identical to the prior inspected file,
SHA-256 `5888dbb9a1d2b18f2c3e6c5f6af1b39de658372b402a0577b002777f14c62ace`.
It accompanies the new binary and is separately hash-pinned. Skia payload pins
and complete notices remain applicable to the unchanged native packages.

Supplement applicability remains explicit by image family. Rules for absent
package managers and PostgreSQL entrypoints are removed for the new image
composition, rather than weakening missing-target checks. Historical notice files
and reviews remain in the repository for their original payloads; final images
copy only their currently applicable supplements and index.

## Development dependency

The scoped `eslint-config-next>typescript-eslint` resolution selects upstream
8.74.0, within Next's declared compatible range, whose parser requires
minimatch ^11.0.1 and locks the patched 11.0.9 release. This updates the coherent parser/plugin closure without forcing
a minimatch major version underneath a package that requests minimatch 10.
Application dependency versions and native source profiles are unchanged.

## Verification scope

The prior scan reports reproduce the affected old framework/package versions.
The complete updated pnpm graph reports no known advisories. The results below
qualify the recorded local proof scope, not a stable/public release or a blanket
security clearance.

## Verified result

Production API/web/tools images were built from clean isolated source commit
`a1276b1fb70ad4e371decdad53dcab2ef6068f3d`, using distinct local
`security-update-2026-11-23` tags and explicitly nonofficial identity. The prior
candidate archives/tags remain retained. The implementation is copied into the
main checkout for review, without committing or changing its existing instance.
The source snapshot's minor minimatch wording is corrected above: the upstream
minimum is ^11.0.1 and the verified lock resolves 11.0.9.

| Check | Result |
| --- | --- |
| Complete release verifier | All 15 checks passed; 756 backend tests with zero skips, 942 frontend tests, 98 Python tests, lint/typecheck, production builds and model consistency |
| Exact-image Trivy scans | No non-OS vulnerability matches and no remaining finding with a listed fixed version, using the same pinned scanner/database as the previous review |
| API raw matches | 73 → 46; remaining matches are OS medium/low |
| Web raw matches | 487 → 475; remaining matches are Debian OS findings |
| Tools raw matches | 685 → 493; remaining matches are Debian OS findings |
| Layer inspection | All 43 distributed layers inspected; removed package managers, gosu, original upstream entrypoints and PostgreSQL server are absent from every layer |
| Fresh HTTPS | Cookies/CSRF/Origin, decoded uploads/privacy/export, update access and logout checks passed |
| Upgrade | Prior `f19a907` candidate → new images preserved database fingerprints/artwork and required new image-set migration acceptance |
| Chromium | Serialized trusted-TLS rerun passed 25 page journeys at four widths, including both-theme admin/update behavior and ordinary/logout concealment |
| Updated maintenance clients | Real backup and isolated database/artwork/key restore preserved all 30 table fingerprints; current empty fixture journal replay and repeated replay passed |
| Unpopulated installation | Zero accounts, catalog items or artwork before test fixtures; five category definitions plus pending setup and migration history only |

The scanner counts are package/advisory matches, not independently exploitable
application bugs. The total fell from 1,245 to 1,014; no ignore or ignore-unfixed
setting concealed remaining findings. Debian's vendor-unfixed/deferred/ignored
findings, and embedded native-component coverage, still require their recorded
review. The previously documented zlib MiniZip applicability qualification is
unchanged; remaining critical SQLite/zlib labels are not described as remotely
exploitable application vulnerabilities or silently accepted.

The first Chromium run encountered `net::ERR_NETWORK_CHANGED` while concurrent
Docker tests created/removed network interfaces. It was rerun after those tests
finished and passed without application changes or a TLS bypass. The maintenance
replay uses an empty current journal and does not replace earlier nonempty
erasure/recovery proof. The PostgreSQL client commands are 17.12; PGDG resolves
their shared libpq library from its 19.0 source package. The separate running
database service and application schema are unchanged.

Seven configured private values matched no image content/configuration. Generic
key-pattern matches are confined to unchanged upstream libgnutls binary bytes,
explicitly distinguished from newly copied local private keys. All disposable
services/volumes were removed; only the existing developer database remains running.

See [verification and scan summary](release-evidence/2026-11-23-security-update/summary.json),
[residual findings](release-evidence/2026-11-23-security-update/residual-findings.json),
[layer evidence](release-evidence/2026-11-23-security-update/layer-audit-summary.json),
[browser results](release-evidence/2026-11-23-security-update/browser-results.json)
and [maintenance proof](release-evidence/2026-11-23-security-update/maintenance-acceptance.json).
Full private logs and large image/source artifacts remain under
`.local/release-security-update-2026-11-23/`.

## Refreshed source delivery and remaining release scope

The matching source set maps **533 current image/component entries**: API 162,
web 206 and tools 165. The lower total reflects removed shipped components, not
reduced test coverage. Unchanged application metadata/notice manifests match the
previous reviewed candidate exactly. Reused OS sources remain version-matched;
the new authenticated source archive supplies **28 verified inputs for nine
changed source package/version pairs**, with complete `.dsc` descriptor sets.
Representative updated Ubuntu APT and PGDG PostgreSQL sources unpacked with
their distribution build rules. The web native sidecar is rebound to the new
image and validates its unchanged complete 406-input profile. The previous native
rebuild/replacement proof retains its scope rather than claiming a repeated build.

The [source validator](release-evidence/2026-11-23-security-update/source-validation.json)
accepted the [coverage/material mapping](release-evidence/2026-11-23-security-update/coverage.json).
The [source review](release-evidence/2026-11-23-security-update/source-review.md)
retains the addon-header/font provenance limits and complete notice conditions.
An [independent review](release-evidence/2026-11-23-security-update/independent-review.json)
found no critical/important implementation or source-binding issue. Neither that
review nor these scans establish exhaustive exploitability or secret absence.

Residual OS/native security review, provider permission, operator-specific
infrastructure acceptance and maintainer notification delivery remain open.
Final main-source integration, stable identity, corresponding artifact
qualification and authorized publication remain separate. No public tag,
registry artifact, release or persistent-instance upgrade was performed.
