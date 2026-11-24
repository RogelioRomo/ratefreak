# Debian 13 runtime trial

Approved scope: isolated trial of web/tools final stages, from the tested a1276b1 source.
Only change final Debian base to official pinned amd64 Trixie, ICU72 to76, and PGDG suite totrixie.
Keep application schema, Node/.NET versions, PostgreSQL server17, Python entrypoints and category behavior unchanged.
Forward: build a new nonofficial image set; audit/compare all image scans; reconcile changed OS sources/notices; run operator/compliance checks, empty install, HTTPS fresh/upgrade and real backup/restore acceptance.
Rollback: retain the prior source/images; discard trial instance/projects. No production migration or overlapping writers; all synthetic upgrade writers stop before selection changes.
Success: compatible runtime behavior and data preservation, measured scan improvement, complete source mapping; residual risks stay explicit.
No public release, main commit or persistent developer-instance change authorized by this trial.
