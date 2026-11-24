# Debian 13 runtime trial — 2026-11-23

Follow-up: all 20 newly reported IDs have now received
[application-specific assessment](debian-advisory-assessment-2026-11-23.md).
Evidence supports conditional exclusions for the reviewed supported flows; raw
scan results remain unchanged and the broader security release gate stays held.

The isolated Debian 13 trial passed runtime compatibility, fresh installation,
Bookworm-to-Trixie upgrade, Chromium and backup/restore acceptance. The retained
scanner/database reports **764 matches versus 1,014**, with **zero critical
matches versus four** and **157 high matches versus 222**. This supports adopting
the runtime change for the next candidate; **security release clearance remains
held**. Twenty advisory IDs are newly reported on the changed baseline and need
their own assessment. Lower-layer retained packages are inventoried separately
and are not included in those merged-filesystem scan counts.

The trial is committed only in the isolated clone at
`.local/debian-trial-2026-11-23/source`, revision
`36b06ca6c6bfd79f3b86c2eefc2c2eac6bae6f8f`, based on tested revision `a1276b1`.
It remains nonofficial `0.1.0-dev.1`. Main product files, the persistent developer
instance/database, registry tags and public releases were not changed. A complete
[three-file patch](release-evidence/2026-11-23-debian-trial/trial.patch) makes the
proposed integration reviewable.

## Changes and compatibility boundary

- Web/tools final stages use official `debian:trixie-slim` pinned to its amd64
  manifest `sha256:7378260c0a098d27ac206225db5e7d92b8c7c84ea7afccc86eeb513756bbd392`.
  APT upgrades the base to Debian 13.8 packages. The pin does not imply that every
  installed package remains at the original image's version.
- Tools use `libicu76` and the signed `trixie-pgdg` suite, retaining PostgreSQL
  client major 17. Node 24.22.3, .NET 10.0.15, build-stage pins, application code,
  database schema and the separate PostgreSQL server image are unchanged.
- The Ubuntu API Dockerfile is unchanged. It was rebuilt with the trial source
  identity so all three labels and application version metadata agree.
- Python entrypoints and health checks remain unchanged. Package removals are
  consequences of the changed distribution/dependency closure; no extra runtime
  footprint rewrite was mixed into this trial.

