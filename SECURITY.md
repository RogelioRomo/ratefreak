# Security policy

## Report a vulnerability privately

Use GitHub private vulnerability reporting for
[RogelioRomo/ratefreak](https://github.com/RogelioRomo/ratefreak/security/advisories).
Open **Security → Advisories → Report a vulnerability** and submit
your report through GitHub's private form. You need to sign in to GitHub.

**Verified 2026-11-18:** the repository owner enabled private reporting, and
GitHub's API confirmed `enabled=true`. This verifies the setting, not the
delivery of a test report or maintainer notifications.

If the **Report a vulnerability** button is unavailable, open a minimal
[public issue](https://github.com/RogelioRomo/ratefreak/issues) asking the
maintainer to enable a private reporting channel. Do not include the affected
endpoint, exploit, proof of concept, sensitive logs or credentials in that issue.
Wait for a private channel before sending those details. No public reporting
email is designated by this policy.

In a private report, include:

- The affected commit, tag or image digest and relevant runtime versions.
- Whether this affects development or a self-hosted installation, with a sanitized
  description of the topology and configuration needed to reproduce it.
- The security impact, required permissions and affected components.
- Minimal reproduction steps or a proof of concept using synthetic data.
- Expected and actual behavior, plus a proposed fix if you have one.

Remove passwords, session/antiforgery cookies, recovery codes, provider/OAuth
tokens, connection strings, proxy/setup secrets, private keys and user data from
attachments. Redact any identifying details not needed for reproduction. If a
secret has already leaked, revoke or rotate it; removing it from a report does
not invalidate the exposed copy.

## Scope and safe reproduction

Relevant issues include authentication/session or account-recovery bypass,
authorization failures, access to private libraries/uploads or account exports,
cross-site request forgery, script or query injection, server-side request forgery,
unsafe artwork processing, and exposure of secrets or protected operator data.
Security issues in the shipped proxy, API, dependency/image payloads and operator
tools are also in scope for reporting.

Use an installation you own or have explicit permission to test, with disposable
accounts and data. This policy does not authorize testing third-party instances,
provider services or infrastructure you do not control. Avoid destructive actions,
load attacks and access to other people's data. Stop if reproduction would expose
or change data outside your authorized test environment.

For ordinary bugs, feature requests and setup questions, use public issues with
sanitized details. Provider account problems and content-rights complaints are
separate from software vulnerabilities; contact the relevant provider or instance
operator without posting private account information.

## Versions and disclosure

RateFreak is preparing its self-hosted release. No stable-release support matrix,
backport window or guaranteed response/fix timeline is currently documented.
Include the exact revision when reporting; dated test results in
[STATUS.md](docs/STATUS.md) are not a security certification or support promise.

Keep vulnerability details private while the maintainer assesses impact and
coordinates a fix and disclosure with you. A private advisory can be used to
coordinate a patch and later publish affected versions and mitigation guidance.
Do not open a public exploit-bearing pull request as the initial report.

An installed third-party dependency's advisory is useful evidence, but also
describe whether and how the affected component is shipped or reachable. A clean
scanner result does not establish that an installation has no vulnerabilities.

## Dependency findings and release decisions

Apply available, compatible vendor security fixes and verify the changed artifacts.
Assess CVEs against actual deployment exposure, attack prerequisites and impact,
using vendor assessments and scanner results as evidence. Findings assessed as
low practical risk may receive a documented risk acceptance; a low scanner label
alone does not establish that risk. Accepted findings remain visible and are
reassessed when fixes, exploitation evidence or deployment conditions change.
See the [CVE assessment and release policy](docs/security-risk-policy.md) for the
current rule and recorded exceptions.

## Self-hosted operator responsibilities

The reference topology and operational steps are in the
[self-hosting guide](docs/self-hosting.md). Operators manage their instance's
patches, HTTPS ingress, trusted proxy settings, secret/key protection, database
and artwork permissions, monitoring, backups and independent erasure journal.
Keep optional provider credentials in server-side protected configuration.

The current reference installation is a single-host Linux amd64 deployment with
one replica per service. Other platforms/topologies need their own verification.
Development loopback mode and Development cookies are not production settings;
do not weaken Secure cookies, CSRF or Origin checks to work around a mismatch.

For an incident affecting an instance, contain the exposure, rotate compromised
credentials and preserve restricted, sanitized evidence. Follow the
[backup/restore procedures](docs/self-hosting.md#backup-and-isolated-restore) and
[account-erasure requirements](docs/account-data-operations.md#backups-and-restore)
when recovering; restoring a backup must not silently restore erased account data.
Report a suspected underlying RateFreak vulnerability through the private channel
above. Repository maintainers do not automatically have access to operators'
instances, storage or secrets.
