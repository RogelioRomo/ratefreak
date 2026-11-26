# PostgreSQL 17 Bookworm to Trixie transition proof — 2026-11-25

This is a proposed transition and host-operator patch, not overall release
clearance. No root product file, persistent developer instance, public registry
or release was changed. All database fixtures and volumes were uniquely named
and disposable. The application image set is the previously qualified parent
proof, not the in-progress SkiaSharp 4.157 image set.

## Proposed choice

For fresh reference installations, the official PostgreSQL 17.12 Trixie image
`postgres:17-trixie@sha256:87c8b42f423b45d5247e6aa8fdb2e7762fec164c68a50fe3eb07e7bfcaf4dbcf`
has Kerberos `1.21.3-5+deb13u2`, the retained Debian fixed version for
CVE-2026-119079 and CVE-2026-107364. The same-database image scan has neither ID.
This addresses those exact installed-library versions without relying on SCRAM
or an absent keytab to claim the preauthentication GSS parser is unreachable.
The separate retained OS findings are not thereby cleared.

Existing Bookworm database volumes must not be silently reused as a supported
upgrade. Preserve the old runtime and a coordinated logical backup, then restore
into a fresh isolated Trixie instance and replay current independent policies.
Retain the original instance as the recovery option; do not write both copies.

## Real collation experiment

`collation_probe.py` creates synthetic text rows, a primary-key index and an
explicit ICU collation/index in a networkless Bookworm 17.12 fixture. It writes a
custom logical dump before stopping the old server. The old volume is then
opened only in a disposable Trixie server to prove the mismatch, not as the
recommended upgrade path.

Observed versions:

| State | Database default stored / actual | Custom ICU stored / actual | Guard |
| --- | --- | --- | --- |
| Bookworm original | 2.36 / 2.36 | 153.120 / 153.120 | accepts |
| Trixie using old volume | 2.36 / 2.41 | 153.120 / 153.128 | refuses |
| Fresh Trixie logical restore | 2.41 / 2.41 | 153.128 / 153.128 | accepts |

The logical restore preserved the synthetic rows and recreated indexes in the
new database. Result ordering matched under the two recorded planner settings.
Matching heap data on the reused old volume was deliberately **not** treated
as evidence that the old indexes were safe.

The first inspection harness observed the entrypoint's temporary initialization
server as ready, then attempted restore during its shutdown/restart gap. Its
failure record is retained as `bootstrap-race-*`. The corrected isolated probe
waits for PID 1 to become the final PostgreSQL server as well as `pg_isready`;
it passed and removed its owned resources. No product readiness rule was weakened.

## Proposed operator guard

`operator-collation-guard.patch` modifies only `ratefreak.py` and adds
`test_database_collations.py` in this isolated source clone. It is not applied to
the shared checkout.

- Before EF migration or application startup, query database default versions
  with `pg_database_collation_actual_version` and all versioned explicit
  collations visible in the reference `ratefreak` database with
  `pg_collation_actual_version`.
- Accept only the exact successful boolean result; mismatches, empty or malformed
  output and SQL failures cannot become startup acceptance.
- Preserve removal of previous migration acceptance before attempting migration.
- Require all application/maintenance writers stopped before `start` can touch
  the database service. This is a deliberate conservative change: repeat
  `start` with running writers refuses rather than implicitly stopping them or
  recreating a changed database underneath them. It follows the documented
  explicit stop → migrate → start sequence.
- Never issue `REFRESH COLLATION VERSION`, `REINDEX`, implicit data migration or
  an automatic stop. The error directs operators to reviewed logical backup and
  fresh-instance restore/replay.
- Leave backup available as the recovery operation. A matching PostgreSQL major
  version or unchanged application-image selection is not a substitute for the
  collation check.

The check is for the single-reference-database topology. It does not inspect
custom collations inside every additional user-created database, prove that
historical indexes were never corrupted, or protect against an administrator
deliberately changing metadata to bypass it. Other topologies need their own
review.

Five behavioral tests were observed failing against the prior implementation,
then passing with the patch. They prove migration refuses before schema writes,
old acceptance is invalidated, startup refuses before writer/deployment
acceptance, running writers cause refusal before `up postgres`, and current
metadata permits migration. The complete local operator suite passed **77 tests**.

## Existing full application recovery path

`run_fresh.py` exercised the qualified parent API/web/tools images against
Bookworm 17.12 over trusted HTTPS. `check_maintenance.py` then stopped writers,
made the actual coordinated database/artwork/key backup, and restored it to a
fresh Trixie 17.12 service using the same application image identities. The
independent current erasure journal and TMDB policy were supplied separately
from the backup, with the required policy ownership preserved.

The restore passed:

- All **33 table fingerprints** and protected artwork/key archives preserved.
- Restore traffic guard present until replay and verification completed.
- Current policy replay and repeated replay passed. The journal fixture is empty;
  this does not replace existing nonempty erasure proof.
- Target database default collation `2.41` matched actual `2.41`.
- Restored API/web services reached readiness after the host collation check.
- Both owned Compose projects' containers and volumes were removed.

The full recovery run exercises the collation helper and restored application
readiness. The subsequent strict stopped-writer preflight has the additional
dedicated RED→GREEN test; the root's final candidate must requalify the combined
host/source/image set after adopting the patch and Skia update.

**Do not bypass backup image binding.** The backup manifest records the old
API/web/tools identities. Restore and replay initially require those matching
images on the fresh Trixie database. Keep writers stopped, then select the new
Skia release images and explicitly migrate/accept them before first startup.
Restoring an old backup directly with different application images is correctly
refused by existing tooling. The final new release still needs that staged
upgrade acceptance.

## Remaining zstd condition and minimum package refresh

The official Trixie image still installs libzstd `1.5.7+dfsg-1`, with a listed fix
`1.5.7+dfsg-1+deb13u1`. A read-only disposable container fetched signed APT
metadata and simulated `--only-upgrade install libzstd1`; APT selects that
security version and the matching `zstd` CLI upgrade. No package/image was
modified. The image's APT cleanup hook emitted a read-only cleanup warning;
the update and simulation exited successfully and the candidate versions are
retained in the log.

`postgres --describe-config` in the exact image reports `wal_compression=off`.
The reference Compose configuration supplies no zstd WAL override, and the
supported coordinated logical backup uses PostgreSQL custom format with its
default gzip compression. Those supported paths do not deliver attacker-chosen
zstd frames to the database image's decoder. The tools image used for actual
logical restore already has the fixed Trixie zstd library.

This is a narrow supported-flow argument, not a package-patched claim. Arbitrary
zstd-compressed archive restoration with the database image's tools, zstd WAL,
physical restore/replication input or operator-added zstd workflows require
reassessment. The remaining SQL/XML and other OS findings also retain their own
caller conditions; authentication or an internal network is not a blanket waiver.

If a fixed library becomes required for supported database flows, the smallest
custom packaging change is a pinned official Trixie base plus a reviewed APT
upgrade of `libzstd1` and its matching `zstd` package, followed by exact-image
inventories/scans and corresponding source/license handling. That introduces a
custom database artifact and maintenance obligation; alternatively wait for an
official image digest containing the fixed packages. No custom database image
was built or published here merely to zero scanner counts.
