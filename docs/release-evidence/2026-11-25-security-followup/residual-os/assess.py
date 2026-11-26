from pathlib import Path
import ast,collections,hashlib,json,re
ROOT=Path(__file__).resolve().parents[2];BASE=Path(__file__).resolve().parent
ledger=json.loads((ROOT/'docs/release-evidence/2026-11-25-security-integration/advisory-ledger.json').read_text())
selected=json.loads((BASE/'selected-records.json').read_text());notes=json.loads((BASE/'vendor-notes.json').read_text())
probes={n:json.loads((BASE/(n+'-probe.json')).read_text()) for n in ['api','web','tools']};quick=json.loads((BASE/'quick-probes.json').read_text())
source_paths=['deploy/self-host/entrypoint.py','deploy/self-host/maintenance.py','deploy/self-host/compose.yaml','src/RateFreak.Infrastructure/DependencyInjection.cs','src/RateFreak.Infrastructure/RateFreak.Infrastructure.csproj','src/RateFreak.Infrastructure/Artwork/ArtworkDecoder.cs','web/instrumentation.ts','web/shared/artwork/provider-images.ts']
assert all('sqlite' not in (ROOT/p).read_text().lower() for p in source_paths[:2]+source_paths[3:6])
assert all('TemporaryDirectory' not in (ROOT/p).read_text() for p in source_paths[:2])
assert all(r['probe']['rmtree_fd_symlink_protection'] for r in quick.values())
for n,p in probes.items():
 for row in p['source_files'].values():
  assert not any(i.startswith(('sqlite','xml','imaplib','poplib','http','ssl','base64','pkgutil')) for i in row['imports'])
 assert not any('seccomp' in lib for row in p['elf'] for lib in row['needed'])
 assert all(not p['commands'][c]['exists'] for c in ['/usr/bin/xmlwf','/usr/bin/bzip2recover','/usr/bin/getfacl','/usr/bin/setfacl','/usr/bin/getfattr','/usr/bin/setfattr'])
