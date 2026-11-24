"""Reconcile the retained exact-image scans with downloaded vendor records.

Run from repository root; inputs are deliberately local snapshots, not live data.
This produces evidence, not scanner suppression rules or release authorization.
"""
import collections
import hashlib
import json
import re
from pathlib import Path

ROOT = Path('.local/os-review-2026-11-23')
SCANS = Path('.local/release-security-update-2026-11-23')
OUT = Path('docs/release-evidence/2026-11-23-os-review')
OUT.mkdir(parents=True, exist_ok=True)


def read(path):
    return json.loads(path.read_text())


def write(name, value):
    (OUT / name).write_text(json.dumps(value, indent=2, sort_keys=True) + '\n')


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


# The family assessments describe use in this application, not a blanket CVE waiver.
families = {
    'acl': 'Local pathname/ACL operations require a caller on attacker-writable paths. Maintenance uses Python file operations, not setfacl/chacl. Library callers beyond the inspected entrypoints are not proven absent.',
    'apt': 'Build-time package acquisition; application entrypoints do not run APT. Signed-by PGDG key and signed repository metadata are used. This is not an audit of all future package acquisition.',
    'attr': 'Local getfattr/setfattr traversal; no such command in inspected application or maintenance entrypoints. A privileged manual invocation remains outside this usage assessment.',
    'bash': 'No shell execution of request content found in inspected entrypoints. Shell remains shipped; temporary vendor advisory requires follow-up rather than an invented CVE or dismissal.',
    'bzip2': 'Recovery-tool malformed-file issue; no bzip2recover call found in inspected application or backup code. Bundled/native compression libraries need separate review.',
    'coreutils': 'Local command arguments, terminal/ioctl or recursive ownership conditions; inspected startup/maintenance do not pass request text to these utilities. Root storage initialization means privilege assumptions must remain scoped.',
    'cyrus-sasl2': 'SASL mechanism-specific conditions. No Cyrus SASL server is configured by the application; libpq/LDAP/curl dependency chains mean client libraries cannot all be dismissed.',
    'dash': 'Local shell pattern/printf inputs; Python entrypoints exec fixed argument arrays rather than constructing shell commands from requests. This does not prove shell bugs unreachable following another compromise.',
    'diffutils': 'Crafted diff3 command/output conditions; no diff3 call found in application or maintenance code.',
    'dpkg': 'Source-package unpacking/patch restoration; production startup does not unpack source packages. Source acquisition/extraction is a separate build/audit workflow.',
    'expat': 'XML parsing features and build options matter. Python startup and maintenance do not import XML parsers, but this does not cover every native parser or transitive caller. Keep remaining XML-memory-safety cases open.',
    'gcc-12': 'Only runtime libraries are required, not a GCC compiler. Several reports concern C++ templates/header code or libiberty; an installed source-package match alone does not identify the compiled caller. Native build/symbol review remains necessary.',
    'glibc': 'Core runtime dependency. Cases include iconv, scanf, regex, resolver, loader and architecture-specific behavior. No blanket exception: native decoding, runtime/DNS use and privilege conditions need function-level assessment where applicable.',
    'gnupg2': 'Signature/crypto/archive-tool cases; production does not import arbitrary user GPG keys or signed messages. gpgtar is absent in all three probes. gpgv repository verification remains a distinct build-time use.',
    'gnutls28': 'TLS/certificate validation and allocation-failure cases. API curl and tools libpq dependency chains load this library through LDAP and related libraries. Vendor disputes for five certificate-policy reports are retained as disputes, not automatic waivers.',
    'gzip': 'Malformed decompression input; backup payloads are uncompressed tar written/read by Python, not GNU gzip. GNU gzip remains installed; other/manual callers are not ruled out.',
    'icu': 'Ubuntu finding concerns the genrb command, which is absent in the exact API probe. ICU runtime-library and separately bundled ICU vulnerabilities are not waived by that absence.',
    'krb5': 'No krb5kdc or kadmind binary in the three probes. Server-only reports have that scoped exposure reduction. GSS client/shared-library bugs remain open because curl/libpq link Kerberos and configurable database/network authentication can invoke clients.',
    'libgcrypt20': 'Protocol/application usage and timing conditions matter. Upstream security-policy disputes are recorded without treating every crypto call as safe. No request-facing direct ElGamal API found in inspected source.',
    'libseccomp': 'Seven temporary Debian identifiers lack normal CVE descriptions in this snapshot. Keep unresolved. Container seccomp filtering is implemented by the host runtime; presence of this userspace library is not an audit of host kernel/runtime policy.',
    'libtasn1-6': 'ASN.1 parsing is a possible TLS dependency; malformed-input conditions require caller review. Do not exempt because no application-level ASN.1 import was found.',
    'libtirpc': 'SunRPC client/server decode conditions; no application RPC service or pmap_getmaps call found in inspected source. Shared-library and external caller reachability are not exhaustively proven.',
    'ncurses': 'Terminal database/infocmp parsing conditions. infocmp IS shipped in all probes. psql can use terminal/readline facilities; no untrusted TERMINFO or infocmp invocation is configured by standard maintenance.',
    'openldap': 'LDAP BER decode and certificate/name checks can be client-side. Tools libpq links LDAP, so the high BER allocation failure finding remains conditional/open. Separate server-module/build exclusions are explicitly scoped by advisory.',
    'openssl': 'Tools libpq and API curl load OS OpenSSL. Node links no OS libssl in ldd and reports its own OpenSSL 3.5.9; that bundled copy needs separate review. The high QUIC source-package match needs upstream version/build confirmation; do not automatically apply it to or waive it for OS 3.0.',
    'p11-kit': 'PKCS#11 RPC parser conditions require an RPC client/server consumer. No p11-kit RPC endpoint is configured by inspected deployment. Library dependency presence remains separate from server exposure.',
    'pam': 'Namespace/userdb/login conditions. pam_namespace.so is shipped, but application authentication uses .NET, not PAM login sessions. Root maintenance does not establish PAM sessions in inspected code; module presence alone is not removal.',
    'perl': 'Perl regex/POD/archive/HTTP modules require a Perl caller. perl IS shipped, but backup uses Python tarfile, not Archive::Tar. No Perl invocation found in inspected startup/maintenance code.',
    'python3.11': 'Python executes entrypoint and backup/restore plus web socket health check. No XML/IMAP/POP/HTTP cookie parser use in those files. Backup uses tarfile.open(mode=r) and extractfile after entry checks, not streaming r|, CLI extraction, extract or extractall. Retain parser/resource-exhaustion risk for operator-supplied backups.',
    'python3.12': 'Python executes API entrypoint; current Ubuntu report is XML hash flooding. Entrypoint does not import XML. This does not waive Python XML parsing in other scripts or bundled parsers.',
    'rtmpdump': 'API curl dynamically links librtmp, but its configured readiness check is fixed internal HTTP and .NET serves application/provider requests. No RTMP request path found; arbitrary/manual curl use could change exposure.',
    'shadow': 'Local accounts, subuid/login conditions. App user is numeric 10001; application registration does not create OS accounts or subuid mappings. Temporary Debian reports remain unresolved pending vendor detail.',
    'sqlite3': 'RateFreak persistence is Npgsql/PostgreSQL and inspected entrypoints do not import sqlite3. No path accepting SQLite databases or arbitrary SQLite SQL found. OS library remains installed/affected; absence of sqlite3 CLI is insufficient to waive the library.',
    'systemd': 'Only selected libraries are present; systemd, journald, homed and oomd executable paths are absent in all three probes. These containers are not a systemd-booted confidential VM. Do not apply this assessment to the host or other service images.',
    'sysvinit': 'No init daemon is used by these image entrypoints. Temporary vendor identifier has insufficient detail for a broad waiver.',
    'tar': 'GNU tar is shipped but backup/restore uses Python tarfile. GNU incremental/one-top-level extraction options are not used. Python tarfile advisories and trust of supplied backups are assessed separately.',
    'util-linux': 'Mount/block-device/local-login cases. Exact images have unconfigured fstab; API/web drop all capabilities and forbid privilege gain. Root maintenance is not equivalently hardened, so do not extend that mitigation to all tools uses. Three Debian binary build exclusions have separate advisory decisions.',
    'zlib': 'OS zlib and separately bundled copies must be distinguished. Debian MiniZip build exclusion is specific to CVE-2023-45853 in Bookworm zlib1g. gzip-stream write-failure and CRC helper conditions remain open for other zlib advisories/native callers.',
}

