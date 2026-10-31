# Moderation and reporting

Status: proposed for approval, 2026-10-30.

## Intent and agreed permissions

Public reviews and comments need a private reporting and moderation workflow.
The user approved ordinary signed-in users reporting content, Moderators reviewing
reports and hiding/restoring content, and Admins assigning/revoking Moderator access.
An ordinary user needs no stored role. Registration never assigns privileged roles.
The detailed defaults below are proposed implementation decisions.

## Approaches

Use existing ASP.NET Identity roles and reversible content hiding. This fits the
existing role tables and retains content for correction/restoration. Permanent
moderator deletion would make mistakes irreversible and complicate evidence
retention. An external moderation service adds an unnecessary dependency. A custom
permission framework adds complexity without improving this two-role workflow.
The user approved the role model; reversible hiding is the recommended approach
for approval with this document.

## Authorization and role administration

Moderator and Admin can access moderation operations and private audit history.
Only Admin can grant/revoke Moderator on an existing account. No HTTP endpoint
grants Admin, and no first-registration or email-based elevation exists.
Privileged authorization checks current database membership, never stale cookie
claims alone. Mutation services recheck authority inside their transaction and
serialize against role changes so revocation cannot race a later authorized write.
Operations already committed before revocation remain valid.

Provide an explicit offline maintenance command for first-Admin appointment by
existing account UUID, using normal server configuration, without starting HTTP.
It is idempotent, requires an operator-supplied reason, records a system/operator
audit event and never prints account secrets. It does not create accounts or run
automatically on startup. Implement/document it but do not appoint a real account
as part of development. Further Admin changes remain operator-managed.

Admin role changes and content decisions require cookie authentication and CSRF.
Anonymous requests return401; signed-in users without permission return403.
Moderator cannot moderate their own content or resolve reports about it; another
Moderator/Admin must handle it. Admin follows this same conflict rule.

## Reporting

POST /api/v1/reviews/{id}/reports and /api/v1/comments/{id}/reports accept
{reason, details?}. Reasons: spam, harassment, hate, sexual_content, violence,
spoiler, other. Details are optional trimmed literal text, at most1000 UTF-16
units with existing valid-Unicode/control rules. Other requires nonblank details.
Session and CSRF required; actor is always derived from the session.
Only currently public content is reportable; missing/hidden targets return404.
Self-reporting returns400. Use a distinct reporting budget of5/account/hour,
no queue, Retry-After on429; ingress-wide coordination remains deployment work.

One report per reporter and content target across its lifetime, enforced by a
unique database constraint. First submission201; duplicates409, with no overwrite
of the original reason/details. No reporter-visible listing or public report count.
The response is an acknowledgement containing report UUID/status/creation time,
without moderation notes, other reports, emails or security data.

## Moderation workflow and API

GET /api/v1/moderation/reports?status=open|resolved&page=1&pageSize=20 lists
reports, oldest creation time then UUID. GET /api/v1/moderation/reports/{id}
returns report detail and current target content, including hidden content and its
UpdatedAt when present. Missing/deleted targets are represented explicitly.
List DTOs omit content bodies/details; detail includes reporter/author public
identity, reason/details, target state, and resolution. No emails/security fields.

POST /api/v1/moderation/reports/{id}/resolve accepts
{decision: dismiss|hide, reason, expectedTargetUpdatedAt}. It resolves an open
report once, records the decision, and hides the target for hide. Reason is required
trimmed plain text1..1000. Missing report404; already resolved409. A missing target
can be dismissed with expectedTargetUpdatedAt null, but cannot be hidden (409).
For a live target the supplied timestamp must match its current UpdatedAt, or409;
this prevents acting on content edited after inspection. It does not auto-resolve
other reports about the target. Hiding an already hidden target can resolve a
separate report, without a duplicate visibility transition.

POST /api/v1/moderation/reviews/{id}/visibility and equivalent comments route
accept {hidden, reason, expectedTargetUpdatedAt}. These allow proactive hiding or
restoration without a report. Same ownership conflict, validation and timestamp
checks apply. Deleted target404. Repeated same-state requests return204 without a
new audit event; real transitions atomically change moderation state and UpdatedAt
and append an audit event. Last-write-wins moderation is not allowed.

