# Applicability of the 20 new Debian trial advisories — 2026-11-23

All 20 newly reported advisory IDs now have an application-specific disposition.
For the exact trial images and supported RateFreak workflows, the evidence
supports **20 conditional exclusions**: no affected operation was found that
requires a product fix in this set. These decisions cover **167 scanner matches**.
They do not assert that upstream packages are patched, or that the product has
no vulnerabilities. **The overall release security gate remains held.**

The assessment is bound to trial source
`36b06ca6c6bfd79f3b86c2eefc2c2eac6bae6f8f` and the exact web/tools image IDs in
the [summary](release-evidence/2026-11-23-debian-advisories/summary.json).
The [per-advisory records](release-evidence/2026-11-23-debian-advisories/advisory-assessments.json)
preserve affected conditions, installed package matches, vendor status,
evidence, limitations and changes that require reassessment. No scanner ignore
rule or product code was changed. The raw trial scan remains at 764 matches;
597 matches belong to other IDs outside this focused assessment.

## Python: 11 supported-flow exclusions

The container entrypoint prepares configuration and uses `exec` to replace
Python with Node or .NET before application requests are served. Web's Python
health check connects to literal `127.0.0.1`; tools' Python performs explicit
offline maintenance. Python does not serve the application's HTTP/TLS requests.

