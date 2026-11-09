# Catalog surface

Implemented 2026-10-31; category controls updated 2026-11-09. Mode: Operate. Extend the approved editorial culture
journal system in `DESIGN.md`; reuse shared shell, buttons, feedback, semantic
theme tokens and Tabler icons. This surface introduces no new visual primitives.

## Browse and filter

`/catalog` pairs a Fraunces heading with a short explanation, then a native labeled
title search, category dropdown and explicit Search catalog button. The internal
item-type selector is no longer shown on `/catalog`. Submitted `q`, `categoryId`
and `page` belong in the URL; legacy type query parameters remain supported.
The links below the heading use a wrapping row with 1.5rem horizontal gaps. New filters reset pagination; browser
Back/Forward restores submitted controls and results. Do not query per keystroke
or retain old results beneath newly submitted filters.

The flat artwork-led semantic list uses four columns above 64rem, three at 64rem,
two at 48rem and one at 25rem. Titles wrap beneath artwork, with muted type/year
metadata. Count and page information precede results; Previous/Next links follow.
The filter form moves from three columns to two at 48rem and one at 28rem.

## Artwork and details

Artwork occupies a 3:4 surface with contained images, preserving provider image
proportions. Accept only the implemented canonical HTTPS TMDB w500 and IGDB
cover-big formats. Missing, unsupported and failed references show the media-type
Tabler icon plus “Artwork unavailable”; never fabricate a cover.

`/catalog/[id]` begins with Back to catalog. Desktop places artwork (up to 20rem)
beside media type, wrapping Fraunces title, date and a divided description section.
At 40rem it stacks, retaining the artwork width cap. Missing dates and descriptions
have explicit text fallbacks. Description remains plain text.

## Loading and recovery

Reuse shared loading/error components. Keep invalid filters with Reset filters,
empty catalog, no matches, out-of-range page, retryable failure, rate limit and
missing detail states distinct. Offer the relevant reset, first-page, browse or
retry action. Maintain visible labels, 16px input text, visible focus, descriptive
title links and usable touch targets in both themes. Add no decorative motion.

## Evidence and limits

Compared the implemented catalog components/CSS and `web/app/globals.css` with
the approved spec and implementation plan, plus the stored dark 320px list and
light 1440px detail captures. They preserve the incumbent Fraunces/DM Sans pairing,
flat semantic surfaces, dark blue/light oxblood accents and Tabler icon language.
The finish review reports SHIP with no material findings; the single-column
missing-artwork list is scroll-heavy at 320px, a minor observation rather than a
new layout requirement.

Evidence: `.impeccable/review/catalog/report.json` and adjacent list/detail captures
at 320/768/1024/1440px in both themes, plus recovery-state captures. The report
records keyboard/history checks and no runtime errors. It used synthetic fixtures
through the production Next proxy; the .NET API was unavailable, so this does not
establish live catalog data or provider artwork availability.

Approved scope: `docs/superpowers/specs/2026-10-31-frontend-catalog-design.md`;
implementation: `docs/superpowers/plans/2026-10-31-frontend-catalog.md`. The spec's
old charcoal/lime proposal is explicitly superseded by its status note and the
incumbent editorial system.
