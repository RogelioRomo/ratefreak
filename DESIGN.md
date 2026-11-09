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
Use labeled search inputs and shared custom select controls with explicit submission and URL-owned
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
optional date inputs, the half-star rating control including zero, a plain textarea and
spoiler checkbox. Explain public visibility before publishing and use inline
confirmation for library removal and review deletion. Render public reviews as
literal text with spoilers initially concealed behind explicit reveal controls.

Keep initial/failed session checks distinct from empty or anonymous states. Routine
background checks retain the last verified identity, visible controls and drafts.
Unknown/failed sessions and explicit account transitions conceal/disable private
controls and pause private work. Confirmed logout/account changes discard old drafts. Review pagination, failed writes and unrelated saves preserve
drafts. No new visual primitives are needed. Surface guidance and evidence are in
[.impeccable/surfaces/personal.md](.impeccable/surfaces/personal.md).

## Collections

Mode: Operate. Collections are lists of existing catalog identities, following
[ADR 0016](docs/adr/0016-custom-catalog-and-list-collections.md). Dynamic field
builders, copied entry titles and custom value editors are retired.

Keep Fraunces headings, DM Sans text, flat divided sections, semantic theme tokens,
wrapping title links, owner/visibility metadata and URL-owned scope/pagination.
Collection creation/editing retains the existing metadata forms and responsive grid.
Details show Items, with explicit owner actions to add, reorder and remove catalog
memberships. Only one inline editor is active at a time. Keep 1.5rem below the
Edit metadata/Add item row before the next divider. Removing a membership preserves
the catalog item and its ratings/reviews. Unavailable memberships have a separately
confirmed removal action without exposing private item details.

Conflicts preserve drafts and require deliberate refresh; uncertain write outcomes
block automatic retries. Routine background session checks preserve visible UI and
same-account drafts. Initial identity discovery, explicit transitions and failed
checks still gate private content; confirmed identity changes discard old drafts.
Surface guidance is in [.impeccable/surfaces/collections.md](.impeccable/surfaces/collections.md).

## Light, dark and system

Dark is the first-visit default, explicitly requested by the user. Saved Light,
Dark or System preferences continue to take precedence.
Warm paper `#f3efe6`, ink `#27251f` and oxblood `#812d35` carry the light theme.
Dark uses warm ink `#1c1b19`, cream `#f0e9dc` and blue `#91b9ee`; it preserves
typography and layout. The geometric SVG artwork uses semantic theme colors.
Semantic tokens own backgrounds, surfaces, text, accent/on-accent, input borders
and error colors. Error text stays readable in both themes.

Dropdowns use `components/ui/select.tsx` throughout catalog, provider, personal
media and collection forms. The custom trigger and options use the existing input
surface, border and accent tokens, Tabler chevron/check icons, wrapping labels and
a scrollable panel positioned within the viewport. Radix Select supplies keyboard
navigation, typeahead and focus handling. A hidden form control preserves exact
values, required validation and form submission. Options remain inside their
form's hidden/inert boundary and close when the form becomes unavailable.

At mobile widths up to 36rem, the header uses a hamburger button to open a
full-screen navigation dialog that slides in from the right. Keep desktop
navigation inline. The dialog supports Escape, a close button, trapped focus,
scroll locking and closing on link selection; reduced motion removes the slide.

A footer group of sun, moon and monitor icon buttons offers Light, Dark and
System. Use Tabler icons, accessible names, native title tooltips, aria-pressed
and compact 32px buttons with 16px icons in the shared footer. Browser storage remembers only `ratefreak.theme`. System follows device
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

## Public profiles, connections and review discussions

Profiles and discussions extend the existing Fraunces/DM Sans editorial system,
flat semantic lists and shared light/dark tokens. Profile text and review/comment
bodies wrap long content, preserve newlines and render literally. Connection lists
show server counts and profile links. Follow/Unfollow are explicit setters because
the current API does not expose point relationship status.

Review cards link to a dedicated discussion instead of eagerly loading threads.
Review and comment spoiler reveals are independent. Discussion forms use labeled
16px textarea inputs, public-visibility guidance, field-associated errors and
visible focus. Author deletion confirmation names the action without exposing
concealed spoiler text. Mobile stacks actions and preserves reading order.
Retain drafts during session checks and recoverable background read failures;
conceal/inert unavailable content. Unknown writes require deliberate recovery;
confirmed write feedback remains independent from read refresh.

## Half-star rating input

Personal rating forms use five filled star silhouettes with separate left/right
half-star hit areas, selecting0.5 through5 stars. Semantic accent fills selected
stars; input-border color marks the remainder. Hover previews the score and resets
on leaving; touch commits directly. Native named radio inputs support arrow-key
navigation, required selection and screen-reader values. Visible numeric feedback
distinguishes Not rated, zero and half-star values. Keep the explicit Save rating
and Remove rating actions and a separate0-star choice for backend compatibility.
Half-star targets are24×48px with visible keyboard focus in both themes.

## Title activity layout

Above640px, Community rating and Your library share the first row; Your rating and
Your review share the second row in two equal columns. At640px and narrower,
stack all four in that reading order. Public community reviews stay full width
below. Retained private wrappers participate in the grid with display:contents
only while visible; hidden/inert wrappers and disabled fieldsets retain their
session protections and draft-preservation behavior.

Community rating retains its exact two-decimal average and rating count, and adds
a read-only five-star display. Round the visual average to the nearest0.5 (ties
up):4.7→4.5,4.8→5. Empty ratings retain No ratings yet, distinct from a real0 average.
