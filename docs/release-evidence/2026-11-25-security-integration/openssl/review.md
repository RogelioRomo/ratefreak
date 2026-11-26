# Focused OpenSSL consumer review — 2026-11-25

The four advisory conditions below support **conditional exclusions for the reviewed Node/.NET/libpq supported consumers**. They do not clear API curl, other native libraries, PostgreSQL/Caddy services, the host, or the overall release gate. The underlying OpenSSL code remains affected; this review does not claim patched vendors or prove a complete dynamic call graph.

## Identity and evidence

- Source snapshot: `c4b8302b1568b88a5400d9a13b01a4ad83b9218e`; exact initial images are in `assessments.json` and `binary-probe.json`.
- Node `v24.22.3`, embedded OpenSSL `3.5.9`; all 62 `.cc`/`.h` files returned by its pinned `src/crypto` listing and `deps/ncrypto/ncrypto.{cc,h}` were inspected for the affected caller APIs.
- .NET `10.0.15`, VMR `8e0f8ed2312954727f8919d7d1c2f78b483b4ee7`; inspected native OpenSSL shim, X509/PKCS7 sources, managed AnyOS PKCS implementation, SignedCms and SignerInfo.
- libpq `postgresql-19 19.0-2.pgdg13+1`; source provenance is retained in the previous authenticated descriptor-based source record. The new tools binary hash is identical to the previously assessed trial binary: `44167a345720d9bf03279f41cab89f0b5a58d7d675c07402f1ba936faffc8755`.
- The new API/tools .NET crypto shim matches the previous binary: `ed43a3226b54594c582a7e51d2b8a325f5dc9e4351511dfa40bd3e955bce578c`.
- Production application/deployment search covered 608 files with no direct targeted CMS/DANE/affected-PEM/revocation API use. This corroborates upstream consumer inspection; it is not sufficient by itself.
- Downloaded source manifests retain primary upstream URLs, exact commits/tags and hashes. Eighty-one source/API responses were retained; a guessed optional `PkcsHelpers.cs` URL returned 404 and is explicitly recorded, not used as evidence. `input-hashes.json` binds the retained inputs.

## Findings

**CVE-2026-107493 — non-NULL existing X509_INFO stack.** No call to the affected `PEM_X509_INFO_read*` family was found in Node bindings/ncrypto, .NET shim or libpq TLS source. Node uses individual `PEM_read_bio_X509[_AUX]` loops (`crypto_context.cc:287,2142`), and .NET scalar PEM readers do likewise (`pal_x509.c:135,141`). libpq uses OpenSSL's certificate-store loading functions. The November 17 OpenSSL advisory expressly says its own TLS CA-loading callers pass NULL, which avoids the required append-to-existing-stack condition.

**CVE-2026-107496 — CMS encryption to malicious EC recipient.** No affected `CMS_encrypt[_ex]` binding/caller exists in the reviewed consumers. .NET AnyOS explicitly selects `ManagedPkcsPal` (`PkcsPal.AnyOS.cs:10`), constructs EnvelopedData in managed code (`ManagedPal.Encrypt.cs:66`) and encrypts transport keys through managed RSA (`ManagedPal.KeyTrans.cs:174`). Standard TLS does not invoke this CMS-encryption path; the November 10 advisory confirms that distinction.

**CVE-2026-84786 — DANE client PKIX/TLSA combination.** No OpenSSL DANE initialization/TLSA binding or call exists in the reviewed consumers. Exact .NET/libpq binaries lack strings for the named DANE imports but contain normal X509 store/verification symbols. Supported RateFreak source/configuration has no DANE client. Underlying OpenSSL availability is not confused with consumer use.

**CVE-2026-121482 — CMS/PKCS7 verification with hostile CRLs.** No `CMS_verify`/`PKCS7_verify` call exists in reviewed consumers. .NET SignedCms uses managed SignerInfo signature verification, then X509Chain (`SignerInfo.cs:843-875`), without populating a native CMS verification store from SignedData CRLs. **Online revocation is present** at `SignerInfo.cs:868`; Node's SetCRL and libpq's sslcrl can also enable CRL checking. These are not generally absent or harmless APIs. The reviewed application has no hostile CMS/PKCS7 verification endpoint; current CRL handling is separate from the advisory's attacker-controlled message/CRL multiplicative condition. The November 24 advisory notes that ordinary TLS CRLs come from the local store; no universal zero-cost TLS/CRL claim is made.

## Reassessment limits

Rebind the binary hashes/image identities after any final rebuild. Reassess for changed Node/.NET/libpq inputs, new native/PInvoke/addon or custom server code, DANE, CMS encryption/verification, attacker-controlled certificates/CRLs or changed revocation-store provisioning. API curl and other native consumers need separate coverage. No scanner suppression, product edit, publication or persistent-instance access was performed.
