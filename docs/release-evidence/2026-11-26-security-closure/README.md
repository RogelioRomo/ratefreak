# Security closure receipts

Read [the assessment](../../security-closure-2026-11-26.md) for conditions and
limitations. `verification.json` binds the configuration, images, counts and
evidence-file hashes. `dispositions.json` overlays only the previous 22 open
records; earlier qualified exclusions retain their conditions. A conditional
caller/configuration exclusion does not claim an upstream package fix.

The remaining inventory is two IDs, six image/source records and 16 package
associations. Release clearance is false. The prior image scans are historical;
these receipts do not claim a refreshed scanner database or stable qualification.

## Local reproduction

The scripts use the repository's retained
`.local/security-closure-2026-11-26/` work directory and exact local candidate
images. Source/consumer scripts also require the earlier retained application
archives and ELF inventories named in their inputs. They are local investigation
tools, not an installation step or a portable stable-release qualification suite.
They do not target the persistent developer instance.

From the repository root, with a C compiler and Docker access:

```bash
cc -O2 -Wall -Wextra -Werror -pthread \
  docs/release-evidence/2026-11-26-security-closure/scripts/glibc_probe.c \
  -o .local/security-closure-2026-11-26/glibc-probe
python3 docs/release-evidence/2026-11-26-security-closure/scripts/verify_policy.py
python3 docs/release-evidence/2026-11-26-security-closure/scripts/run_glibc.py
python3 docs/release-evidence/2026-11-26-security-closure/scripts/node_stream_proof.py
```

The retained policy-failure receipt describes the configuration before the
change. Reproduction on the current configuration should pass, not recreate that
historical failure. The cache-disabled probe is diagnostic only; the reference
package applies only the one-arena policy.

`acquire_os.py` downloads exact Debian source descriptors/archives over HTTPS
and checks descriptor SHA256 values. `debug_headers.py` downloads debug packages
and matches their Build IDs to the exact payloads. Neither independently
authenticates package signatures. The source/preprocessor checks do not claim a
reproduced Node or OS build. Debug header absence supplements source/caller
review; positive APT bitset and bool-vector observations remain in the receipts.

`acceptance.py` retains a disposable synthetic HTTPS fixture for
`check_maintenance.py`, which runs coordinated backup/restore and removes its
containers and volumes. Private fixture credentials, database/artwork/key backups
and large raw source/debug files remain outside this evidence directory.
