# ADR 0019: Staff access and operator attribution

Status: accepted policy direction, 2026-11-09. The user selected named
host/deployment records for operator attribution. Target-deployment procedures
still require acceptance evidence before launch.

Extends ADRs [0011](0011-moderation.md), [0015](0015-installation-setup.md),
[0016](0016-custom-catalog-and-list-collections.md),
[0017](0017-account-data-lifecycle.md), and [0018](0018-profile-text-moderation.md).

## Staff access

Use individually assigned accounts and the least role needed. Current Admins
manage Moderator membership through the reasoned, audited workflow. Admin
appointment belongs to the authenticated deployment operator, using initial setup
once or the existing offline command for an existing account. Registration and
email matching confer no role.

| Capability | Moderator | Admin |
| --- | --- | --- |
| Read report queue, report details and moderation audit history | Yes | Yes |
| Inspect public/hidden reviews, comments, public custom items and profile text | Yes | Yes |
| Hide/restore these targets and resolve their reports | Yes, excluding own content | Yes, excluding own content |
| Inspect reported public categories | Yes | Yes |
| Resolve category reports and change category visibility | No | Yes, with existing own-content and seeded-target restrictions |
| Admin category listing, rename and merge | No | Yes, with existing owner/version/seed protections |
| Search account directory and grant/revoke Moderator | No | Yes |
| Revoke Admin while another Admin remains | No | Yes |
| Grant Admin through the application UI/API | No | No |
| Remove baseline registered-user access through role management | No | No |
| Browse other people's private libraries, collections or security data through staff privileges | No | No |

Read access is shared across the queue/history, without per-case assignment.
Own-content restrictions prevent decisions, not inspection: a staff author may
read a report about their own content. Reporter identities/details are available
to authorized staff, so reporting must not be described as anonymous to staff.
Use this access only for moderation and necessary administration.

Ordinary staff browsing does not bypass private-catalog access. A report created
while content was public permits explicit report-scoped inspection of the current
live target after withdrawal, including later edits. It is not an original-text
snapshot or a general private-content directory. Deleted targets do not reconnect
to reused UUIDs. This preserves ADR 0016 and existing scoped-report behavior.

Decisions use current database roles, CSRF-protected writes, required reasons and
inspected versions. Revocation removes access on subsequent protected requests;
previously delivered content cannot be recalled. Owners retain existing editing/
deletion rights. Profile edits stay hidden until explicit staff restoration.
No suspension, appeal process or administrative password reset is introduced.

## Attribution

Account-mediated changes record the authenticated account UUID. Real role/
visibility changes and report resolutions append application events in the same
transaction. No-op role changes create no new event. History is not tamper-proof
against infrastructure administrators, and the application does not audit every
staff read. No per-read audit guarantee is made.

Offline appointments/revocations and initial setup retain the `operator` actor kind. Identify
the person through a protected operation record tied to an authenticated named
host/deployment principal. For CI, record both the triggering person and executing
service principal. Shared credentials without individual attribution do not
satisfy production acceptance. A typed personal name in a reason is not verified
identity. The setup secret alone does not identify the person completing setup.

The minimal external record contains verified operator identity, UTC time,
environment/database, action, target UUID where applicable, change/run reference
and outcome. Link the application event UUID when a real change created one;
record an idempotent no-change result accurately. Do not copy secrets, environment
values, recovery codes, cookies, profile bodies, exports or report details.

Protect these minimal correlation records and retain them for 365 days from the
operation, aligned with standalone application audit retention; expire them after
that window. Deployment must configure and verify this external lifecycle. This
does not require keeping all HTTP logs or full shell transcripts for 365 days.

## Removal, retention and boundaries

Remove unnecessary Moderator membership through the Admin workflow and verify
loss of protected access. Current Admins may remove Admin in Staff management,
and operators may use `--revoke-admin`; both preserve at least one administrator,
including under concurrent requests. Removing Admin preserves any Moderator role
and registered-user access. UI/API removal records the authenticated account;
offline removal records `operator` and requires external named attribution.
No-op removals create no new application event. Granting Admin stays operator-only.
These paths are locally tested; deployed removal/recovery acceptance remains a
release gate. Direct SQL is not automatically audited by the application.

ADR 0017 controls application erasure/retention: pseudonymization on deletion,
365 days after resolution for reports/related events, 365 days for standalone
events, and open reports retained until resolved. Keep staff reasons relevant and
minimal; retained notes may identify people and are not guaranteed anonymous.

Database, backups, artwork, keys and the erasure journal have separate restricted
infrastructure access. App staff roles do not confer these powers. Approved
maintenance/recovery uses named operator records and the existing runbooks.
Named attribution adds no application identity field or migration. Admin removal
was subsequently implemented at the user's request with the boundaries above.

See [staff operations](../staff-access-operations.md) and the
[release gates](../frontend-release-verification.md#outstanding-release-gates).
