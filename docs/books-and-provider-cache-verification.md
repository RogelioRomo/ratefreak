# Books and shared provider-cache verification

Verified locally: 2026-11-17 (PST). This records the approved Open Library work
import and all-provider caching change. The persistent local database migration
was subsequently authorized and applied as recorded below. No provider account
registration, deployment or publication was performed.

## Delivered behavior

- `book` catalog type, stable Books category and guarded migration
  `20261118034056_AddBooks`. Work identity is `openlibrary` / `work` / `OL…W`.
  Search, preview, import, catalog, library, rating, review and collection behavior
  use the existing shared catalog flow. Editions/ISBNs are not separate entries.
- Imported normalized metadata stays in PostgreSQL. Publication years retain
  their precision; absent work fields remain unavailable. Author enrichment is
  bounded and cached; optional service failures retain usable work details.
- Successful search/detail metadata from TMDB, MusicBrainz, IGDB and Open Library
  is cached by the API for five minutes/one hour. Keys include provider, type,
  operation, query/page or canonical identity. Cache bounds are 16 MiB/2048 entries,
  64 distinct flights and a 20-second shared deadline. Errors/missing results are
  not retained; disabled/input validation checks precede lookup. Individual
  cancellation is isolated; abandoned work is cancelled and retains its admission
  slot until upstream cleanup finishes.
- Public provider artwork uses the shared Next.js optimizer/cache, with 24-hour
  minimum TTL, 1 GiB configured capacity, finite variants and a 5 MiB upstream body
  limit. Uploaded/moderation artwork retains current authorization and `no-store`.
  The reference Compose mounts a privately prepared persistent image-cache folder.
- A dedicated numeric CoverID route validates Open Library's actual Archive
  redirects, rejects private IPs, pins DNS results and limits time/bytes/hops.
  Optimizer local sources are limited to that route; private/API paths are denied.
  The public Caddy ingress denies the entire internal source namespace; alternate
  ingress must do likewise. Source work is bounded to eight active operations and
  64 queued waiters, with queue time included in the ten-second deadline.
