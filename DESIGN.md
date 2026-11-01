# RateFreak visual direction

Direction selected and homepage approved by the user on 2026-10-31: editorial
culture journal. The user then requested the existing account pages adopt this
design and asked for a dark app theme. Build path: approved code-first/native.
The approved catalog browsing and details slice was implemented on 2026-10-31.

## Homepage

Mode: Persuade. A visitor should understand that RateFreak brings their media
interests together, see the four supported formats and find an account entry point.

Replace the charcoal/lime slogan-and-columns composition with warm ivory,
near-black ink, oxblood accents, distinctive editorial serif display typography
and a restrained sans-serif for reading and controls. Use a wide two-column lead:
direct headline and account action at left, original media SVG artwork at right.
On mobile, keep the headline/action first and then the artwork. Follow with a
quiet, asymmetric introduction to planned library, rating and collection flows.

The artwork is illustrative, not catalog data or a product screenshot. Do not
invent members, quotes, ratings, media counts or available business interfaces.
Preserve working Home and account navigation, authentication behavior, skip
navigation, semantic landmarks, labeled controls and keyboard focus.

Apply the editorial design system to every existing page and shared shell. The
catalog inherits it in the implemented browsing and details slice.

## Account pages

Mode: Operate. Login, registration and account share the editorial wordmark,
Fraunces headings, DM Sans text and the app-wide palette. Desktop uses a quiet
heading/description column beside the form or account details; mobile stacks
these in reading order. Keep input text at 16px or larger, visible labels,
password guidance, inline errors and session/loading/retry states.

Account information uses simple labeled rows. Keep the existing auth APIs,
password handling, validation, redirects, logout/session lifecycle and privacy
behavior. Do not decorate forms with a duplicate hero or collage.

## Catalog

Mode: Operate. Browse, search and details extend the existing editorial system:
Fraunces titles, DM Sans controls and text, semantic theme colors and Tabler icons.
Use native labeled search/select controls with explicit submission and URL-owned
filters; keep results as a responsive artwork-led semantic list with title links,
type/year metadata and Previous/Next navigation. Details place artwork beside the
title and description on desktop and stack on mobile.

Provider artwork is optional: preserve its proportions and show a quiet, labeled
fallback for missing, unsupported or failed images. Loading and recovery use the
shared feedback components and clear text. Preserve wrapping titles, visible focus
and usable controls at 320px. Surface-specific guidance and evidence are recorded
in [.impeccable/surfaces/catalog.md](.impeccable/surfaces/catalog.md).

## Provider discovery and import

Mode: Operate. Discovery and provider preview extend the catalog's Fraunces/DM
Sans typography, flat semantic surfaces, dark blue/light oxblood accents and
Tabler icons. Use explicit, labeled title/provider/type search with submitted
filters in the URL; present only known enabled providers and supported types as
usable choices. Keep provider attribution readable beside the relevant content.

Reuse catalog artwork and its labeled fallback, wrapping title links and simple
preview metadata. Separate public preview from the explicit authenticated Import
to catalog action. Show session checks, login continuation, pending, created,
already-present and recoverable error states honestly; import is a shared catalog
action, not a personal library action. No new visual primitives are needed.
Surface guidance and evidence are in
[.impeccable/surfaces/providers.md](.impeccable/surfaces/providers.md).

## Personal library, ratings and reviews

Mode: Operate. The private library and title activity reuse Fraunces headings,
DM Sans text, flat divided sections, semantic theme colors and Tabler icons.
Use labeled Status/Media type filters with explicit submission and URL-owned
pagination. Library entries are artwork-led rows with wrapping title links,
readable status and saved dates; reuse the catalog artwork fallback.

Tracking, rating and public review editing remain separate native forms. Keep
optional date inputs, a labeled score select including zero, a plain textarea and
spoiler checkbox. Explain public visibility before publishing and use inline
confirmation for library removal and review deletion. Render public reviews as
literal text with spoilers initially concealed behind explicit reveal controls.

Keep session checks distinct from empty or anonymous states. Conceal private
controls during checks while retaining same-account drafts; disable interaction
and private reads, cancel pending writes, and discard drafts on confirmed logout
or account changes. Review pagination, failed writes and unrelated saves preserve
drafts. No new visual primitives are needed. Surface guidance and evidence are in
[.impeccable/surfaces/personal.md](.impeccable/surfaces/personal.md).

## Light, dark and system

Dark is the first-visit default, explicitly requested by the user. Saved Light,
Dark or System preferences continue to take precedence.
Warm paper `#f3efe6`, ink `#27251f` and oxblood `#812d35` carry the light theme.
Dark uses warm ink `#1c1b19`, cream `#f0e9dc` and blue `#91b9ee`; it preserves
typography and layout. The geometric SVG artwork uses semantic theme colors.
Semantic tokens own backgrounds, surfaces, text, accent/on-accent, input borders
and error colors. Error text stays readable in both themes.

A header group of sun, moon and monitor icon buttons offers Light, Dark and
System. Use Tabler icons, accessible names, native title tooltips, aria-pressed
and 44px touch targets. Browser storage remembers only `ratefreak.theme`. System follows device
changes; explicit Light/Dark overrides them. Other tabs receive updates. A static
first-party head script applies the saved choice before first paint; keep the
pages statically renderable without server cookies. If storage is unavailable,
switching still works for the current document. Theme switching does not remount
the page, discard form input or affect authentication/query state.

## Direction contract

Seed run: `7be71344` on 2026-10-31. The user's explicit editorial culture journal
selection overrides the random assignment and its unrelated challenger worlds.
No comp was approved: the user explicitly selected code-first execution.

THESIS: RateFreak is a personal home for cultural interests across media formats.
OWN-WORLD: An independent culture journal, with paper, ink, serif headlines and
original geometric SVG artwork; no fabricated catalog or community activity.
STORY: Understand the idea, create an account, then see what is being built.
FIRST VIEWPORT: Clear headline and account action alongside four-format SVG artwork.
FORM: A responsive editorial lead followed by an asymmetric introduction and
definition-list feature descriptions. Native links and document scrolling.

- Warm paper and ink establish a recognizably different first impression.
- Editorial serif headline and genuinely visual media content carry the first view.
- Deep red marks the primary action, with readable white text and visible focus.
- Rhythm comes from varied layout, image scale and whitespace, not repeated cards.
- Planned functionality is explicitly identified. All actionable links resolve.
- Artwork stays secondary to readable content at 320px; no horizontal overflow.
- Static-first presentation; no required motion, animation dependency or scroll trap.

## Implementation

CSS Modules, self-hosted fonts and local optimized artwork. Keep homepage parts
focused and props in separate interfaces where needed. Verify with the full
frontend suite, lint, typecheck, build, keyboard checks and desktop/mobile browser
captures. No changes to backend contracts, session lifecycle or proxy boundaries.
