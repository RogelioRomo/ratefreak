# Account export and deletion

Date: 2026-11-09.
Status: approved by the user, including retention periods and a final deletion
confirmation dialog. Backup retention approved at 30 days.

## Intent and scope

Give people control over the data they have put into RateFreak. Add a Data and
privacy section to account settings with a downloadable export and permanent
account deletion. Use the existing .NET API, PostgreSQL, Next.js proxy, cookie
authentication and private query-cache isolation.

The user approved retaining public custom catalog items and categories without
account attribution, while deleting private content and personal contributions.
Detailed implementation choices below are builder decisions within that direction.

This slice includes export, deletion, ownership confirmation, artwork cleanup,
session invalidation, and documentation. It does not add account suspension,
email verification, email recovery, account merging, a deletion grace period,
or general email/password changes. No real account is deleted while implementing
or testing this feature. Tests use disposable databases and artwork directories.

## Approaches

1. Delete every contribution, including public catalog entries. Simple ownership
   semantics, but damages other people's libraries and collection references.
2. Permanently delete personal data and private contributions, preserving public
   catalog entries without account attribution. The recommended approach, accepted
   by the user. Requires explicit ownership detachment and a database constraint
   change; shared entry IDs and visibility remain stable.
3. Deactivate accounts and keep their data for a recovery window. Adds reversible
   state, scheduled erasure, and retention complexity. Deferred; this slice offers
   permanent deletion with an explicit confirmation instead.

## Export

An authenticated, CSRF-protected POST generates a ZIP download directly for the
requesting browser. Do not email it, expose a public URL, or keep a server-side
archive after the response. Include a versioned manifest, UTC export timestamp,
and readable JSON files using camelCase property names:

- Account ID, handle, display name, email, bio, creation/update timestamps.
- Linked provider names, without provider subjects, tokens or credentials.
- Library tracking data, ratings, reviews and authored comments, including hidden
  personal contributions and referenced catalog IDs/titles.
- Owned collections, membership order and catalog references.
- Following/follower account IDs and public handles; no other accounts' email or
  private profile information.
- Owned custom items and categories, including private/archived/hidden state and
  current artwork. Include stored WebP artwork files and JSON-to-file mappings.
- The account's recovery-event action/timestamp history and reports it submitted.
  Reports include its own reason/details/status; exclude staff notes, internal
  moderation events, other reporters, and private content written by others.

Exclude password/recovery hashes, saved recovery codes, security/concurrency
stamps, cookies, CSRF values, OAuth subjects, storage paths and server secrets.
Provider artwork URLs are references; do not fetch third-party images for export.

Obtain a consistent database snapshot. Open local artwork while holding the
existing catalog/artwork coordination lock so replacements and cleanup cannot
produce a mixed export. Use streams and a private temporary file to prepare a
complete ZIP before sending response headers; cap total archive generation at
256 MiB and return an actionable error without deleting account data if exceeded.
Always remove temporary files on success, failure and cancellation. A missing
referenced artwork file fails the export explicitly; do not silently omit data.
Use fixed/UUID-derived archive paths, never user-supplied titles as filesystem paths.
Return attachment disposition and Cache-Control: no-store. Export generation has
an account-specific limit of two attempts per ten minutes and is never retried
automatically by the browser.

Extend the browser proxy with a dedicated export response path: preserve the
attachment filename, enforce the same 256 MiB limit, stream rather than buffer
the upstream archive, use a bounded five-minute generation/download timeout,
propagate cancellation and keep no-store headers. Ordinary API requests retain
their existing timeout and response behavior.

## Confirming ownership

Export and deletion expose sensitive data or have permanent consequences. Both
require fresh ownership confirmation in addition to the authenticated session:

- Accounts with a local password confirm their current password. Bound input to
  128 characters, use Identity's password verification and existing durable
  account-level failure budgeting; do not log or cache the supplied password.
- Provider-only accounts confirm through an already-linked Google/GitHub identity.
  Extend the existing OAuth challenge flow with a dedicated reauthentication
  purpose, bound to account ID and current security stamp. It cannot register an
  account, link another identity, or switch the current account. The callback must
  match an existing provider/subject pair on that same account.
