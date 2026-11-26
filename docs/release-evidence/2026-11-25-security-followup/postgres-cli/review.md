# PostgreSQL remaining CLI caller review

22 per-advisory records for the exact selected PostgreSQL17 Trixie image are in assessments.json. Reviewed the actual retained entrypoint, supplied compose/role SQL and host/tool maintenance command graph; source hashes are retained. No product edits or live-instance access.

The main positive caller is privileged ownership repair: root executes find/chown before gosu and before server startup. The primary discoverer's [CVE-2017-18018 report](https://www.openwall.com/lists/oss-security/2018/01/04/3), independently checked2026-11-25, concerns recursive dereference traversal. Entrypoint uses nonrecursive chown operands without those flags. This does not make generic find/chown pathname races impossible; the supported-volume sequencing and lack of concurrent untrusted PGDATA writer supply a second boundary. Shared volumes, hostile extra processes or operator changes invalidate this exclusion. No claim that chown is absent.

Dash is also a real caller because health uses CMD-SHELL and psql initialization uses backtick cat. Those commands are fixed and do not expose the named pathological pattern or Unicode escape converter. Password output is data rather than executable shell code. The assessment never substitutes Bash presence for a false absent-dash claim.

Other records exclude the named utility operation from the exact startup/health/plain-SQL contract. GNU tar is not the product backup implementation; GPG/dpkg-source workflows are not invoked at runtime. Existing upstream optional compressed/custom init branches remain acknowledged and outside the supplied-only init-file scope. No arbitrary authenticated SQL, COPY PROGRAM, custom extension/init script or operator shell is approved by these exclusions.

These records support conditional caller non-reachability, not fixed packages or overall release clearance.
