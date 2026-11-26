# Reference service finding investigation — 2026-11-25

This is a focused supported-workflow assessment, not clearance of either service
image. No product, image, instance or scanner setting was changed.

## Recommendation

Use the current official PostgreSQL 17 Bookworm digest, subject to the main
worker's upgrade/restore acceptance: it changes PostgreSQL 17.11 to 17.12 and
gosu 1.19 to 1.20, including moby/sys/user 0.1.0 to 0.4.1. This removes the
specific passwd/group parser advisory from the scan. A larger raw OS count does
not negate that concrete remediation. Do not change the database OS suite just
to reduce a count without reviewing collation/native dependency consequences.

The Caddy findings assessed here do not establish a need for a custom rebuild
for the supplied reference configuration. Its ten Go-component matches have
conditional supported-flow exclusions below. Eleven Alpine OS matches remain
open in this focused report; 530 original / 554 updated PostgreSQL OS matches
also remain outside this assessment. A custom build could still be a separate
defense-in-depth decision; this report does not approve it or all service risks.

## Exact inputs and limits

- Original Caddy service index: `ce773ec7a742ce15ee6bf2bb5f65882b9e2a4487b10733e6f1484f86e6b5d8bc`.
- Current official Caddy index: `4851e54a46b206517c574821bb5cc67f170d2156041e9fbc4c9193b45e84a75b`.
- Original PostgreSQL index: `665dc7b37ac52a17e298511414c21d23ffa4e6a6ef6225b3e5d59c7afb35e994`.
- Current official PostgreSQL index: `1eff563f6c868b9b433566e9f3cf6e5c79fbd3b95bf70ec7277d376cdbfc50d7`.

All prefixes above are SHA-256 digests. Config IDs, package versions, finding
counts and classifications are in `assessment-records.json`, derived from the
four exact scan reports. `input-hashes.json` binds source/config files and raw
scans; `acquisition*.json` binds official upstream source archives/advisories.
`image-extracts.json` binds original-image gosu, Caddy and entrypoint bytes to
their distributed layers. The fetched master PostgreSQL entrypoint is background
only; conclusions use the entrypoint extracted from the original exact image.
The current official Caddy binary is byte-identical to the original pinned
binary. The updated PostgreSQL entrypoint differs only in replacing
`/var/run/postgresql` with `/run/postgresql` for socket-directory setup; its
gosu invocation is unchanged. Both updated binaries/entrypoint are also retained
in the image-extract manifest. The changed socket path still needs the main
worker's installation/upgrade check.

Pinned source is reviewed by release tag and hash, not reproduced byte-for-byte
from each upstream service binary. NUL-terminated Go function-like string searches
are supporting evidence, not a whole-program callgraph or proof against inlining.
Source acquisition and this assessment do not claim a fresh exploit test,
binary rebuild, authenticated source-signature verification or exhaustive audit.

## Caddy: ten Go matches, eight distinct advisory IDs

RateFreak's `deploy/self-host/Caddyfile:2` disables the admin API. Its public
handlers are a fixed provider-artwork denial and `reverse_proxy web:3000`.
There is no request_body, templates, tracing, ACME-server or JWT/auth handler.
The shell entrypoint reads the protected ingress key then execs `caddy run`.

- **CVE-2026-97054 / Go HTTP/1 MaxBytesReader race:** official Go record
  `GO-2026-6859.json` requires MaxBytesReader/MaxBytesHandler and concurrent body
  reading/header writing. Exact Caddy `admin.go:1098` installs MaxBytesReader in
  the disabled admin config API; `modules/caddyhttp/requestbody/requestbody.go:73`
  installs it only when that handler's MaxSize is positive. Those are the only
  production call sites in the reviewed Caddy source. No such wrapper is
  configured on the supplied proxy route. Enabling admin, request_body/max_size
  or custom handlers requires reassessment; do not describe Go 1.27.3 as patched.