conditions={
'CVE-2025-12781':'Alternate-alphabet Python base64 decoding used as a security/data-integrity boundary.',
'CVE-2025-15366':'User-controlled IMAP command is passed to Python imaplib.',
'CVE-2025-15367':'User-controlled POP command is passed to Python poplib.',
'CVE-2026-103995':'Python HTTP client parses attacker-controlled chunk sizes/response framing.',
'CVE-2026-115557':'Python SSLContext relies on acceptance of the identified compromised certificate.',
'CVE-2026-115559':'Python imaplib STARTTLS retains attacker-controlled plaintext read-ahead.',
'CVE-2026-115791':'Python tarfile/zipfile/gzip module CLI decompresses to current directory, permitting import shadowing after an error.',
'CVE-2026-116566':'Attacker controls Python socket.getaddrinfo host or port including an embedded NUL.',
'CVE-2026-12003':'Windows legacy Python installer/in-tree VPATH landmark handling.',
'CVE-2026-12345':'tempfile.TemporaryDirectory cleanup resets permissions/flags through a raced replacement path.',
'CVE-2026-124249':'Python http.cookiejar handles expired Set-Cookie headers across domains.',
'CVE-2026-16052':'Python http.cookies.Morsel formats attacker-controlled cookie attribute values.',
'CVE-2026-19552':'Python ssl.get_server_certificate(ca_certs=...) is used as an identity-verification boundary.',
'CVE-2026-3479':'Untrusted resource path is passed to Python pkgutil.get_data under the disputed documented model.',
'CVE-2026-7210':'Python xml.parsers.expat or xml.etree.ElementTree parses attacker-controlled XML.',
'CVE-2026-54369':'Privileged caller uses pathname-based libacl acl_get_file/acl_set_file/acl_extended_file/acl_delete_def_file on attacker-controlled path components.',
'CVE-2026-54370':'Privileged getfacl/setfacl/chacl traverses attacker-controlled path components.',
'CVE-2026-54371':'Privileged getfattr/setfattr traverses attacker-controlled path components.',
'CVE-2026-42250':'bzip2recover processes a crafted recovery input file.',
'CVE-2017-18018':'GNU chown/chgrp recursive symlink/path traversal, distinct from direct fchown/chown syscalls.',
'CVE-2025-5278':'GNU sort receives the affected key specification and input.',
'CVE-2026-56391':'GNU uniq processes crafted multibyte input.',
'CVE-2026-56392':'GNU unexpand processes crafted tab stops.',
'CVE-2026-102473':'dash pmatch processes the affected pathological pattern with libc fnmatch disabled.',
'CVE-2026-102474':'dash conv_escape processes the affected Unicode escape.',
'CVE-2026-53910':'GNU diff/diff3 performs the affected large line-mapping calculations.',
'CVE-2026-91094':'dpkg-source unpacks a crafted 3.0(quilt) source package.',
'CVE-2026-128241':'xmlwf -d output mode writes through a planted symlink.',
'CVE-2026-102633':'Expat allocation-size arithmetic on a 32-bit ABI parses attacker-controlled XML.',
'CVE-2025-69720':'infocmp analyze_string processes crafted terminal capability data.',
'CVE-2025-6141':'ncurses postprocess_termcap parses crafted termcap data.',
'CVE-2011-4116':'Perl File::Temp _is_safe handles attacker-controlled symlinks.',
'CVE-2026-15534':'Perl S_regmatch operates on a roughly286MB subject/pattern combination that overflows its cache size.',
'CVE-2026-40558':'Perl Net::FTP handles the affected FTP response/data.',
'CVE-2026-82560':'Perl Pod::Text formats maliciously nested POD.',
'CVE-2026-9538':'Perl Archive::Tar reads a crafted tar header size.',
'CVE-2026-40228':'systemd-journald/logger terminal logging flow.',
'CVE-2026-121876':'systemd-gpt-auto-generator handles the described confidential-computing/partition environment.',
'CVE-2026-15059':'systemd-oomd IPC service processes an unprivileged request.',
'CVE-2026-16742':'systemd-homed manages an attacker-controlled local home record/group assignment.',
'CVE-2023-31437':'Systemd journal sealing/viewer code consumes modified sealed journal files.',
'CVE-2023-31438':'Systemd journal sealing/integrity code consumes truncated/modified journal files.',
'CVE-2023-31439':'Systemd journal sealing/integrity code consumes modified past events.',
'CVE-2026-18477':'GNU tar incremental dumpdir X rename extraction.',
'CVE-2026-18508':'GNU tar --one-top-level extraction of crafted hard links.',
'CVE-2005-2541':'GNU tar extracts setuid/setgid archive members without the discussed warning.',
'CVE-2026-5704':'GNU tar parses/extracts the affected crafted archive.',
'TEMP-0290435-0B57B5':'GNU tar remote-tape rmt command is invoked with the affected authority/input.',
'CVE-2019-1010023':'A caller runs ldd on attacker-supplied ELF objects.',
'CVE-2026-84243':'Attacker controls LANGUAGE plus an applicable locale and can place a crafted gettext catalog across a privilege boundary.'}
rows=[]
for original in selected:
 src=original['source'];id=original['advisory'];matches=[r for r in ledger if r['image'] in probes and r['source']==src and r['advisory']==id and r['disposition']=='open']
 row={'source':src,'id':id,'title':original.get('title'),'disposition':'open-caller-analysis-required','condition':conditions.get(id,original.get('title')),
      'images':{r['image']:r['application_image_id'] for r in matches},'matches':[{'image':r['image'],**m} for r in matches for m in r['matches']],
      'upstream_patched_claim':False,'evidence':['probe.py','probe-executions.json','api-probe.json','web-probe.json','tools-probe.json','quick-probes.json','vendor-notes.json'],
      'scope':'Exact API/web/tools images and supported production startup, fixed health checks, protected storage preparation and coordinated logical-backup/restore. Not arbitrary operator commands, host/kernel/container-engine security, external database configurations or embedded copies.'}
 conditional=False
 if src in ['python3.12','python3.13']:
  conditional=True
  reason='Production Python launches Node/.NET via exec, supplies a fixed literal127.0.0.1 TCP health check, or runs entrypoint/maintenance JSON/filesystem/PostgreSQL helpers. Exact source import/call inventory contains no affected mail/HTTP/cookie/TLS/base64/XML/SQLite/resource APIs. Native libpq/.NET network code is a different consumer, not Python networking.'
  if id=='CVE-2026-115791':reason='Supported maintenance imports tarfile, validates all regular-file members and uses extractfile plus explicit O_NOFOLLOW/O_EXCL writes; it never invokes Python archive module CLI or extracts scripts into the interpreter current directory. Existing eight hostile-archive probes verify the actual code path. This excludes this CLI/import-shadowing condition, not all archive resource-exhaustion bugs.'
  if id=='CVE-2026-116566':reason='Python socket health check supplies literal127.0.0.1 and integer3000, not user-provided host/port; maintenance database hostnames go to native libpq. No Python getaddrinfo interface accepts HTTP input or the operator database hostname.'
  if id=='CVE-2026-12003':reason='Exact inspected images are Linux; there is no Windows legacy installer/in-tree layout workflow. This does not assess Windows-native installations.'
  if id=='CVE-2026-12345':reason='Upstream fix changes tempfile.TemporaryDirectory error-cleanup/reset-permissions behavior. Container entrypoint uses mkstemp, not TemporaryDirectory. clear_cache uses shutil.rmtree(name,dir_fd=fd) without the tempfile reset-permissions error callback, after private-directory/no-link/mount preflight and stopped-writer requirement. Actual Python reports fd-based symlink protection. Host-side operator/source tooling may use TemporaryDirectory and is not cleared by the container-package decision.'
  row['assessment']=reason
 elif src=='expat':
  conditional=True
  row['condition']=conditions.get(id,'The affected OS Expat XML parser operation is called with hostile XML.')
  row['assessment']='Exact selected-root ELF inventories link OS Expat through the Python interpreters; supported Python code does not invoke XML parsing. HTTP/provider data is processed by Node/.NET, not Python XML. The inspected application/runtime ELF files do not link OS Expat as an image decoder. Embedded Expat in Sharp/Skia is a separate binary/version and is explicitly not cleared by this OS decision.'
  if id=='CVE-2026-128241':row['assessment']='The affected xmlwf executable is absent in all exact images; neither storage/backup code nor health checks run xmlwf. Exclusion is command-specific, not a generic parser-build exclusion.'
  if id=='CVE-2026-102633':row['assessment']='Vendor description requires32-bit allocation arithmetic; all matched OS packages and reference processes are Linux amd64. This does not clear separately shipped32-bit Skia binaries or unsupported architectures. Supported Python paths additionally do not parse XML.'
 elif src=='sqlite3':
  conditional=True;row['condition']='The affected SQLite SQL/FTS/RTree/WAL/changeset/database processing path is invoked.'
  row['assessment']='Application persistence is Npgsql/PostgreSQL; repository application sources and entrypoint/maintenance contain no SQLite access or SQLite-file import workflow. Exact selected-root ELF inventories find OS libsqlite3 only in the optional Python _sqlite3 extension; production Python does not import it. Node has a separately embedded SQLite copy, excluded from this OS-library decision. CLI absence is supporting context, not the exclusion basis.'
 elif src=='perl':
  conditional=True;row['assessment']='The exact supported startup/maintenance command inventory runs Python, Node, dotnet, efbundle and PostgreSQL client programs, not Perl. No inspected application/runtime ELF links libperl. Backup parsing uses Python tarfile, not Perl Archive::Tar; application regex uses .NET/V8, not Perl S_regmatch. Perl remains shipped and arbitrary Perl/build-package invocations are outside this supported-flow assessment.'
 elif src in ['acl','attr']:
  conditional=True;row['assessment']='The affected getfacl/setfacl/getfattr/setfattr utilities are absent. For the libacl API finding specifically, supported storage code uses Python file-descriptor/syscall ownership/permissions and guarded explicit file writes, not pathname ACL APIs; selected-root ELF inventory has no libacl dependency or selected ACL API imports. This does not assume root maintenance has API/web privilege restrictions, or claim all pathname operations universally race-free.'
 elif src in ['coreutils','dash','diffutils','tar']:
  conditional=True;row['assessment']='No supported container entrypoint/health/maintenance flow invokes the affected command/operation: os.execvpe and subprocess argument arrays select Node/.NET/PostgreSQL tools directly; sorting/chowning/archiving is implemented by Python APIs, with archive extraction using tarfile.extractfile rather than GNU tar. These decisions cover the named program conditions only, not arbitrary operator shell commands or all build/source-audit tools.'
 elif src=='bzip2':
  conditional=True;row['assessment']='The advisory concerns bzip2recover, which is absent from all exact inspected images and never invoked by supported flows. This does not waive decompressor libraries or other malformed archive issues.'
 elif src=='dpkg':
  conditional=True;row['assessment']='The affected dpkg-source executable and Dpkg::Source::Package::V3::Quilt module are absent from all exact final images. Runtime package installation/build source-extraction duties are separate from this final-image command exclusion.'
 elif src=='ncurses':
  conditional=True;row['assessment']='infocmp IS present; supported startup/health/maintenance never invokes it. PostgreSQL psql clients can link readline/tinfo, but supported maintenance supplies fixed SQL on pipes with -X and no attacker-controlled TERMINFO/TERMCAP/TERM configuration. Images/compose do not pass locale/terminal inputs from HTTP users or ship a remote login endpoint. This is an affected-input/workflow exclusion, not a library-removal claim; interactive psql or custom terminal databases require reassessment.'
 elif src=='systemd' and id!='CVE-2013-4392':
  conditional=True;row['assessment']='Exact image command probes find no systemd init, journald, homed, oomd or gpt-auto-generator executable; container entrypoint execs Node/.NET or offline Python maintenance. There is no supported sealed-journal reader/writer, homed-managed login, oomd IPC endpoint or confidential-VM auto-generator flow. No inspected application/runtime ELF links libsystemd. This is scoped to shipped application flows and does not clear host systemd or every shared-library API.'
 elif src=='libseccomp':
  conditional=True;row['condition']='The named libseccomp rule-construction/optimization/transaction operation processes the affected rule state.'
  row['assessment']='These TEMP records have GHSA descriptions/fixes in the retained raw Debian tracker and are not missing-detail placeholders. Exact application/runtime/maintenance ELF inventory contains no libseccomp dependency; supported Python/Node/.NET/PostgreSQL-client workflows neither compile nor accept seccomp rule programs. Docker applies its profile through the host runtime before these processes start; that host library/kernel assessment remains separate. No exception is inferred solely from a TEMP identifier or container hardening.'
 elif src=='glibc':
  if id=='CVE-2019-1010023':
   conditional=True;row['assessment']='Supported application/storage/maintenance flows never run ldd on user uploads or arbitrary ELF. Uploads are treated as raster-image bytes, not executables; operator-image audit ldd probes inspect only exact trusted package binaries. This does not exempt the ELF loader or arbitrary manual ldd use.'
  elif id=='CVE-2026-84243':
   conditional=True;row['assessment']='Exact base images leave LANGUAGE/LANG/LC_ALL/LC_MESSAGES/CHARSET/G_FILENAME_ENCODING unset; reference compose does not map HTTP data or user profile locale into those process environment variables and no SSH/restricted-command AcceptEnv endpoint is shipped. A user cannot select a gettext catalog via normal application input. Reassess any untrusted environment forwarding or custom locale/catalog mounts.'
  elif id in ['CVE-2026-121757','CVE-2026-121758','CVE-2026-124024']:
   row['assessment']='Web libvips actually imports iconv/iconv_open. Retained source search locates generic GLib conversion, libxml2 input/output charset handlers and libarchive conversions; the finding is not dismissed as unused code. CP949 requires UTF8//IGNORE destination, ISO-2022-CN-EXT is a source decoder (not the historical outbound encoder), and IBM1390/1399 requires that output target from UCS4. Normal fixed locale and existing SVG/HEIF restrictions narrow exposure, but this review did not complete a final-format/metadata-to-converter proof for every native caller. Keep open; no new product restriction is proposed.'
  elif id in ['CVE-2019-1010022','CVE-2019-1010024','CVE-2019-1010025']:
   row['assessment']='Vendor/upstream classifies these mitigation/ASLR reports as non-security or non-standalone issues. That classification is retained, but no blanket exception is imposed: maintainer threat-model/disposition decision remains explicit rather than inferred from ignored/no-dsa status.'
  else:
   row['assessment']='Selected application/runtime ELF symbols show no imported POSIX regcomp/regexec/glob or wide-scanf APIs; application regex/globbing uses managed/.NET/V8/Python implementations. This is useful narrowing, not an exhaustive proof for all transitive native library/internal callers. Keep the exact affected-function case open until that boundary is established; no demonstrated RateFreak exploit was identified.'
 elif src=='gcc-14':
  row['assessment']='Affected libstdc++ operations include header-instantiated binary heap erase_if, parallel search, vector<bool>, from_chars ranges, streambuf I/O failure and aligned allocation overflow. Shared-library presence or absent compiler/headers does not clear already compiled consumers. Exact symbols/source callers and their controlling bounds must be reviewed; Skia/Sharp/Node native consumers are real. No blanket exclusion based on upload size alone is made.'
 elif src=='systemd':
  row['assessment']='The old generic permission/SELinux symlink report was not narrowed to its exact current library/program implementation in this review. Absent init executable alone is not used as a blanket exclusion for all library callers.'
 if conditional:row['disposition']='conditional-supported-flow-exclusion'
 row['reassess_if']=['Change runtime/native/package inputs or enable new parser/network/shell/Python/SQLite/XML consumer','Enable custom plugins, interactive/local-login workflows, user-controlled locale/terminal/environment or arbitrary operator commands','Change protected backup/restore trust boundary or the supported image/architecture/topology']
 rows.append(row)
