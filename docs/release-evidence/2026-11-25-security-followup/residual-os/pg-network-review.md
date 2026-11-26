# PostgreSQL remaining network-library findings

`pg-network-assessments.json` covers13 current PostgreSQL Trixie source/advisory records /31 package matches. Each has a conditional caller exclusion, not a package-wide vulnerability waiver. No product/scanner/deployment change or live-instance inspection occurred.

The retained exact-image full ELF inventory and default configuration are supplemented by `pg-network-config-probe.json`: a small local C probe runs as postgres UID999 in a read-only networkless disposable container, with no service/data mounts. It loads installed libgssapi, asks for default INITIATE/ACCEPT credentials, records status and presence of an opaque handle only, and never exchanges authentication tokens or reads credential bytes. Both acquisitions fail (major458752, no handle). /etc/krb5.keytab,krb5.conf,system/postgres-home pg_service.conf and relevant credential/service environment overrides are absent. This describes the immutable reference, not arbitrary existing instance credentials.

Five Kerberos exclusions:

- CVE-2018-5709 is administrative Kerberos database dump parsing, outside PostgreSQL SQL/dump and absent KDB/admin programs.
- CVE-2024-26458 is libgssrpc portmapper helper xmt_rmtcallres, absent from actual linked protocol/library inventory.
- CVE-2024-26461 is outgoing established-context GSS wrapping. PostgreSQL has actual wrap calls, but reference cannot acquire service credentials or establish the required context.
- CVE-2026-107708 official fix modifies only KDC kdc_util.c PAC/S4U2Proxy processing; application GSS acceptor is not a KDC.
- CVE-2026-107778 is forwarded KRB-CRED decoding. PostgreSQL genuinely accepts GSSENC negotiation before pg_hba. MIT1.21.3 accept_sec_context.c:848 verifies AP-REQ with acceptor keytab before process_checksum and rd_and_store_for_creds. Reference absence of acceptor credentials prevents reaching this authenticated forwarded-credential operation. **gss_accept_delegation=false alone is insufficient**: the library still decodes forwarded credentials even with NULL output. Existing fixed ASN.1 input-parser CVEs remain independently handled, not declared unreachable by this argument.

Three Cyrus SASL exclusions: exact plugin ELF inventory has only libsasldb; DIGEST-MD5,SRP,SCRAM mechanism modules are absent. PostgreSQL SCRAM implementation is its own server/client code. Installed base libsasl is acknowledged; custom plugin installation/auth changes reopen these conclusions.

Five OpenLDAP exclusions: actual libldap links OpenSSL, not NSS or GnuTLS. Thus CVE-2015-3276 NSS cipher-string parser is absent. Slapd PID-file and nops overlay operations are absent; mdb_load LMDB input loader absent. For CVE-2020-15719, OpenSSL certificate identity code may exist, but default SCRAM/bootstrap has no LDAP authentication or service lookup caller; no system/user PG service file or override is present. It is excluded by supported caller scope, not vendor dispute.

Official Kerberos patches/announcement, upstream1.21.3 caller excerpts, raw vendor stanzas, probe source and output are retained with SHA256 in `pg-network-hashes.json`. Reassess all Kerberos conclusions on added credentials/keytabs/config/plugins; all LDAP/SASL conclusions on auth/service/plugin/config changes; all conclusions on altered initdb scripts/schema/extensions/commands/mounts. No overall release clearance is claimed, and the three mitigation blockers remain unchanged.
