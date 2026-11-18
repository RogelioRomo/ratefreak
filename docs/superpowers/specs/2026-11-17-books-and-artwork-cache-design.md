# Open Library books and provider artwork caching

Date: 2026-11-17. Status: approved by the owner. Follow-up clarification extends
server-side metadata caching to all providers. Persistent-database migration and
publication remain separate operations.

## Intent and decisions from the conversation

Add Open Library as RateFreak's book provider. Search/preview/import should follow
the existing provider workflow. Imported metadata belongs in the instance's own
database, and existing catalog pages must remain usable without fetching that
metadata again from Open Library. The owner also requested image caching for every
provider to reduce repeated upstream loads and delegated the choice of image
component/cache implementation.

The proposals below make the remaining implementation choices explicit. They are
not earlier owner decisions. Existing application identity, UI layout, permissions,
and self-hosted architecture remain the baseline.

## Approaches considered

1. **Open Library API import and Next.js image optimization/cache (recommended).**
   Fits the provider interface and common artwork component. Uses the installed
   framework's image cache and resizing instead of implementing a separate image
   service. Requires strict remote patterns, persistent cache storage, and a
   documented cache-purge procedure. This is the proposed choice under the owner's
   delegated implementation discretion.
2. **Open Library API import and a custom artwork cache.** Could offer more precise
   refresh/invalidation and provider-specific policies, but adds fetching, locking,
   eviction, storage, and image-serving code that Next.js already supplies.
3. **Locally indexed Open Library dumps.** Appropriate for bulk catalog replication
   and independence from hosted search limits. Adds substantial indexing, storage,
   and update operations; unnecessary for user-triggered search/import initially.

## Books: catalog behavior

- Add the `book` media type and a seeded public Books category, alongside existing
  movies, TV, albums, and games. Include it in backend/frontend validation, filters,
  labels, icons, provider discovery, and every exhaustive media-type mapping.
- Use one catalog entry per Open Library **work**. Editions and ISBNs do not create
  separate ratings/reviews. This avoids duplicate entries for the same book in
  different bindings. Edition tracking, page counts, edition-specific publication
  dates, and ISBN-specific libraries are outside this change.
- Canonical provider identity: provider `openlibrary`, namespace `work`, external
  ID such as `OL45804W`; public link `https://openlibrary.org/works/OL45804W`.
  Accept only bounded canonical work identifiers; arbitrary URLs/paths cannot enter
  provider requests. Normalize search's work keys at the adapter boundary.
- Keep the current user-triggered discovery -> preview -> authenticated, CSRF
  protected import flow. Import fetches authoritative details server-side, rather
  than trusting search/client metadata. Existing/concurrent imports return the
  existing shared item using the unique provider identity.
- Import bounded title, author identities/names, first publication year when known,
  and a positive CoverID. Record normalized metadata and external provenance, not
  full upstream responses. Do not pretend a work has an edition's ISBN, page count,
  language, or publication date. A year belongs in metadata, without inventing
  January 1 as an exact release date. Omit unavailable optional fields honestly.
- Description text may be imported when supplied, bounded by the existing 20,000
  character rule and rendered as plain text. Retain source attribution; this design
  does not assert that contributed descriptions or covers are public domain.
- Author enrichment must use bounded requests under the same provider budget,
  cache resolved authors, and avoid fetching authors separately for every search
  result. The builder chooses the exact work/detail enrichment requests after
  validating official response shapes. Missing optional enrichment must not erase
  already usable work details; malformed identities remain errors.
- Imported books participate in existing libraries, tracking, ratings, reviews,
  collections, exports, reports, and moderation through the existing catalog model.
  Add Open Library to Credits and imported-book source links.

## Open Library transport and shared metadata caching

- Adapter is disabled by default, consistent with other optional providers. No API
  key is required. Enabled instances configure a valid operator contact email in
  an identifying `RateFreak` User-Agent. Document application registration guidance.
- Use explicit search fields and 20-result pages. Never fetch `fields=*`, crawl
  HTML, bulk-harvest through the API, or distribute requests to evade limits.
- Pace all actual metadata/author HTTP requests conservatively at one per second
  per API process, including retries and enrichment. The supported reference is
  single-replica; shared-egress/multi-replica operators need an aggregate budget.
- Bound outstanding work, request duration, response bytes, JSON depth, and mapped
  arrays/text. Disable automatic redirects. Honor provider throttling and
  Retry-After; preserve caller cancellation and existing sanitized failure codes.