- Successful confirmation issues a cryptographically random, single-use grant,
  valid for five minutes, bound to account ID, security stamp and operation
  (export or delete). Persist only its purpose-bound hash and expiry, not plaintext.
  Pass plaintext to the browser through a short-lived HttpOnly, Secure, SameSite
  cookie limited to the account lifecycle API path; never a URL/query parameter.
- Consume the grant atomically with operation authorization. Replay, wrong
  operation, changed identity, expired grant, logout and recovery must fail.
  Expired grants are cleaned up; account deletion cascades its grants.

Use existing Origin/proxy and antiforgery protection for all start/confirm/action
POSTs. Keep ownership-confirmation failures generic, with a bounded per-account
and per-IP request budget. Existing process-local budgets still require the
documented production perimeter protections.

Extend both the ordinary proxy cookie allowlist and OAuth completion handling for
the lifecycle grant cookie and reauthentication completion response. Normalize
cookie paths for the browser proxy and preserve existing provider login/link
responses. Test browser-to-proxy-to-API confirmation and export end to end at the
HTTP contract level, rather than testing only the backend service.

## Deletion behavior

Open a modal confirmation dialog before the final request. In that same dialog,
explain what is removed and preserved, offer export,
and require typing the exact current account handle. Deletion does not happen
automatically after a provider callback or after downloading an export.

| Data | Result |
| --- | --- |
| Account/profile, email, password, linked logins, tokens, roles, recovery verifier/events, lifecycle grants | Delete |
| Library entries, ratings, reviews, authored comments, follows, owned collections/memberships | Delete |
| Comments by others on a deleted review | Delete with the parent review; explain this dependency |
| Custom items with public visibility, including hidden/archived ones | Preserve ID/content/artwork; remove creator attribution; preserve all visibility/moderation flags |
| Public categories | Preserve ID/name/aliases/visibility flags; remove owner attribution |
| Private custom items and their artwork | Delete; remove dependent memberships, library entries, ratings, reviews/comments and mappings before deleting the item |
| Private categories | Delete after their private items and any aliases; never publish them implicitly |
| Provider-backed catalog entries | Preserve |
| Other people's references to retained public items | Preserve |

Private items that were once public can still have other people's references or
reviews. The confirmation text must state that deleting private items also removes
their dependent references and discussions. Do not preserve private titles or
artwork in an anonymously owned public item.

Unowned custom items keep user provenance. Update the catalog provenance
constraint to permit a null creator only for retained public custom items; do not
misclassify them as provider/system entries or introduce a synthetic account.
Existing owner-only commands must continue denying edits to these items.
Increment revisions on ownership changes so stale owner edits cannot succeed.
Update moderation target queries to use optional authors instead of inner joins
to live accounts. Retained entries must remain reportable and available to staff;
show a neutral Deleted account author label without inventing a live user.
Make the original-author report field nullable for newly reported unowned entries,
and update self-report/conflict checks to handle absent authors explicitly.

### Moderation data

Preserve moderation decisions and visibility protections; deletion must not undo
hiding or remove abuse evidence indiscriminately. Replace references to the
deleted account in original reporter/author, resolver and actor fields with a
fresh random pseudonym for this deletion, with no retained lookup back to the
account. Set the live reporter foreign key to null, since a pseudonym is not a
live account. Do not reuse the deleted account's UUID as that pseudonym.
Also replace moderation-event target IDs where the target type is account and
the target ID is the deleted account, including role appointment/revocation.
Remove details supplied by the deleted reporter, substituting a fixed deletion
notice where the report constraints require text. Retained staff decision text
can contain identifying information: describe these records as pseudonymized,
not guaranteed anonymous. Do not export staff-only material.

Approved bounded retention: recovery events expire after 90 days; resolved
reports and related moderation events expire after 365 days. Open reports remain
until resolved; deletion does not close reports about retained public content.
Use a bounded recurring cleanup job, UTC timestamps and tests at the time
boundaries. Expiring a report must also remove its related events in one
transaction; standalone moderation events expire independently after 365 days.
This is a product retention policy, not a claim of legal compliance. Retention
exceptions/legal holds are outside this slice and require a later policy decision.

### Safety and concurrency

Prevent self-deletion of the last administrator. Return a conflict explaining
that another administrator must be appointed first, using the existing offline
administrator tooling. Serialize this check across concurrent admin deletions.

