# Frontend delivery checklist

Design: [frontend roadmap](superpowers/specs/2026-10-30-frontend-design.md).

- [x] Agree on feature-by-feature delivery and separate implementation sessions.
- [x] Agree on separate interface files, focused components and TanStack Query.
- [x] Review and approve Phase 1 written design.
- [x] Write [Phase 1 implementation plan](superpowers/plans/2026-10-30-frontend-foundation.md).
- [x] Review implementation plan and select native execution.
- [x] Phase 1: foundation, transport/proxy, Query provider and shell.
- [x] Phase 2: local accounts and session lifecycle.
- [x] Approve [Phase 3 catalog design](superpowers/specs/2026-10-31-frontend-catalog-design.md)
  and [implementation plan](superpowers/plans/2026-10-31-frontend-catalog.md).
- [ ] Phase 2 follow-up: external sign-in and identity linking.
- [x] Phase 3: catalog browsing/search and details.
- [x] Phase 3 follow-up: provider preview/import.
- [x] Phase 4: library, ratings and reviews.
- [x] Approve [Phase 5 metadata/visibility design](superpowers/specs/2026-11-01-frontend-collections-design.md).
- [x] Review [Phase 5 implementation plan](superpowers/plans/2026-11-01-frontend-collections.md); preserve native execution.
- [x] Phase 5: collection metadata and visibility.
- [x] Approve [Phase 5 fields/entries design](superpowers/specs/2026-11-01-frontend-collection-contents-design.md).
- [x] Review [Phase 5 fields/entries implementation plan](superpowers/plans/2026-11-01-frontend-collection-contents.md); preserve native execution.
- [x] Phase 5 follow-up: fields, items and schema-change confirmation.
- [x] Phase 6 first slice: public profiles and profile editing.
- [x] Phase 6 second slice: follows and follower/following lists.
- [x] Phase 6 final slice: review comments.
- [x] Phase 7 first slice: public review/comment reporting.
- [x] Phase 7 second slice: staff moderation, visibility and audit.
- [x] Phase 7: local cross-feature release checks and readiness record.
- [ ] Phase 7 remaining: production deployment verification and launch gates.

## Resume notes

2026-11-05 local release verification completed. See
[release verification record](frontend-release-verification.md) for launch gates.
Fresh588 frontend tests, lint/typecheck/build and whitespace checks pass.
Full backend clean rerun:591 passed, zero skipped (64 unit,373 real PostgreSQL
integration,154 provider contract). Missing local NuGet packages restored first;
no tracked dependency changes. Live browser checked11 routes at4 widths, keyboard
skip navigation and5 no-store authorization/filter probes; no runtime exceptions,
intercepted responses or synthetic browser fixtures. API liveness/readiness and
proxy catalog200. No live data writes. Temporary Chromium stopped; API/frontend
remain available. Artifacts `/tmp/release-*`. Updated web README. Changes remain
uncommitted; no push/deployment. Production launch gates remain open.

2026-11-05 staff moderation implemented and verified after user approval of its
design, native execution and dedicated temporary local moderator provisioning.
Account links to Moderation tools; existing API authorization governs access.
`/moderation` provides Open/Resolved queues with URL filters and paging;
report inspection supports explicit hide/resolve or dismissal, including deleted
target dismissal. Direct review/comment inspection supports hide/restore; audit
history uses real actor IDs, target filters and newest-first paging. All text is
literal and spoilers remain behind explicit controls.

Exact staff GET/POST proxy allowlists retain Origin/CSRF/JSON/body/no-store guards.
Account-scoped queries and an external denial latch conceal staff content during
session uncertainty, cancel requests/remove private caches on401/403 and require
explicit successful authorization retry. Same-account checks retain hidden/inert/
disabled drafts; account changes discard them. Inline consent binds raw server
microsecond timestamp, report status, target, action and reason; revision/session
changes cancel it.409 preserves reasons and requires fresh inspection; unknown
POST outcomes block replay. Confirmed204 feedback survives failed refresh while
another write stays blocked until successful inspection. Sensitive reasons stay
out of URLs/storage/retained mutation variables/results.

Final **588/588 frontend tests across84 files**, lint/typecheck, production build
and whitespace checks pass. **28/28 real PostgreSQL moderation integration tests**,
zero skips, pass. Missing-feature and regression checks observed RED→GREEN.
Fresh whole-change reviewer found no material issues; no deferred minors.
Mechanical design scan `[]`. One test harness issue was a reset callback returning
a mock, treated by Vitest as teardown; block-bodied reset fixed it without changing
production error handling. Test-only generic corrected before final passing build.

Live Chromium→Next3100→API5080→PostgreSQL verified ordinary403, explicit staff
access recovery, exact-version concurrent-edit409 and draft retention, review and
comment hide/restore with public404→200, live/deleted-target dismissal and six
persisted audit events. All three staff POST routes passed CSRF400/foreign-Origin403.
Real role revocation with a loaded pending confirmation produced403, removed staff
content, survived same-account session refresh, kept explicit retry denied and
left public content unchanged. Native Tab/Enter skip-to-main passes. Both themes
at320/768/1024/1440: eight report captures plus revoked mobile capture inspected;
no horizontal overflow or browser runtime exceptions. No API response interception
or synthetic browser fixtures. Scripts/captures/manifests/SQL: `/tmp/moderation-*`.

Accumulated local registration budget blocked the third test account after two
were created; restarted only Development API with unchanged limits/settings,
resumed the exact recorded accounts, then provisioned one temporary Moderator
membership/role. Dev Next restarted after build to avoid its earlier reload loop.
Guarded transactions revoked membership, then removed exactly three users, two
reviews/remaining comment, four reports, six test audit events and unused role;
zero matching rows remain. Two existing catalog titles preserved. No existing
real account promotion or backend/schema/dependency/configuration changes.

Implementation remains uncommitted; reporting commit `459a918` stays on main.
API5080/frontend3100 remain running; headless browser stopped after verification.
Next: cross-feature release verification and operational readiness. Production
ingress/deployment remains unverified; moderator-role administration UI and
external sign-in are separate optional work. No push/merge/deployment.


2026-11-05 user requested commit then next implementation. Public reporting
committed locally on main as `459a918` (`feat: add public review and comment
reporting`); fresh556/556 frontend tests and whitespace check passed immediately
before commit. No push/deploy. Next proposed slice:
[staff moderation design](superpowers/specs/2026-11-05-frontend-moderation-design.md),
covering queues, decisions, visibility and audit, with role administration separate.
No staff product code yet. Design review and dedicated local staff verification
account authorization precede its implementation; preserve native execution.

2026-11-05 public reporting implemented and verified. Catalog reviews, original
review discussions and comment cards now offer explicit inline reports for other
people's content, with seven backend reasons and Other details validation.
Anonymous login continuation preserves canonical review/catalog pagination and
never submits automatically. Spoilers stay concealed. Own-content controls are
absent. Details remain out of URLs/storage/retained mutation variables and results.

