from pathlib import Path
import json,re,hashlib
B=Path(__file__).resolve().parent;C=B.parent;R=C.parents[1];S=C/'source';P=R/'.local/security-clearance-2026-11-25/postgres-os-investigation/postgresql-17.12'
ledger=json.loads((R/'docs/release-evidence/2026-11-25-security-followup/advisory-ledger.json').read_text())
pattern=re.compile(r'\b(?:xmlparse|xmlserialize|xpath|xmltable|xml_is_document|xmlagg|xmlelement|xmlconcat|query_to_xml|xslt_process|XmlDocument|XDocument)\b|CREATE\s+EXTENSION',re.I)
controls={sample:bool(pattern.search(sample)) for sample in ['XMLPARSE (document foo)', 'xmlparse(document foo)', 'CREATE EXTENSION xml2', "SELECT xpath('x', xml_doc)"]}
assert all(controls.values()),controls
files=[p for p in (S/'src').rglob('*') if p.is_file() and p.suffix in ['.cs','.sql'] and not {'obj','bin'}.intersection(p.parts)]
hits=[str(p.relative_to(S)) for p in files if pattern.search(p.read_text())];assert not hits,hits
app={'files_checked':len(files),'pattern':pattern.pattern,'flags':pattern.flags,'positive_controls':controls,'hits':hits,'sha256':{str(p.relative_to(S)):hashlib.sha256(p.read_bytes()).hexdigest() for p in files}}
(B/'application-query-review.json').write_text(json.dumps(app,indent=2)+'\n')
sourcepaths=[P/'src/backend/libpq/be-secure-openssl.c',P/'src/interfaces/libpq/fe-secure-openssl.c',P/'src/common/base64.c',P/'src/backend/utils/adt/xml.c']
(B/'upstream-source-inputs.json').write_text(json.dumps({str(p.relative_to(P)):hashlib.sha256(p.read_bytes()).hexdigest() for p in sourcepaths},indent=2)+'\n')
probe=json.loads((B/'probe.json').read_text())
# Actual dynamic imports complement, but do not replace, reviewed upstream caller source.
for name in ['postgres','libpq']:
 for symbol in probe[name]['undefined_symbols']:
  assert not re.search(r'^(CMS_encrypt|CMS_verify|PKCS7_verify|EVP_EncodeBlock|PEM_X509_INFO|SSL_CTX_set_cert_flags|SSL_CTX_dane|SSL_dane|DTLS_|OSSL_QUIC)',symbol),symbol
rows=[]
for x in ledger:
 if x['image']!='postgres-trixie' or x['disposition']!='open':continue
 condition=None;assessment=None
 if x['source']=='libxml2':
  condition='The affected XML/catalog/validation/XPointer/XInclude/output or Python-binding path described in '+x['advisory']+' receives attacker-controlled input.'
  assessment='PostgreSQL really links and exposes SQL/XML; no claim that authentication or external-entity blocking makes it safe. Current RateFreak schema/migrations contain no XML columns, SQL/XML or XML extension calls; all application text/JSON remains ordinary parameters and is not implicitly XML-parsed. Reviewed supported bootstrap, migration, health, backup and restore of that schema do not invoke the affected XML operations. This excludes current application callers only: arbitrary authenticated SQL, imported schemas, XML columns, custom extensions and external database configurations remain unassessed.'
 elif x['source']=='libxslt':
  condition='The affected XSLT/EXSLT transform, math.random or result-tree-fragment key handling receives a stylesheet/document.'
  assessment='No application schema, migration, provider or maintenance call uses XSLT, EXSLT or the PostgreSQL xml2/xslt extension. Core SQL/XML is a separate libxml2 capability, not evidence of libxslt use. Current source query review finds no CREATE EXTENSION or stylesheet/transform caller. An operator-added extension or arbitrary SQL/stylesheet is outside this exclusion.'
 elif x['source']=='openssl':
  condition=x['title']
  assessment='Exact reference PostgreSQL defaults SSL off; supplied Compose has no SSL override/certificate provisioning, and host operator SQL uses local sockets. Its real libcrypto use for SCRAM/hashes does not invoke these TLS/DTLS/QUIC, CMS/PKCS7, DANE, AES-GCM-SIV, PEM-stack-append or EVP_EncodeBlock conditions. Reviewed PostgreSQL17.12 server/libpq source uses the TCP SSLv23_method API (not QUIC/DTLS), has no SuiteB cert-flag setter, CMS/DANE/affected PEM/EncodeBlock caller; own pg_b64_encode implements base64. Exact ELF imports independently lack the affected direct APIs. No arbitrary openssl command is invoked by the supplied entrypoint/health/init/maintenance flow. This does not approve arbitrary crypto commands, TLS/custom provider configuration, operator-added extensions or every OpenSSL consumer in other images.'
 elif x['source']=='libzstd':
  condition='A crafted Zstandard frame is passed to the vulnerable1.5.7 decompressor (CVE-2026-124851).'
  assessment='Exact PostgreSQL default wal_compression=off and supplied Compose has no override. Coordinated pg_dump custom backups use default gzip and the actual restore client is the separate tools image with fixed1.5.7+dfsg-1+deb13u1. Current reference has no physical restore/replication or attacker-selected Zstandard archive/WAL input. Database-image zstd packages remain unpatched. Imported zstd archives, custom WAL compression, physical replication/restores or direct database-image pg_restore need reassessment.'
 if condition:
  rows.append({'image':x['image'],'image_reference':x['image_reference'],'source':x['source'],'id':x['advisory'],'matches':x['matches'],'condition':condition,'assessment':assessment,'disposition':'conditional-supported-caller-exclusion','evidence':['postgres-core/probe.json','jit-policy/acceptance.json','postgres-core/application-query-review.json','postgres-core/upstream-source-inputs.json','postgres/transition-review.md','../2026-11-25-security-integration/postgres/assessment.md'],'source_limit':'Retained PG17.12 upstream source, not a bit-for-bit patched PGDG source reconstruction; exact selected binary imports/defaults and application caller scope independently bound.','reassess_if':['Change schema/query/extension/crypto/compression/replication/restore or operator configuration','Change PostgreSQL/native image bytes, supported architecture or application inputs']})
(B/'assessments.json').write_text(json.dumps(rows,indent=2)+'\n')
print(len(rows),'records',sum(len(x['matches']) for x in rows),'matches')
