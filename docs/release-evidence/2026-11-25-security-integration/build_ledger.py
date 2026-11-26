from pathlib import Path
import collections, hashlib, html, json, re

BASE = Path(__file__).resolve().parent
ROOT = BASE.parents[1]
OUT = ROOT / 'docs/release-evidence/2026-11-25-security-integration'
OUT.mkdir(parents=True, exist_ok=True)

def read(path):
    return json.loads(path.read_text())

def write(name, value):
    (OUT / name).write_text(json.dumps(value, indent=2, sort_keys=True) + '\n')

images = read(BASE / 'images/built-images.json')
debian = read(BASE / 'vendor/debian-tracker.json')
prior20 = {row['id']: row for row in read(ROOT / 'docs/release-evidence/2026-11-23-debian-advisories/advisory-assessments.json')}
service = read(BASE / 'service-investigation/assessment-records.json')
service_map = {(row['service_scan'], row['target'], row['id'], row['package']): row for row in service}
caddy_os = { (row['id'], row['package']): row for row in read(BASE / 'service-investigation/caddy-os-assessment.json')['assessments'] }
web_probe = read(BASE / 'web-probe.json')
tools_probe = read(BASE / 'tools-probe.json')
assert not [hit for hit in web_probe['shipped_js']['feature_pattern_hits'] if not re.search('zstd', hit['feature'], re.I)]
assert web_probe['node']['features']['quic'] is False
assert len(tools_probe['python']['archive_checks']) == 8
assert all(row['rejected_before_any_destination_write'] for row in tools_probe['python']['archive_checks'])
assert tools_probe['python']['safe_flat_and_avatar_extract_without_extract_filters']

records = {}
summaries = {}
scan_bindings = {}
for image in ['api', 'web', 'tools', 'postgres-updated', 'caddy']:
    scan = read(BASE / (image + '-trivy.json'))
    execution = read(BASE / (image + '-trivy-execution.json'))
    assert execution['exit_code'] == 0
    findings = []
    for result in scan.get('Results', []):
        packages = {row['Name']: row for row in result.get('Packages', [])}
        for finding in result.get('Vulnerabilities', []):
            findings.append(finding)
            package = packages.get(finding['PkgName'], {})
            source = package.get('SrcName', finding['PkgName'])
            advisory = finding['VulnerabilityID']
            key = (image, result['Target'], source, advisory)
            if key not in records:
                row = {'image': image, 'scan_config_id': scan['Metadata'].get('ImageID'),
                       'application_image_id': images.get(image), 'target': result['Target'],
                       'class': result['Class'], 'type': result['Type'], 'source': source,
                       'advisory': advisory, 'title': finding.get('Title'),
                       'disposition': 'open', 'matches': []}
                if result['Type'] == 'debian':
                    release = 'bookworm' if image == 'postgres-updated' else 'trixie'
                    vendor = debian.get(source, {}).get(advisory, {})
                    row['vendor'] = {'url': 'https://security-tracker.debian.org/tracker/' + advisory,
                                     'release': release, 'record': vendor.get('releases', {}).get(release),
                                     'description': vendor.get('description')}
                elif result['Type'] == 'ubuntu':
                    raw = BASE / 'vendor/ubuntu' / (advisory + '.txt')
                    page = BASE / 'vendor/ubuntu' / (advisory + '.html')
                    status = re.search(r'^noble_' + re.escape(source) + r': (.+)$', raw.read_text(), re.M) if raw.exists() else None
                    row['vendor'] = {'url': 'https://ubuntu.com/security/' + advisory,
                                     'release': 'noble', 'status': status[1] if status else 'see-retained-official-page',
                                     'evidence': str((raw if raw.exists() else page).relative_to(ROOT))}
                    assert raw.exists() or page.exists()
                if image in ['web', 'tools'] and advisory in prior20:
                    old = prior20[advisory]
                    row.update(disposition='conditional-supported-flow-exclusion',
                               condition=old['required_condition'], assessment=old['assessment'],
                               reassess_if=old['reassess_if'],
                               evidence=['web-probe.json', 'tools-probe.json', 'probe.py',
                                         '../2026-11-23-debian-advisories/advisory-assessments.json'],
                               limit='Prior pinned consumer/source assessment carried forward with current-image feature, native-binary, Python API and hostile-archive probes; upstream package is not claimed patched.')
                service_row = service_map.get((image, result['Target'], advisory, finding['PkgName']))
                if service_row and service_row['assessment'] != 'open-outside-focused-review':
                    row['disposition'] = service_row['assessment']
                    row['assessment'] = service_row['reason']
                    row['evidence'] = ['service/assessment-records.json', 'service/assessment.md']
                if image == 'caddy' and (advisory, finding['PkgName']) in caddy_os:
                    assessment = caddy_os[advisory, finding['PkgName']]
                    row.update(disposition=assessment['disposition'], assessment=assessment['assessment'],
                               condition=assessment['required_condition'], reassess_if=assessment['reassess_if'],
                               evidence=['service/caddy-os-assessment.json', 'service/caddy-os-assessment.md'])
                records[key] = row
            records[key]['matches'].append({'package': finding['PkgName'], 'installed': finding['InstalledVersion'],
                                            'source_version': package.get('SrcVersion'), 'architecture': package.get('Arch'),
                                            'severity': finding['Severity'], 'severity_source': finding.get('SeveritySource'),
                                            'scanner_status': finding.get('Status'), 'fixed': finding.get('FixedVersion', '')})
    summaries[image] = {'matches': len(findings), 'distinct_ids': len({row['VulnerabilityID'] for row in findings}),
                        'severities': dict(collections.Counter(row['Severity'] for row in findings)),
                        'fixed_version_listed': sum(bool(row.get('FixedVersion')) for row in findings)}
    scan_bindings[image] = {'sha256': hashlib.sha256((BASE / (image + '-trivy.json')).read_bytes()).hexdigest(),
                            'bytes': (BASE / (image + '-trivy.json')).stat().st_size,
                            'scan_config_id': scan['Metadata'].get('ImageID'),
                            'repo_digests': scan['Metadata'].get('RepoDigests'), 'execution': execution}

rows = [records[key] for key in sorted(records)]
summary = {'date': '2026-11-25', 'release_security_gate': 'held',
           'source_identity': read(BASE / 'images/identity.json'), 'application_images': images,
           'scanner': 'aquasec/trivy@sha256:8f6bb947dbc528902e11f2f5be52f7b519f6a39d5d148f41166f2040499768fb',
           'database': read(BASE / 'db/metadata.json'), 'scans': summaries,
           'records': len(rows), 'raw_matches': sum(row['matches'] for row in summaries.values()),
           'record_dispositions': dict(collections.Counter(row['disposition'] for row in rows)),
           'match_dispositions': dict(collections.Counter({state: sum(len(row['matches']) for row in rows if row['disposition'] == state) for state in {row['disposition'] for row in rows}})),
           'scope': 'Current merged-filesystem scans and recorded scoped assessments. Not all-layer vulnerability scanning, a claim of exploitability, or final stable-release clearance.',
           'no_suppressions_added': True, 'persistent_instance_changed': False, 'published': False}
write('advisory-ledger.json', rows)
write('scan-bindings.json', scan_bindings)
write('summary.json', summary)
print(json.dumps({'scans': summaries, 'dispositions': summary['match_dispositions'], 'records': len(rows)}, indent=2))