- **CVE-2026-116585/116586/116587 / go-jose v3:** the vulnerable library and
  parser/decrypt functions are actually present, through `go.step.sm/crypto`
  `jose/types.go:11,226,274,284`. The conditions are untrusted CBC-HMAC JWE
  decryption, deeply nested JSON parsing or attacker-supplied RSA JWK validation.
  No configured public route reaches a Smallstep provisioner/JWK/JWE endpoint.
  Caddy's internal TLS issuer constructs a local embedded signing authority
  (`modules/caddytls/internalissuer.go:103`, `modules/caddypki/ca.go:222`) without
  provisioner config, to sign its own certificate CSR. The separate public ACME
  server handler constructs provisioners in `acmeserver/acmeserver.go:173` and is
  not configured. Adding that handler, auth/provisioners or user key input
  invalidates these exclusions. The presence of fixed v4.1.6 does not fix v3.
- **CVE-2026-128085 / OpenTelemetry:** three module matches concern trace/log
  attribute length truncation. Caddy's `modules/caddyhttp/tracing/tracer.go:72`
  constructs a trace provider for its tracing handler. That handler is absent.
  Caddy's SDK metric usage is a different operation and is not the advisory's
  trace/log attribute path. Reassess tracing, SDK log exporters or external
  auto-instrumentation; module presence is not removal.
- **CVE-2026-78661 and CVE-2026-97045 / x/net/html:** parser/renderer functions
  are present. Selected pinned transitive source use is Smallstep cli-utils
  `usage/report.go:54,110` for CLI-help HTML parsing/rendering. The supplied
  `caddy run` configuration has no templates/HTML transform handler. Proxying
  upstream HTML bytes does not invoke this parser/renderer. Reassess added HTML
  handlers, CLI-help processing or other untrusted parser inputs. This is not
  a claim that every shipped dependency source was searched.
- **GO-2026-5932 / x/crypto/openpgp:** the module is present, but the selected
  Caddy/Smallstep/ACME source search found no OpenPGP caller and the exact binary
  has no matching OpenPGP function-like strings. Supported proxy/certificate
  issuance has no OpenPGP input. Reassess plugin, key-format or handler changes.

### Alpine library caution

The extracted Caddy ELF has no dynamic section, so its TLS/HTTP3 uses Go/quic-go,
not the installed OS OpenSSL. However, `tls internal` can call
`caddypki/ca.go:439` truststore installation, and exact truststore 0.13.0
`truststore_linux.go:36,109` can run `update-ca-certificates`. Do not waive all OS
libraries based only on the static Caddy executable. The installed curl and
certificate helpers, their local input trust and remaining package findings
need their own evidence. CVE-2026-4738's description concerns GDAL's
contrib/infback9 code, but this investigation does not establish Alpine's exact
zlib build exclusion. All eleven OS matches stay open here.

## PostgreSQL gosu: 62 original / 61 updated Go matches

The original exact image's entrypoint invokes `exec gosu postgres ...` at
`postgres-docker-entrypoint.sh:343`; gosu then changes identity and replaces
itself with the shell/PostgreSQL process. It is not a database protocol listener.
Upstream 1.19 and 1.20 have byte-identical `main.go` and `setup-user.go`.

- `main.go:3` imports os, os/exec, runtime and syscall; `main.go:75,79,84` changes
  identity, resolves the executable and calls syscall.Exec.
- `setup-user.go:3,22,26,30` uses moby/sys/user and x/sys/unix to read local
  passwd/group and set uid/gid/groups. `compose.yaml` does not mount an
  attacker-supplied passwd/group file.
- **60 stdlib matches:** official Go records and the source establish affected
  HTTP/TLS/certificate, mail/URL, archive, ASN.1/XML/PEM, template/MIME/network and
  os.Root operations. No such workflow exists in this local identity/exec helper.
  Exact original gosu binary searches found no function-like strings for key
  network/parser packages or os.Root functions; source callers are the principal
  evidence, not absence of a CLI command. The old compiler/runtime is still
  present and is not described as patched. Changed gosu source, new parsers,
  networking or os.Root calls require reassessment.
- **x/sys Windows integer overflow:** platform condition does not apply to the
  Linux/amd64 artifact; gosu imports x/sys/unix, not windows.
- **CVE-2026-61801 / moby user parser:** original local protected passwd/group
  input limits supported exposure, but current official gosu1.20 explicitly
  upgrades the affected module to 0.4.1. Prefer the fixed image. Untrusted image
  passwd/group content, new mounts or manual parsing invalidate the old
  protected-input condition.

None of these findings applies automatically to the PostgreSQL server's own
native libraries, extensions, SQL/XML parsers, filesystem inputs or authentication.
Those 530/554 OS matches require separate assessment and final scan coverage.