| Advisory | Required affected operation | RateFreak disposition |
| --- | --- | --- |
| [CVE-2026-114583](https://security-tracker.debian.org/tracker/CVE-2026-114583) | Tar-filter extraction of crafted link entries | Restore does not call `extract`/`extractall` or extraction filters, and rejects links before writing. |
| [CVE-2026-19672](https://security-tracker.debian.org/tracker/CVE-2026-19672) | Filter extraction creates directories from traversal-and-return paths | Restore rejects those member names and creates only its explicit destination paths. |
| [CVE-2026-82049](https://security-tracker.debian.org/tracker/CVE-2026-82049) | Filter extraction follows a hard-link-to-symbolic-link chain | Restore accepts regular-file entries only; hard links and symlinks are rejected. |
| [CVE-2026-87910](https://security-tracker.debian.org/tracker/CVE-2026-87910) | Link-extraction fallback ignores a filter decision | Restore does not invoke the affected link/filter extraction operations. |
| [CVE-2026-15308](https://security-tracker.debian.org/tracker/CVE-2026-15308) | Incremental Python HTML parser processes hostile markup | No HTMLParser use in startup, health or maintenance. |
| [CVE-2026-15310](https://security-tracker.debian.org/tracker/CVE-2026-15310) | Python ZIP decompression with affected compression methods and sizes | No production Python ZIP-consumption workflow; backups use tar/PostgreSQL dumps. Account ZIP export is produced by .NET. |
| [CVE-2026-15806](https://security-tracker.debian.org/tracker/CVE-2026-15806) | urllib password manager matches credentials across URL schemes | Runtime Python does not create a password manager or perform authenticated HTTP requests. Host operator URL parsing is not that credential transport API. |
| [CVE-2026-17084](https://security-tracker.debian.org/tracker/CVE-2026-17084) | Python stringprep/IDNA processes particular Unicode domains | Fixed numeric Python health-check host; database names go to libpq/.NET. No Python IDNA workflow found. |
| [CVE-2026-18503](https://security-tracker.debian.org/tracker/CVE-2026-18503) | `csv.Sniffer.sniff` processes unbounded hostile samples | No Sniffer use in supported workflows. |
| [CVE-2026-19445](https://security-tracker.debian.org/tracker/CVE-2026-19445) | Python TLS server switches SNI context and releases its original context | No Python TLS server or SNI callback. |
| [CVE-2026-19553](https://security-tracker.debian.org/tracker/CVE-2026-19553) | Python BIO/async TLS relies on hostname checking while omitting hostname | No such Python TLS client; health check is plain TCP. |

An offline probe loaded the **actual tools-image maintenance code**, replaced
`extract`, `extractall`, `tar_filter` and `data_filter` with failing sentinels,
and exercised eight hostile archives. Parent traversal, traversal-and-return,
absolute paths, normal/directory/empty-name symlinks, hard links and directory
entries were all rejected **before any destination file was written**. A safe
flat-file/avatar restore still passed without calling those affected APIs.
No HTML, ZIP, SSL, IDNA, CSV or urllib modules were loaded by that maintenance
exercise. [Tools probe](release-evidence/2026-11-23-debian-advisories/tools-probe.json).

This checks the named extraction-filter conditions, not every possible tar
parser bug or resource-exhaustion input. Backup hashes establish consistency
with a manifest, not authenticity of its origin. Verification-only acceptance
scripts that read account ZIP exports are not production entrypoints.

Reassess if Python gains new parser/network duties, the health-check host becomes
attacker-controlled, or restore changes formats, accepted member types, paths or
extraction APIs.

## OpenSSL: six supported-consumer exclusions

The vendor bulletins describe specific QUIC, server-certificate-flag and
AES-GCM-SIV operations. These conditions differ from simply loading OpenSSL or
making an ordinary TLS connection.
[10 November bulletin](https://openssl-library.org/news/secadv/20261110.txt),
[17 November bulletin](https://openssl-library.org/news/secadv/20261117.txt).

| Advisory | Required affected operation | RateFreak disposition |
| --- | --- | --- |
| CVE-2026-124709 | OpenSSL QUIC server retains rejected/reset streams | No supported OpenSSL QUIC server consumer. |
| CVE-2026-88822 | OpenSSL QUIC automatic peer-address detection | No supported QUIC peer-detection consumer. |
| CVE-2026-95691 | OpenSSL QUIC client processes overlapping CRYPTO frames | No supported OpenSSL QUIC client consumer. |
| CVE-2026-35190 | TLS 1.3 server sets `SSL_CERT_FLAG_SUITEB_128_LOS` | No such certificate-flag caller in pinned Node/.NET/libpq TLS implementations or application configuration. |
| CVE-2026-88823 | AES-GCM-SIV fragmented AAD/context handling | Supported callers use ordinary AES-GCM/AES-CBC, not GCM-SIV. |
| CVE-2026-95692 | AES-GCM-SIV initialization with nonstandard IV length | Same missing GCM-SIV caller; no supported arbitrary-algorithm endpoint. |

The evidence distinguishes **present** code from **used** code:

- Node reports `process.features.quic === false`. Its embedded OpenSSL still
  contains QUIC symbols; no claim is made that the code was stripped out.
  No `node:quic` use was found in 1,412 shipped JavaScript files. libmsquic is
  absent in both inspected images, and the supported Node/.NET listeners use
  internal HTTP. The separate edge service is outside this assessment.
- The exact libpq19 TLS implementation and native imports use PostgreSQL
  TCP/TLS, not the named QUIC interfaces. The exact .NET crypto shim does not
  expose those interfaces. It would be incorrect to dismiss the linked TLS
  libraries as entirely unused.
- The pinned Node TLS-context code, exact .NET SSL shim and exact libpq19 TLS
  implementation do not call `SSL_CTX_set_cert_flags`; the affected Suite B
  server flag is not configured by RateFreak.
- **GCM-SIV is available in Node's cipher list.** The exclusion instead rests
  on actual callers: shipped Next encryption uses ordinary AES-GCM or AES-CBC;
  WebCrypto encryption names AES-GCM; the exact .NET cipher implementation uses
  ordinary `EVP_aes_*_gcm` functions. The JavaScript search found no GCM-SIV
  feature string or generic cipher-list selection. Algorithm-token evidence
  includes comments as well as code; it is supporting lexical evidence, not a
  JavaScript AST or complete dynamic-callgraph proof.

See [web probe](release-evidence/2026-11-23-debian-advisories/web-probe.json),
[shipped JavaScript inventory/search](release-evidence/2026-11-23-debian-advisories/shipped-js-search.json),
[cipher call sites](release-evidence/2026-11-23-debian-advisories/js-cipher-calls.json),
[algorithm tokens](release-evidence/2026-11-23-debian-advisories/js-cipher-algorithms.json),
[pinned upstream source hashes](release-evidence/2026-11-23-debian-advisories/upstream-inputs.json)
and [libpq source provenance](release-evidence/2026-11-23-debian-advisories/libpq-source-provenance.json).

Reassess when enabling QUIC/HTTP3, Suite B, GCM-SIV, native plugins/custom server
code, arbitrary crypto selection, or changed TLS/crypto dependencies. These six
decisions do not clear other OpenSSL advisories, including the previously
identified PEM/CMS issues outside this 20-ID set.

## util-linux: two workflow exclusions

**CVE-2026-78408** requires a privileged `nsenter --join-cgroup` operation against
an attacker-controlled target. nsenter is installed, but no supported startup,
storage preparation, backup/restore or operator command invokes it.
[Upstream advisory](https://github.com/util-linux/util-linux/security/advisories/GHSA-55fx-f4gg-cfhj).

**CVE-2026-78409** requires an authorized `X-mount.subdir` operation reaching the
affected detached-tree path. Both exact images have an unconfigured fstab and
no supported script performs that operation. The upstream/debian version-range
details are not used as an automatic version exclusion.
[Upstream advisory](https://github.com/util-linux/util-linux/security/advisories/GHSA-8f2p-47x3-43mv).

The probes confirm that mount remains **setuid**. Root maintenance is not
assumed to inherit API/web capability restrictions. These decisions depend on
the actual commands and fstab configuration, not a blanket nonroot argument.
Reassess for host-fstab mounts, namespace/debug helpers, privileged mount
operations or manual nsenter use against untrusted targets.

## C++: one scoped source/caller exclusion

**CVE-2026-115455** concerns wide-character stream extraction into `std::bitset`
compiled from affected libstdc++ headers. The upstream fix changes
`include/std/bitset`. Replacing a shared library does not repair vulnerable code
already instantiated into another binary.
[Vendor record and upstream fix](https://security-tracker.debian.org/tracker/CVE-2026-115455).

The trial has runtime libraries but no GCC compiler or C++ headers. That fact
alone is insufficient to clear compiled consumers, so the review also checked:

- ELF symbol/section metadata for 389 web and 507 tools files under the recorded
  application/runtime/library roots, including shipped ARM/other-architecture
  Skia payloads. This is a selected-root inventory, not every ELF in the images.
- Node compiler metadata predating the affected GCC 13+ handling. Observed Node
  bitset stream symbols are narrow-character output, not wide input. Observed
  Skia bitset symbols use the libc++ namespace; its GCC CRT comments are not
  treated as proof of the C++ header version.
- 28 source archives whose hashes match the locked native profile, plus the
  supplied sharp and minimum node-addon-api header materials: **9,352 C/C++
  source/header/template files**. Direct bitset uses concern flags/masks,
  constructors/output or tests. The only generic-istream search hits are a
  libtiff comment documenting narrow byte I/O; no affected wide-bitset input
  operation was found.

[Native source search](release-evidence/2026-11-23-debian-advisories/native-source-search.json),
[reviewed references and limits](release-evidence/2026-11-23-debian-advisories/native-source-observations.json).
This supports a conditional exclusion for the reviewed application operation;
it is not a complete proof against every inlined instruction, a native rebuild,
or new proof of exact upstream addon-header resolution. The existing minimum
header-provenance limit remains. Reassess when compiler/headers, native inputs,
addons or stream-processing code changes. Any future affected instantiation
needs a header fix and recompilation, not merely a runtime-library update.

## Verification and remaining release work

Vendor statuses remain open. The
[22 retained vendor pages/bulletins](release-evidence/2026-11-23-debian-advisories/vendor-acquisition.json)
and pinned upstream files are hash-bound; full downloaded texts remain locally
under `.local/debian-advisory-assessment-2026-11-23/`. The
[input manifest](release-evidence/2026-11-23-debian-advisories/input-hashes.json)
also binds probes, the original 20-ID inventory, raw findings and locked native
profile. Actual image script hashes match the frozen trial source, and 289
production application source files were searched for the named features.
[Deployment source facts](release-evidence/2026-11-23-debian-advisories/deployment-source-facts.json).
The [verification record](release-evidence/2026-11-23-debian-advisories/verification.json)
confirms all 40 input hashes, exact match reconciliation and probe assertions.
An [independent review](release-evidence/2026-11-23-debian-advisories/independent-review.json)
found no outstanding issue in these assessment artifacts and specifically
confirmed that the C++ decision retains its conditional source/caller scope.

Probes used no network, no application volumes, read-only root filesystems,
dropped capabilities and disposable tmpfs. No developer instance or data was
accessed. An initial inspection harness handled only ELF64; adding ELF32 and
endianness handling allowed the shipped ARM payloads to be inspected. This was
an inspection change, not a product fix. No new image build, vulnerability scan,
full application-test run, scanner suppression or publication was performed.

The 20 new IDs no longer lack application-specific assessment. Integration of
the Debian trial, assessment/remediation of other residual IDs, other bundled
runtime/native advisories, separate service images and final candidate/release
qualification remain open. This report does not convert raw scanner warnings
into confirmed exploitable bugs or erase them from the evidence.
