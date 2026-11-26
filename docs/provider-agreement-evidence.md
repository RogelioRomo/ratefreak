# Provider agreement access and remaining decisions

Reviewed 2026-11-13; current disposition updated 2026-11-26. This supplements the [provider review](provider-terms-review.md).
It distinguishes retrieved public guidance from agreements that were not obtained.
No agreement was accepted, no account was authenticated and no provider credentials
were used. Public documentation is not a substitute for an operator's applicable
contract or permission to redistribute underlying copyrighted artwork.

## Current disposition, 2026-11-26

The owner confirmed that RateFreak is completely free, self-hosted and
noncommercial, with no monetization or financial gain. Commercial IGDB partnership
eligibility is therefore not a pending requirement for the described use under
the [published noncommercial access guidance](https://api-docs.igdb.com/#account-creation).
No separate partnership is established by this record; partnership termination
is not the current operating scenario. Other operators must assess their actual
use if they monetize an installation. General provider terms still apply.

Fresh [IGDB research](igdb-provider-review-2026-11-26.md) reconfirms that local
storage and serving retrieved data to users are explicitly allowed; those are
not unresolved permission questions. Retention after partnership termination is
also explicit. The narrower remaining issues are applicable accuracy/update and
removal duties under the Twitch agreement, developer-agreement termination, and
conditions for our transformed/stale cover cache. No blanket 24-hour IGDB
metadata expiry is established. Commercial partnerships remain per operator.

The owner subsequently chose the TMDB-style lifecycle for IGDB: refresh from
five calendar months, retry, and expire unrefreshed provider content at six months
while retaining independent user content/internal identities. This resolves the
project's lifecycle choice; implementation/verification are pending. It is not
an IGDB-published interval or a resolution of cover-processing permission. See the
[recorded decision](igdb-provider-review-2026-11-26.md#concrete-next-step).

Fresh [Open Library research](open-library-provider-review-2026-11-26.md)
confirms catalog reuse guidance and requested registration/identification/pacing.
Its form expressly disclaims individual-case approval. Published cover guidance
requests direct Covers image `src` URLs; the current optimizer arrangement remains
unresolved. Direct cover delivery or omitting covers would avoid that particular
mismatch, without deciding all underlying rights or Archive Collections scope.
The official Archive metadata terms body was freshly retrieved and matches the
previously reviewed agreement description. No new metadata expiry was established.

The owner requested a later implementation handoff after selecting the IGDB
lifecycle and the recommended direct Open Library cover approach. The
[handoff](provider-implementation-handoff.md) records these settled choices and
their required verification. Provider correspondence is not a prerequisite to
starting those implementations; implementation/acceptance remain release tasks.
The terms interpretation limits above remain recorded, without claiming new
provider permission. Clean software distribution with optional, default-disabled
adapters is distinct from populated distribution, which is not cleared here.
The [unsent drafts](provider-agreement-questions.md) remain available for optional
clarification. No provider was contacted, form submitted or runtime changed.
Public direct-retrieval hashes are in the
[receipt](release-evidence/2026-11-26-provider-review/retrieval.json).

<a id="current-disposition-2026-11-24"></a>

## TMDB disposition, 2026-11-24

TMDB staff clarification is available in the public
["Clarification about TMDB API use" discussion](https://www.themoviedb.org/talk/6ac663597ca2844b5f25e72b).
In the 2026-10-15 reply, Travis Bell (marked STAFF) accepts refreshing database
metadata before six months and deleting data no longer refreshed, explaining that
the clause concerns staying synchronized. He also says TMDB IDs can remain in
user-owned saved/watched records after termination while metadata/images are
purged. This answers the earlier storage-scope question; it does not permit
indefinitely retaining unrefreshed metadata. The noncommercial-use answer applies
to the described free, unmonetized website, not automatically every operator.

The owner confirmed on 2026-11-24 that poster refresh follows the same six-month
guideline and resizing/re-encoding is allowed. These are owner-supplied decisions;
the retrieved thread does not expressly approve image transformation. Its later
image reply describes CDN infrastructure and logging rather than answering the
unchanged-byte proxy permission question.

The owner approved automatic refresh starting at five months, retries until six
months and replacement of expired provider metadata/posters with an unavailable
placeholder while preserving user content, internal UUIDs and TMDB IDs. See the
[approved lifecycle design](superpowers/specs/2026-11-24-tmdb-content-lifecycle-design.md).
Implementation/local acceptance are complete and merged locally into `main`: five-month refresh, six-month expiry, independent poster cache, removal
and current-policy restore passed the [verification record](tmdb-lifecycle-verification.md).
Final stable artifact/source qualification, required attribution
and operator eligibility remain release work. Other provider questions remain independent. No additional TMDB
image-processing permission request is pending under the owner's decision.

This follow-up used the public thread without authentication or provider API
credentials; no provider correspondence or agreement acceptance occurred.

<a id="current-disposition-2026-11-18"></a>

## Previous disposition, 2026-11-18

**The missing TMDB agreement was obtained. Its retention and termination clauses
raised a scope question for permanent database imports. That question is now
addressed by the 2026-11-24 disposition and implementation evidence above.**
Earlier retrieval failures and findings below are historical where superseded by
this section. This follow-up used public pages without account authentication,
provider API calls, agreement acceptance or outbound correspondence.

| Provider | Resolved by this review | Still required |
| --- | --- | --- |
| TMDB | Full API/site terms retrieved; staff refresh/ID-retention clarification and owner image-handling decision recorded above | Qualify the verified lifecycle for final artifacts; maintain required attribution and record operator eligibility |
| IGDB | Current documentation expressly permits local storage; full Twitch agreement retrieved again | Written scope clarification for public catalog storage/display, updates, removal and termination; operator partnership when applicable |
| MusicBrainz | Core-data and hosted-service rules rechecked | Instance eligibility/contact and aggregate pacing; maintain reviewed core-field scope |
| Open Library | Metadata caching supported; direct cover-source guidance confirmed | Cover proxy/optimizer and Archive terms scope clarification; application registration and operator usage record |

The detailed [Open Library/MusicBrainz research](provider-public-guidance-2026-11-18.md)
separates explicit guidance from unresolved interpretations. Ready-to-review
[provider questions](provider-agreement-questions.md) have been drafted but not sent.

### TMDB: retrieved clauses and engineering consequence

The [API terms](https://www.themoviedb.org/api-terms-of-use) display an update date
of October 20, 2023. Section 1.C prohibits caching information beyond six months
and restricts derivatives. Section 1.D requires stopping use and purging content
when the license ends. Sections 1.A/2 restrict transfer, sublicensing and commercial
use; section 3 specifies logo/notice attribution. These terms incorporate the
[site terms](https://www.themoviedb.org/terms-of-use), which separately address
noncommercial use and content rights. [FAQ attribution wording](https://developer.themoviedb.org/docs/faq)
differs from section 3; the UI currently follows the FAQ.

Historical implementation comparison (before the lifecycle change): imported fields persist without expiry; repeat import
returns the existing item without refreshing. The five-minute/one-hour request
cache does not bound PostgreSQL retention. The image cache minimum TTL likewise
does not guarantee eventual deletion during outages. No provider-content purge
workflow exists. The 2026-11-24 follow-up resolves the earlier retention-scope
question and records the owner's resizing/re-encoding decision; implementing the
approved lifecycle is now locally verified; final integration/artifact qualification remains required. The recorded API/FAQ attribution wording
difference remains part of the attribution review.

Do not delete existing catalog rows as a research action. A separate design must
cover provider fields, stable internal references, user-authored content, removal
requests, caches and restored backups under the applicable permission.

### IGDB: confirmed storage guidance, unresolved agreement scope

The current [IGDB business FAQ](https://api-docs.igdb.com/#business-related-faq)
allows local storage and serving retrieved data, prefers that approach, and allows
retention after partnership termination. It still directs commercial integrations
to the partner program. This supports persistent metadata, not a conclusion that
every termination or content-rights issue is waived.

The [Twitch agreement](https://legal.twitch.com/en/legal/developer-agreement/)
still displays `12/04/2024`. Section I limits schedules by the activity/materials
and gives conflicting executed agreements priority. VI.A broadly defines data
covered by the data policy; VI.C/F and IX address sharing and deletion. VII
requires accuracy/updates and attribution. Schedule 1 defines its APIs by reference
to the Twitch developer site and has a storage exception for authorized uses or
rights, otherwise a 24-hour cache. The FAQ alone does not settle how those clauses
apply to IGDB catalog fields/artwork. Do not impose an invented blanket 24-hour
IGDB expiry or treat partnership termination as developer-agreement termination.

Request confirmation of the governing terms, permitted public display/retention,
refresh duties and removal scope. No written exception or operator partnership
was obtained during this review.

### Dated retrieval evidence

Approved direct public retrieval returned HTTP 200 for all four sources below.
The browsing service still denied TMDB terms retrieval; direct HTTP succeeded.
Snapshots remain ignored under
`.local/release-compliance/provider-agreements/2026-11-18/`.

| Snapshot | Bytes | SHA-256 |
| --- | --- | --- |
| TMDB API terms HTML | 119602 | `1e027f7950f7eee3382b9141e1665458dfbcefe29f42e1ead3e0e87d37081dca` |
| TMDB site terms HTML | 132233 | `5182df8668d4c783c63036de403b119d49e2e95a6ae1fec2366f5815139153f2` |
| Twitch raw gzip response | 28639 | `aea37acf021c95ef3a276bde27a83054f99e4c5da2dd6455eb8b592cd8931b6f` |
| Twitch decoded HTML | 101415 | `8fbde374e022e4e29d0e0abb6e49a7399c430a7fe43ed90d6340ffa57a13332d` |
| IGDB documentation HTML | 1290053 | `2d15dbf2290d2b1f6c441a78c230af6028de2e54f10561d02b8d773d2f4bd34d` |

Hashes identify the retrieved bytes, not immutable contract versions. Actual
operators must keep their applicable terms/permissions and acceptance records.

## Retrieved official sources

| Source and exact URL | Obtained evidence | Limits |
| --- | --- | --- |
| [TMDB FAQ](https://developer.themoviedb.org/docs/faq) | Noncommercial use with attribution; commercial users directed to sales; approved logo plus disclaimer in About/Credits; TMDB disclaims ownership of API images/data | Does not establish a cache lifetime, permanent storage or bulk redistribution permission |
| [TMDB application authentication](https://developer.themoviedb.org/docs/authentication-application) | Account-issued API read token used as server bearer credential | No credential transfer/shared-distribution right established |
| [IGDB official documentation](https://api-docs.igdb.com/) | Account Creation and Authentication sections; Business related FAQ questions 1–5; Images reference | Useful explicit storage guidance, but underlying Twitch agreement and signed partnership still matter |
| [MusicBrainz data license](https://musicbrainz.org/doc/About/Data_License) and [database overview](https://musicbrainz.org/doc/MusicBrainz_Database) | Core CC0 versus supplementary CC BY-NC-SA 3.0; field categories | Data license does not waive hosted API service conditions |
| [MusicBrainz API](https://musicbrainz.org/doc/MusicBrainz_API) and [rate limiting](https://musicbrainz.org/doc/MusicBrainz_API/Rate_Limiting) | Noncommercial service use, commercial contact/plans, meaningful maintainer User-Agent, one request/second | Aggregate shared-egress use needs coordination |
| [MetaBrainz account selection](https://metabrainz.org/supporters/account-type) | Distinguishes personal/noncommercial, nonprofits/open-source authors, startups and commercial support tiers | Page concerns datasets/Live Data Feed signup; do not automatically apply every signup category to this application's individual API requests |
| [MetaBrainz PostgreSQL datasets](https://metabrainz.org/datasets/postgres-dumps) | Lists CC0 core and separately licensed supplementary dumps; financial support encouraged for commercial core-data users | RateFreak currently uses API lookups, not these dumps |

These pages were obtained through the web browsing tool. Their returned text was
reviewed, but no exact response bytes were saved; no content hashes are claimed.
This note contains paraphrases rather than copies of the agreements.

## IGDB clauses actually resolved by public guidance

The [Business related FAQ](https://api-docs.igdb.com/#business-related-faq) says
local storage/cache is allowed, commercial integration uses a partnership with
visible static attribution, and retrieved data may be retained after partnership
termination. This resolves the narrower question of whether IGDB publicly forbids
all persistent metadata storage: it does not. The Images reference warns that
removed/replaced image files expire after 30 days; that describes CDN availability,
not a universal requirement to erase local metadata every 30 days. Application
credentials and backend access are documented. Commercial partnership acceptance,
underlying artwork rights and bulk redistribution remain separate questions.

The full Twitch agreement was subsequently retrieved directly; its scoped storage
terms and termination duties require the comparison below. Partnership termination
and termination of the developer agreement are distinct events; the FAQ does not
automatically establish an exception for the latter.

## Full agreement retrieval attempts

| Agreement / route | Observed response on review date | Remaining evidence needed |
| --- | --- | --- |
| `https://www.themoviedb.org/api-terms-of-use` via web browsing | Robots policy denied retrieval | Actual applicable API terms text, date/version and relevant storage/removal/redistribution clauses |
| `https://legal.twitch.com/en/legal/developer-agreement/` via web browsing | Legal navigation/page shell, no agreement body | Current full developer agreement, including scope for IGDB and any special terms |
| `https://www.twitch.tv/p/en/legal/developer-agreement/` via web browsing | Redirected to the same legal-domain shell | Same as above; redirect did not provide the missing body |
| Public agreements via shell `curl` | Sandbox DNS resolution failed for both domains | Successful approved network fetch or operator-supplied public text |
| Final approved direct retrieval | TMDB terms returned HTTP 403; Twitch returned HTTP 200 with a gzip-compressed full agreement body | TMDB still needs applicable terms; Twitch access is resolved, interpretation for IGDB remains below |

The [TMDB branding page](https://www.themoviedb.org/about/logos-attribution)
is separate from API terms. Obtaining branding assets or implementing Credits
cannot close the missing contract review. No agreement-byte hash or effective
date can honestly be assigned to the inaccessible TMDB agreement.

## Twitch full-text follow-up

Direct retrieval obtained the complete [Developer Services Agreement](https://legal.twitch.com/en/legal/developer-agreement/),
including schedules. It displays `12/04/2024` as its last-modified label.
Decoded HTML: 100,890 bytes, SHA-256
`3c1123661265f4fd88200ea908a88e9781f571252e917c92124ee497e1d79eb1`;
compressed response: 28,664 bytes, SHA-256
`68d60cad4003f1148dbf1a05d4f67c946c8de220d17f0e4b5ef34ca199f839b3`.
The decoded snapshot is retained privately under
`.local/release-compliance/provider-agreements/twitch-developer-agreement.html`.
The prior shell-only responses are superseded by this retrieval.

Section I limits schedules to applicable activities/materials and lets an executed
separate agreement prevail on conflicts. IV.A–B prohibits account sharing and
credential transfer; operators must supply their own credentials. VI.C/F and IX
restrict data sharing and require deletion on specified requests/termination.
VII calls for accurate updates and attribution. Schedule 1 defines Twitch APIs
by reference to `dev.twitch.tv`; B/C restrict redistribution and storage, allowing
specified authorization/rights or a 24-hour cache. That schedule is not an
established blanket IGDB cache rule. IGDB's explicit storage FAQ and partnership
terms require a documented scope determination. Current imports have no automatic
refresh, provider takedown or full-provider erasure workflow. Neither global
retention permission nor an arbitrary expiry should be inferred.

## Implementation comparison and disposition

- Imports persist selected normalized metadata and external identities. Repeat
  import returns the existing item; no automatic expiration or provider-removal
  workflow is established by that path. This is compatible with IGDB's public
  storage statement, but TMDB contractual compatibility remains unverified.
- MusicBrainz selected fields appear to be core data, as described in the provider
  review. Ratings, tags, annotations and full responses are not imported. CC0 permits
  retention/reuse of core data; service-access conditions still apply. No forced
  periodic refresh should be invented; MusicBrainz discourages change polling.
- Provider artwork is referenced remotely, not copied into the protected custom
  artwork store. A remote URL does not eliminate display rights obligations or
  the need to handle removed images.
- The parallel Credits/catalog attribution change addresses UI gaps identified in
  the initial review. Its own verification evidence must establish final behavior;
  this agreement note does not claim browser verification.
- Defaults disable provider adapters. Operators should supply independent app
  credentials/contact details and record applicable commercial eligibility before
  enabling them. A shared release credential is not part of the proposed package.

## Remaining blockers recorded 2026-11-13 (superseded above)

1. **TMDB contract evidence:** obtain the full applicable terms and record provisions
   on permanent storage, cache, redistribution, takedown and termination. No cache
   interval or deletion rule is invented here.
2. **Twitch/IGDB scope and lifecycle:** full agreement retrieval is resolved. Confirm
   applicable schedule/partnership scope and any required provider correction/removal
   workflow. Do not conflate partnership termination with developer-agreement
   termination or presume that the FAQ waives every deletion duty.
3. **Operator acceptance:** commercial permission/partnership/service arrangements
   are instance-specific; this research neither accepts nor purchases them.
4. **Populated distribution:** no broad right to ship provider databases/artwork
   under MIT has been established. Review any populated artifacts separately.

These limits block a claim that all provider-enabled uses or populated artifacts
are cleared. They do not establish that clean source distribution with disabled
optional adapters is forbidden. No further implementation changes were made by
this research task.

## Operator acceptance checklist

Before enabling an adapter, keep a protected record of the account/application,
applicable terms URL/version/date, intended noncommercial/commercial use and any
permission or executed partnership. Record acceptance privately; never commit
credentials or confidential contracts.

- TMDB: supply an operator-owned application token, verify required Credits/branding,
  obtain applicable full API terms, and resolve storage/removal/redistribution rights.
  Revenue-oriented use needs the provider's commercial permission.
- IGDB: supply an operator-owned Twitch application/client secret, record partnership
  eligibility where required, and resolve the schedule/lifecycle questions above.
  Keep account credentials and app secrets private; do not ship shared keys.
- MusicBrainz: configure meaningful maintainer contact, respect the aggregate request
  budget, keep imported fields within the reviewed core-data mapping, and arrange
  commercial service access where applicable. Review new fields/artwork separately.
- Every adapter: ship clean installation artifacts, preserve attribution, provide a
  way for operators to handle content-rights complaints, and stop provider use if
  the applicable permission ends. Disabling access alone does not remove old imports.

This checklist defines evidence to record; no operator acceptance or commercial
agreement was made during this work.

## Open Library implementation follow-up, 2026-11-17

Open Library was selected for book work imports. Its published
[API](https://openlibrary.org/developers/api), [licensing](https://openlibrary.org/developers/licensing),
[search](https://openlibrary.org/dev/docs/api/search),
[authors](https://openlibrary.org/dev/docs/api/authors) and
[cover](https://openlibrary.org/dev/docs/api/covers) guidance was reviewed.
The application retains normalized metadata/source identities and now caches
successful external metadata across all providers. Public image caching has
independent technical TTLs; it does not establish ownership or contractual rights.

Open Library's cover guidance requests direct image URLs; the implemented
same-origin optimizer changes that HTML URL. No explicit cover-proxy permission,
new agreement acceptance, unrestricted contribution rights or populated-artifact
redistribution clearance is claimed. Operator-specific usage and complaints/removal
handling remain necessary. Earlier TMDB/Twitch/IGDB blockers are unchanged.
