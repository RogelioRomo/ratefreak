# IGDB provider requirements follow-up

Reviewed 2026-11-26 using official public sources and the local implementation.
This narrows the [earlier agreement review](provider-agreement-evidence.md).
No provider was contacted, account authenticated, registration submitted or
operator agreement executed. No application or installation settings changed.

## Published answers

**Owner-confirmed use:** completely free, noncommercial self-hosted software,
without monetization or financial gain. IGDB's published noncommercial access
route fits that described use; pursuing commercial partnership eligibility is
not outstanding. The partnership-termination FAQ is contextual guidance, not a
current project requirement. This does not waive general developer terms or
determine eligibility for independently monetized installations.

The [IGDB documentation](https://api-docs.igdb.com/#business-related-faq)
expressly permits local storage and serving retrieved data to end users. It
permits retention after **partnership termination**. Commercial integrations
require a partnership; the FAQ describes free access and visible, static
attribution. The account section requires an operator's Twitch application and
secret. Its Images section says removed/replaced images remain available for
30 days; this is CDN behavior, not a general metadata expiry rule. The technical
FAQ recommends backend API access. None of those statements specifies a refresh
interval or expressly describes our resized, re-encoded, stale cover cache.

The [content/image policy](https://www.igdb.com/content-policy) accepts several
image-rights categories, including fair use. Consequently, an image's presence
on IGDB does not establish that it is public domain or covered by RateFreak's MIT
license. This is a rights distinction, not a finding that every cover display
requires a separate license request.

## Remaining agreement question

The freshly retrieved [Twitch Developer Services Agreement](https://legal.twitch.com/en/legal/developer-agreement/)
still displays `12/04/2024`. Its body includes broad Program Materials and data
definitions, third-party sharing restrictions (VI.C), deletion on request or
agreement termination (VI.F/IX), and frequent accuracy updates and source
attribution (VII). Section I scopes schedules to applicable activities/materials
and gives a conflicting executed agreement priority. Schedule 1 defines its APIs
by reference to `dev.twitch.tv`; its storage clause also recognizes written
authorization. These provisions do not justify inventing a blanket 24-hour IGDB
expiry. Equally, the FAQ's partnership-retention answer does not expressly waive
developer-agreement termination or rights-holder removal duties.

**Interpretation limit:** public guidance supports storing and displaying an
IGDB-backed catalog. What remains unresolved is the applicable update/removal
process and whether the cover optimizer needs any additional conditions. No
fresh public clarification settling those points was located. Absence of such a
reply is not proof that an ordinary IGDB integration is prohibited.

## Comparison with this checkout

- [Provider/mapping](../src/RateFreak.Infrastructure/Providers/Igdb/IgdbMediaProvider.cs)
  retrieves selected game fields and a `t_cover_big` image reference through
  server-owned credentials. [Pacing](../src/RateFreak.Infrastructure/Providers/Igdb/IgdbSession.cs)
  serializes requests with 250 ms spacing per API process; multiple credential
  users/replicas still need a shared budget.
- [Import behavior](../src/RateFreak.Infrastructure/MetadataImport/MediaImportService.cs)
  returns existing imports without refreshing their fields. No IGDB automatic
  lifecycle equivalent to TMDB's is implemented. Do not confuse five-minute/
  one-hour response caches with database retention.
- [Image configuration](../web/shared/artwork/provider-images.ts) transforms
  covers through Next.js, with a minimum 24-hour TTL. The
  [operator guide](self-hosting.md#provider-configuration-and-image-cache)
  documents manual cache clearance and the separate need to block/remove
  affected references; clearing files alone permits refetching.
- [Credits](../web/features/credits/components/credits-screen.tsx) supplies a
  static linked IGDB credit. Final browser/artifact qualification remains separate.
- IGDB defaults off in [API configuration](../src/RateFreak.Api/appsettings.json).
  A clean source/installation release contains neither shared provider keys nor
  an imported provider database. This distinction does not clear every enabled
  operator's use.

## Concrete next step

**Owner lifecycle decision, 2026-11-26:** use the same lifecycle as TMDB for
IGDB imports: begin metadata/cover refresh at five calendar months, retry failures,
and remove unrefreshed provider content at six months while preserving internal
identities and independent user-authored records. This selects application
behavior; implementation and validation remain outstanding. Six months is an
owner-selected bound, not a documented IGDB allowance or proof that the agreement's
frequency requirement is satisfied. Confirmed removals and termination remain
separate from ordinary age-based expiry; cover-processing conditions are not
resolved by choosing a refresh schedule.

The subsequent [implementation handoff](provider-implementation-handoff.md)
records the owner's request to return later for implementation. Begin with the
selected lifecycle and its verification; provider correspondence is not a
prerequisite to starting. The narrower
[unsent questions](provider-agreement-questions.md#igdbtwitch) remain available
for clarification, with the interpretation limits above preserved. Commercial
eligibility is not pending for the owner's confirmed noncommercial use.

## Retrieval evidence

Public HTTP retrieval succeeded for both pages. The browsing tool returned only
the Twitch navigation shell; direct retrieval returned the full gzip-compressed
agreement, decoded locally. Its decoded hash matches the 2026-11-18 receipt.
Saved bytes remain ignored under
`.local/release-compliance/provider-agreements/2026-11-26/`.
The public [receipt](release-evidence/2026-11-26-provider-review/retrieval.json)
records byte hashes, not immutable agreement versions or operator acceptance.
