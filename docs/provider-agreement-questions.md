# Provider clarification drafts

Prepared 2026-11-18 from the [agreement review](provider-agreement-evidence.md).
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
They currently persist without automatic refresh. Ratings, reviews and list
memberships are authored locally and reference internal catalog identities.

Successful request responses are cached for five minutes (search) or one hour
(details). Public artwork uses a server-side image optimizer that resizes/re-encodes
images, has a minimum 24-hour TTL and can serve stale cached images during upstream
failures. Cache files survive service recreation. Disabling provider access does
not erase imports or image-cache files. No blanket retention exception is assumed.

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

Use [IGDB support](https://api-docs.igdb.com/#support); commercial partnership
questions go to the partner contact listed in the
[business FAQ](https://api-docs.igdb.com/#business-related-faq). Ask:

1. Which agreement and schedules govern IGDB metadata and artwork for this instance?
   How does the storage FAQ apply to Twitch VI.C/F, VII, IX and Schedule 1.C?
2. May imported fields and optimized artwork be stored and displayed publicly as
   described? What refresh and provider-removal duties apply?
3. What differs between partnership termination, developer-agreement termination
   and an individual rights-holder request? What must be removed from backups?
4. Is the described monetization, if any, eligible for the partnership, and does
   static Credits plus per-item source attribution meet its requirements?

## Open Library/Internet Archive

Use the contact route on [API guidance](https://openlibrary.org/developers/api).
Application registration is a separate operator action. Ask:

1. Does the direct cover-service image-source guidance permit the server optimizer
   described above, including cached variants and stale responses during outages?
2. How do the Archive Collections-use terms linked from Open Library apply to
   cover display and imported descriptions in this self-hosted catalog?
3. What notification, correction and takedown process should operators follow for
   retained metadata and cached covers? Are there specific registration or
   aggregate/shared-egress conditions beyond the public API guidance?

Record a reply's scope, date and application identity. A permission for one
operator does not automatically authorize every installation or populated release.