- Add a bounded process-level cache for successful search and detail responses
  from **all providers** (TTLs: five minutes for searches, one hour for details), with
  concurrent identical requests coalesced. Failures are not cached as success.
  Every actual upstream request still passes through the gate. React Query keeps
  its existing UI JSON cache and query identity/invalidation behavior.
- Include provider, operation, type, canonical identity or normalized query/page
  in cache keys; never mix results across providers/types/pages. Disabled adapters
  must fail before a cache hit. Validate input before cache lookup, never cache
  failures or missing details, cap in-flight work and retained entry bytes, and
  let one caller cancel without cancelling other interested callers. Shared work
  is cancelled when no callers remain and has its own finite deadline.
- Database-backed catalog reads and repeat imports do not depend on upstream
  metadata availability. This does not add bulk ingestion or automatic catalog
  refresh/correction/takedown workflows.

## Provider artwork: shared server cache

- Route every supported **public provider image** through Next.js `<Image>` and
  the default image optimizer/cache. This covers current TMDB and IGDB images,
  Open Library CoverID images, and future providers when explicitly integrated.
  MusicBrainz currently supplies no artwork; do not introduce an unrelated album
  cover provider as part of this work.
- Preserve the common component's aspect ratio, object containment, accessible
  text, lazy loading, and unavailable-artwork fallback. Supply accurate responsive
  `sizes` and dimensions/fill positioning so lists avoid unnecessarily large files.
  Keep branding/static assets on their existing local path with normal static
  asset caching. Inline SVG illustration markup needs no image request cache.
- Keep original provider image references in catalog metadata. Open Library uses
  `https://covers.openlibrary.org/b/id/{positiveCoverId}-L.jpg?default=false` as
  the canonical source; CoverID avoids the documented ISBN lookup quota. The
  browser requests optimized same-origin variants rather than repeating the
  provider fetch for each viewer.
- Configure matching frontend URL validation and narrow `remotePatterns`: HTTPS,
  exact known hosts, canonical provider path prefixes, and exact permitted query
  strings. No user-supplied host, private IP, arbitrary local API route, SVG input,
  credential forwarding, or unreviewed redirect is allowed. Start with zero
  redirects; verify real provider cover requests, and only add narrowly validated
  handling if a required provider proves to redirect.
- Explicitly deny optimizer local-image sources except the dedicated CoverID
  route described below, rather than relying on the UI never passing private
  artwork. Verify direct requests to `/_next/image` reject local API/moderation
  paths. Check ingress treatment and resource limits for this newly used endpoint.
- Implementation evidence resolved the anticipated redirect case: Open Library
  covers redirect through `archive.org` and numbered Archive storage hosts. Use
  the dedicated `/provider-artwork/openlibrary/{CoverID}` route as the sole local
  optimizer source exception. It validates every HTTPS target against the same
  CoverID/archive path, pins publicly resolved addresses, strips caller headers,
  and bounds redirects/time/bytes. All private/API/moderation local sources remain
  refused. Original metadata retains the canonical Open Library cover URL.
- The cover source namespace is internal-only: bundled Caddy denies public
  `/provider-artwork/` requests before proxying, and alternate ingresses must do
  the same. Bound cover source work to eight active operations and 64 queued
  waiters per web process, with the ten-second deadline including queue time and
  503/Retry-After for excess work. Metadata admission counts cancelled operations
  through completed upstream cleanup, not just dictionary-visible flights.
- Verification surfaced the upstream SVG-validation advisory in Next.js 16.4.3.
  Upgrade Next.js and its matching ESLint configuration to patched 16.4.6 as a
  prerequisite for enabling the optimizer. Preserve version-bound license notices
  and repeat frontend/build/runtime verification against that patched candidate.
- Set a minimum public-image cache TTL of 24 hours and a maximum disk cache of
  1 GiB. Upstream Cache-Control can increase Next.js's actual TTL. Bound upstream
  response sizes using the installed version's supported image configuration.
  Keep image widths/qualities finite and use the default permitted quality unless
  verification shows a concrete need for more variants.
- Next.js caches per source/width/quality/output format: different variants can
  cause separate initial upstream requests. This is a reusable cache, not a
  permanent artwork mirror or an unlimited offline guarantee. Cold cache misses
  still require a working provider. Verify retained-cache behavior during upstream
  failure, including expired entries, before documenting any stale fallback claim.
- Persist the image-cache directory across web container recreation; replace the
  current ephemeral cache arrangement with an explicitly provisioned writable
  cache mount owned by UID/GID 10001. Keep other web storage read-only. Cached
  files are disposable and excluded from account/database/artwork recovery backups.
  Document capacity, clearing/recreation, refresh limits, and cold-start behavior.