binary_exclusions = {
    ('zlib', 'CVE-2023-45853'): 'Bookworm zlib binary packages do not build/provide contrib/minizip; explicit Debian vendor note.',
    ('util-linux', 'CVE-2022-0563'): 'Debian util-linux disables chfn/chsh; these commands are supplied from shadow, not this matched source package.',
    ('util-linux', 'CVE-2025-14104'): 'Affected setpwnam caller is chfn/chsh, explicitly disabled in the matched Debian util-linux binaries.',
    ('util-linux', 'CVE-2026-3184'): 'Bookworm login is not built from util-linux; vendor explicitly ignores this source/binary association.',
    ('expat', 'CVE-2023-52426'): 'Debian builds with XML_DTD; vendor says the vulnerable without-XML_DTD build condition does not apply.',
    ('cyrus-sasl2', 'CVE-2026-129483'): 'Debian explicitly builds with --disable-srp; this is the SRP server-mechanism report.',
    ('openldap', 'CVE-2017-17740'): 'Debian vendor note says the affected nops slapd module is not built.',
    ('openldap', 'CVE-2026-22185'): 'Debian vendor note says bundled lmdb mdb_load.c is not built.',
}

debian = read(ROOT / 'debian-tracker.json')
notes = read(ROOT / 'debian-matched-notes.json')
probes = read(ROOT / 'runtime-probes.json')
ledger = {}
inputs = [ROOT / 'debian-tracker.json', ROOT / 'debian-cve-list.txt', ROOT / 'runtime-probes.json',
          ROOT / 'debian-matched-notes.json', ROOT / 'runtime-probe.py', OUT / 'build_review.py',
          ROOT / 'openssl-vulnerabilities.html',
          Path('docs/release-evidence/2026-11-23-security-update/summary.json'),
          Path('docs/release-evidence/2026-11-23-security-update/layer-audit-summary.json')]
