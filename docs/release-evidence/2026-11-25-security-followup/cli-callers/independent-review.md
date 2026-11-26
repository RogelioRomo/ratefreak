# Independent CLI caller exclusion review

Reviewed all44 draft records (14 advisory IDs), source input hashes and supported command implementations on 2026-11-25. No product edits.

**Important evidence correction — CVE-2025-6141:** the assessment says maintenance uses non-TTY Docker exec/run. `deploy/self-host/ratefreak.py` uses `compose run` without `-T`; installed `docker compose run --help` says pseudo-TTY allocation is auto-detected and stdin defaults open. An interactive operator invocation may allocate a tools-container TTY. Replace the blanket non-TTY assertion with actual subprocess evidence: `maintenance.query()` passes SQL through stdin PIPE and captures stdout PIPE; pg_dump writes stdout to a file; pg_restore stdout goes to DEVNULL. Host collation psql explicitly uses exec -T. PostgreSQL initialization feeds psql fixed stdin SQL. None of those psql invocations accepts attacker-selected interactive termcap. Supported compose does not expose request-selected TERM/TERMCAP/TERMINFO. The narrower affected-caller exclusion remains defensible under current input/environment constraints. No product change is necessary merely to correct the evidence claim.

No other Critical or Important caller-condition defect identified in this focused review. Fixed Python/Node/.NET entrypoints, no OS-account creation from app fields, no mount/newgidmap/su/login/infocmp/PAM service in the supported command graph, and distinct host-engine mount responsibility support the other scoped exclusions. PostgreSQL's Bash entrypoint/gosu re-execution needs its exact retained source binding, as already listed in the draft hash manifest; this review does not replace runtime identity evidence with an assertion that Bash is absent.

The draft references postgres-entrypoint.sh and ../residual-os/probe-executions.json that are not yet colocated in the draft cli-callers directory; ensure final packaging resolves those evidence paths and preserves image identities. This is a draft evidence-delivery bound rather than an observed runtime bug.

These findings do not clear arbitrary operator shell/configuration/plugin/TTY input or package-installation/build trust.

## Re-review

Parent regenerated the assessment after the correction. Re-read the CVE-2025-6141 record: it now documents actual pipe/file/DEVNULL subprocess boundaries and explicitly disclaims that compose run always disables TTY. The Important evidence issue is resolved; no remaining Critical or Important issue identified in this focused draft review.