Exact POST report proxy routes retain Origin/CSRF/JSON/body/no-store guards.
Account-keyed forms preserve drafts during same-account checks under native
hidden/inert/disabled guards; unavailable targets and session uncertainty abort
writes. Identity changes discard editors/feedback. Known201/409 are terminal
local feedback; validation/rate-limit rejection preserves editable drafts.
Unknown POST outcomes block replay and warn that reload loses the draft.
Catalog review background failures now retain report editors with independent
retry feedback; retained404 content is concealed and disabled.

Final **556/556 frontend tests across77 files**, lint/typecheck, production build
and whitespace checks pass. **28/28 real PostgreSQL moderation integration tests**,
zero skips, pass. Focused missing-feature checks observed RED→GREEN. Fresh whole
change reviewer found no material issues/deferred minors. Mechanical scan `[]`.

Live Chromium→Next3100→API5080→PostgreSQL verified report201 for both review and
comment, duplicate409 after reload, native Other validation/recovery, real429
with editable draft retention, CSRF400, foreign-Origin403, anonymous401,
own-content exclusion, real login continuation without posting, cancel focus and
native Tab/Enter skip-to-main. No intercepted or synthetic API responses.
Both themes at320/768/1024/1440: eight captures inspected, no overflow or browser
runtime exceptions. Scripts, manifest, SQL and captures: `/tmp/reporting-*`.

Initial automation was interrupted by Development reloads before registration;
restarting only Next fixed it. Production preview correctly rejects the existing
Development loopback/cookie configuration; live evidence is Development, not
production ingress. Next TypeScript subprocess/MSBuild IPC required sandbox
escalation; successful checks above supersede those blocked attempts. Existing
rate limits stayed unchanged; real reporting429 exercised recovery instead of
resetting budgets. No system packages installed: browser libraries extracted/tmp.

Two exact dedicated accounts plus one review/comment and two reports removed by
guarded transaction; zero matching users/reviews/comments/reports remain. Existing
Breaking Bad catalog record preserved. No backend/schema/dependency/configuration
changes, commits, merge, push, deployment or real-account staff promotion.
API5080/frontend3100 remain running. Next: staff moderation as a focused slice,
then release verification. External sign-in remains optional and separate.


2026-11-05 user approved the public reporting design and focused-slice order.
[Implementation plan](superpowers/plans/2026-11-05-frontend-reporting.md) written
and self-reviewed; native execution preserved. Plan awaits review before product
implementation. Includes exact reporting routes, account-keyed drafts, uncertain
POST protection, retained background-error content and page-aware safe login
continuation. No product code changed.

2026-11-05 resumed Phase7 contract investigation. Proposed first focused slice:
[public review/comment reporting](superpowers/specs/2026-11-05-frontend-reporting-design.md).
Grounded in existing report endpoints, validation, duplicate lifetime semantics,
hidden-parent behavior and staff authorization contracts. Design awaits user
review; no product code changed and Phase7 remains pending. Next: approve/adjust
the reporting design, then write its implementation plan and execute natively.

2026-11-04 community rating now adds read-only five-star artwork alongside its
unchanged exact two-decimal average/count. Stars round to nearest0.5, ties up:
4.7→4.5 and4.8→5. Reuses rating icon/theme/half-fill styles; no-ratings remains
separate from zero.32 focused personal/rating tests, lint/typecheck and whitespace
checks pass. Read-only browser average fixtures verified both rounding directions,
exact numeric copy, mobile/light and tablet/dark visuals with no overflow; both
captures inspected. No database writes. Evidence `/tmp/community-stars-*`.

2026-11-04 requested two-column title activity layout delivered. Above640px,
Community rating/Your library and Your rating/Your review form two rows; mobile
stacks all four and community reviews remain full width below. Lint/typecheck and
22 focused personal-flow tests pass. Real browser verified placements at641/768/
1024/1440 and mobile stacking at320/640, no overflow, retained review draft and
native hidden/inert/disabled-fieldset guards. Both palettes captured at four main
widths; representative captures inspected. Temporary exact-identity verification
account removed; sample catalog titles preserved. Evidence `/tmp/activity-grid-*`.

2026-11-04 user-requested rating control update: replaced the score dropdown with
five Tabler star icons and ten half/full-star hit areas,0.5..5. Native named radios
retain keyboard arrow selection, required validation and accessible names. Pointer
hover previews without changing the selected value; touch selects directly. A
separate0-star choice preserves the existing backend range and saved zero ratings.
Save/Remove actions, rating API and session/draft protections stay intact. Uses
semantic light/dark accent/input tokens and24×48px half-star hit areas.

Focused missing-component tests observed RED→GREEN. Final **527/527 frontend tests
across74 files**, lint/typecheck and production build pass. Real Chromium→Next→API→
PostgreSQL verified all10 half-star targets, preview/reset without committing,
native ArrowRight/Left, saved4.5 and0 reload, mobile touch2.5 persistence and
removal404. Both palettes at320/768/1024/1440: eight rating-form captures inspected,
no overflow/runtime exceptions. Script/captures at `/tmp/stars-browser.mjs` and
`/tmp/stars-*.png`. Exact verification account/title removed in a guarded transaction
after checking the rating was absent; zero matching records remain. No backend,
dependency, schema or configuration changes. Phase7 remains the next feature work.

2026-11-04 review comments complete; Phase6 profiles/follows/comments delivered.
Public review cards link to `/reviews/{guid}`, a focused discussion page with the
original review, independent review/comment spoiler controls, profile links and
literal plain text. Comment lists use URL `page`1..10000,20 rows, server counts,
oldest-first order, invalid/empty/unavailable/rate-limit/retry/out-of-range states.
Anonymous reads and safe login continuation work. Signed-in users can add multiple
public comments; only comment authors see editing and inline delete confirmation.
Review authors gain no authority over other people's comments. Edits replace body
and spoiler flag with the backend's last-write-wins semantics.

Exact proxy routes retain Origin/CSRF/JSON/body/no-store guards. Feature-owned
contracts/API/query/hooks/components/styles stay separate. Public comment and
review-discussion queries survive initial session discovery; confirmed identity
changes still clear caches. Same-account session checks retain drafts but hide,
inert and disable controls and abort writes; identity changes discard old editors.
Unknown write outcomes block resubmission and offer a full reload with a draft-loss
warning; non-idempotent POST never retries automatically. Confirmed edits/deletes
update cached records before independent refresh; confirmed feedback does not
become a failed save after read failure. Profile/review writes invalidate the new
author/discussion caches.

Final **523/523 frontend tests across73 files**, lint/typecheck, production build
and whitespace checks pass. **32/32 real PostgreSQL review-comment integration,
persistence/snapshot/migration tests**, zero skips, pass. Focused missing feature
checks observed RED→GREEN. Fresh review found background review/comment-list
errors unmounting drafts. Reproduced both paths with failing regressions; retained
data/editors with separate retry feedback, and concealed/inert retained404 content.
Follow-up review found no remaining material issues. Tests also cover validation,
unknown POST outcomes, cancelled/late completions, author isolation, spoiler reveal,
delete consent, confirmed-cache updates and persistent deletion feedback.

