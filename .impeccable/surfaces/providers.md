# Provider discovery and import surface

Implemented 2026-10-31. Mode: Operate. Extend the approved editorial culture
journal system in `DESIGN.md`, reusing shared shell, buttons, feedback, catalog
artwork, semantic theme tokens and Tabler icons. Preserve Fraunces headings and
DM Sans text, dark as the default with blue accent, and light oxblood accents.
This ordinary extension introduces no new visual primitives.

## Discovery

`/catalog/discover` starts with Back to catalog, a heading and a short explanation.
Use visible Title, Provider and Media type labels and an explicit Search providers
button. The initial screen offers guidance without an external search. Submitted
`provider`, `type`, `q` and `page` live in the URL; new submissions reset the page,
and Back/Forward restores controls and matching results. Changing draft provider
updates supported type choices without submitting. Show known unavailable providers
as disabled choices; only enabled providers and their known supported types can
be searched. Offer catalog/reset recovery if none is available.

The flat artwork-led semantic list has wrapping title links and muted type/year
metadata. Show the returned count for this page and the current page, never an
invented total. Previous/Next navigation follows results; Next offers another page
without promising results. Empty later pages offer first-page recovery.

Desktop places the title field, two selects and button in one row. The title field
spans the row at 64rem, the button spans at 48rem, and controls stack at 28rem.
Results use four columns above 64rem, three at 64rem, two at 48rem and one at
28rem. Preserve 16px field text, visible focus and usable targets at 320px.

## Attribution, artwork and preview

Show backend-supplied attribution for the selected provider on search and preview,
including TMDB's non-endorsement sentence. Provider links use the implemented
homepage allowlist; metadata must not supply arbitrary attribution destinations.

Reuse the catalog's contained 3:4 artwork surface, canonical HTTPS TMDB/IGDB image
formats and media-type icon with “Artwork unavailable” for missing, unsupported or
failed references. MusicBrainz has no supplied cover. Never fabricate artwork.

`/catalog/discover/[provider]/[type]/[externalId]` offers Back to discovery and a
public preview. Desktop places artwork capped at 20rem beside the wrapping title,
format/provider, available date and divided description section; stack at 40rem.
Keep descriptions plain text and missing date/description fallbacks explicit.

## Session, import and recovery

Keep checking-session and retry-session states distinct from provider loading.
Anonymous visitors see Log in to import with a continuation to this exact preview.
Authenticated visitors explicitly choose Import to catalog; explain that the shared
catalog and personal library are separate. Disable repeat submission while pending.
Remain on preview after success with “Imported to the catalog.” or “Already in the
catalog.” and a View in catalog link. Preserve preview on failure and offer explicit
retry or login recovery. Never show optimistic success or replay import after login;
discard outcomes belonging to a previous account or preview.

Reuse shared feedback for invalid discovery, disabled providers, missing preview,
rate limits and provider outages. Keep these distinct from an empty result page.
Offer the relevant reset, browse, first-page or retry action. Add no decorative
motion or credential-entry interface.

## Evidence and limits

Compared provider components/CSS and `web/app/globals.css` with the approved spec,
implementation plan, `.impeccable/review/providers/report.json` and all stored
captures via `/tmp/provider-contact-0.png` through `-5.png`. Both themes at
320/768/1024/1440px preserve the incumbent type, palette, flat layout and icon
language; state captures cover initial, empty, unavailable/error and import
created/already-present/error outcomes. The fresh combined finish review reports
SHIP with no material findings. PRODUCT.md now records discovery, preview and
explicit authenticated catalog import as current capabilities.

The browser report records keyboard/history/login-continuation checks and no
runtime errors. Production browser responses and all three import calls were
synthetic CDP fixtures. Separate live discovery/preview reads through the dev proxy
succeeded for TMDB, IGDB and MusicBrainz; no real authenticated import was performed.
Fixture artwork fallbacks do not establish live artwork availability.

Approved scope: `docs/superpowers/specs/2026-10-31-frontend-provider-import-design.md`.
Implementation: `docs/superpowers/plans/2026-10-31-frontend-provider-import.md`.