counts = collections.Counter()
for image in ('api', 'web', 'tools'):
    scan_path = SCANS / f'{image}-trivy.json'
    inputs.append(scan_path)
    scan = read(scan_path)
    for result in scan['Results']:
        packages = {p['Name']: p for p in result.get('Packages', [])}
        for finding in result.get('Vulnerabilities', []):
            assert result['Type'] in ('ubuntu', 'debian')
            assert not finding.get('FixedVersion')
            counts[image] += 1
            package = packages[finding['PkgName']]
            src, advisory = package['SrcName'], finding['VulnerabilityID']
            assert src in families, src
            key = (result['Type'], src, advisory)
            if key not in ledger:
                row = dict(distribution=result['Type'], release='noble' if image == 'api' else 'bookworm',
                           source=src, advisory=advisory, title=finding.get('Title', ''),
                           disposition='open', assessment=families[src], matches=[])
                if image == 'api':
                    path = ROOT / 'ubuntu' / f'{advisory}.txt'
                    inputs.append(path)
                    status = re.search(r'^noble_' + re.escape(src) + r': (.+)$', path.read_text(), re.M)
                    assert status, key
                    row['vendor'] = dict(url=f'https://ubuntu.com/security/{advisory}', status=status[1])
                else:
                    vendor = debian[src][advisory]
                    status = vendor['releases']['bookworm']
                    row['vendor'] = dict(url=f'https://security-tracker.debian.org/tracker/{advisory}', **status)
                    row['trixie_comparison'] = vendor['releases'].get('trixie', {'status': 'missing'})
                    if (src, advisory) in binary_exclusions:
                        assert advisory in notes
                        row['disposition'] = 'vendor-binary-exclusion'
                        row['decision'] = binary_exclusions[src, advisory]
                if row['disposition'] == 'open' and advisory.startswith('TEMP-'):
                    row['disposition'] = 'open-vendor-detail-needed'
                if src == 'icu' and advisory == 'CVE-2025-5222':
                    assert not probes['api']['probe']['files']['/usr/bin/genrb']
                    row['disposition'] = 'affected-command-absent'
                    row['decision'] = 'genrb command absent in exact API image; exception covers this command-specific finding only.'
                if advisory == 'CVE-2026-105712':
                    assert not probes[image]['probe']['files']['/usr/bin/gpgtar']
                    row['disposition'] = 'affected-command-absent'
                    row['decision'] = 'gpgtar absent in exact matching images; no exception for other GnuPG findings.'
                ledger[key] = row
            ledger[key]['matches'].append(dict(image=image, image_id=probes[image]['image_id'],
                package=finding['PkgName'], installed=finding['InstalledVersion'],
                source_version=package['SrcVersion'], architecture=package['Arch'],
                severity=finding['Severity'], scanner_status=finding.get('Status', ''),
                severity_source=finding.get('SeveritySource', ''), fixed=finding.get('FixedVersion', '')))

