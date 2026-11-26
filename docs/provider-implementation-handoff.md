# Provider implementation handoff

Recorded 2026-11-26 for implementation in a later session. No application change
has been made by this decision record.

## Agreed scope and status

RateFreak is completely free, self-hosted and noncommercial, without monetization
or financial gain. No commercial IGDB partnership is pending for that use.
Ship clean software/installation artifacts, with optional providers disabled by
default and no provider database, artwork cache or shared credentials bundled.
Independent operators remain responsible for their actual use and registrations.

**The implementation choices are settled; provider correspondence is not a
prerequisite to starting this work. The implementations and their verification
remain release tasks.** This disposition does not assert that IGDB approved a
six-month interval or our cover processing, or that Internet Archive clarified
every terms interpretation. Those research limitations remain recorded below;
they have not become new provider permissions.

## IGDB: implement the selected TMDB-style lifecycle

The owner chose the existing TMDB lifecycle as the behavioral model:

- Start refreshing imported metadata and cover references at five UTC calendar
  months after the last successful synchronization.
- Retry temporary failures without advancing the successful-refresh timestamp.
  At six calendar months, clear unrefreshed provider content and show an
  unavailable placeholder.
- Preserve internal identities, source identifiers as appropriate to the
  applicable removal policy, ratings, reviews, libraries, history and collections.
- Handle confirmed provider removals and operator termination/removal separately
  from age-based expiry. Disabling API access alone must not be described as a
  purge. Carry applicable current removal decisions through backup restore so
  removed content is not silently republished.

Inspect the existing TMDB implementation and its verification before deciding
what can be shared. Preserve provider-specific behavior and do not invent an
IGDB-mandated six-month deadline. Cover-reference refresh alone does not ensure
previous optimized variants have been invalidated; include actual served images
and prevention of refetch after removal in the implementation review.

Verify calendar boundaries, success/retry/not-found paths, expiry, preservation
of user content, idempotency, request pacing, cache handling, disablement/removal
and isolated restore. Keep the existing TMDB lifecycle working. Qualify affected
API/web/tools artifacts and installation behavior for the final candidate.

## Open Library: direct cover delivery

The selected approach is direct image `src` URLs on `covers.openlibrary.org`,
using its documented sizes and CSS for layout. Bypass both the same-origin cover
proxy and Next.js image optimization for these covers. Preserve strict source
validation, attribution/source links, image dimensions and accessible fallbacks.

Review every cover consumer and any applicable Content Security Policy. Remove
or disable obsolete Open Library optimizer/source-route access once no caller
needs it. Document safe disposal of obsolete cached covers without disrupting
IGDB or TMDB artwork. Do not silently retain an optimizer fallback that recreates
the original mismatch. Direct delivery means browsers contact the provider;
review relevant privacy/operator documentation accordingly.

Verify browser-visible cover URLs across preview, catalog and library displays,
fallback behavior, source allowlisting and continued handling of other providers
and protected uploads. Update operator instructions and affected artifact proof.
Do not add an invented six-month Open Library metadata expiry.

## Remaining terms and operator records

- IGDB's published storage/display permission is established. The selected
  refresh interval is our policy, not a provider-confirmed interpretation of
  "frequent" updates. Cover-processing and general termination/removal scope
  retain the limits in the [IGDB review](igdb-provider-review-2026-11-26.md).
- Direct Open Library covers address the published image-source arrangement;
  they do not determine all content rights or broader Archive Collections scope.
  See the [Open Library review](open-library-provider-review-2026-11-26.md).
- Each enabled installation records real operator identification/contact,
  Open Library registration, applicable terms, aggregate request budgets and
  complaints/removal handling. The registration form does not approve individual
  cases. A monetized installation requires its own eligibility assessment.
- [Clarification drafts](provider-agreement-questions.md) remain optional unsent
  material, not evidence of permission or authorization to contact providers.
  Reassess if provider guidance, actual use or the implementation changes.

## Resume later

Ask: "Implement the IGDB lifecycle and direct Open Library covers documented in
docs/provider-implementation-handoff.md."

Use [STATUS](STATUS.md) for all other release gates. Completing this handoff's
work does not itself freeze, qualify or publish a release.
