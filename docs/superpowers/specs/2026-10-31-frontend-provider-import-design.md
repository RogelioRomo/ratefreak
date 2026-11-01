# Frontend catalog provider search, preview and import

Status: approved by the user, 2026-10-31. Fresh independent builder check:
“nothing important left.” Continues the approved frontend
roadmap after completed local catalog browsing/details. Preserve code-first
execution, existing feature checkout and no automatic commits/pushes/deployment.

## Intent and scope

Let visitors find a provider title, inspect authoritative details, then explicitly
import it into the shared local catalog when signed in. Import does not add a
title to a personal library. Reuse the existing .NET endpoints and account session,
CSRF client, TanStack Query, separate interfaces and editorial design system.
No backend changes, migrations, provider activation or new dependencies.

## Approach

Recommended builder choice: dedicated `/catalog/discover` search and
`/catalog/discover/[provider]/[type]/[externalId]` preview routes. This keeps local
catalog search separate from remote provider requests, permits shareable previews,
and lets Back/Forward preserve the search. Add Find titles links to the catalog,
including its empty/no-match states. Use the established dark/blue default,
Light theme, Fraunces/DM Sans, semantic tokens and Tabler icons.

Alternatives considered: combine local and remote search in one screen (adds
ambiguity about which titles exist locally and sends more provider requests), or
use a preview modal (less useful deep links/history and additional focus state).
Neither is needed for the existing route-based architecture.

## Discovery

Fetch GET media/providers; only known providers and their supported media types
are selectable. Show readable provider names and their enabled/unavailable status.
Disabled providers do not issue search requests. If none is enabled, show a clear
unavailable state and a working link back to the local catalog. Do not imply the
visitor can enable a provider or enter credentials in the browser.

Use an explicit labeled title search, provider select and supported media-type
select. All provider/type/q/page values for submitted searches live in the URL;
new submitted filters reset page to1. Initial visits show the form and guidance,
without making external search requests. Builder default: first known enabled
provider in TMDB, MusicBrainz, IGDB order, first supported known type; preserve a
valid submitted URL selection. Provider changes update the draft type options
and select the first supported type if necessary, without searching until submit.

Search q is trimmed1–200 UTF-16 code units, no control characters, and page1–500.
Reject duplicate/unknown URL filters, invalid combinations/IDs, and disabled
providers with recoverable states and no external request. Back/Forward restores
submitted controls and matching results; late requests cannot replace new results.

Search responses contain provider, page and items, with no total or hasNext.
Display actual returned-item count and current page, never invented totals.
Previous is available above page1; Next is available when items are nonempty and
page<500, labeled as navigation rather than a guaranteed further page. An empty
later page offers Previous and first-page recovery with filters preserved.
Search results link to preview and use the existing safe artwork/fallback.

## Preview and import

Preview fetches GET media/external/provider/type/externalId. Show title, format,
available date/description/artwork and provider attribution. Keep metadata plain
and omit generic metadata viewers. Use honest missing-field fallbacks. Missing
records, disabled/unavailable provider,429/Retry-After and502/503/504 have distinct,
recoverable messages; do not display failure as zero results.

Visitors can preview without signing in. Signed-out visitors see Log in to
import; its login URL carries a returnTo pointing to this exact preview. Extend
login to accept only a canonical internally generated provider-preview return
path matching known provider/type/ID rules (never external URLs, protocol-relative
paths or arbitrary destinations). Otherwise retain existing `/account` destination.
This continuation is limited to login; registration keeps its existing flow.
An already-signed-in login visit with a valid continuation links to that preview.

Show session loading/error separately and hide the import action during session
revalidation/identity transition. Authenticated Import to catalog sends only
provider, externalId and mediaType through the existing CSRF mutation client.
Disable repeated submission while pending; do not optimistically claim success,
automatically retry mutations, or send preview metadata/title/artwork to import.

Success201 (created) or200 (already present) displays the respective clear status
and a link to `/catalog/{mediaId}`; remain on preview so the outcome is readable.
Invalidate local catalog lists and the returned detail key. If the session expires,
refresh session and offer login continuation; no automatic replay after login.
Import errors retain the preview and permit explicit retry. Bound the mutation to
the initiating identity and preview; cancel/ignore stale outcomes on identity or
route changes so they cannot reappear for another user/title. No library write.

## Attribution and artwork

Show backend-supplied attribution text for the selected known provider on search
and preview. Links use a hardcoded provider homepage allowlist matching inspected
capabilities (TMDB, MusicBrainz, IGDB), never arbitrary URLs from metadata. TMDB
attribution must preserve its non-endorsement sentence. Do not add unsupported
licensing claims. Continue accepting only canonical HTTPS TMDB/IGDB artwork formats
already inspected; MusicBrainz has no supplied cover, so use the fallback.

## Restricted proxy

Add only GET media/providers, GET media/external/search, GET
media/external/{provider}/{type}/{externalId}, POST media/import. Match fixed
segments before GUID detail. Provider/type pairs and IDs follow
MediaProviderInput in `src/RateFreak.Application/MetadataImport/MediaProviders.cs`:
TMDB movie/tv_show and IGDB video_game use positive Int32 decimal IDs (max10
digits, canonicalized); MusicBrainz album uses a nonempty D-format GUID lowercase.
Keep an exact incoming-path check and canonical forwarding.

Only external search accepts q/type/page/provider query parameters; duplicates,
unknowns, controls, empty/overlong q, invalid pages and pairs return controlled400.
Capabilities, previews and import reject all query parameters. Preserve the fixed
upstream origin, account routes, current catalog validation, no-store, session
cookie selection, timeout/cancellation, attribution headers and sanitized errors.
POST import reuses exact configured Origin, JSON/body limits and CSRF protections.
Provider credentials remain in .NET server configuration.

## Verification and completion

Use test-first proxy route/query and domain-input boundary cases; API tests through
the real client; real QueryClient DOM tests with mocked network for capability
states, URL restoration/late requests, drafts/type options, search pagination,
preview fallbacks/404/errors/attribution, login continuation rejection, session
changes, pending duplicate prevention, success201/200/cache invalidation and
retry without automatic replay. Preserve existing auth/catalog tests.

Run full test suite, lint, typecheck, production build and diff check. Inspect both
themes at320/768/1024/1440px, keyboard/focus/history and console. Perform read-only
live capabilities/search/preview checks against the configured local API when
available; use synthetic fixtures for import success/error/session edge cases.
Do not perform a real authenticated import merely to test the UI: that writes the
shared catalog. No need to obtain/store user credentials. Record fixture versus
live evidence, fresh finish review, exact checks, process state and the next slice
(personal library/ratings/reviews) in docs/frontend-checklist.md.

## Builder-owned details

Choose focused component boundaries, CSS spacing and test helpers within current
conventions. Choose exact concise recovery copy consistent with these semantics.
Keep every new object contract/props definition in separate interfaces files.
No redesign interview, raster generation, generic proxy, external sign-in or
unrelated account refactor is part of this slice.