Real Chrome → Next3100 → API5080 → PostgreSQL verified login continuation,
validation/recovery, POST/spoiler/literal text/newlines, PATCH, cancel/delete and
persisted404, author isolation404 including the review author, anonymous reads and
write401, CSRF400/foreign-Origin403 on all3 methods, URL recovery and native keyboard
skip-to-main. Supplemental22-comment workflow verified Next/Previous/Back and
catalog review→Comments navigation. Both palettes at320/768/1024/1440 passed
no-overflow/runtime-error checks. Sixteen main/final captures inspected; final
capture script resets scroll/focus to avoid full-page screenshots showing the
fixed skip-link outside the live viewport. Back-to-title spacing was improved.
Scripts/artifacts: `/tmp/comments-{browser,finish}.mjs`, `/tmp/comments-*.png`.
No browser request interception or synthetic API responses in live checks.

Local verification initially hit the accumulated registration budget and an empty
catalog. Restarted only the Development API with unchanged limits/settings;
created2 dedicated accounts and1 UUID-identified temporary title, then1 review and
comment fixtures. Guarded transaction matched exact account UUID/username/email,
media UUID/title, review identity and22-comment manifest; removed2 accounts and1
media row, cascading the review/comments. Verified zero matching users/reviews/
comments/media remain. Existing data excluded. Initial cleanup's wrong ownership
column name rolled back before changes; corrected to verified `user_id` columns.
Missing local .NET packages restored before backend tests. No backend/schema/
dependency/configuration file changes, commits, merges, push or deployment.
API5080/frontend3100 remain available. Preserve all current uncommitted work.
Next: Phase7 moderation/reporting in focused slices, then release verification;
external sign-in remains optional and separate. Phase6 completion does not imply
public-launch/deployment readiness.

2026-11-04 follows slice implemented. Public profiles now include Followers /
Following lists with URL-owned direction/page,20-row paging, oldest-first ordering,
profile links, server counts, empty/error/retry and out-of-range recovery. Exact
proxy allowlists retain Origin/CSRF/JSON/body/no-store guards. Anonymous readers
get a safe profile login continuation; login performs no follow action. Verified
nonowners get explicit repeat-safe Follow/Unfollow setters. The existing API has
no point relationship-status endpoint, so no inferred current-state toggle or
unbounded graph scan. Self controls are absent. Session uncertainty conceals/inerts
controls and aborts writes; account changes reset feedback. Known204 feedback is
independent from list refresh; unknown outcomes allow only deliberate safe repeats.

Focused tests observed RED→GREEN. Full frontend suite: **491/491 across66 files**;
lint/typecheck/production build pass. **9/9 real PostgreSQL FollowTests**, zero skips,
pass. Fresh independent review found stale connection-list names after profile
edits; reproduced with a failing regression, fixed by connection invalidation.
Live browser exposed initial session discovery cancelling profile queries and
leaving Loading profile indefinitely; two regressions observed RED→GREEN and
public-profile/connections now survive initial discovery, while confirmed identity
changes still clear caches. Follow-up reviewer found no remaining material issues.

Real Chrome → Next3100 → API5080 → PostgreSQL checks passed: login continuation,
follow/unfollow and repeated writes, exact public counts and both graph directions,
public/anonymous reads, self-control exclusion,401/CSRF400/foreign-Origin403,
Back navigation, invalid/out-of-range recovery and native Tab/Enter skip-to-main.
Both palettes at320/768/1024/1440 have no overflow/runtime errors; all eight captures
visually inspected. Artifacts/scripts live under `/tmp/follows-*`; no intercepted
API responses. Two dedicated accounts and their graph rows were removed by guarded
exact-identity transaction, with zero matching accounts remaining.

Design/plan: `docs/superpowers/{specs,plans}/2026-11-04-frontend-follows*`.
Previous uncommitted profiles work preserved. No backend/schema/dependency/config
file changes, commits, merge, push or deployment. API5080/frontend3100 remain running.
Next focused implementation: review comments, then Phase7 moderation/release.

2026-11-04 public profiles/editing implemented and verified. `/users/{handle}`
shows the existing public DTO, with case-insensitive handle lookup, literal bio
text and member date. Review author names and Account link to profiles. Account
editing updates display name/bio through the existing guarded PATCH proxy.
Feature interfaces, API functions, query hook and CSS Modules remain separate.
User explicitly authorized implementation after the earlier approval discussion.

Verification found and fixed draft loss caused by session-transition remounts,
missing server field associations, stale account/public author caches, unguarded
unknown-outcome resubmission, and the profile route's missing Suspense boundary.
Editor drafts remain mounted but hidden/inert during session checks; identity
changes discard the previous account's editor. Confirmed saves update the account
cache and invalidate public profile, review and collection queries. Validation
and rate-limit rejections preserve editable drafts; uncertain outcomes require a
page reload before another write. Backend same-field concurrency remains
last-write-wins; the form submits display name and bio together.

Final frontend suite: **456/456 tests across62 files**. Lint, typecheck,
production build and whitespace checks pass. **36/36 profile PostgreSQL integration
tests**, zero skips, pass (profile security/cache/concurrency/migration cases).
Real Chrome → Next3100 → API5080 → PostgreSQL verified registration/login,
CSRF profile writes, validation recovery, saved feedback, public links, literal
HTML-looking text/newlines, uppercase anonymous lookup, bio clearing and reload.
Native Tab/Enter skip-to-main passes. Eight public-profile captures cover both
palettes at320/768/1024/1440; automated checks found no horizontal overflow or
runtime exceptions. Representative desktop/mobile public captures and mobile
account capture visually inspected after typography/input fixes.

Browser script/captures: `/tmp/profile-browser.mjs`, `/tmp/profile-*.png`.
Chromium required libraries extracted under `/tmp/profile-browser-libs` because
system dependency installation needed sudo. No system packages were installed.
Dedicated browser verification account deleted in a transaction guarded by exact
UUID/username/email; zero matching records remain. API5080/frontend3100 remain
running, using existing ignored settings with process-only frontend Origin3100.
No backend/schema/dependency/configuration file changes, commits or deployment.
Next: follows in its own focused slice, then review comments; Phase7 stays pending.

2026-11-01 integration: user requested committing the completed collection work
and current homepage edits, merging locally into `main`, and deleting
`feat/collection-metadata` after verification. No remote push or deployment.
Current Git branch/history records the integration result; earlier uncommitted
notes below describe the implementation sessions. Local secrets, browser evidence
and execution ledgers remain ignored.

2026-11-01 Phase 5 collection contents complete on `feat/collection-metadata`.
Implemented typed text/number/boolean/date/select field management, explicit
catalog/custom entry creation, URL entry paging, dedicated entry detail/editing,
and inline field/entry removal confirmation. One local owner editor at a time;
immutable field keys/types and canonical media identities. No backend changes,
migration, dependency additions, provider imports or personal-library writes.

Feature-owned exact JSON/decimal codec preserves the backend decimal range and
unchanged values through Query caching/replacement. Opt-in shared transport
callbacks retain default behavior elsewhere. Stable detail→item→detail reads
prevent mixed-schema editor snapshots, with one bounded read retry. Generic409
preserves drafts without guessing whether it was revision/duplicate/compatibility.
Confirmed mutations close their editor before the separate refresh phase; failed
reads block another write and never replay a mutation. Deletion consent binds to
the exact preview/snapshot and is invalidated by session uncertainty or observed
revision changes; field deletion removes values permanently only after confirmation.

