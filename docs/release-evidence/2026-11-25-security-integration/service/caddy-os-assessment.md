# Caddy's eleven OS scan matches — 2026-11-25

The evidence supports **ten conditional supported-consumer exclusions** and
**one binary-build exclusion** for the six advisory IDs making up the eleven OS
matches in each reviewed Caddy image. No product code, scanner ignore, image or
instance was changed. This does not decide the overall security gate or promise
that every advisory absent from the scans was assessed.

Scope: supplied reference Caddy configuration with public ACME/automatic HTTPS
or the documented internal-TLS installation check, on the original pinned and
current official images. Arbitrary operator commands, image modifications,
custom providers/plugins, added handlers or hooks require reassessment.

Exact index/config identities, per-match conditions/dispositions and scan hashes
are in `caddy-os-assessment.json`. The reviewed OS package/advisory/version tuples
are identical in both scans. All eleven selected extracted OS files—including
both certificate helpers, their hook, native libraries and BusyBox—are
byte-identical between the images (`caddy-os-image-comparison.json`). Earlier
inspection separately established byte-identical Caddy executables.

## Actual native execution chain

Caddy's public ACME/TLS implementation runs in the extracted **static Go** Caddy
binary, which has no ELF dynamic section. HTTP/3 uses quic-go, not OS OpenSSL.
This alone is insufficient to clear native libraries because the internal CA
can install its root into the container trust store:

1. Caddy 2.11.9 `modules/caddypki/pki.go:92` calls CA root installation unless
   disabled; `modules/caddypki/ca.go:450` calls truststore.Install.
2. Exact truststore 0.13.0 `truststore_linux.go:34-36,64-78` selects
   `/usr/local/share/ca-certificates/%s.crt`, invokes `tee`, then
   `update-ca-certificates`. NSS/Java helper paths are unsupported by this
   image: no certutil/keytool executable is present in its recorded rootfs.
3. The exact image's `/usr/sbin/update-ca-certificates` is an Alpine C binary,
   not the Debian shell script. ELF inspection shows only musl as a dependency.
   Exact source `update-ca.c:311-329` combines configured/local certificate
   files and updates symlinks/bundle; `update-ca.c:334-336` execs run-parts on
   `/etc/ca-certificates/update.d/`, with a cleared environment.
4. The **only** hook in that directory is the exact 48-byte `certhash` script:
   `exec /usr/bin/c_rehash /etc/ssl/certs`. `/bin/run-parts` and `/bin/sh` are
   BusyBox symlinks; the BusyBox ELF links only musl.
5. `/usr/bin/c_rehash` links **libcrypto and musl**, with no libssl or zlib.
   Exact source `c_rehash.c:187-218` reads PEM certificate/CRL data, extracts
   names and computes certificate/CRL hashes. `c_rehash.c:339` chooses SHA1.
   Its dynamic imports agree with those source operations. It has no CMS
   encryption, cipher initialization, QUIC or network code.

The exact image has no OpenSSL CLI. Curl and libcurl are shipped and do link
native TLS/compression libraries; they are **not called** by the supplied
entrypoint, Caddyfile, reviewed truststore helper chain or public ACME client.
An arbitrary manual curl command is outside this conditional assessment.
No argument relies on treating the whole container as nonroot.

Package `ca-certificates=20261020-r0` records aports commit
`e78f5987166c6ffbe23f6472e2e8f040298fe2da`. Its exact APKBUILD names the upstream
Alpine ca-certificates archive; the acquired archive **matches the APKBUILD's
SHA-512**. The Makefile builds update-ca with libc and c_rehash with -lcrypto,
matching the exact ELF dependencies. Acquisition records and retained helper
sources are hash-bound. No helper binary reproduction or exploit test is claimed.

## OpenSSL: ten matches, five IDs

The named advisory conditions come from official OpenSSL's
[10 November bulletin](https://openssl-library.org/news/secadv/20261110.txt).
Each ID is reported once for libcrypto3 and once for libssl3, both 3.5.9-r0.

| ID | Required operation | Supported caller result |
| --- | --- | --- |
| CVE-2026-107496 | CMS_encrypt/CMS_encrypt_ex with attacker EC recipient parameters | No CMS encryption caller in the complete certificate-helper source/imports; Caddy uses Go crypto. |
| CVE-2026-95691 | OpenSSL QUIC client receives retransmitted CRYPTO frames | Go/quic-go handles Caddy networking; certificate helpers do not perform QUIC/network operations. |
| CVE-2026-88822 | OpenSSL QUIC automatic peer-address detection | No such native network caller in the supported service/helper flow. |
| CVE-2026-88823 | AES-GCM-SIV fragmented AAD/context reuse | Certificate helpers use SHA1/X509/name digests, not cipher operations; Caddy crypto is Go. |
| CVE-2026-95692 | AES-GCM-SIV initialization with an abnormal IV length | No GCM-SIV/cipher/IV-selection caller in the supported service/helper flow. |

These are caller exclusions, **not** claims that the installed library is
patched, that no OpenSSL code executes, or that all malformed-certificate
advisories are irrelevant. In particular, c_rehash really does parse PEM.

An extra non-scan observation: its exact call at `c_rehash.c:189` is
`PEM_X509_INFO_read_bio(b, NULL, NULL, NULL)`. The non-NULL caller-stack
precondition in the official [17 November bulletin](https://openssl-library.org/news/secadv/20261117.txt)
for CVE-2026-107493 is absent for **this helper caller**. That does not clear
other consumers or all newer OpenSSL advisories.

## Zlib: one binary-build exclusion

CVE-2026-4738 describes GDAL's copied `contrib/infback9/inftree9.c` decoder.
The [upstream GDAL fix](https://github.com/OSGeo/gdal/pull/12244) changes that
specific cloned code.

Exact installed `zlib=1.3.2-r1` records aports commit
`0afa2da0e8c8051c6f8f64a7a388e5a259904245`. Its pinned APKBUILD configures
the ordinary shared library and invokes normal make/install. The exact zlib
1.3.2 archive **matches that recipe's SHA-512**. `Makefile.in:59-71` specifies
the ordinary library objects, including `infback.o` and `inftrees.o`, without
the affected contrib infback9 clone. Install targets at lines 313/343 install
those built libraries. No GDAL package/file is present in the exact image.
The recipe's sole patch, CVE-2026-85091, also matches its pinned SHA-512 and
changes only gzwrite.c; it does not change build objects or enable infback9.
The installed library exports ordinary inflateBack functions, with no
inflateBack9 symbols; this supports the recipe/source evidence rather than
substituting for it.

This exclusion is specific to that affected decoder clone in the matched
Alpine binary. It does not exempt other zlib reports, copies embedded into
other payloads, or future builds that enable contrib/infback9/GDAL.

## Reassessment and verification

Reassess after changing service image/package builds, native compiler or
dependencies, Caddy plugins/handlers, public/internal TLS configuration beyond
the supplied modes, native CMS/QUIC/GCM-SIV workflows, compression decoders,
arbitrary command execution, trust-store mounts or certificate-hook contents.
Manual curl/native commands and custom image contents are outside this proof.

`caddy-os-input-hashes.json` binds both raw scans, reference configuration,
rootfs/layer extracts, ELF inspection, exact package recipes and helper/zlib
sources. `caddy-os-assessment.json` reconciles exactly eleven match tuples in
each image. `caddy-os-elf-inspection.json` records eight inspected ELF files.
All work used static archive/source inspection; no running installation, private
application data, new image build or changed scan result was involved.