Perform database erasure and detachment in one transaction under the existing
moderation/catalog coordination lock and an account row lock. Follow existing
lock ordering; re-read account/security stamp under the lock. Coordinate with
profile, collection, personal-media, follow, provider-link and recovery writes:
a concurrently authenticated request must not recreate ownership or commit
personal data after deletion. Database foreign keys remain the final guard;
translate expected deleted-account races rather than returning accidental 500s.
In particular, category/item creation must recheck that the actor still exists
after acquiring the coordination lock; an earlier middleware check is insufficient.

After commit, sign out and expire relevant cookies. The missing account makes all
other sessions invalid on their next protected API request. In the browser, use
the existing exclusive identity transition to cancel requests, clear private
queries/CSRF/drafts and notify other tabs. Redirect to a public confirmation page.
Unrelated routine background session checks remain silent.

Remove private artwork files after the database commit; failure must not roll back
or misreport successful account deletion. Attempt immediate cleanup, then let the
existing hourly orphan sweeper retry files older than 24 hours. Never delete a
file still referenced by retained public content. Log only sanitized cleanup
failures, without account email, authored text, passwords or confirmation grants.

Deletion is irreversible in the live app. Backup copies expire according to the
operator's documented backup schedule; do not promise immediate removal from
backups. A restore must replay completed erasures before serving traffic. The
deployment runbook must define the protected erasure record and restore process.
Use a preprovisioned, durable append-only erasure journal outside database/artwork backups containing
only the erased account UUID and completion time, never credentials or profile
text; it must not map to moderation pseudonyms. Journal retention must outlast
all restorable backups. Before serving a restored database, re-run erasure for
the journal's account IDs, including private artwork, and then run orphan cleanup.
Record an erasure request durably before deletion and mark completion after commit;
an incomplete record is a pending erasure that must be completed, not discarded.
Provision and flush the empty journal and its parent directory before enabling
this feature. Runtime appends require an existing file and flush before returning;
a non-newline-terminated crash tail can be discarded because it never completed
that protocol, while malformed complete records fail closed. The journal is
operator-only security data excluded from user exports. Failure to
durably record the request must prevent deletion from beginning. This local
feature is not approval of a production backup retention schedule.

## User interface

Add Data and privacy to the existing accessible account tabs. Explain export
contents and deletion consequences before asking for credentials. Keep the
destructive control visually separate and explicitly named Delete account.
Show pending state only for the action the user initiated; preserve the current
silent background session behavior. Never store password, grant or export data
in query caches, localStorage or sessionStorage. Clear credential inputs after
confirmation and on confirmed identity changes. Download through a temporary
browser object URL and revoke it immediately after use.

Recoverable errors preserve ordinary drafts, display an actionable message, and
require a deliberate retry. A lost deletion response triggers a session check;
it must never automatically resubmit deletion or claim the account survived.
If an export fails, the deletion confirmation remains a separate explicit action.

The final dialog must state the retention periods without promising that all
data disappears on one date: live account/personal data and recovery events are
deleted immediately, private artwork is retried by the hourly sweeper after its
24-hour age threshold if immediate removal fails, backup copies expire within
30 days, and pseudonymized resolved moderation records expire 365 days after
resolution. Open reports remain until resolved; public catalog contributions
remain without account attribution. While the account is active, recovery events
expire after 90 days. Include Cancel and Permanently delete account, move focus
into the dialog, trap focus, support Escape/cancel, and restore trigger focus.
The 30-day backup deadline is an operator obligation, documented as a production
release gate requiring actual backup expiration and restore-erasure verification.

## Verification and delivery

Use disposable PostgreSQL integration tests for export isolation and secret
exclusion, ZIP/artwork integrity, local/provider confirmation, grant expiry and
replay, CSRF, concurrent deletion/writes, last-admin protection, moderation
pseudonymization, private dependency cleanup, public-reference preservation,
rollback and session-cookie replay. Exercise retention boundaries and failed
filesystem cleanup separately.

Frontend tests cover keyboard-accessible tabs, export download/error handling,
typed-handle confirmation, both ownership-confirmation paths, session transitions,
draft/secret cleanup and the absence of automatic destructive retries. Run the
relevant tests, TypeScript, ESLint, backend tests and EF model consistency check;
record any unavailable browser verification honestly.

Use a reviewed additive migration; do not apply it to the persistent local
database without recording that action, and never reset that database. Update
README API contracts, current status, retention/restore runbook and an ADR. Keep
implementation commits separate from any later deployment or real account erasure.
