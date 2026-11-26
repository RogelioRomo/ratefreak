# PostgreSQL native-family caller review

The 16 records in `assessments.json` cover the selected immutable PostgreSQL17 Trixie image `sha256:87c8b42f423b45d5247e6aa8fdb2e7762fec164c68a50fe3eb07e7bfcaf4dbcf`. Fifteen have scoped supported-caller exclusions; CVE-2026-13573 remains open. No product or database-instance changes were made.

GnuTLS is a real installed dependency of dirmngr. The actual PostgreSQL LDAP library uses OpenSSL, matching the server/libpq TLS implementation. Supported PostgreSQL startup, health and maintenance do not launch dirmngr's TLS/certificate flow. Each of the nine GnuTLS records retains its exact TLS/certificate/RSA-PSK condition; supplier disagreement is not used as a waiver.

Libgcrypt is a real dependency of GnuPG tools and libexslt. The supported flow does not invoke GnuPG ElGamal encryption or RSA private-key decryption, and current SQL schema has no XSLT/EXSLT extension. PostgreSQL SCRAM uses its own routines and OpenSSL rather than these libgcrypt public-key algorithms. These are two caller exclusions, not a claim that every libgcrypt operation is safe.

The p11-kit RPC conditions require a reachable RPC service/channel. None is started by the supported entrypoint or PostgreSQL protocol. CVE-2026-18938 additionally requires a 32-bit allocation calculation; the retained actual PostgreSQL ELF header identifies the selected runtime as ELF64 x86-64. Other architectures and operator-provided PKCS11 forwarding require review.

LLVM use is real. `llvmjit` imports both bitcode loading and IR-building APIs; upstream PostgreSQL source uses StringMap as well. CVE-2024-7883's Cortex-M CMSE transition is excluded by the actual x86 JIT target. CVE-2026-13574's malformed bitcode intrinsic requires hostile LLVM bitcode: the reviewed loader reads installed `pkglib_path/llvmjit_types.bc` and `pkglib_path/bitcode/<module>`; the current schema introduces no custom LLVM/native module or raw bitcode input. This conclusion relies on the immutable installed-module boundary and supported schema, not the absence of a bitcode parser.

CVE-2026-13573 is retained open because the current review does not establish every SQL-derived symbol/renaming bound at the LLVM ValueSymbolTable/StringMap path. A tested reference configuration preventing JIT provider initialization could establish a separate exclusion; this review does not claim that configuration is already present.

`caller-proof.json` records exact native dependency hashes, upstream source hashes and imported-probe bindings. Its PostgreSQL17.12 upstream source limit is explicit: no bit-for-bit PGDG build-source attestation is asserted. Exact source excerpts are retained under `source/`. The 16 primary CVE records and acquisition URLs/hashes are retained locally. `proof-manifest.json` hashes all current evidence. The supplementary `network-assessments.json` is exploratory only and is superseded by the separate residual-agent review; it should not be merged over that review.
