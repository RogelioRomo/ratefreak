# Remaining OS vulnerability review — 2026-11-23

The remaining OS scan inventory is reconciled with current Debian and Ubuntu
vendor records. **The security release gate remains held.** There are supported,
scoped explanations for 60 package matches, but 954 remain open. No scanner
ignore rule, product image, database, installation content or release was changed.

This review covers the three existing `a1276b1` proof images from the
[runtime security update](runtime-security-update-2026-11-23.md). It does not
cover the separate PostgreSQL/Caddy service images, host kernel/container engine,
all embedded native libraries, or advisories absent from the retained scans.
The reviewed artifact is still nonofficial and the implementation in the main
checkout remains uncommitted.

## Inventory and evidence

| Image | OS | Raw package/advisory matches | Critical | High |
| --- | --- | ---: | ---: | ---: |
| API | Ubuntu 24.04 Noble | 46 | 0 | 0 |
| Web | Debian 12 Bookworm | 475 | 2 | 109 |
| Tools | Debian 12 Bookworm | 493 | 2 | 113 |

These are scanner severities, not demonstrated application exploitability.
The 1,014 matches reduce to 217 distinct advisory IDs: 205 CVEs and 12 Debian
temporary IDs. Keeping distribution/source context produces 236 review records:
209 Debian and 27 Ubuntu. Every record has a vendor status and every raw match
is preserved with its package/version, source version, architecture, image ID,
scanner status and severity source in the
[advisory ledger](release-evidence/2026-11-23-os-review/advisory-ledger.json).

The [summary](release-evidence/2026-11-23-os-review/summary.json) records the
counts. [Source assessments](release-evidence/2026-11-23-os-review/source-assessments.json)
cover every affected source-package family; these are usage assessments, not
blanket exceptions for every advisory in a family. Downloaded vendor inputs,
the exact scans and inspected deployment source are bound by
[input hashes](release-evidence/2026-11-23-os-review/input-hashes.json).