GET /api/v1/moderation/reviews/{id} and /api/v1/moderation/comments/{id}
provide private current content, moderation state and UpdatedAt independently of
reports, including individually hidden content and comments under hidden parents.
Same Moderator/Admin authorization, safe author identity, read budgets and no-store
apply. Deleted target404. These reads support proactive inspection and restoration.

GET /api/v1/moderation/audit lists private events newest timestamp then UUID,
optionally filtered by target type/UUID. Common pagination: page1..10000,
pageSize1..100. Repeatable-read count/page snapshots. All new routes no-store,
including errors. Strict JSON rejects unknown/duplicate properties. Separate
moderation read60/account/min and write30/account/min budgets; role writes10/admin/min.

PUT /api/v1/admin/moderators/{userId} with {reason} grants Moderator;
DELETE same with a required {reason} JSON body revokes it. Existing desired state
returns204 without a new event; missing user404. Changes and audit insert are atomic.
Admins retain moderation access through Admin membership even if Moderator is revoked.

## Content visibility and author ownership

Add IsHidden=false to reviews/comments, separate from spoiler flags. Public
review lists/details exclude hidden reviews. Comment lists/details exclude hidden
comments and all comments under hidden reviews; a hidden parent returns404, and
new comments under it return404. Restoring a parent reveals only comments not
individually hidden. Pagination totals use the same visibility predicate.
Ratings and their aggregates remain independent of review moderation.

Authors may edit/delete their own hidden content, but editing never clears
IsHidden. Author-only mutations can return their own content directly; public GET
still returns404. A comment author may edit/delete their comment under a hidden
parent without exposing that parent or other comments. Hidden reviews still occupy
the existing unique author/media slot. No author-only GET is added in this slice.

## Persistence, audit and deletion policy

Use explicit nullable review/comment report FKs, reporter FK, unique
per reporter/target indexes, status/reason CHECKs and ordered queue indexes.
Keep immutable original target UUID/type and original actor/author UUID metadata
alongside nullable live references. Live references use SET NULL on deletion;
Exactly one immutable typed target is required; its matching live FK may be null
after deletion, and the FK for the other target type must always be null.
report/audit metadata survives author deletion, reporter deletion, review deletion
and parent-thread cascades. No retained handles/emails or copied content bodies.
Deleted-account identities project as unavailable. Target UUIDs remain opaque
historical identifiers, never a reason to recreate or reconnect deleted records.

Audit events include UUID, UTC timestamp, actor UUID (or explicit operator actor),
action, target type/UUID, optional report UUID, required reason and before/after
visibility or role state. Resolve decisions, real visibility transitions and role
changes are appended in the same transaction as their state changes. Reports retain
original reason/details and final resolution; no edit/delete audit API exists.
Do not log report details, content bodies or decision reasons in application logs.
This is application-level append-only history, not tamper-proof storage against
database administrators. No full content snapshots are retained; historical exact
text cannot be reconstructed after edits/deletion. Store reviewed UpdatedAt on
resolution events to identify the version acted upon.

Retain report text and audit metadata without automatic expiry for this initial
slice. This is an explicit development default, not an approved public-launch
retention policy; deletion/retention/export requirements must be resolved before
deployment. Reports can contain personal information, and surviving UUIDs are
pseudonymous rather than anonymous. Limit all access to moderation staff.

Serialize conflicting content edits/deletions, report decisions and role changes
with a documented consistent lock order. Recheck visibility, target version and
authority under lock. Failed checks, stale decisions and failed audit inserts
leave both content/report/role state and history unchanged. No orphan report on
concurrent deletion. New migration is additive and preserves existing data.
Down must refuse while hidden content, reports or audit events exist, rather than
silently republish hidden content or erase history; tested guarded rollback/reapply.

## Scope and proof

No account suspensions, automatic moderation, appeals/notifications, collection
targets, frontend, real Admin assignment, public deployment or OAuth activation.
Implementation details not specified here belong to the builder, including
maintenance command project layout and lock implementation consistent with these rules.

Prove authentication/CSRF/role boundaries, immediate revocation, no self-elevation,
own-content conflict, reporting duplicates/races/validation/budgets, visibility on
every public list/detail/create path, parent/child restoration, author mutation
preserving hides, stale decisions, report resolution and role-write concurrency,
atomic audit rollback, deletion survival, safe DTOs/no-store, and migration
apply/reapply/refusal preserving all previous data. Run focused tests during work,
full suite and Release build at completion, EF model check and dependency audit.
Update README/ADR/checklist with actual evidence; no merge or push inferred.
