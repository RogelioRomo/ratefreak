# Personal library, ratings and reviews surface

Implemented 2026-10-31. Mode: Operate. Extend the approved editorial culture
journal system in `DESIGN.md`: Fraunces headings, DM Sans text, flat divided
sections, default Dark with blue accent, Light with oxblood accent and Tabler
icons. Reuse shell, buttons, feedback, artwork and semantic tokens; this ordinary
extension introduces no new visual primitives.

## Library

`/library` pairs one editorial heading with a short explanation. Visible Status
and Media type labels accompany native selects and an explicit Apply filters
button. Submitted status/type/page belong to the URL; new filters reset page one
and Back/Forward restores matching controls and results. Invalid filters offer
reset without querying. Use the five readable tracking statuses and four existing
media types, with all-status/all-type choices.

Use a semantic list of flat artwork-led rows, wrapping title links, muted
type/status and optional saved dates. Reuse catalog artwork and its labeled
fallback. Each title leads to its activity editor. Show the returned total and
current page with Previous/Next navigation, using 20 entries per page. Keep empty
library, no matches and empty later pages distinct, with catalog, clear-filter
or first-page recovery as appropriate.

Desktop uses two filter columns and the action; filters stack at 32rem. The heading
stacks at 48rem. Rows retain a 6rem artwork column and a wrapping text column at
320px, with a smaller editorial title and 1rem gap. Keep visible focus, 16px field
text and usable controls in both themes. Library navigation has an active state.

## Title activity and public reviews

Below public catalog details, separate Community rating, Your library, Your
rating, Your review and Community reviews with quiet rules and serif headings.
Keep form width at most 48rem. Tracking status and optional native date inputs
share three columns on desktop and stack at 48rem. Changing status preserves
dates; explain optional dates and reject completion before start. Rating uses a
labeled native select from zero to five in half steps, with a distinct unrated
placeholder and separate removal action. Public averages/counts come from actual
data; show No ratings yet when absent.

Review editing uses a visibly labeled plain textarea, a spoiler checkbox and an
explicit Publish review/Save review action. State that reviews are public before
the editor. Preserve drafts after errors and creation conflicts; loading a saved
review to resolve a conflict is explicit. Library removal and review deletion use
inline Confirm/Cancel controls explaining which independent resources remain.
Pending actions disable repeat submissions; success appears only after completion.

Public entries show author name, handle and date, with literal text preserving
line breaks and wrapping long content within 75ch. Spoiler bodies begin absent
from rendered content; native Show spoilers/Hide spoilers buttons expose their
expanded state. Independent URL-owned review pagination preserves personal form
drafts. Retain genuine count, loading, empty, error and first-page/reset recovery.

## Session and draft continuity

Verify the session before library reads or private form reads. Checking, session
failure/retry, expired-session recovery and anonymous login continuation are
distinct from absent personal records. Public details, rating and reviews remain
available while private controls are concealed. Never interpret a failed read as
an empty saved record.

During a same-account session check, retain form state under hidden, inert and
disabled controls, block private reads and abort pending writes. Restore drafts
only after verification; confirmed logout or an account change discards them.
Review-page history, unrelated successful saves and write failures preserve drafts.
Initial session discovery retains only known public query namespaces; private or
unknown cached data is cleared, and real identity changes still clear prior state.
Never replay an attempted write after login or display a stale account's outcome.

## Evidence and limits

Compared the approved spec/plan with personal components/CSS, shared tokens,
`PRODUCT.md`, `.impeccable/review/personal/report.json` and all 24 stored captures
via `/tmp/personal-contact-0.png` through `-5.png`. Also inspected the full
session-checking capture and readable `/tmp/personal-library-mobile.png`.
Both themes at 320/768/1024/1440px preserve the established palette, type, artwork
fallback and flat layout. State captures cover anonymous, checking, saved,
confirmation, empty, invalid-filter and failed-write states.

The fresh review initially found draft loss during session checks. After draft
retention and initial public-cache preservation fixes, the scoped finish review
reports SHIP with both findings resolved. Reported verification passed 316
frontend tests and 54 backend tests. Browser evidence records keyboard skip-link
behavior, review-page history and same-account draft continuity with no runtime
errors. All five browser writes and production-browser responses were synthetic
CDP fixtures; separate live public reads returned 200 and anonymous private reads
returned 401. No live authenticated writes were performed. Fixture artwork
fallbacks do not establish live artwork availability. PRODUCT.md correctly records
library tracking, ratings and public review creation/editing as current, with
collections and social interfaces remaining future work.

Approved scope: `docs/superpowers/specs/2026-10-31-frontend-personal-media-design.md`.
Implementation: `docs/superpowers/plans/2026-10-31-frontend-personal-media.md`.
