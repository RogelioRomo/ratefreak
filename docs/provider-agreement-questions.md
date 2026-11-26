# Provider clarification drafts

Prepared 2026-11-18; IGDB/Open Library drafts narrowed 2026-11-26 after the
[fresh IGDB review](igdb-provider-review-2026-11-26.md) and
[Open Library review](open-library-provider-review-2026-11-26.md).
These are unsent questions, not permissions or accepted agreements. Before sending,
the operator supplies their account/application identity and actual intended use,
including monetization. Keep credentials and confidential replies outside Git.

Follow-up, 2026-11-24: the TMDB refresh/ID-retention questions below are historical
where answered by the public staff reply, and the owner confirmed permitted
resizing/re-encoding and poster refresh. See the
[current disposition](provider-agreement-evidence.md#current-disposition-2026-11-24).
Do not send those questions again as unresolved permission requests. The approved
lifecycle is now locally [implemented and verified](tmdb-lifecycle-verification.md);
other provider/operator questions remain.

## Common implementation description

RateFreak is MIT-licensed self-hosted catalog software. Each installation supplies
its own provider credentials/contact; release packages are intended to contain no
provider database or shared keys. Users search, preview and import selected titles.
Imports store selected normalized fields and source identities in PostgreSQL.
IGDB/Open Library imports currently persist without automatic refresh; TMDB now
has its own verified refresh/expiry/removal lifecycle. Ratings, reviews and list
memberships are authored locally and reference internal catalog identities.

Successful request responses are cached for five minutes (search) or one hour
(details). IGDB/Open Library artwork uses a server-side optimizer that resizes/re-encodes
images, has a minimum 24-hour TTL and can serve stale cached images during upstream
failures. Open Library CoverID images enter through a restricted same-origin
source route before optimization. Cache files survive service recreation and are
excluded from coordinated recovery backups. Disabling provider access does not
erase imports or image-cache files. No blanket retention exception is assumed.

## TMDB

Use the official [API support](https://developer.themoviedb.org/docs/getting-started)
route, or the [FAQ commercial contact](https://developer.themoviedb.org/docs/faq)
when appropriate. Include the common description and ask:

1. How should section 1.C's six-month cache limit apply to imported normalized
   records, source IDs and image URLs? What constitutes a sufficient refresh?
2. Is ordinary thumbnail resizing/re-encoding permitted under the derivative
   restriction, including persistent/stale optimizer caching?
3. What fields and backup copies must be purged on termination or a removal request?
   Can independent user-authored material retain an internal placeholder identity?
4. Which notice should an application use where API section 3 and FAQ wording differ?
   Does the operator's described use need a separate commercial agreement?

## IGDB/Twitch

Use [IGDB support](https://api-docs.igdb.com/#support). The owner confirmed on
2026-11-26 that the described project/use is completely free and noncommercial,
without monetization or financial gain. Describe that use in any inquiry;
do not request a commercial partnership or ask about ending one as our current
scenario. Ask:

The FAQ already permits local storage, serving data to end users and retaining
data after partnership termination. Do not ask those questions again.

The owner selected TMDB-style IGDB refresh starting at five calendar months,
retries, and expiry of unrefreshed provider content at six months, preserving
independent user content/internal identities. Implementation is pending; describe
this as proposed behavior, not a provider-mandated or already implemented interval.

1. For the described public catalog, how do Twitch VI.C/F, VII and IX apply to
   IGDB metadata/covers? What update cadence or correction/removal process is
   required, and does the proposed five-month refresh/six-month expiry satisfy
   the update obligation? What applies after developer-agreement termination (as distinct from
   partnership termination)? What must happen to stored records/backups?
2. May covers be resized/re-encoded and cached as described, including stale
   variants during upstream failures? Are there additional removal conditions?

Existing static Credits and source links can be described. Independently
monetized installations need their own eligibility assessment; they are outside
the owner's described use.

## Open Library/Internet Archive

Use the contact route on [API guidance](https://openlibrary.org/developers/api).
Application registration is a separate operator action. Ask:

1. Does the direct cover-service image-source guidance permit the server optimizer
   described above, including cached variants and stale responses during outages?
2. How do the Archive Collections-use terms linked from Open Library apply to
   cover display and imported descriptions in this self-hosted catalog?
3. Are there specific cover correction/takedown instructions beyond the published
   guidance that operators should follow?

Metadata caching, identifying User-Agent, application registration and published
request limits are already documented; do not ask for individual API-use approval
as a substitute for following those rules. If direct cover `src` URLs are adopted,
question 1 about permission for our optimizer becomes unnecessary. Registration
submission remains separate from this unsent clarification draft.

Record a reply's scope, date and application identity. A permission for one
operator does not automatically authorize every installation or populated release.
