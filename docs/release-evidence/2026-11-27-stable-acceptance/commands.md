# Local commands and proof entrypoints

Executed from `/home/rhaydon/documents/repos/ratefreak` with Docker/local-port
sandbox escalation. Images were already cached; candidate tags were not changed.
Private stdout/stderr logs live beside the scripts in the local acceptance root.
The sanitized copies here document methods; their `BASE` path expects placement
back under the local acceptance directory.

```sh
python3 .local/stable-qualification-2026-11-27/acceptance/run_initial.py
python3 .local/stable-qualification-2026-11-27/acceptance/run_upgrade.py
python3 .local/stable-qualification-2026-11-27/acceptance/run_browser.py
python3 .local/stable-qualification-2026-11-27/acceptance/runtime.py
python3 .local/stable-qualification-2026-11-27/acceptance/cleanup_upgrade.py
python3 .local/stable-qualification-2026-11-27/acceptance/capacity.py
python3 .local/stable-qualification-2026-11-27/acceptance/prepare_maintenance.py
RATEFREAK_ACCEPTANCE_ROOT_FILE=maintenance-root.txt python3 .local/stable-qualification-2026-11-27/acceptance/maintenance.py
RATEFREAK_ACCEPTANCE_ROOT_FILE=maintenance-root.txt python3 .local/stable-qualification-2026-11-27/acceptance/resume_maintenance.py
python3 .local/stable-qualification-2026-11-27/acceptance/prepare_capacity.py
RATEFREAK_ACCEPTANCE_ROOT_FILE=capacity-root.txt python3 .local/stable-qualification-2026-11-27/acceptance/capacity-populated.py
python3 .local/stable-qualification-2026-11-27/acceptance/finalize.py
```

The initial shipped upgrade runner failed on missing current policy bind paths;
`run_upgrade.py` adds current storage preparation and migration-aware original-row
fingerprinting. The first maintenance setup correctly hit the provenance CHECK
constraint when a user-authored fixture was assigned a provider type; synthetic
provider fixtures were corrected to system provenance and a fresh fixture was
used. The current deletion journal correctly emits an intent/completion pair;
a single-record assertion was corrected to two records for one account. The
retained valid backup/current policies were then resumed directly for restore.
No application source changes were needed for these fixture corrections.

The initial high-rate capacity attempt exceeded the existing shared-IP catalog
budget; its failed threshold receipt is retained as `capacity-burst.json`.
The two passing workload receipts are separately scoped by their datasets.
Fixture setup rate buckets were reset with a disposable API restart before
measurement. Application rate limits were unchanged.

Backup/restore proofs contain no real account/provider content. Independent
journal replication, real provider account behavior and security-report delivery
remain installation/external duties. Never publish the private fixture directory.
