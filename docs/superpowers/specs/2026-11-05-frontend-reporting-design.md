# Phase 7 first slice: public content reporting

Status: approved by the user on 2026-11-05; implemented and verified in the approved native plan.

## Intent and scope

Let signed-in readers report another person's public review or comment using
the existing reporting API. Continue Phase 7 in focused slices: public reporting,
staff moderation, then cross-feature release verification. External sign-in stays
an optional separate follow-up.

## Approaches

1. **Public reporting first (recommended):** add reports to existing public
   content surfaces and verify their security/session behavior independently.
   Staff tools follow in a separate slice. This gives each write workflow a
   focused review and verification boundary.
2. **Reporting and staff tools together:** delivers the full handling workflow
   at once, but combines public forms, privileged reads, concurrent moderation
   writes and staff navigation into a substantially larger change.

The user approved the first approach on 2026-11-05.

## Existing contracts

`POST /api/v1/reviews/{id}/reports` and
`POST /api/v1/comments/{id}/reports` require authentication and antiforgery,
and use the reporting rate budget. Requests contain `reason` and optional
`details`; successful responses are 201 with report ID, status and creation time.

Reasons are `spam`, `harassment`, `hate`, `sexual_content`, `violence`,
`spoiler`, and `other`. Trimmed details are limited to 1000 characters and are
required for Other. The server applies its existing text validation.

Own-content reports are invalid. Missing/hidden content returns 404; a comment
whose parent review is hidden is also unavailable. A reporter can report a given
target only once, even after resolution; duplicates return 409. There is no
reporter-facing report history, status lookup, edit or withdrawal endpoint.

## Proposed interface

Add a quiet Report action to catalog public review cards, the discussion's
original review and public comment cards. Do not offer it on known own content.
Anonymous readers receive a safe login continuation to the current public page;
logging in never submits a report.

Use an inline form, one per target, with a labeled reason select, labeled details
textarea, contextual Other requirement, public-content identification, Submit
report and Cancel. Reuse the editorial design tokens and existing native form,
focus, error and responsive conventions. The report form must not reveal a
concealed spoiler. Explain that report details are sent to moderators; do not
claim anonymity, a response time or a guaranteed moderation action.

Confirmed creation replaces the form with Report submitted. A duplicate response
shows You have already reported this content and disables another submission.
These states are local to this account and mounted target, not invented persistent
history. No optimistic success or automatic POST retries.

Validation and rate-limit rejection preserve drafts and permit deliberate retry.
Unavailable content and forbidden/expired sessions receive clear recovery text.
Network errors, timeouts or cancellation after submission may have committed:
preserve the draft, block resubmission and offer a full reload with a draft-loss
warning. A reload may lead to a server-confirmed duplicate response.

## Architecture and session behavior

Keep reporting interfaces, API functions, hooks and components feature-owned and
separate. Extend only the two exact POST proxy routes; retain Origin, CSRF, JSON,
body-size, cancellation and no-store guards. Reuse TanStack Query mutation
infrastructure with retries disabled and existing write/session protections.
Report details must not enter URLs, browser storage or retained mutation history.

During session checks retain same-account drafts but hide, inert and disable
controls and abort writes. Confirmed identity changes discard drafts and report
feedback. Public content refreshes must not silently unmount active drafts or
replay submissions. Confirmed submission feedback stays successful if a later
public read fails. Reporting changes no public content directly, so no broad
public-query invalidation is needed.

## Verification

Use focused tests for reason/details validation, own-content exclusion, anonymous
continuation, exact proxy allowlisting/security, 201/409/404/429 outcomes,
uncertain outcomes, session transitions, draft retention and late completions.
Observe new behavior tests failing before implementation, then passing. Run the
full frontend tests, lint, typecheck and production build.

Verify real browser-to-API reporting with dedicated local accounts and content,
including duplicate persistence, review/comment targets and authorization guards.
Clean up only exact dedicated fixture identities with guarded database operations.
Check keyboard/focus, concealed spoilers, both palettes and 320/768/1024/1440px
layouts; inspect captures and check overflow/runtime errors.

## Later slices and limits

Staff moderation will cover open/resolved reports, live target review, explicit
resolution/visibility reasons, exact `expectedTargetUpdatedAt` concurrency tokens,
403 revocation handling and audit reads. Account DTOs expose no roles, so staff
navigation must be designed around actual authorization responses rather than
inferred privileges. Moderator administration requires its own scope decision.

No backend/schema/dependency changes, real-account staff promotion, provider
activation, commits, merges, push or deployment are authorized by this proposal.
Phase 7 remains incomplete until staff work and release verification are delivered.
