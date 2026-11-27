# Default music artwork filter

Owner approved a default-on, optional Only show results with artwork filter for
MusicBrainz album and song discovery. Check actual successful image loads, not
just imageReference presence. Preserve other providers and preview/import access.

Implement in browser discovery using the existing artwork renderer: at most four
cover checks in flight, an eight-second deadline per image, reveal each result
only after load succeeds, hide missing/failed/timed-out covers. Keep partial
results visible while checks run. Failed covers can be shown through Show all
results; don't claim those recordings do not exist. No new server image fetch,
cache, API endpoints, provider metadata requests, dependencies or migrations.

Persist the opt-out as artwork=all in discovery URLs. This is a UI filter, never
forward it to the metadata API. Checkbox changes apply with Search providers,
like the other search fields. Keep pagination based on raw API rows, including
Next on a fully filtered page. Do not combine or refill upstream pages.

- [x] Add failing parsing/UI regressions for music defaults, opt-out, actual cover
  load/error, bounded concurrency/timeouts, page recovery and other providers.
- [x] Implement URL/form controls and extract discovery results with cover states.
- [x] Add opt-in artwork load/error callbacks and eager checks to shared renderer;
  preserve default lazy rendering in all other consumers.
- [x] Run focused/full frontend tests, lint, typecheck and independent review.
- [x] Verify running app, document behavior, commit only implementation-owned files.


Verification: nine new regressions failed before implementation; focused 55 tests
then passed. Full frontend suite passes 1103 tests across 129 files; lint and
Next.js type generation/TypeScript pass. Independent read-only review found no
actionable issues. Chromium verified album/song defaults, real image load/error
handling using controlled responses, opt-out restores all six fixture results,
query forwarding and raw pagination. Dark/light desktop/mobile captures at
1440/390/320px have no horizontal overflow or page errors; screenshots inspected.
Mechanical design detector has no findings. Existing local app remains running;
no backend code, live database, provider configuration or dependencies changed.

Live Chromium verification of an unmocked Survivalism song search finished with
four visible results, each containing a successfully decoded cover image. The
checkbox was checked by default. Evidence and screenshots are saved privately
under .local/localhost/music-artwork-filter/. Changes committed locally; existing
owner STATUS edits and investigation file excluded.