rows = [ledger[k] for k in sorted(ledger)]
assert counts == {'api': 46, 'web': 475, 'tools': 493}, counts
assert len(rows) == 236
assert len({r['advisory'] for r in rows}) == 217
assert all(m['architecture'] in ('amd64', 'all') for r in rows for m in r['matches'])
summary = dict(review_date='2026-11-23', scope='Existing exact-image OS matches only; not whole-installation or native/bundled security clearance.',
    source_revision='a1276b1fb70ad4e371decdad53dcab2ef6068f3d', release_gate='held',
    raw_matches=dict(counts), total_matches=sum(counts.values()), distinct_advisory_ids=217,
    distinct_cve_ids=len({r['advisory'] for r in rows if r['advisory'].startswith('CVE-')}),
    distribution_source_advisory_records=len(rows), vendor_records_missing=0,
    record_dispositions=dict(collections.Counter(r['disposition'] for r in rows)),
    match_dispositions=dict(collections.Counter({d: sum(len(r['matches']) for r in rows if r['disposition']==d) for d in {r['disposition'] for r in rows}})),
    debian_bookworm_statuses=dict(collections.Counter(r['vendor']['status'] for r in rows if r['distribution']=='debian')),
    ubuntu_noble_statuses=dict(collections.Counter(r['vendor']['status'].split()[0] for r in rows if r['distribution']=='ubuntu')),
    trixie_comparison=dict(collections.Counter(r['trixie_comparison']['status'] for r in rows if r['distribution']=='debian')),
    trixie_comparison_limit='Vendor source-package comparison only; missing is not unaffected. Not a build, target-image scan, projected total or migration acceptance.',
    suppression_files_changed=False, product_files_changed=False)
write('summary.json', summary)
write('advisory-ledger.json', rows)
write('source-assessments.json', families)
write('runtime-probes.json', probes)
source_files = ['deploy/self-host/entrypoint.py', 'deploy/self-host/maintenance.py', 'deploy/self-host/compose.yaml', 'deploy/self-host/ratefreak.py', 'src/RateFreak.Infrastructure/DependencyInjection.cs',
                'deploy/self-host/api.Dockerfile', 'deploy/self-host/web.Dockerfile', 'deploy/self-host/tools.Dockerfile']
inputs.extend(Path(p) for p in source_files)
write('input-hashes.json', [dict(path=str(p), sha256=digest(p), bytes=p.stat().st_size) for p in sorted(set(inputs))])
print(json.dumps(summary, indent=2))