The authoritative snapshot inputs are the
[Debian tracker JSON](https://security-tracker.debian.org/tracker/data/json),
[Debian CVE notes](https://salsa.debian.org/security-tracker-team/security-tracker/-/blob/master/data/CVE/list)
and the 27 relevant records from the
[Ubuntu CVE tracker](https://git.launchpad.net/ubuntu-cve-tracker/tree/active).
Full downloaded snapshots remain locally under `.local/os-review-2026-11-23/`;
the hashes make this a dated review, not a promise that live records will remain
unchanged. The retained builder and probe scripts reproduce the evidence from
those local inputs. The builder checks that all 1,014 matches and all 236 review
records are accounted for; it does not prove exploitability or release safety.

## Scoped exclusions

| Disposition | Review records | Package matches | Meaning |
| --- | ---: | ---: | --- |
| Vendor binary build exclusion | 8 | 56 | Vendor notes identify affected code that is not built into the matched Debian binaries. |
| Affected command absent | 3 | 4 | Exact-image probes show the command named by the advisory is absent. |
| Open | 213 | 926 | Applicable conditions need a fix, narrower evidence or an explicit residual-risk decision. |
| Open, vendor detail needed | 12 | 28 | Temporary Debian identifiers lack sufficient detail for a defensible waiver. |

The eight binary exclusions are specific to the matched Debian packages:

- **CVE-2023-45853 / zlib:** Bookworm's zlib binaries do not include the affected
  MiniZip code. This explains two critical matches, one in each Debian image.
  It does not clear other zlib findings or embedded copies. [Debian record](https://security-tracker.debian.org/tracker/CVE-2023-45853).
- **CVE-2022-0563, CVE-2025-14104 and CVE-2026-3184 / util-linux:** the affected
  chfn/chsh or login code is not supplied by Bookworm's matched util-linux
  binaries. This does not exempt separate shadow packages.
  [chfn/chsh build note](https://security-tracker.debian.org/tracker/CVE-2025-14104),
  [login build note](https://security-tracker.debian.org/tracker/CVE-2026-3184).
- **CVE-2023-52426 / expat:** the affected build without XML_DTD differs from
  Debian's configuration. [Debian record](https://security-tracker.debian.org/tracker/CVE-2023-52426).
- **CVE-2026-129483 / cyrus-sasl2:** Debian disables the affected SRP mechanism.
  [Debian record](https://security-tracker.debian.org/tracker/CVE-2026-129483).
- **CVE-2017-17740 and CVE-2026-22185 / openldap:** Debian does not build the
  affected nops server module or bundled lmdb mdb_load component, respectively.
  [nops record](https://security-tracker.debian.org/tracker/CVE-2017-17740),
  [mdb_load record](https://security-tracker.debian.org/tracker/CVE-2026-22185).

The command exclusions are **CVE-2025-5222 / genrb** in API and
**CVE-2026-105712 / gpgtar** in API and Debian. Exact-image probes found neither
command. The corresponding ICU/GnuPG runtime or verification components are not
generally cleared. [Ubuntu genrb record](https://ubuntu.com/security/CVE-2025-5222),
[Debian gpgtar record](https://security-tracker.debian.org/tracker/CVE-2026-105712).

No suppression file was added. These are documented review dispositions; raw
scanner results still include all 1,014 matches.

## Remaining exposure and priorities

1. **Keep the SQLite critical finding open with reduced observed exposure.**
   CVE-2025-7458 affects the installed Debian SQLite library and requires
   attacker-controlled SQL. RateFreak configures Npgsql/PostgreSQL; inspected
   startup/maintenance code has no SQLite import or database-file workflow.
   That supports a conditional exposure assessment, not a vendor-unaffected
   claim. The other SQLite FTS/session/database-parser reports also remain open.
   Missing `sqlite3` CLI does not remove a shared library.
   [Debian status and SQL condition](https://security-tracker.debian.org/tracker/CVE-2025-7458).

2. **Prioritize Kerberos client/library and TLS/LDAP review when choosing the
   next base image.** The probes found no krb5kdc or kadmind executable, which
   narrows server-only reports. However, tools' libpq dynamically links
   libgssapi/libkrb5, LDAP, OpenSSL and indirectly GnuTLS; API curl has a similar
   network-library chain. These libraries are not all unused. Vendor records
   associate 18 Kerberos reports with DSA-6650-1 while still showing Bookworm
   open; an advisory's existence does not mean this installed version is fixed.
   [Debian advisory](https://security-tracker.debian.org/tracker/DSA-6650-1).
   Five GnuTLS certificate-policy reports are disputed by upstream and deferred
   by Ubuntu, but disputes are not release exceptions by themselves.
   [Example Ubuntu disposition](https://ubuntu.com/security/CVE-2026-67693).

3. **Resolve version/build ambiguity rather than accepting the high OpenSSL
   QUIC match at face value.** CVE-2026-107494 is mapped to OS OpenSSL 3.0 by
   Debian's source tracker, while its listed fixes are on newer branches.
   No affected-version/build conclusion was established for the shipped OS
   library, so the ledger leaves it open. Separately, Node reports embedded
   OpenSSL 3.5.9 and links no OS libssl in `ldd`; an OS package update cannot
   update that copy. Upstream now lists 3.5.9 within the affected range for
   CVE-2026-107493 and CVE-2026-107496. Those advisories have specific PEM/CMS
   caller conditions; this review establishes neither a RateFreak exploit nor
   a waiver. Put them into the separate bundled-runtime review despite the
   previous scanner's zero non-OS matches.
   [Debian QUIC record](https://security-tracker.debian.org/tracker/CVE-2026-107494),
   [upstream affected versions and conditions](https://openssl-library.org/news/vulnerabilities/index.html).

4. **Keep native parser and core-library cases open.** Expat XML, glibc
   iconv/regex/resolver/loader, libstdc++ and zlib reports need caller/build
   analysis where installed. Python entrypoints do not parse XML, but that
   does not cover every bundled native decoder. Input-size limits are useful
   context, not proof against every arithmetic or memory-safety report.

5. **Treat local privilege and maintenance cases separately from web traffic.**
   The exact images have an unconfigured fstab and no systemd, journald,
   homed or oomd executable at the inspected paths. API/web run as UID 10001
   with all capabilities dropped, read-only root filesystems and
   no-new-privileges. This reduces conditions for mount/login/service findings.
   Tools' storage preparation and backup/restore run as root and do not inherit
   all those restrictions. PAM modules, infocmp, Perl, GNU tar and gzip remain
   shipped. No blanket nonroot exception applies to maintenance.

6. **Keep backup trust and Python parser conditions explicit.** Maintenance
   uses Python `tarfile`, not GNU tar or Perl Archive::Tar. Its archive reads
   use mode `r`, not the `r|` streaming mode named by CVE-2026-11972; restore
   uses checked regular-file entries and `extractfile`, not CLI extraction or
   `extractall`. This narrows two Python archive conditions. It does not prove
   resource bounds for all malformed operator-supplied archives. Backup hashes
   check consistency with a manifest; they do not authenticate its origin.
   [Python streaming advisory](https://security-tracker.debian.org/tracker/CVE-2026-11972),
   [Python CLI advisory](https://security-tracker.debian.org/tracker/CVE-2026-115791).

All 209 Debian records remain `open` at the vendor **source-package** level,
including the explicit binary exclusions above. Of these, 42 have postponed
updates, 23 have a no-DSA disposition with an empty reason code, eight are
ignored, and 136 have no such disposition. Ubuntu marks 14 relevant source
records `needed` and 13 `deferred`. No listed fixed version exists in the
retained scanner findings. Neither that absence, vendor low priority,
`no-dsa`, nor `ignored` alone establishes that a package is safe or unfixable.

## Recommended next change

Trial **Debian 13 Trixie for the web/tools final stages** in an isolated build,
alongside removal of demonstrably unnecessary runtime packages. Bookworm is
still supported under LTS, but Debian 13 has a longer full-support runway.
The vendor snapshot marks 60 of the 209 existing Debian source/advisory pairs
resolved in Trixie, 130 open, and 19 missing. This is evidence to try the move,
not a prediction of the new image's scan count: package names, dependencies and
new findings can change, and missing records are not unaffected.
[Bookworm lifecycle](https://www.debian.org/releases/bookworm/),
[Trixie lifecycle](https://www.debian.org/releases/trixie/).

That change must update pinned digests, PGDG suite, ICU/runtime dependencies,
corresponding source/notice coverage and exact-image scans, then repeat fresh
installation, upgrade and real backup/restore acceptance. The API Ubuntu base
needs its own residual decisions. Bundled Node/OpenSSL/libvips/Skia review and
PostgreSQL/Caddy service-image scans remain separate release work. None was
silently cleared by this OS review.

## Verification and limits

Three disposable probes ran with no network, a read-only root filesystem,
dropped capabilities and no-new-privileges; they were bound to the existing
image IDs. [Probe output](release-evidence/2026-11-23-os-review/runtime-probes.json)
contains inspected paths, fstab, library links and Node component versions.
The [existing image summary](release-evidence/2026-11-23-security-update/summary.json)
maps Docker's OCI index image IDs to the config digests reported by Trivy;
the [layer audit](release-evidence/2026-11-23-security-update/layer-audit-summary.json)
records the verified index/manifest/config binding. These different identifiers
describe the same retained payloads, rather than interchangeable digest types.
Those restrictions protect the inspection; they do not describe every
production maintenance invocation. No persistent developer service was started
or changed, and the probes removed themselves.

No exploit harness, complete dynamic reachability analysis, new image build,
product-code change or fresh vulnerability scan was performed in this review.
No product tests were repeated for these documentation/evidence-only changes.
The [verification record](release-evidence/2026-11-23-os-review/verification.json)
confirms match reconciliation, 47 input hashes, image identity bindings, retained
Dockerfile equality, script/JSON parsing, links and whitespace checks. An
[independent review](release-evidence/2026-11-23-os-review/independent-review.json)
confirmed the counts and scoped exclusions, with no outstanding findings in
the review artifacts.
The 954 open matches require the next remediation/decision work; they are not
an assertion that 954 independent application vulnerabilities are exploitable.
