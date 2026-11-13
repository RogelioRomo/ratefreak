# Provider agreement access and remaining decisions

Reviewed 2026-11-13. This supplements the [provider review](provider-terms-review.md).
It distinguishes retrieved public guidance from agreements that were not obtained.
No agreement was accepted, no account was authenticated and no provider credentials
were used. Public documentation is not a substitute for an operator's applicable
contract or permission to redistribute underlying copyrighted artwork.

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

## Remaining blockers, precisely scoped

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
