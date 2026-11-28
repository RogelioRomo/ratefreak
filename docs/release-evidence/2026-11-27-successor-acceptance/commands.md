# Executed qualification commands

From the shared repository, the private `run_initial.py` waited for the completed
candidate manifest, checked all five IDs/platform/version/source labels, then ran
these actual frozen CLI commands (private logs retain the concrete arguments):

```sh
python3 .local/stable-qualification-2026-11-27/successor-source/deploy/release/acceptance.py --confirm-disposable --report-directory .local/stable-qualification-2026-11-27/successor-acceptance/fresh --images-json .local/stable-qualification-2026-11-27/successor-candidate/built-images.json --reference-images-json .local/stable-qualification-2026-11-27/successor-candidate/built-reference-images.json --keep-disposable
python3 .local/stable-qualification-2026-11-27/successor-source/deploy/release/acceptance.py --confirm-disposable --report-directory .local/stable-qualification-2026-11-27/successor-acceptance/upgrade --images-json .local/stable-qualification-2026-11-27/successor-candidate/built-images.json --reference-images-json .local/stable-qualification-2026-11-27/successor-candidate/built-reference-images.json --baseline-images-json .local/stable-qualification-2026-11-27/successor-acceptance/baseline-images.json --keep-disposable
python3 .local/stable-qualification-2026-11-27/successor-acceptance/run_browser.py
python3 .local/stable-qualification-2026-11-27/successor-acceptance/runtime.py
python3 .local/stable-qualification-2026-11-27/successor-acceptance/prepare_capacity.py
python3 .local/stable-qualification-2026-11-27/successor-acceptance/capacity.py
python3 .local/stable-qualification-2026-11-27/successor-acceptance/maintenance.py
python3 .local/stable-qualification-2026-11-27/successor-acceptance/finalize.py
```

`TMPDIR` was the private successor-acceptance directory and
`PYTHONDONTWRITEBYTECODE=1` prevented Python import caches in frozen source.
Docker/local-port escalation was limited to disposable qualification services,
trusted verification Chromium and read-only immutable-image inspection. Initial
watcher state survived the interrupted session. The fresh edge was resumed with
its existing volume/CA through the frozen Compose command before unfinished
browser work continued. No successful shipped fresh/upgrade proof was repeated.

The browser driver waited for initial network/hydration completion and asserted
real auth/login HTTP 200 before /account navigation. Its root-owned result file
was assigned to the local verification user so the wrapper could stamp exact
artifact identities; successful browser assertions were retained. All attempts
and synthetic diagnostics remain private. No production limit, TLS check or
application security control was bypassed.

The prepared resume_operations.py records phase status and durable HANDOFF.md
updates. Capacity setup restarts only the disposable API before measurement to
exclude fixture-registration/login rate buckets. Full source/keys/data restart
and current erasure/provider policy replay occur separately in maintenance.py.