Baseline377 tests; final **442/442 frontend tests across59 files** pass, including
a fresh run after resuming the usage-limit interruption. Lint/typecheck/production
build and whitespace checks pass. **58/58 real PostgreSQL collection integration
tests**, zero skips, pass. Mechanical design scan `[]`. New feature tests observed
RED before implementation; focused regressions also caught QueryClient token
identity loss, stale cache variants, uncertain resubmit and metadata reload/cancel
integration. Independent whole-change review found2 P2s: known-save completion
lost across session-interrupted refresh, and validation errors missing native
control associations. Both reproduced RED→GREEN, then the complete suite passed.
No deferred findings. User homepage edits made during the pause were preserved;
removed only their obsolete unused IconArrowDown import to keep combined lint clean.

Real Chrome→Next3100→.NET5080→local PostgreSQL verification passed **17 workflow
assertions**, plus **3 real30-field stress assertions** and **4 native keyboard
assertions**. No request interception or synthetic responses. Covered all5 field
types, exact28-digit decimal/false/date/select persistence, canonical selection,
duplicate409, title-only precision preservation, real stale entry409, incompatible
schema409, required-add prevention, zero/populated removal previews, stale deletion
consent409/new preview, permanent key-only value removal, public/nonowner reads,
owner-only impact, catalog/library independence and server visibility revocation.
Native Tab/Enter skip-to-main and optional field Space/typing pass. No unexpected
JavaScript exceptions or horizontal overflow. All62 capture files inspected:
40 main screen matrix captures,6 real recovery/confirmation states and16 live
30-field stress captures, both themes at320/768/1024/1440. Fresh visual verdict:
**ship** for the46 main captures and **ship** for the16 supplemental captures.
Documenter updated PRODUCT/DESIGN/collection surface from actual source, preserving
tokens and leaving unrelated format/sidecar drift untouched.

Evidence ignored at `.impeccable/review/collection-contents/`; executable scripts
are `/tmp/ratefreak-contents-{live,finish,stress,keyboard}.mjs`. Live automation
was paced within existing API budgets. A harness selector quoting mistake required
resume on the same account; a later background session check correctly invalidated
deletion consent, so finishing checks explicitly obtained a new preview. The usage
limit interrupted reviewer handoff, not a product failure; the live finish process
completed and its report was verified after resumption.

Cleanup used a guarded transaction matching the exact verification account UUID,
handle and complete3-collection manifest. Removed that account,3 collections,
34 fields and1 remaining item; verified zero matching accounts/collections remain.
Existing account/catalog/library records were excluded. Disposable integration
databases were managed by Testcontainers. Temporary credential state and Chrome
are removed after checks; requested API5080/frontend3100 stay available. No secret
configuration changes, commits, merges, pushes or deployment. Work stays
uncommitted. Next slice: Phase6 public profiles/editing, follows and comments in
focused sessions; optional external sign-in remains separate.

2026-11-01 collection contents design approved. Wrote and self-reviewed the
[fields/entries implementation plan](superpowers/plans/2026-11-01-frontend-collection-contents.md).
Seven tasks cover exact decimal/JSON transport, typed APIs/proxy routes, stable
revision snapshots and separate mutation/read phases, field/value controls,
entry pages/catalog selection, revision-bound deletion confirmation and real
PostgreSQL/browser verification. Native execution preserved; plan review pending.
Documentation-only work; no product/data/configuration changes or tests required.
Whitespace check passes. Preserve all existing uncommitted metadata work.

2026-11-01 next-slice preparation: drafted
[collection fields, entries and schema-change confirmation](superpowers/specs/2026-11-01-frontend-collection-contents-design.md)
from the actual field/item commands, endpoints and numeric validation. Extends
current collection detail with typed field forms, paginated entries, catalog/custom
entry creation and focused entry detail/editing. Defines stable revision-bracket
reads, exact decimal round trips and owner-only deletion preview/confirmation.
Real PostgreSQL/backend/browser verification remains required by the user's
preference. Independent builder check: “nothing important left.” Design approval
pending; no product code, dependencies, configuration,
schema or data changed in this preparation. Preserve the existing uncommitted
metadata work on `feat/collection-metadata`.

2026-11-01 live Phase 5 verification, requested by the user: started the existing
PostgreSQL service and Development API5080 using existing ignored configuration.
Readiness200 and real development frontend proxy collection GET200. Ran
`dotnet test tests/RateFreak.IntegrationTests --no-restore --filter
FullyQualifiedName~CollectionsTests --verbosity quiet`: **58/58 passed**, zero
skipped, against disposable real PostgreSQL. This includes collection metadata,
visibility, security, fields/items and concurrent revision behavior.

Completed **19 real browser assertions** through Chrome → Next3100 → .NET5080 →
the existing local PostgreSQL, with no request interception or synthetic responses.
Verified registration remains anonymous, login continuation, HttpOnly session,
private-default create201/revision1, no-store, anonymous private404, metadata and
public visibility persistence/revision2, uppercase URL reload, public anonymous200,
anonymous write401, real concurrent update/revision3, stale-save409 with preserved
draft and blocked resubmit, explicit reload/Cancel/reopen, final private save on
uppercase route/revision4, revoked anonymous access, My collections and logout.
No browser JavaScript exceptions. Real browser screenshots/report are ignored
under `.impeccable/review/collections-live/`; executable check source is
`/tmp/ratefreak-collections-live.mjs`.

Interruption details: first real browser attempt stayed on session loading amid
Turbopack reloads; API/frontend/Chrome later terminated with SIGTERM (termination
cause unknown). No data created by that first stalled attempt. Resumed using
Webpack locally; two subsequent harness attempts needed a corrected URL assertion
and waits for real session revalidation, not product changes. The final run passed.
Removed exactly the three new verification accounts and three empty collections
from those resumed runs in a guarded transaction checking exact IDs, handles,
counts and absence of fields/items. Verified zero matching users/collections
remain. Existing accounts/catalog/library data were not targeted. Testcontainers
disposed their separate test databases. No schema/configuration/product changes.
API5080 and frontend3100 remain running; temporary Chrome stopped after checks.
Earlier fixture-only/live-unavailable notes below describe the prior run and are
superseded by this live evidence. No commits, pushes or deployment.

2026-11-01 Phase 5 metadata/visibility complete on `feat/collection-metadata`.
Added `/collections` accessible summaries, explicit All accessible/My collections
scope and URL paging, private-default creation and shareable detail pages with
owner-only inline metadata/visibility editing. Uses existing collection APIs;
no backend changes or migrations. Fields, items, schema confirmation and external
sign-in remain separate follow-ups. No collection deletion control or invented
entry counts.