- Next.js and eslint-config-next were patched from 16.4.3 to 16.4.6. The build's
  advisory was confirmed against the [official 16.4.5 security release](https://github.com/vercel/next.js/releases/tag/v16.4.5),
  which includes an image-optimizer SVG-rejection fix. The
  [16.4.6 release](https://github.com/vercel/next.js/releases/tag/v16.4.6)
  stays in the same release family. Exact-version supplemental license text was
  retrieved from upstream and hashed; no new application dependency was added.

## Automated results

| Check | Result |
| --- | --- |
| .NET unit suite | 27 passed |
| PostgreSQL/API integration suite | 431 passed, zero skipped; disposable PostgreSQL 17 |
| Provider contract suite | 196 passed, zero skipped; includes malformed-Unicode and delayed-cancellation cleanup regressions |
| Frontend Vitest on patched Next.js | 775 passed across 102 files |
| Frontend lint/typecheck | Passed; production build also runs TypeScript |
| Frontend production build | Passed on Next.js 16.4.6; 63 production packages, zero missing collected license texts |
| API Release publish | Passed on the final reviewed API revision |
| Operator storage/lifecycle suite | 32 passed |
| Compliance source/notice tooling suite | 18 passed |

Tests cover work-only identities and mapping, author/year/cover bounds, request
pacing/throttling/timeouts/cancellation, JSON/Unicode failures, cache separation,
expiry/coalescing/cancellation, migrations and category collision refusal,
authentication/CSRF, concurrent imports, own-database reads and existing workflows.
Older deliberate two-upstream-call rendezvous fixtures use an uncached registry
only within those database-race tests; production coalescing has separate coverage.

## Production image-cache proof

Tested Docker image ID:
`sha256:e990c90ce33ab82d32b4fdc4e530d53c795b10a8ee7c0a7b42e3a230bf3a3c26`
(`ratefreak-web:books-cache-proof`, local only). It runs Next.js 16.4.6.

Disposable containers used UID/GID 10001, a read-only root filesystem, dropped
capabilities, no-new-privileges, temporary `/tmp`/cache parent and a persistent
privately prepared `/app/.next/cache/images` mount. The image's Node server was
started directly to isolate caching from instance-secret setup. A test-only Node
transport hook supplied controlled upstream bytes/failures at canonical provider
URLs; production code and source restrictions were unchanged.

- TMDB, IGDB and Open Library sources each returned optimized WebP `MISS`, then
  `HIT` without another upstream image fetch for that variant.
- Six simultaneous requests for a new variant produced one upstream fetch.
- After container recreation with a simulated provider outage, all three retained
  variants returned `HIT`, HTTP 200, without contacting their upstream sources.
- A cold variant during the outage returned 503. There is no cold-cache offline
  guarantee.
- With the application stopped, disposable disk-entry expiry timestamps were
  moved into the past. The restarted application returned the retained image as
  `STALE`, HTTP 200, while the background refresh encountered the injected outage.
  This characterizes retained-cache behavior without waiting 24 hours.
- Direct optimizer requests for local API/private/moderation sources, an unrelated
  external host and loopback IP returned 400. Dedicated cover-query/path validation
  and hostile/private redirect behavior also have focused tests.
- A separate unhooked container fetched real CoverID 6498519 through Open Library
  -> archive.org -> numbered Archive storage, returning HTTP 200/image/jpeg,
  41,632 bytes. The upstream response advertised public `max-age=10800`.
- A real reference Caddy ingress returned 404 for direct cover-source paths,
  including case/encoded variants. The same edge continued serving optimized
  covers as HTTP 200 `MISS` -> `HIT`. Source routing is internal to the optimizer.

The 1 GiB production capacity was configured, not filled in a disk-pressure test.
No representative load/egress benchmark or multi-replica cache qualification is
claimed. Raw runtime evidence is retained alongside this note under
`release-evidence/2026-11-17-books-cache/`.

## Browser scope

Real Chromium exercised discovery -> Open Library work preview -> CSRF-bearing
import -> own-catalog details, source attribution and mixed-provider artwork.
Runs covered 1440px desktop and 390px mobile in Light and Dark themes, with no
horizontal overflow or browser runtime errors. Three public provider image paths
used the optimizer; the uploaded-artwork fixture kept its direct API URL.

The browser used deterministic API fixtures and controlled image transport against
the production web candidate. Actual API/database authorization/import behavior is
established separately by PostgreSQL integration tests. This is not an external
provider-enabled HTTPS deployment acceptance or a live account activation.
The four functional journeys were repeated successfully on the final reviewed
image identified above; visual confirmation used the same UI before the server
resource-bound fixes. Final raw browser records include the matching image ID.
Temporary screenshots/harnesses are under `/tmp/ratefreak-image-proof/` and are
not durable public artifacts. Final screenshots wait for settled detail content
and account for Next.js retaining inactive page DOM.

## Independent review

The independent review identified two Important resource-bound findings: cancelled
metadata operations released capacity before cleanup completed, and direct public
cover-source requests could bypass the optimizer cache without aggregate source
limits. Both were reproduced before correction and verified afterward. The final
whole backend suite passed 654 tests; the final frontend suite passed 775 tests.
The final candidate repeated the cache/outage/recreation proof and added a real
ingress denial/cache-hit proof. Fixes were verified with tests rather than a second
review pass. The small library size hint and API type-validation copy findings
were also corrected to match the approved feature requirements.

Implementation used the authorized shared checkout,
using inline execution and one independent whole-change review. Existing database
race fixtures deliberately bypass only the new metadata coalescer to keep testing
database uniqueness independently. The CoverID redirect adapter and patched Next.js
release were necessary interoperability/security adjustments; both have bounded
tests and explicit evidence. Provider permissions, deployment acceptance and
multi-replica capacity were not cleared by this review.

## Operational and release limits

- Operators still supply a real monitored Open Library contact email, enable the
  optional adapter. The local database migration is applied; other installations
  must explicitly review/apply it. Existing public
  Books names or seed-ID collisions refuse upgrade rather than modifying them.
- Follow stop/build/prepare/migrate/start. Preparation provisions the new writable
  image-cache mount; recovery backups exclude disposable cached provider images.
  For removals/refresh, stop the web process and clear only public cache contents,
  preserving permissions. Browser copies can last until HTTP expiry.
- Open Library requests direct public cover URLs. This optimizer uses a local URL;
  reviewed guidance does not explicitly approve that proxy arrangement or grant
  unrestricted rights to contributed descriptions/artwork. See the
  [provider review](provider-terms-review.md#open-library-follow-up-2026-11-17).
- Provider agreement, content-rights, final artifact/security and deployment gates
  remain open under STATUS. The patched build is not a whole-project vulnerability
  audit. Old image-bound source-sidecar/native evidence does not automatically
  qualify this new candidate; update matching release artifacts before publishing.
- No background refresh/correction/takedown feature, ISBN edition tracking, bulk
  ingestion, album artwork provider or new activity/profile feature was added.

## Authorized local migration and commit follow-up

On 2026-11-17 (PST), the user explicitly authorized applying the migration and
committing this feature. The existing local `ratefreak-postgres-1` container was
started, with no other database clients present. Only AddBooks was pending and
there were no Books seed/name conflicts. A private PostgreSQL custom-format backup
was created and its archive directory validated before migration. The first EF
attempt stopped before SQL because local NuGet packages were missing; restore
completed, and the retry applied `20261118034056_AddBooks` successfully.

Verification confirmed 21 applied migrations, the expected public seeded Books
category, the expanded media-type constraint, unchanged counts/fingerprints for
all 26 other application tables, and unchanged original category rows. The only
row additions were one migration-history row and the Books seed. Private backup
and evidence are ignored under `.local/localhost/books-migration/`; no database
content or credentials are included in the commit. The database container remained
running for local development. At migration time, Open Library remained disabled
pending a monitored
operator contact email in `Providers:OpenLibrary:Contact`.

Fresh pre-commit verification passed 196 provider/cache contract tests and all
775 frontend tests across 102 files. The migration command rebuilt the API and
its dependencies successfully. The earlier whole-suite/build/browser evidence
above remains the feature validation record.

## Live local activation, 2026-11-18

Started the existing local API on localhost:5080 with settings loaded from the
ignored root `.env`, including the operator's enabled flag and contact email.
The local runner does not load `.env` itself; these settings must be passed in
its startup environment. No contact email or credentials are recorded here.

- Readiness and provider capabilities returned HTTP 200; Open Library was enabled.
- A real search for `Fantastic Mr Fox` returned HTTP 200 with 20 work results,
  including `OL45804W`, Roald Dahl, first publication year 1970 and CoverID 6498519.
- The real `OL45804W` preview returned HTTP 200 with description, enriched author
  name and cover reference. The work omitted publication year, correctly retained
  as unavailable rather than copied from search or fabricated.
- An immediate repeated preview returned identical data in 4 ms versus 1.126 s
  for the initial request. This timing is consistent with the metadata cache;
  upstream call counts were not instrumented in this check.

Sanitized local response/timing evidence is saved under
`.local/localhost/openlibrary-live/verification.json`. The API was left running.
These were read-only API checks: no catalog import, account change, browser
journey, image-byte retrieval or deployment acceptance was performed.

## Live local browser acceptance and cover correction, 2026-11-18

Used the real Open Library provider with the contact configured in `.env`, an
isolated API on localhost:5081, a development frontend on localhost:3001 and a
new disposable PostgreSQL database. Existing application migrations initialized
only that database. A temporary administrator initialized it; browser registration
created a separate ordinary reader account for the import checks.

- Anonymous preview offered login; importing with a valid anonymous CSRF token
  returned 401. Login returned the reader to the requested preview. Authenticated
  import without CSRF returned 400.
- Browser search for `Fantastic Mr Fox`, preview, CSRF-bearing import (201), and
  saved catalog details passed. The author, description, cover and Open Library
  source identity/attribution were retained. Repeating import returned 200 with
  `created=false` and the original catalog ID.
- A database query confirmed exactly one book, one `openlibrary/work/OL45804W`
  identity and zero personal-library entries. Import does not add to a library.
- The imported cover decoded successfully. Catalog screenshots/checks covered
  1440px and 390px in Light and Dark; discovery covered both widths. No horizontal
  overflow or browser runtime errors were observed.

The initial search exposed a real cover-adapter defect: newer covers redirect
through `archive.org/download/l_covers_0012/l_covers_0012_89.zip/0012895064-L.jpg`
and matching numbered Archive storage. The validator supported only the earlier
`olcovers…` layout, rejecting these valid redirects with 502. Three public redirect
chains were traced to successful image/jpeg responses before editing code.

The correction accepts this additional layout with bucket, bundle and ten-digit
filename derived from the requested CoverID. Existing hostname, protocol, DNS,
query, byte, time and capacity checks remain. Regression tests first reproduced
five failures, then all 34 cover tests passed, including mismatched ID/bucket,
unrelated hosts/paths and duplicate query rejection. All eight covers checked
through the optimizer returned HTTP 200 after the fix; Chromium decoded all 10
covers present in the search results. Results without cover references retain
their normal fallback.

Final verification: 794 frontend tests across 102 files, lint, typecheck and
production build passed. The first sandboxed build compiled but failed to parse
the TypeScript subprocess configuration; the complete build passed outside the
sandbox. No application dependency was added. The local migration tool required
restoring existing NuGet dependencies because generated assets referenced a
Windows cache path. Browser tooling/libraries were provisioned only under `/tmp`.

Sanitized response, database, redirect and image evidence plus screenshots are
saved in ignored `.local/localhost/openlibrary-browser/`. Temporary API/frontend
processes were stopped and the disposable database was removed. The existing
database was untouched. Browser acceptance used the development frontend; this
does not replace final production-image, HTTPS or self-hosted release acceptance.