- Next.js has no general per-image invalidation API. Document stopping the web
  process and clearing/recreating the public image cache for removals or refresh;
  retain the source reference so operators can identify the upstream origin.
  Browser copies may remain until their HTTP cache expires. Never describe this
  technical cache as a perpetual redistribution license.

## Uploaded and private artwork

Uploaded custom artwork is already stored locally; it has no remote provider to
re-fetch. Keep those images on their existing authenticated/visibility-checked API
route and direct `<img>` rendering. Preserve `no-store` and do not submit these
URLs to Next.js's public optimizer/cache, including moderation report artwork.
An item can become private, hidden, deleted, or unavailable to a viewer; a shared
public image cache would bypass those transitions. The existing disk-backed asset
is the local source for these requests. Static branding uses ordinary asset
caching. This implements provider-independent caching without making private
uploads public or weakening erasure/access controls.

## Schema and operations

- Add an EF migration widening the existing media-type check and inserting the
  stable Books category. Use the existing seeded-category conventions and handle
  a preexisting public category named Books without discarding user items or
  silently violating uniqueness. The builder must inspect collision behavior and
  document a safe deterministic resolution or refusal before applying anything.
- Preserve all existing rows, roles, category memberships, artwork, and erasure
  records. Test upgrades on disposable databases. A down migration must refuse to
  remove support while book rows depend on it, rather than deleting content.
- Prepare/review the migration without applying it to the persistent project DB.
  Existing local migration authorizations in STATUS concern earlier migrations.
  Any persistent-database application needs its own concrete reviewed action.
- Update setup/configuration examples, container storage preparation and tests,
  provider/release evidence, frontend documentation, and STATUS. New source changes
  invalidate prior candidate/image fingerprints; do not reuse older release proof
  as verification of the new binary. No publication/deployment is included.

## Verification required

- Provider contracts: response mapping, work-only identities, plain-text bounds,
  optional author/year/cover fields, malformed/oversized responses, redirects,
  status handling, Retry-After, timeout/cancellation, pacing, cache/coalescing,
  disabled/configuration behavior, and no requests for unsupported identities.
- PostgreSQL integration: migration/category collision behavior and preservation,
  book construction/filtering, idempotent/concurrent imports, permissions/CSRF,
  own-database catalog reads with upstream unavailable, library/collection/report
  integration, and model/migration consistency.
- Frontend: Book and Open Library options, search/preview/import, source credits,
  type mappings, private-session isolation, approved/rejected artwork URLs,
  correct optimizer versus protected-upload rendering, and fallback/accessibility.
- Production runtime: actual optimized-image MISS -> HIT without a second upstream
  fetch for the same variant, simultaneous requests, cache persistence after
  container recreation, configured disk bounds, blocked arbitrary/private/local
  sources and redirects, and upstream-outage behavior. Test private artwork access
  after logout/unpublish/removal to prove it never enters the shared image cache.
- Run relevant frontend/backend/operator suites, lint/typecheck, Release/frontend
  builds, and bounded browser journeys in the existing themes and mobile/desktop
  layouts. Do not alter persistent local account/catalog data for verification.

## Primary sources and limits

Reviewed 2026-11-17:

- [Open Library API usage](https://openlibrary.org/developers/api): caching encouraged;
  identified requests up to three/second; human-triggered usage and bulk restrictions.
- [Search API](https://openlibrary.org/dev/docs/api/search): work/edition distinctions,
  explicit fields and pagination.
- [Covers API](https://openlibrary.org/dev/docs/api/covers): public display supported,
  direct source URLs requested by the guidelines; non-CoverID/OLID access is limited
  to 100/IP/five minutes; bulk cover crawling prohibited.
- [Licensing](https://openlibrary.org/developers/licensing): Internet Archive asserts
  no new proprietary rights; preexisting contribution rights can still apply.
- [Data dumps](https://openlibrary.org/developers/dumps): monthly bulk-access route.
- Installed Next.js documentation:
  `web/node_modules/next/dist/docs/01-app/03-api-reference/02-components/image.md`
  describes image caching, strict remote patterns, redirect behavior, disk limits,
  header isolation, TTLs, and invalidation limitations.

Using a local optimizer changes the public HTML image URL from the direct-origin
pattern requested by Open Library's cover guidelines. The documents reviewed do
not explicitly approve this proxy arrangement; metadata caching guidance must not
be presented as cover-specific permission. Record this distinction in provider
evidence and operator guidance. This design requests approval of the technical
approach, not acceptance of provider agreements or a claim of unrestricted rights.