All collection caches are viewer-scoped, including verified anonymous reads.
Identity checks conceal/inert/disable content and cancel reads/writes while
retaining same-account drafts; confirmed identity changes discard them. Editing
retains its original revision through background refreshes. Conflict recovery
preserves drafts and requires explicit replacement with latest metadata. Unknown
create outcomes offer My collections rather than replaying the write. Exact
proxy routes and canonical collection login paths retain existing guards.

Baseline **316/316**; final **377/377 frontend tests across 42 files**, lint,
typecheck, production build and whitespace checks pass. New feature tests observed
RED before implementation. Fresh code review found uppercase GUID cache identity
mismatch and stale cached details after conflict reload; both reproduced with
failing regressions and fixed, then the full suite passed. Reload-cache finding
was treated as material because Cancel/reopen otherwise reintroduced a stale
revision. No deferred findings. Mechanical design scan returned `[]`.

Production browser fixtures cover both themes at320/768/1024/1440 for list, create,
detail and edit (32 captures), plus seven recovery/state captures. All inspected;
no overflow or JavaScript exceptions. Expected HTTP401/404/409/429/503 logs belong
to injected failure cases. Verified native keyboard skip-to-main, scope submission
and browser Back, conflict draft/reload, saved visibility and unknown create
recovery. Fresh visual review found only outdated PRODUCT.md capability copy;
documenter corrected it and recorded the incumbent collection surface without
changing tokens. Reviewer scored that sole finding resolved: **ship**.
Ignored evidence: `.impeccable/review/collections/`; browser script/report source:
`/tmp/ratefreak-collections-browser.mjs`.

Live limit: local API5080 is stopped; read-only request could not connect.
Production preview proxy returned503 under its existing production configuration.
Authenticated/browser reads and writes used intercepted synthetic fixtures, not
live account/database state. Backend tests were not rerun because backend code is
unchanged. No real data writes, credentials/configuration changes, commits, pushes
or deployment. Managed worktree was read-only under this sandbox; used the existing
writable checkout on a feature branch, preserving planning files/dependencies.
Temporary preview3102 and verification Chrome were stopped after checks; no
pre-existing services were changed. Next slice: collection fields/items and
schema-change confirmation. Work remains uncommitted for review.

2026-11-01 Phase 5 design approved. Wrote and self-reviewed the
[implementation plan](superpowers/plans/2026-11-01-frontend-collections.md): exact
proxy/contracts, viewer-aware reads and retained session boundary, browsing,
creation, revision-safe owner editing, and whole-slice verification. Native
execution preserved. Plan review pending; no product code changed and no tests
were run for these documentation-only changes. Whitespace check passes.

2026-11-01 Phase 5 preparation: user selected public/access-aware collection
browsing plus owner metadata/visibility management. Draft
[collection metadata and visibility design](superpowers/specs/2026-11-01-frontend-collections-design.md)
uses the existing backend list/detail/create/update APIs, private-by-default
creation, viewer-scoped caches and explicit revision-conflict recovery. Fields,
items and schema changes remain separate. Design approval was pending at this
preparation point (subsequently approved above); no product
code, dependencies, backend configuration or data changed.

2026-11-01 integration: at the user's request, committed the completed editorial
UI/themes, catalog/provider flows and Phase4 personal-media work and merged it
locally into `main`. No remote push or deployment. Earlier uncommitted-state notes
below are historical; the current Git branch/history records the integration.
Local secrets and verification captures remain ignored.


2026-11-01 resumed after the usage-limit pause. Phase4 implementation and scoped
review were already complete; the documentation agent had written DESIGN.md and
`.impeccable/surfaces/personal.md` before its final handoff failed. Verified the
documents directly against components, styles and stored review evidence, preserving
the existing design system. No product-code changes during this resumption.
Reran **316/316 frontend tests**, lint, typecheck, production build and whitespace
checks successfully. The previously recorded **54/54 backend integration tests**
remain the backend evidence; backend code is unchanged. Restarted dev preview3100
and API5080 with existing ignored configuration/user-secrets; PostgreSQL remains
healthy with its existing volume. No real account/data writes, migrations, commits,
pushes or deployment. Phase4 is complete; next is collection metadata/visibility.


2026-10-31 Phase4 implementation: completed private `/library` status/type filters
and URL paging, title tracking with optional dates, zero-inclusive half-point
ratings, own public-review publishing/editing/deletion, community aggregate and
paginated spoiler-controlled plain-text reviews. Login continuation supports exact
library/title paths. Tracking removal preserves rating/review; deletion confirms
inline. Forms load own saved state before replacement writes and preserve drafts
on errors, conflicts and unrelated review-page changes.

Added the two approved authenticated .NET own-record reads, filtered by session
owner and excluding hidden reviews. Existing DTOs/write semantics retained;
explicit own-review no-store added, no migration. New integration fact observed
405 before routes existed, then **54/54 PersonalMedia integration tests** passed
against disposable PostgreSQL. Exact proxy allowlists add PUT/PATCH/DELETE only
where needed, with existing Origin/attribution/JSON/body/CSRF guards intact.

Fresh review found one P1: background session checks unmounted new personal forms
and discarded drafts. Added regression tests, observed two failures, then fixed
only the new consumers: retain forms hidden/inert/disabled while identity checks,
pause private reads/abort pending writes, resume same-account drafts and discard
on confirmed logout/account change. No existing session privacy policy relaxed.
Final frontend suite **316/316 across 35 files**, lint, typecheck, production build
and whitespace checks pass. Mechanical design scan `[]`. A validation-found startup race also has a deterministic RED→GREEN regression:
initial session discovery preserves only known public query namespaces, while
private/unknown data and confirmed identity changes retain existing clearing.
Scoped reviewer verdict: **ship**, both session corrections resolved; independently
reran24 focused tests. No remaining minors in that scope.

Production browser checks use synthetic APIs and intercepted writes: library and
activity in both themes at 320/768/1024/1440, inline removal, empty/invalid/anonymous,
write error, saved activity, and session-checking states. Review-page navigation
and Back preserve drafts; status edit keeps dates and zero rating saves; tracking
removal leaves rating/review. No overflow/runtime errors. Evidence ignored under
`.impeccable/review/personal/` and `/tmp/ratefreak-personal-review.mjs`.
Live read-only checks through the frontend proxy: public aggregate/reviews200,
new private tracking/own-review401 with no-store. No real-account writes or catalog
changes; disposable backend test data and browser fixtures cover writes.

All work stays uncommitted on `feat/homepage-editorial`. Existing API5080,
PostgreSQL and dev preview3100 remain available; temporary verification processes
are stopped after review. Next slice: collection metadata/visibility. External
sign-in and collection fields/items remain separate follow-ups.


2026-10-31 personal-media slice preparation: user requested the next Phase4 slice.
Draft [personal library, ratings and reviews design](superpowers/specs/2026-10-31-frontend-personal-media-design.md)
covers private library, title controls and public reviews. Existing write APIs
are ready; proposed two small authenticated own-record reads avoid paginated scans
and accidental replacement of saved dates/reviews. Detailed design awaits user
approval; no product/backend code changed in this preparation.