Microsoft lists Debian 13 as supported for .NET 10. The trial adds actual
acceptance evidence for this application's copied runtime/native dependencies;
the support table alone would not prove that compatibility.
[Microsoft Debian support](https://learn.microsoft.com/en-us/dotnet/core/install/linux-debian),
[official Debian image](https://hub.docker.com/_/debian).

Rollback for this trial is selecting the retained prior image set on a disposable
test instance. Upgrade acceptance stops writers before replacement and requires
new migration acceptance for the new complete image set. No persistent instance
was migrated, and no destructive rollback over user data was attempted.

## Measured security comparison

| Image | Previous matches | Trial matches | Previous critical/high | Trial critical/high |
| --- | ---: | ---: | ---: | ---: |
| API, Ubuntu 24.04 | 46 | 46 | 0 / 0 | 0 / 0 |
| Web, Debian 13.8 | 475 | 341 | 2 / 109 | 0 / 77 |
| Tools, Debian 13.8 | 493 | 377 | 2 / 113 | 0 / 80 |
| Total | 1,014 | 764 | 4 / 222 | 0 / 157 |

The scans use the exact saved trial images, pinned Trivy 0.76.2 and the same
cached database as the previous comparison, updated 2026-11-23. Database updates
were disabled for comparability; no ignore or ignore-unfixed rule was used.
All scanner runs completed successfully. Zero non-OS matches and zero findings
with a listed fixed version are scanner results, not security clearance.
[Summary, identities and hashes](release-evidence/2026-11-23-debian-trial/summary.json),
[all residual matches](release-evidence/2026-11-23-debian-trial/residual-findings.json).

Distinct advisory IDs decrease from 217 to 158: 79 old IDs disappear and 20 new
IDs appear. Package/advisory association churn also includes renamed libraries
and Python/GCC versions; it must not be read as that many distinct CVEs fixed or
introduced. The previous Bookworm binary exclusions are not automatically
transferred to Trixie packages. The
[Trixie vendor records](release-evidence/2026-11-23-debian-trial/trixie-vendor-statuses.json)
retain source-package status and keep the new baseline pending assessment.

The [20 newly reported IDs](release-evidence/2026-11-23-debian-trial/new-advisory-ids.json)
fall into four groups:

| Group | New IDs | Follow-up |
| --- | ---: | --- |
| OpenSSL | 6 | QUIC, TLS Suite B and AES-GCM-SIV conditions; OS OpenSSL now has newer functionality. Review consumers/build flags rather than inheriting a Bookworm 3.0 conclusion. |
| Python 3.13 | 11 | tarfile, HTML, ZIP, URL/IDNA, CSV and SSL conditions; inspect actual entrypoint/maintenance use. |
| util-linux | 2 | nsenter cgroup authority and mount subdirectory symlink conditions; tools' root maintenance is distinct from hardened API/web execution. |
| GCC/libstdc++ | 1 | std::bitset stack behavior needs compiled-caller assessment. |

The vendor snapshot marks these new records open. For example,
[CVE-2026-124709](https://security-tracker.debian.org/tracker/CVE-2026-124709)
concerns QUIC stream handling, while
[CVE-2026-19445](https://security-tracker.debian.org/tracker/CVE-2026-19445)
concerns Python server SSL-context switching. Standard entrypoints do not run a
Python TLS server or QUIC server. Maintenance uses checked regular-file entries
and `extractfile`, rather than tarfile `extract`/`extractall` or their extraction
filters. These observations narrow some conditions but do not waive all parser,
crypto or memory-safety findings. The existing
[OS review](release-os-review-2026-11-23.md) remains historical evidence for its
exact Bookworm images, not a blanket assessment of the new images.

Node still embeds OpenSSL 3.5.9; changing Debian packages cannot update that copy.
The separate Node/OpenSSL, libvips/Skia and other bundled-component review remains
open despite zero non-OS scanner matches. Separate PostgreSQL/Caddy service-image
and host/runtime assessment also remains outside this three-image trial.

## Acceptance evidence

| Check | Result |
| --- | --- |
| Production images | API Release publish, Next production build and tools migration bundle built successfully; current inventories collected. |
| Operator/release/compliance tests | 52 + 26 + 20 = 98 passed. Initial sandbox run blocked Node's nested Git process with EPERM; the same release tests passed outside the sandbox without a code change. |
| Fresh HTTPS install | Setup, secure cookies, CSRF/Origin rejection, ordinary registration, image decoding/privacy/export and administrator access checks passed. |
| Bookworm-to-Trixie upgrade | Existing table fingerprints and referenced artwork preserved; stale acceptance rejected and new migration acceptance required. |
| Empty installation | Before any fixture seeding: zero accounts, catalog items and artwork; pending setup, migration history and the same five category definitions only. |
| Chromium | 25 page journeys, widths 1440/768/390/320; administrator/update checks in both themes, ordinary routes in the existing default-theme scope; TLS verified without bypass. |
| Backup/isolated restore | All 30 table fingerprints, artwork and protected key archives preserved using the trial tools; independent current journal supplied, backup journal excluded, startup/replay guard and repeated replay passed. |

The replay fixture has an **empty journal**; this is not new evidence for
nonempty erasure replay. Synthetic acceptance data exists only in disposable
test installations and is not shipped as installation content. The first browser
attempt expected a truncated revision in the fixture; the UI displays the full
revision. Correcting that fixture made the unchanged browser assertions pass;
no product or TLS change was made.

See [fresh acceptance](release-evidence/2026-11-23-debian-trial/fresh-acceptance.json),
[upgrade acceptance](release-evidence/2026-11-23-debian-trial/upgrade-acceptance.json),
[empty installation](release-evidence/2026-11-23-debian-trial/empty-install.json),
[browser results](release-evidence/2026-11-23-debian-trial/browser-results.json),
[first browser attempt](release-evidence/2026-11-23-debian-trial/browser-first-attempt.json)
and [maintenance acceptance](release-evidence/2026-11-23-debian-trial/maintenance-acceptance.json).
The full 15-check application verifier was not repeated for this Dockerfile-only
trial. Its prior application-test counts are not presented as a new trial run.

## Source, notices and inherited layers

Current source coverage validates **502 installed image/component entries**:
API 162, web 188, tools 152, with 40 material files. All three application notice
manifests match the prior proof byte-for-byte. The web native sidecar is rebound
to the exact new web image and verifies the same 406 locked inputs and native
binary hash; its earlier native rebuild was not repeated.
[Coverage](release-evidence/2026-11-23-debian-trial/coverage.json),
[source validation](release-evidence/2026-11-23-debian-trial/source-validation.json),
[source review](release-evidence/2026-11-23-debian-trial/source-review.md).

The 74 new installed OS source versions are supplied as **233 complete inputs**
in a locally prepared archive. Input hashes match authenticated signed APT source
indexes and complete `.dsc` SHA-256 lists. Nine duplicate main/security source
records had identical filenames/hashes/sizes and were checked before selecting
one mirror path. Representative ICU and PostgreSQL client17 source extraction
passed and exposed `debian/control` and `debian/rules`; this is not a binary
rebuild or independent maintainer-signature check for all 233 inputs.
[New source archive](release-evidence/2026-11-23-debian-trial/debian13-os-sources.json),
[source extraction](release-evidence/2026-11-23-debian-trial/source-extraction.json).

All **43 distributed layers** were inspected. Both Debian images retain six
superseded base-layer package versions: base-files, libc-bin/libc6,
libssl3t64/openssl-provider-legacy and zlib1g. These bytes are hidden by updated
merged filesystems but remain distributed in the saved images. Four additional
source versions, their **12 descriptor inputs**, six inherited copyright texts
and exact layer/package bindings are supplied separately. Their descriptors were
retrieved from official Debian HTTPS mirrors and verified with `gpgv` against
Debian developer/maintainer keyrings installed through signed APT repositories in
a disposable verification container. They are not falsely attributed to the
current signed Sources indexes.
[Inherited-layer source summary](release-evidence/2026-11-23-debian-trial/lower-source-summary.json),
[descriptor signatures](release-evidence/2026-11-23-debian-trial/lower-source-signatures.json),
[exact layer/source bindings](release-evidence/2026-11-23-debian-trial/lower-source-manifest.json).

No configured private value matched any image layer/configuration. The API's
generic private-key pattern match remains the same upstream GnuTLS binary bytes
as the prior proof; web/tools have no such match. This is a scoped inspection,
not proof that all possible secret patterns or vulnerabilities are absent.
The new merged-filesystem vulnerability counts do not scan the superseded
versions as independently active packages.
[Layer audit](release-evidence/2026-11-23-debian-trial/layer-audit-summary.json).

Prepared image/source archives remain under `.local/debian-trial-2026-11-23/`.
The [final verification](release-evidence/2026-11-23-debian-trial/verification.json)
checks image/scan/config/label bindings, all source-material hashes, the exact
three-file patch and unchanged main product files. An
[independent review](release-evidence/2026-11-23-debian-trial/independent-review.json)
also verified every new/inherited source input and found no outstanding issue
in these review artifacts. [Cleanup evidence](release-evidence/2026-11-23-debian-trial/cleanup.json)
confirms all six disposable projects and volumes are gone; only the preexisting
developer PostgreSQL container remains running.
No public/stable bundle was finalized. The next integration should take the
three-file patch, preserve source/notice delivery for both current and inherited
versions, resolve residual advisory decisions and qualify the final source/image
identity. This successful trial does not authorize publication or mark the
release security gate complete.
