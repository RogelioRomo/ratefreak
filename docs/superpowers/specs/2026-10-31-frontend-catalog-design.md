# Frontend Phase 3: catalog browsing and details

Status: approved and implemented on 2026-10-31 after the requested homepage
redesign and app-theme refinements. The homepage's selected editorial visual direction
in `DESIGN.md` supersedes the charcoal/lime proposal below for subsequent UI work.

## Intent and scope

Let visitors browse the local catalog, search titles, filter by media type and
open a detail page without signing in. Deliver the next slice identified in
`docs/frontend-checklist.md`. Provider preview/import remains a later slice.

## Approach

Extend the existing browser client, restricted proxy and TanStack Query setup.
Keep Next.js App Router, TypeScript, CSS Modules and separate `interfaces/` files.
This follows the approved feature-by-feature approach. A mocked catalog would
delay proving the API flow; SSR query hydration adds a second caching lifecycle
that this read-only slice does not need.

## Screens and interaction

- `/catalog`: title, short explanation that this is the local catalog, labeled
  title search, media-type select, result count, results and Previous/Next links.
  Types are All types, Movies, TV shows, Albums and Video games.
- Search uses explicit form submission. `q`, `type` and `page` live in the URL;
  submitting new filters resets page to 1. Page size is fixed at 20. Back/Forward
  restores the submitted filters and results. No request on every keystroke.
- `/catalog/[id]`: title, media type, available release date, description and
  available artwork, plus a Catalog link. Missing descriptions/dates/artwork have
  honest fallbacks. A missing record has a distinct not-found state.
- Add a working Catalog navigation link and homepage entry point. Active catalog
  navigation includes detail routes. Do not add library/rating/import controls.

## Visual direction and accessibility

Builder proposal: retain the charcoal surfaces, lime accent and existing type
and focus styles. Use a compact filter area and responsive artwork-led result
grid with readable titles and type/year metadata. Results are semantic lists
with descriptive title links. Missing artwork uses a quiet labeled fallback;
never generate cover art that could misrepresent an actual work.

Use one main heading per screen, visible input labels, native controls, visible
keyboard focus, wrapping titles and non-color status cues. Show loading, retryable
failure, rate-limit guidance, empty catalog and no-match states. Do not show old
results under newly submitted filters. Verify 320, 768, 1024 and 1440px widths.
Do not add decorative animation or dependencies.

## Transport and data

Enable only GET `/media` and GET `/media/{guid}` in the proxy, preserving existing
account routes. Only the list accepts query parameters: `q`, `type`, `page` and
`pageSize`. Reject unknown/duplicate parameters and invalid values locally;
reject query parameters on other routes. Trim search; allow at most 200
characters without nulls. Types match the backend's four exact identifiers.
Page is 1–10000; pageSize is 1–100. Forward validated, re-encoded parameters to
the fixed upstream origin. Continue no-store, cookie handling, attribution,
timeouts, cancellation and sanitized transport errors.

Use the existing MediaSummary, CatalogPage and MediaDetails JSON contracts.
Query keys include normalized search, type, page and page size; detail keys
include the canonical ID. Pass query cancellation signals to the API client.
Invalid URL filters show a recoverable validation state without sending a query.

Treat imageReference as untrusted optional data. Before selecting rendering,
inspect actual provider reference formats; render only explicitly supported
HTTPS artwork hosts/formats, otherwise use the fallback. Never create a generic
image proxy or render arbitrary metadata as HTML. This slice does not need a
generic metadata viewer or external identity links.

## Verification and limits

Test query validation/forwarding, unchanged account restrictions, API contracts,
filter/pagination/cache behavior and visible loading/error/empty/detail states.
Run all frontend tests, lint, typecheck and production build. Inspect browser
layouts, keyboard flow and console output. Use disposable fixtures for a live
API check when available; report fixture-only verification explicitly.

No backend business changes, migrations, provider activation, deployment,
automatic commits or pushes. Record exact evidence and remaining work in the
frontend checklist.