2026-10-31 provider discovery/import: user approved the new detailed design and
requested help enabling existing provider credentials. Implemented dedicated
`/catalog/discover` search and provider/type/ID preview routes, explicit native
filters with URL/history state, attribution, honest pagination without totals,
disabled/empty/error/retry states and catalog entry links. Preview offers login
continuation with a strict canonical internal-path allowlist. Authenticated import
sends only identity through the existing CSRF boundary, suppresses duplicate and
stale account/route results, reports created/already-present and invalidates catalog
queries. No personal-library writes or automatic mutation retries/replays.

Local runtime: `.env` provider flags were already true, but .NET does not load
that file automatically. Copied existing IGDB and MusicBrainz settings privately
via stdin to Development user-secrets, then restarted the API. All three capability
flags are now true. Configured a generated matching browser-proxy secret in API
user-secrets and ignored `web/.env.local` with loopback attribution, required by
existing mutation guards; exact frontend origin is 3100. No credentials were logged
or added to tracked files. Ordinary `dotnet run` now retains enabled providers.

Live verification through the frontend proxy: TMDB search200/20 results and
Star Wars preview200; IGDB search200/14 and The Witcher IV preview200; MusicBrainz
search200/20 and Year Zero preview200. No live import/account/catalog writes.
Synthetic production browser checks cover login continuation and import201/200/503,
Back/Forward, keyboard skip/focus, both themes at 320/768/1024/1440 (16 captures)
plus 7 state captures, with no overflow/runtime errors. Evidence is ignored
`.impeccable/review/providers/` and `/tmp/ratefreak-provider-review.mjs`.
Final full suite **260/260 across 31 files**, lint, typecheck and production build pass;
mechanical design scan `[]`. Fresh combined code/UI review: **ship**, no material
findings; independently reran94 focused tests. Documentation records completed
capabilities. Compact-test formatting remains a nonblocking maintenance observation.
Development preview3100, API5080 and PostgreSQL remain running; temporary production
preview3102 and headless Chrome were stopped after verification.
Current work remains uncommitted on the existing feature branch. Next slice:
personal library statuses, ratings and reviews. External sign-in stays separate.


2026-10-31 next-slice preparation: provider preview/import is the next catalog
slice. Draft design: [provider search, preview and import](superpowers/specs/2026-10-31-frontend-provider-import-design.md).
Live capabilities confirm TMDB enabled and MusicBrainz/IGDB disabled; keep current
provider configuration. Proposed dedicated discovery/preview routes reuse the
editorial system and restrict import to an explicit authenticated action. This
new slice's detailed design awaits approval; no provider product code changed.


2026-10-31 local API follow-up: at the user's request, started the existing
PostgreSQL Compose service and Development API on5080. Created ignored
`web/.env.local` with the API origin and frontend origin3100; existing API
configuration has no browser-proxy shared secret enabled. Readiness returns200
and the real frontend proxy GET media returns200 with two existing catalog
records. Earlier fixture-only verification remains accurate for that run; live
list/detail/filter reads are now verified. No database migrations or data writes.
API, PostgreSQL and existing frontend preview remain running.


2026-10-31 catalog implementation: completed the approved browsing/details slice
on the existing `feat/homepage-editorial` branch, preserving the approved design
and all earlier uncommitted work. `/catalog` supports explicit title/type search,
20-item URL pagination, Back/Forward restoration, invalid-filter recovery,
loading/error/429/retry, empty/no-match and out-of-range states. GUID detail pages
show available fields with honest missing-artwork/date/description fallbacks and
a distinct missing-title state. Catalog navigation is active for list/details;
the homepage has a working catalog entry point. Dark/blue remains the default.

The restricted proxy adds only GET media and GET media/GUID; list parameters are
validated/re-encoded, duplicates and unknowns rejected, all other route/method,
origin/cookie/attribution/cancellation/no-store protections preserved. Contracts
match the C# catalog records. Artwork supports only inspected canonical HTTPS
TMDB/IGDB provider formats; no arbitrary hosts or generic image proxy.

Verification: baseline **134** tests. New query/API/proxy and real QueryClient DOM
tests cover filters, pagination, late responses, URL restoration, retry/429,
empty/detail/missing states, long titles, failed and untrusted artwork. Final
checks: **189/189 tests across 25 files**, lint, typecheck, production build and
`git diff --check` pass. Fresh independent code/UI review: **ship**, no material
findings. Minor observation: single-column artwork makes long lists scroll-heavy
at 320px; retained as the approved artwork-led layout. Mechanical design scan: `[]`.
Production build required a Suspense boundary around runtime detail params under the
installed Next.js Cache Components rules; build passes after that correction.
Browser checks passed in both themes at **320/768/1024/1440px** (16 list/detail
captures), plus six recovery/error captures. No overflow or runtime errors.
Keyboard verifies skip link/focus and Enter submission; browser history verifies
pagination/search/detail round trips. Real Next proxy verifies canonical query
forwarding and local 400/404/no-store rejection. Ignored evidence:
`.impeccable/review/catalog/`, `/tmp/ratefreak-catalog-review.mjs`.

Integration limit: the .NET API on 5080 was unavailable. Browser/proxy integration
used a disposable synthetic upstream on 5090, with no real account/catalog writes;
it does not prove a live database/API connection. Development preview on 3100 uses
its existing configuration; no synthetic records or fixture mode ship in the app.
Next slice: provider preview/import (still unimplemented). No automatic commits,
pushes, deployment, backend changes or migrations.


2026-10-31 theme adjustment: user selected **Dark as the default** and **blue**
instead of the dark theme's pink accent. HTML/CSS, pre-paint initialization,
server control state and invalid/removed-preference fallbacks now default Dark.
Saved Light/Dark/System choices remain respected. Dark accent is `#91b9ee`,
with coordinated hover/pressed/on-accent colors; Light retains its oxblood palette.
Four updated assertions observed failing before implementation, then the full
**134/134 tests** passed; lint, typecheck and production build pass. Production
browser verifies dark first frame even on a light device, selected moon button,
saved Light after reload, no runtime errors and no overflow at320/1440px.
Blue button contrast is **7.53:1**. Design scan clear. Local captures/report:
ignored `.impeccable/review/blue/`. Development preview remains on3100; temporary
production3102 and headless Chrome stopped. Still uncommitted; catalog stays queued.

2026-10-31 icon/SVG refinement: at the user's request, replaced the theme dropdown
with accessible sun/moon/monitor icon buttons and replaced the homepage raster
collage with original inline SVG. Added Tabler React icons and updated the lockfile.
The old raster file is removed. Current source/visual guidance is in `DESIGN.md`.
Still uncommitted on `feat/homepage-editorial`; catalog remains approved/queued.

2026-10-31 follow-up: user approved the homepage and requested the existing
account pages match it, plus a dark app theme. Home, login, registration and
account now share the editorial system with a Light/Dark/System header control.
Still uncommitted on `feat/homepage-editorial`; catalog remains approved but
unimplemented. Latest app-theme evidence below supersedes the homepage-only
palette behavior in the earlier record. No new npm dependencies or backend work.

