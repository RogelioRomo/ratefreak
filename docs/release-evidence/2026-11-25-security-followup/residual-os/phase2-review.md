# Phase2 libc / systemd caller review

Scope: the API, web and tools image IDs recorded verbatim in `phase2-probe-executions.json`, source revision7140d1a. This adds current-image evidence to the historical first-pass report; it does not clear every source package or rebind all78 earlier exclusions. No product, scanner or deployment settings changed. All image probes were disposable, read-only, no-network containers with no service/data mounts. No malformed attacker test input was executed.

## Result

`phase2-assessments.json` records seven conditional caller exclusions and three unresolved mitigation findings. No residual-risk acceptance is proposed. Three iconv findings and the separate gcc findings are outside this additive review and retain their previous status pending their own evidence. In particular, gconv module presence is **not** an exclusion.

| IDs | Caller conclusion |
|---|---|
| CVE-2010-4756, CVE-2026-126765 | GSS calls glob with fixed `/etc/gss/mech.d/*.conf`; directory is root-owned0755 and empty in API/tools. Web has no GSS native library. No public pattern/config-directory writer exists in supported application/maintenance. Empty matches cannot enter the allocation-failure doublefree after match rewriting. |
| CVE-2018-20796, CVE-2019-9192, CVE-2026-100290 | libkrb5 regex is the configured auth_to_local RULE path. Config files/dirs and override env are absent; application does not perform local OS account mapping. Other installed regex callers are apt, PAM, form, bash, smartcols; these paths are outside supported direct-exec workflows. |
| CVE-2026-115819 | No wide scanf import in501/440/558 inspected ELF files; matching exact .NET/Node runtime C/C++/C# source contains no call or matching entrypoint string. App native interop is fixed filesystem calls. |
| CVE-2013-4392 | systemd/tmpfiles caller absent; direct Node/.NET/Python entrypoints. SELinux filesystem was visible, so SELinux absence is not claimed. |
| CVE-2019-1010022, CVE-2019-1010024, CVE-2019-1010025 | **Open.** Runtime thread/stack guard/cache behavior can execute. Disputed status does not establish caller absence. Generic API500 responses and lack of deliberate public native-pointer APIs do not prove absence of every independent disclosure/stack overwrite required to exploit the mitigation weaknesses. |

## Evidence details

- `phase2-{api,web,tools}-probe.json`: ELF headers/imports/DT_NEEDED/aliases plus exact hashes. Full `/usr/lib/x86_64-linux-gnu` was included, strengthening the older application-only probe. `phase2-probe-executions.json` records image IDs, bounded Docker commands, success and output hashes.
- `phase2-config-*-probe.json`: `/etc/gss/mech.d` root0755/empty in API/tools, absent on web; `/etc/gss/mech`, `/etc/krb5.conf`, `/etc/krb5.conf.d` absent. GSS_MECH_CONFIG/KRB5_CONFIG/KRB5_KTNAME/LD_PRELOAD/LD_LIBRARY_PATH unset. These are absence/metadata checks; no credentials were read.
- `krb5-callers.json`: all15 source matches in each upstream1.20.1 and1.21.3 snapshot. GSS `g_initialize.c:62-64,503` defines and uses the fixed glob. `localauth_rule.c:100,101,121,124` provides the libkrb5 POSIX regex calls. Other source matches are separate PKINIT/kadmind/admin programs, not libkrb5's generic GSS token decoder. Official acquisition URLs and SHA256 appear in `vendor/caller-acquisition-phase2.json`.
- `runtime-callers.json`: scans36,620 .NET and12,775 Node C/C++/C# source files. .NET has zero matches; Node actual regex calls are GoogleTest only. Remaining Node glob tokens are comments/SQLite SQL function metadata, not libc glob. The deployed native runtimes/addons corroborate no imports. Python `re` uses Python's own `_sre` implementation; no production Python FFI is invoked.
- `entrypoint.py:278-285` directly execs Python maintenance or fixed role command. Maintenance:66-71,104-106,167 invokes PostgreSQL clients with argument arrays, never a shell, apt, PAM login or terminal form UI. API curl health is fixed internal HTTP, not SSH/SFTP. Default .NET HTTP uses its runtime sockets/OpenSSL flow, not curl's libssh.
- `vendor/cve-2019-101002{2,4,5}.json`: official CVE records, with independent stack-overflow requirement explicitly stated for1010022. The app's private native interop is limited to filesystem functions in TmdbContentPolicy.cs; GlobalExceptionHandler.cs returns generic status text and traceId. These facts narrow public interfaces but are not promoted into a proof about unknown memory safety issues.
- GLIBC0032 official author bulletin: https://www.openwall.com/lists/oss-security/2026/11/19/9 explains directory-prefix rewriting, allocation failure and attacker control of growing glob results. Fixed trusted empty GSS directory eliminates that caller state; it is not a generic assertion that allocation failure never happens.
- Raw retained Debian systemd2013 notes identify systemd+SELinux/tmpfiles, not installed libsystemd0 APIs. Systemd and systemd-tmpfiles executables are absent in allthree image probes.

## Limits and reassessment

Exclusions apply to supplied entrypoints, image configuration, application/native callers and documented maintenance flows. Root tools is included: it has authority but no supported public operation populates GSS config or runs arbitrary shell/native commands. They are not permissions to waive arbitrary future root commands. Reassess on custom auth/locale/config mounts, added native interop, new packages, changed commands, plugin/rule installation, or any new public glob/regex/wide-input interface. No claim is made that package inventory is clean, host runtime/kernel is cleared, or PostgreSQL server GSS token parsing is absent.

The three open mitigation entries require a real fix/change of affected runtime behavior or a documented deployment-specific non-applicability proof. Existing evidence supports neither. Do not mark them clear solely because vendors consider them non-security bugs or because no independent exploit is currently known.

`phase2-hashes.json` records evidence/source hashes. It must be refreshed if evidence is edited.
