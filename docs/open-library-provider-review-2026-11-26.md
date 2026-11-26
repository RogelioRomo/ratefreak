# Open Library / Internet Archive provider review

Reviewed 2026-11-26. This supplements the [November 18 guidance](provider-public-guidance-2026-11-18.md) and [unsent clarification drafts](provider-agreement-questions.md). Read-only public-source research; no account authentication, registration, form submission, provider contact, agreement acceptance or runtime change occurred.

## Release disposition

Subsequent owner decision: use direct cover delivery and document the work for
implementation in a later session. See the
[implementation handoff](provider-implementation-handoff.md). That choice is
settled; implementation/verification remain pending. The research findings and
terms interpretation limits below retain their scope.

**No reviewed source establishes that distributing clean MIT-licensed software with an optional, default-disabled Open Library adapter requires individual provider approval.** This is a bounded evidence finding, not a provider-granted exception or a declaration that every deployment is compliant. A clean release means no provider catalog, artwork/cache files or shared operator identity is bundled. Default-disabled status and artifact cleanliness still need release verification.

**The current same-origin cover optimizer remains an unresolved departure from published display guidance.** An operator can avoid that specific question by using direct Covers image URLs, or leave cover delivery disabled while retaining that uncertainty. Any implementation change is a separate decision; this research authorizes no code changes. Registering an application does not itself resolve the cover question: the official form says individual cases are not reviewed or approved. [Application registration](https://docs.google.com/forms/d/1119CgdEWHtaAy2lOpUmtbe4S-w6xhJUin-nqP7Bl1XQ/viewform?edit_requested=true).

## Public covers versus same-origin delivery

The current [Covers API guidance](https://openlibrary.org/dev/docs/api/covers) asks public-facing pages to use image `src` URLs pointing to `covers.openlibrary.org`. It prohibits crawling, provides S/M/L variants, appreciates a courtesy link, and exempts CoverID/OLID lookups from its stated 100-requests/IP/five-minutes restriction. That exemption concerns rate limits, not display or content rights.

RateFreak's reviewed configuration uses `/provider-artwork/openlibrary/*`, then Next's optimizer, with WebP output and a minimum 86,400-second TTL. The existing question additionally covers persistent cached variants and stale delivery. **Inference:** its browser-visible same-origin source differs from the requested source arrangement. The guidance neither expressly permits that arrangement nor states a blanket ban on caching, resizing or re-encoding. No applicable maximum artwork retention period, stale-serving allowance or mandatory purge interval was found there. Metadata-cache encouragement and ordinary HTTP cacheability do not establish a specific optimizer exception.

**Narrow corrective option:** render direct Covers URLs, selecting the documented size and using CSS for layout; bypass both proxy and optimizer for those images. This follows the stated display arrangement without claiming a new redistribution license. Alternatively omit covers, or seek a focused exception if same-origin delivery is essential. [Covers API](https://openlibrary.org/dev/docs/api/covers).

Historical context is weaker than current guidance: a [2011 official coverstore post](https://blog.openlibrary.org/2011/04/27/coverstore-improvements/) contains an Evergreen user's description of server caching and an author response discussing whitelisting. The user's comment is not provider policy; the author's response is not an explicit present-day license for RateFreak's resizing, re-encoding or stale persistent cache. It should not be used to invent either a universal caching ban or an optimizer exemption.

## Metadata retention and licensing

The [catalog-use FAQ](https://openlibrary.org/help/faq/using) describes public-domain library donations, requests CC0 contributions, and expressly offers catalog downloads and record exports. The separate [licensing page](https://openlibrary.org/developers/licensing) asserts no new Internet Archive proprietary rights while preserving possible existing rights in contributions and jurisdictions. Together they support catalog reuse, without warranting that every description or cover is unrestricted.

**Inference:** selected bibliographic metadata need not receive an invented periodic expiry merely because an adapter imports it. No such retention deadline or automatic refresh duty was found in these sources. Maintain source identity and distinguish local reviews from imported content; avoid extending the software's MIT claim to artwork or all contributed prose. These are practical documentation/content-scoping measures, not newly discovered provider requirements. For bulk catalog acquisition use the published [data dumps](https://openlibrary.org/developers/dumps), which include revision and modification fields and a deletes dump. Their existence does not itself impose a downstream synchronization or deletion schedule.

## Hosted API and operator registration

The current [API page](https://openlibrary.org/developers/api?show_page_status=1) supports open, human-directed book discovery; requests registration, application/contact identification and response caching; and prohibits HTML scraping, API bulk harvesting, spreading requests across IPs and high-traffic commercial backend use. Published budgets are one request/second unidentified and three identified. **Inference:** on-demand search/preview/import is compatible with the stated discovery purpose when actual traffic remains suitable; per-process throttling alone does not prove aggregate deployment compliance.

The linked [registration form](https://docs.google.com/forms/d/1119CgdEWHtaAy2lOpUmtbe4S-w6xhJUin-nqP7Bl1XQ/viewform?edit_requested=true) requires contact email, User-Agent, services used and acknowledgments of single-IP-at-a-time operation, as-is service, no individual-case review/approval, and discretionary denial of service. Its application-nature choices include commercial, open source, academic and personal; choosing a category does not override service guidance.

**Operator action:** accurately register the intended use and supply real identification before enabling regular service. An environment-variable email is not a registration submission. The reviewed pages do not expressly decide whether a single project registration covers independent self-hosted operators. Do not represent it as universal coverage or require a nonexistent individualized approval workflow. Record actual operator identity, registration status and egress arrangements; resolve separate-instance coverage if needed for the planned deployment. Registration is a requested operational step, not evidence of a source-distribution approval requirement.

## Internet Archive Collections terms: scope and evidence

Open Library's footer links [Internet Archive terms](https://archive.org/about/terms). The browser retrieved only a JavaScript shell and could not retrieve its metadata body. A subsequent authorized direct HTTP retrieval of the [official metadata body](https://archive.org/metadata/ia-page-terms) succeeded on November 26. The metadata description matches the November 18 saved body exactly.

The retrieved body displays terms dated December 31, 2014. It defines Collections broadly as web pages and digital content available through the Archive, restricts Collections access to scholarship/research, requires lawful noninfringing/fair use or compliance with a declared material license, disclaims content warranties, and reserves removal and termination/access blocking. It does not state a downstream blanket cache expiry, automatic purge-on-termination deadline, or special prohibition on publishing unused adapter code. [Official metadata endpoint](https://archive.org/metadata/ia-page-terms).

**Unresolved scope:** the broad wording should not be dismissed as applying only to scanned books, but the reviewed terms do not explicitly reconcile it with Open Library's specific catalog-reuse and public cover-display documentation. No authoritative scope clarification was found. Consequently this review cannot certify unrestricted commercial cover redistribution or declare every ordinary Open Library discovery integration prohibited. Any required review gate should identify this actual scope question and the actual deployment, rather than make individualized approval a universal prerequisite for clean source distribution.

Fresh preserved evidence: ignored `.local/release-compliance/provider-agreements/2026-11-26/archive-terms-metadata.json`, 21,345 bytes, SHA-256 `fd435e5e58ead8c25a70cb0f6c3717b042e642450a322baf2157edd8a7c9d114`. The [public retrieval receipt](release-evidence/2026-11-26-provider-review/retrieval.json) records this response. Whole-response hashes differ from November 18; the agreement description itself is identical. The previous review documents the official static-page loader connecting the terms page to this item; this note does not claim to have freshly verified that loader.

## Retrieval and remaining choices

Fresh official web results supplied Covers guidance, licensing, catalog-use FAQ, dumps and the historical coverstore post. The API page was also successfully opened with `?show_page_status=1`; following its registration link returned the readable form with `?edit_requested=true`. Initial direct opens of the four Open Library guidance pages timed out; direct form access initially failed. Archive terms returned only the JavaScript shell; browser metadata access and the initial sandboxed Python fallback failed. Authorized direct HTTP retrieval subsequently obtained the metadata body as recorded above. Search freshness labels are retrieval/index observations, not guarantees of an uncached origin response. No private reply or newly accepted agreement exists.

1. **Clean optional-adapter release:** verify disabled defaults and clean artifacts; document activation responsibilities and the unresolved cover arrangement. No blanket release prohibition was established by this review. This finding does not independently close a repository review gate.
2. **Enabled deployment following published display guidance:** use direct Covers `src` delivery or omit artwork; register accurately, identify requests and coordinate actual egress traffic. Direct delivery resolves the source-arrangement mismatch, not every underlying content-rights or Collections-scope question.
3. **Enabled same-origin optimizer:** retain an explicit unresolved item for that behavior. If the operator wants provider clarification, ask specifically about resizing/re-encoding, persistent/stale variants and applicable removal conditions; no contact was authorized or performed here.

Existing [self-hosting removal instructions](self-hosting.md) describe manual metadata removal and cache flushing separately. Their presence is operational capability, not proof that disabling an adapter purges imports or cached artwork, or evidence that the provider mandates those exact procedures.