2026-10-31: user approved the Phase 3 catalog design/plan, then requested a
homepage redesign first. Selected editorial culture journal (warm ivory, ink,
oxblood, media collage) and code-first native implementation. Work is on
`feat/homepage-editorial` in the existing checkout; no commit or push. Phase 3
implementation has not started. The next slice keeps its approval and uses the
new visual direction in root `DESIGN.md`; do not repeat the direction interview.

Homepage implementation and browser evidence are below. The previous branch
and merge history remains recorded in the 2026-10-30 notes.

2026-10-30: approved roadmap, written design and Phase 1 task plan; completed
Phase 1 using native execution on `feat/frontend-foundation`. Phase 1 was committed
as `f68535e` at the user's request, then Phase 2 was implemented on the same branch.
Phase 2 was committed as `8834c6f`; both commits were fast-forwarded into local
`main`. The merged `feat/frontend-foundation` branch was deleted. Nothing was
pushed. Start the next implementation slice on a new branch from `main`.
Backend changes affect proxy attribution and rate-limit partitioning only;
no migrations or provider settings changed. Tests were not rerun for the
commit/merge, at the user's request; the implementation evidence below is retained.

At the last runtime check, only the frontend preview on port 3100 was running;
the API was stopped. Recheck processes in the next session. Account functionality
needs the API/database and matching server-only proxy settings documented in
`web/README.md`; the isolated verification fixture was shut down and removed.

## Phase 1 evidence

- [x] Vitest/Testing Library harness; object contracts and props in separate
  `interfaces/` files. Node types updated to match the Node 24 runtime.
- [x] Restricted fixed-origin GET proxy (`auth/csrf`, `users/me`), separate cookie
  headers/deletions, no-store, sanitized errors and timeout/cancellation handling.
- [x] Browser client: validated paths, JSON/204, Problem Details/validation errors,
  Retry-After, cancellation and fresh/coalesced CSRF token issuance.
- [x] TanStack Query v5 provider: stable mounted cache, independent clients,
  one-minute freshness, five-minute GC, focus refetch and bounded transient retries.
- [x] Responsive server-rendered shell/homepage and shared Button/loading/empty/error
  components. No nonfunctional search/login controls or dead feature links.
- [x] Test-first focused checks; final `pnpm --dir web test`: **84/84 tests pass**
  across 12 files. `lint`, `typecheck` and `build` all exit zero. Build succeeds
  without API environment configuration; homepage is static, proxy is dynamic.
- [x] Production HTTP smoke: homepage 200; unconfigured enabled proxy route 503;
  unknown GET route 404; POST/HEAD/OPTIONS 405; proxy responses use no-store.
- [x] Headless Chrome at 375px and 1280px: screenshots inspected, no horizontal
  overflow or runtime exceptions; Tab to skip link, Enter focuses main,
  Shift-Tab and Home navigation work. Screenshots saved in `/tmp/ratefreak-phase1-375.png`
  and `/tmp/ratefreak-phase1-1280.png` (temporary local artifacts).
- [x] Independent whole-change review. Fixed error-body cancellation being converted
  into ApiError, plus the analogous token-body path and signal-completion race;
  four regression tests observed RED→GREEN, followed by the full green suite/build.

## Phase 2 evidence

Plan: [frontend accounts](superpowers/plans/2026-10-30-frontend-accounts.md).

- [x] Thin `/register`, `/login`, `/account` routes, feature-owned interfaces/APIs/hooks,
  labeled forms and field errors, generic login errors, 429 guidance and protected
  account display. Registration stays anonymous; logout explains all-device revocation.
- [x] Exact-Origin/CSRF/JSON/body-size guarded POST account allowlist. Other methods
  and business routes remain closed; server-only attribution never creates a user.
- [x] Fixed the shared proxy-IP budget mechanism through authenticated attribution;
  eight backend HTTP tests prove independent client budgets, mapped-IP normalization,
  forged-header rejection and unchanged direct-client behavior. See ADR 0013 and
  `web/README.md` for matching server secrets and verified runtime-mode requirements.
- [x] Session transitions cancel old requests, clear caches and pending CSRF state;
  submitted passwords are absent from mutation variables/history and browser storage.
- [x] Independent review found unbounded session freshness. Fixed with active-tab
  one-minute polling, window focus/pageshow and protected-entry checks, plus
  credential-free BroadcastChannel invalidation. Tests cover expiry without manual
  refresh and two independent clients changing accounts/signing out.
- [x] Browser-discovered competing logout redirects reproduced RED and fixed GREEN:
  protected-page logout now goes to login; public-page logout remains on that page.
- [x] Final frontend checks: **123/123 tests across 19 files**, lint and typecheck
  pass without warnings; production build passes with static account templates and
  a dynamic API proxy. Required build/test subprocesses ran outside the sandbox.
- [x] Full backend suite: **590/590**, no failures/skips (64 unit, 154 provider,
  372 integration). No provider credentials/live external requests needed.
- [x] Real browser flow through Next.js → .NET → isolated PostgreSQL: anonymous
  protected-page redirect; registration 201 remains anonymous; rejected login clears
  password; successful login creates HttpOnly session/CSRF cookies; private profile
  loads; logout 204 removes session cookie; another open tab redirects to login
  and `/users/me` returns 401. No browser runtime exceptions.
- [x] Screenshots inspected at 375px and 1280px, no horizontal overflow, keyboard skip
  navigation works. Temporary artifacts: `/tmp/ratefreak-phase2-login-375.png`,
  `/tmp/ratefreak-phase2-register-1280.png`, `/tmp/ratefreak-phase2-account-{375,1280}.png`.
  Verification used a disposable database/container and temporary processes, not
  existing accounts or persistent API configuration.

## Next session and release limits

### Homepage redesign evidence — 2026-10-31

- Replaced the dark slogan/pill/three-column homepage with an editorial lead,
  warm paper/oxblood palette, self-hosted Fraunces and DM Sans, and original local
  WebP collage. Font licenses and artwork generation prompt are retained with
  the assets. No new npm dependencies or backend changes.
- Added a working registration entry point and an in-page planned-features link.
  Library/review/collection copy remains explicitly future functionality; no
  fabricated media records, activity, ratings or social proof.
- Theme applies to the homepage and shell through the active navigation pathname.
  Hidden cached homepage DOM cannot keep the paper theme on account routes.
  Existing shell test covers the retained-DOM route transition.
- Final frontend suite: **123/123 across 19 files**; lint, typecheck and production
  build pass. Homepage remains statically prerendered. `git diff --check` passes.
- Production headless Chrome captures inspected at **320, 768, 1024 and 1440px**:
  loaded artwork/fonts, no horizontal overflow and no runtime exceptions. Tab
  focuses Skip to content; Enter focuses main. Planned-features anchor works;
  primary link opens registration; its original dark palette returns, and Home
  navigation restores ivory. Screenshot/report artifacts are local and ignored
  in `.impeccable/review/`.
- Browser verification fixtures only `GET users/me` as anonymous (401); it does
  not exercise live account writes or require API/database/provider configuration.
  Previous Phase 2 live account evidence remains unchanged.
- Mechanical design scan reported advisory font-popularity warnings for Fraunces
  and the pre-existing Arial account fallback. Fraunces is the deliberate editorial
  display face; the account theme is outside this redesign's scope.
