# Staff management in moderation

## Intent and scope

Extend the existing moderation UI so administrators can find accounts, inspect
their staff roles, and grant or revoke the Moderator role with an audit reason.
This is the first staff-management slice requested by the user. Profile moderation
and the remaining production-readiness backlog are separate work.

## Approach

Extend the existing moderation feature at `/moderation/staff`, reusing its
navigation, CSS, paging, session handling, and audit history. This is the approach
the user requested. A separate dashboard would duplicate the existing interface;
UUID-only role controls would omit the account lookup needed for practical use.

## Behavior

- Add a Staff management link to the existing moderation navigation. The destination
  checks admin access independently; moderators who follow it see an admin-access
  message and retain access to their report queue.
- Show a paginated account list, with a submitted search by handle or display name
  and an optional staff-only filter. Default to staff-only so current appointments
  are easy to review. Show public identity and Admin/Moderator role indicators.
- Each eligible account offers Grant moderator or Revoke moderator. A confirmation
  step identifies the account and action and requires a nonblank reason, at most
  1,000 characters, matching backend validation. Cancelling performs no write.
- Admin accounts show that their role already grants moderation access. Their
  roles are read-only in this UI; administrator appointment remains operator-managed.
- Successful writes refresh account roles and relevant audit data. Link each account
  to its existing account-filtered audit history. Failed or uncertain writes require
  refreshing role state before another attempt; never automatically retry writes.
- Conceal account results and cancel pending work during logout/account changes.
  Losing admin access clears admin data without incorrectly revoking moderator access.

## API and security

Add an admin-authorized paginated account-read endpoint using public identity fields
and role flags only. Search uses bounded, validated input and literal matching;
results have deterministic ordering. Apply the existing moderation-read budget and
no-store response policy. Do not expose emails, credentials, recovery information,
private libraries, or other account data.

Reuse the existing admin-only PUT/DELETE moderator endpoints and operator audit
events, preserving their reason validation, transactional authorization, and rate
limits. Add only the exact required browser-proxy routes, methods, and query filters;
preserve CSRF protection. No schema migration or role change on the persistent
database is required to implement the feature.

## Implementation choices

Use the existing .NET service and Next.js moderation feature. Keep admin access
state distinct from general staff access. Backend authorization remains authoritative
for every read and write. The account list, filter defaults, read-only Admin rows,
and confirmation flow above are proposed implementation choices.

## Verification and delivery

Verify anonymous/ordinary/moderator/admin permissions, account-search boundaries,
paging, response privacy, audited grant/revoke, and immediate loss of access.
Verify proxy allowlisting and CSRF behavior, UI confirmation/reason validation,
failure handling, and account-transition data isolation. Run relevant frontend and
backend tests, frontend lint/typecheck/build, and inspect desktop/mobile behavior.
Use disposable accounts/databases for role-mutation checks; do not alter the user's
test account or appoint additional persistent staff for verification.

Update `docs/STATUS.md` with the implemented slice and actual verification evidence.
Restart the local API/frontend as needed to make the result reviewable. Deployment
is outside this change.