assert len(rows)==len(selected)==97
summary={'source_advisory_records':len(rows),'conditional_records':sum(r['disposition']=='conditional-supported-flow-exclusion' for r in rows),'open_records':sum(r['disposition']!='conditional-supported-flow-exclusion' for r in rows),'matched_package_associations':sum(len(r['matches']) for r in rows),'conditional_package_associations':sum(len(r['matches']) for r in rows if r['disposition']=='conditional-supported-flow-exclusion'),'image_ids':{n:p['image_id'] for n,p in probes.items()},'security_release_gate_cleared':False,'scope':'API/web/tools selected OS source families only; no scanner ignores, product changes or persistent instance access.'}
(BASE/'assessments.json').write_text(json.dumps(rows,indent=2,sort_keys=True)+'\n');(BASE/'summary.json').write_text(json.dumps(summary,indent=2)+'\n')
paths=[BASE/'probe.py',BASE/'run_probes.py',BASE/'quick_probe.py',BASE/'quick-probes.json',BASE/'selected-records.json',BASE/'vendor-notes.json',BASE/'native-iconv-calls.json',BASE/'vips-text-callers.json',BASE/'assess.py',ROOT/'.local/security-clearance-2026-11-25/vendor/debian-tracker.json',ROOT/'.local/security-clearance-2026-11-25/vendor/debian-cve-list.txt',ROOT/'docs/release-evidence/2026-11-25-security-integration/advisory-ledger.json']
paths += [BASE/(n+'-probe.json') for n in probes]+[ROOT/p for p in source_paths]+[p for p in (BASE/'vendor').iterdir() if p.is_file()]
paths += [ROOT/'.local/security-clearance-2026-11-25/postgres-os-investigation/postgresql-17.12/src/bin/psql'/f for f in ['startup.c','input.c','mainloop.c']]
paths += list((ROOT/'.local/release-compliance/native-sources').glob('*vips-8.18.7*'))+list((ROOT/'.local/release-compliance/native-sources').glob('*libexif-*'))+list((ROOT/'.local/release-compliance/native-sources').glob('*libtiff-*'))+list((ROOT/'.local/release-compliance/native-sources').glob('*glib-2.90*'))+list((ROOT/'.local/release-compliance/native-sources').glob('*libxml2-*'))+list((ROOT/'.local/release-compliance/native-sources').glob('*libarchive-*'))
inputs=[]
for p in sorted(set(paths)):
 with p.open('rb') as f:sha=hashlib.file_digest(f,'sha256').hexdigest()
 inputs.append({'path':str(p.relative_to(ROOT)),'sha256':sha,'bytes':p.stat().st_size})
(BASE/'input-hashes.json').write_text(json.dumps(inputs,indent=2)+'\n')
for r in inputs:
 p=ROOT/r['path']
 with p.open('rb') as f:assert hashlib.file_digest(f,'sha256').hexdigest()==r['sha256']
print(json.dumps(summary,indent=2))