- Independent fresh design/code review disposition: **ship**, no material fixes.
  It reviewed all four production captures and the changed design/source files.
- Left the development preview on **http://localhost:3100** for user review.
  Stopped temporary production servers on 3101/3102 and headless Chrome. No API
  or database was started for this redesign.

### Editorial account pages and app themes — 2026-10-31

Plan: [editorial app themes](superpowers/plans/2026-10-31-editorial-app-themes.md).

- Applied the approved editorial typography, wordmark, palette, buttons, form
  fields, feedback and spacing to login, registration and account. Desktop uses
  a heading/form split; mobile stacks in reading order. Account details are
  simple labeled rows. Authentication/session logic remains unchanged.
- Replaced homepage-specific palette selection with app-wide Light/Dark/System
  themes. Light remains the first-visit default. Dark uses warm ink, cream and
  rose; original artwork remains an illustrative printed collage.
- Native labeled header select; browser-only `ratefreak.theme` persistence,
  pre-paint static initialization, device changes in System mode, other-tab
  synchronization and usable switching when storage is blocked. Theme changes
  do not remount forms, clear inputs, alter sessions or persist account data.
- Theme tests observed **9 failing assertions → 9 passing**. Additional control
  tests cover saved selection, labeled options and preserving adjacent input.
  Full suite: **134/134 across 21 files**. Lint, typecheck and production build
  pass. Home and account route templates remain statically prerendered.
- Inspected **16 production screenshots**: Home/login/register/account in both
  themes at 320px and 1440px. All fit without horizontal overflow; artwork and
  local fonts load. Also checked account-page layout widths at 768/1024px.
  Captures and report: ignored `.impeccable/review/themes/`.
- Real keyboard ArrowDown/Enter switches the native select. Theme changes preserve
  actual email/password nodes and entered values; failed login still clears the
  submitted password and preserves email. Error guidance remains present across
  theme changes. Tab/Enter skip navigation still reaches main.
- Reload with a saved Dark preference is dark on the first rendered frame.
  Browser device emulation switches System appropriately; explicit Dark overrides
  a light device setting. A real second page's localStorage change updates the
  first page's theme/control. No application runtime exceptions or console errors.
- Measured text contrast: light body 13.35, muted 5.44, primary action 8.56,
  error 6.04; dark 14.26, 8.40, 7.69 and 7.85 respectively. Input/selector borders
  are at least 3.10. Mechanical design scan returned **no findings**.
- Browser verification uses intercepted API responses for an anonymous session,
  a synthetic signed-in account and login validation failure. No live account
  credentials, writes, database or provider calls. Prior live auth evidence remains
  historical; backend suites were not rerun for these frontend-only changes.
- Independent fresh finish review: **ship**, no material fixes after reviewing
  all 16 final production captures and the theme/page source. Left the development
  preview on **http://localhost:3100**; stopped the temporary production server
  on 3102 and headless Chrome. No commit, push or deployment.

### Icon controls and SVG artwork — 2026-10-31

- Replaced the dropdown with three native theme buttons using Tabler sun, moon
  and desktop icons. Accessible names, native tooltips, aria-pressed state,
  visible focus and 44px targets preserve all three choices and keyboard use.
  Homepage links also use small Tabler direction icons.
- Removed `culture-collage.webp` and its page dependency. Original inline SVG
  in `features/home/components/culture-artwork.tsx` represents the four media
  formats with crisp geometry and semantic theme colors, plus an accessible label.
- Updated theme-control assertions observed **2 failing tests → passing**; final
  full suite remains **134/134 across 21 files**. Lint/typecheck/build pass.
  Build and temporary browser/preview commands required sandbox escalation;
  successful build evidence supersedes the blocked TypeScript subprocess attempt.
- All 16 production captures inspected at 320/1440px across Home/login/register/
  account and both themes: no overflow. Account layouts also checked at768/1024px.
  Native Enter activates Dark, preserving typed fields. Reload/first-frame theme,
  System changes, explicit override, real second-page storage sync and skip
  navigation pass. No runtime/console errors; API responses are synthetic fixtures.
  The initial automation omitted Enter's carriage-return text; correcting the
  event payload verified behavior without changing application code.
- Design scan: **no findings**. Independent fresh finish review: **ship**,
  no material fixes. Captures/report are ignored in `.impeccable/review/icons/`.
- Left the development preview on **http://localhost:3100**. Temporary production
  preview3102 and headless Chrome stopped; no commit, push or deployment.

### Catalog and release work

Local account Phase 2 is complete. External Google/GitHub sign-in, callback/correlation
cookie handling and explicit identity linking are a separate optional follow-up.
The next planned feature slice is Phase 3 catalog browsing/search/details,
using the existing transport; external sign-in is optional and does not block it.
Future private query keys must include account identity and every filter.

Configure the same proxy secret on both servers and the appropriate trusted mode
before using the account UI. Production Vercel headers/HTTPS/secrets have not been
provisioned or verified by local tests; another ingress needs a verified adapter.
Cross-instance shared rate budgets, recovery policy and deployment hardening remain
release work. Live client disconnect propagation through a deployed ingress remains
an operational check; unit tests cover abort signals/timeouts and live local
navigation cancellation was observed reaching the proxy.

## Implementation decisions

- Kept the existing checkout on a new local feature branch to preserve the approved
  uncommitted planning files and installed dependencies. Files share this checkout;
  no filesystem-isolated worktree was created.
- Used `vitest.config.mts`, current oxc JSX options and per-file jsdom directives
  for Vitest 5 compatibility. Future DOM tests must include the directive.
- Updated `@types/node` from 20 to 24 to satisfy Vitest's peer requirement and
  describe the actual runtime. No runtime Node upgrade was needed.
- Deferred the shared backend-IP bucket to the explicit Phase 2 blocker above;
  Phase 2 now implements authenticated attribution. Omitting/misconfiguring the
  corresponding backend setting still leaves proxied callers sharing a budget.
- Phase 2 uses an explicit Vercel trust mode instead of assuming static Next egress
  IPs; self-hosted production support requires a separate ingress adapter.
- Session synchronization and bounded checks supersede the initial infinite-freshness
  implementation considered during Phase 2. Other-device revocation is discovered
  at the next check; same-browser windows receive immediate invalidation signals.

No deferred minor code-review findings.

## Suggested next-session prompt

> Continue RateFreak frontend Phase7 release verification after reporting and staff moderation, then
> release verification. Phase6 profiles/editing, follows and review comments are
> complete, including real backend/browser checks. Read this checklist, the roadmap,
> web/README.md, web/AGENTS.md and DESIGN.md first. Preserve all current uncommitted
> profiles/follows/comments work; do not assume integration into Git has happened.
> Reporting/staff moderation are implemented; read the newest resume notes and Git status.
> Ground release checks in actual API/authorization contracts and current operational settings.
> Keep interfaces separate, components focused and TanStack Query for server state.
> Verify with dedicated local data and guarded cleanup. No automatic commit, merge,
> push, deployment, provider activation or promotion of real accounts to staff.
> External sign-in remains an optional separate follow-up.
