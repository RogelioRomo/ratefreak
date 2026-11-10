# Staff access and operator operations

Policy: [ADR 0019](adr/0019-staff-access-and-operator-attribution.md).
This runbook documents responsibilities/evidence. It does not provision logging,
grant real roles, or authorize deployment.

## Application staff

Use a named account per person. Assign Moderator for report work and reserve Admin
for people managing moderators/public categories. Routine app actions do not
require a second administrator's approval.

Use **Moderation → Staff management** for Moderator appointments/removal. Locate
the correct handle, verify the account, enter the reason, confirm, and check its
role and linked audit history. Admin rows offer **Revoke Admin**, with a reason
and explicit confirmation; the UI does not grant Admin. Email matching and copied
credentials do not establish authority.

Inspect the current content/version before deciding; enter a relevant reason and
confirm. Category decisions require Admin. Refer own-content decisions to another
staff member. Staff may still read their own reports; read access is not split by
case ownership. Do not promise reporter anonymity from staff.

For a formerly public target now private, use the report-scoped inspection only.
It displays current live content, including later edits, not a snapshot. Staff
privileges do not provide a general private-content browser. Owners can correct
hidden profile text, but staff must inspect and explicitly restore the new version.

## Offline administrator appointment

Use the existing command with the intended API environment/database configuration
supplied through normal protected configuration:

```bash
dotnet run --project src/RateFreak.Api --no-launch-profile -- --appoint-admin EXISTING_ACCOUNT_UUID --reason "Recorded appointment reason"
```

Verify the exact environment/database and existing account UUID, and that the
site owner's authorization covers the change. Use an individually authenticated
host/deployment session. Keep secrets out of command arguments and recordings.

Record the platform's verified operator identity, UTC time, environment/database,
`grant_admin`, target UUID, change/run reference and result. For CI include both
requester and executing principal. Correlate a newly created application event UUID
through the authorized audit view, and check the role. An idempotent repeat creates
no additional application event: record the no-change outcome accurately.

The command exits without HTTP or migrations: 0 success, 2 invalid/missing account,
1 operational failure. Its `operator` event alone does not identify a person.
A typed name in the reason is not a substitute for authenticated attribution.

## Administrator removal

An existing Admin can use **Staff management → Revoke Admin**, or an authorized
operator can run:

```bash
dotnet run --project src/RateFreak.Api --no-launch-profile -- --revoke-admin EXISTING_ACCOUNT_UUID --reason "Recorded removal reason"
```

Verify the target/environment and reason. Both paths refuse to remove the last
Admin, including concurrent attempts. Appoint another Admin through the operator
workflow first if necessary. Do not bypass this protection with direct SQL.
Removing Admin preserves Moderator membership if present, registered-user access,
account contents and security stamps. Remove Moderator separately using the
existing workflow when that is also intended.

Check that subsequent Admin requests are denied in the existing session and that
the intended remaining roles still work. Self-removal in the UI immediately
conceals admin-only content. No baseline User-revocation operation exists.
For a failed/uncertain UI result, reload roles before another attempt.

A real removal produces `revoke_admin`, before true/after false. The UI records the
authenticated account; the offline command records `operator`, requiring the named
external record above. The command is idempotent on an existing non-Admin account
and adds no event in that case. Exit codes: 0 success/no change, 2 invalid/missing
account or last-Admin refusal, 1 operational failure. It starts no HTTP and applies
no migrations.

## Setup and maintenance

For first setup, record the named deployment session/run, completion time and
first Admin UUID. Exclude the setup secret/recovery code. Established installations
do not reopen setup to replace staff.

For migrations, backups, restore replay and emergency maintenance, record the
named operator/run, intended environment and outcome using the relevant runbook.
Operations without an application event need external evidence; do not imply
that application history captures every direct SQL operation.

Recover an intentionally removed Admin role using the existing offline appointment
command under named operator access. The locally tested procedures still require
acceptance through the actual deployed sessions, infrastructure and record store.

## Record storage and deployed acceptance

Restrict minimal operation records to authorized operators. Retain them for
365 days from the action and verify expiration. If execution logs expire sooner,
keep the minimal authenticated identity/run correlation in a protected record
store for the policy window. Do not extend full transcripts or body logging merely
to meet this requirement.

Before launch, record evidence for:

- Named host/CI identities and requester-to-service-principal correlation.
- An isolated Admin appointment tied to a named operation and application event,
  plus an accurately recorded idempotent repeat.
- Moderator grant/revoke and subsequent-request denial through real deployed
  sessions/proxy configuration.
- Category/own-content restrictions, scoped inspection after withdrawal and no
  general private-data bypass.
- Deployed Admin removal/recovery preserving another Admin and any retained Moderator
  role, with current-session denial and correct account/operator audit attribution.
- Restricted external records and 365-day expiration, plus application retention
  and erasure pseudonymization under ADR 0017.

Existing integration tests cover application role checks/audits and the offline
appointment command. They do not establish deployed host identity, external-record
protection/expiration or Admin-removal readiness. Keep those operational release
gates open until tested in the target environment.
