# Candidate vulnerability review — 2026-11-23

The exact `f19a907` / `0.1.0-dev.1` candidate completed vulnerability scanning,
but **does not pass the security release gate**. Functional acceptance and
source-material integrity remain valid for their recorded scope; they do not
clear these findings. No dependency, image, persistent database or release was
changed by this review.

Subsequent [runtime security updates](runtime-security-update-2026-11-23.md) are
implemented and verified in a new nonofficial local proof. They remove the
reported non-OS and fix-available image findings, refresh matching source
materials, and retain residual OS/native security review as an open gate.
The findings below remain the historical `f19a907` baseline.

## Checks and evidence

Production and complete frontend dependency graphs were audited with pnpm;
the solution's direct/transitive NuGet packages were audited separately. Trivy
0.76.2 scanned the three saved candidate image archives, without the Docker
socket, using the vulnerability database updated `2026-11-23T13:07:53Z`.
The scanner image is pinned to
`aquasec/trivy@sha256:8f6bb947dbc528902e11f2f5be52f7b519f6a39d5d148f41166f2040499768fb`.
Archive checksums and scanned image configuration digests match the previously
reviewed candidate. No application images or private configuration were uploaded.

| Check | Result |
| --- | --- |
| pnpm production graph | No known advisories reported |
| pnpm complete graph | One moderate advisory in the ESLint/parser dependency chain: minimatch 10.2.6 |
| NuGet solution, including transitive packages | No vulnerable packages reported across seven projects; this does not cover the base-image shared framework |
| API image | 73 package/advisory matches: 5 high, 62 medium, 6 low |
| Web image | 487 matches: 2 critical, 115 high, 201 medium, 139 low, 30 unknown |
| Tools image | 685 matches: 4 critical, 147 high, 287 medium, 210 low, 37 unknown |

These are raw scanner counts, including repeated matches for multiple binary
packages/images: **1,245 matches and 326 distinct advisory IDs**. They are not
counts of independently exploitable application bugs. No findings were hidden
through ignore rules or an ignore-unfixed setting. A scanner exit code of zero
records successful scanning, not a clean result.

The [scan summary](release-evidence/2026-11-23-security/scan-summary.json) binds
versions, database metadata, commands, report hashes and counts to the candidate.
The [complete compact match inventory](release-evidence/2026-11-23-security/findings.json)
retains package paths, versions, statuses and scanner severities. Full local
reports/logs/database cache are under `.local/release-security-2026-11-23/`.
An [independent read-only review](release-evidence/2026-11-23-security/independent-review.json)
confirmed report hashes, compact finding fidelity, counts and the documented
security/fresh-install scope, with no substantive finding. It did not independently
establish exploitability or repeat the scans.

## Findings and dispositions

### Update the .NET runtime before release

API and tools ship .NET/ASP.NET Core **10.0.14**. The scan reports three high
ASP.NET findings and two medium framework findings with available fixes.
Microsoft's [10.0.15 release](https://github.com/dotnet/core/blob/main/release-notes/10.0/10.0.15/10.0.15.md)
contains the November security updates; use a reviewed, pinned patched runtime
and compatible SDK, then rebuild both consumers and the migration bundle.
Host-machine SDK updates do not replace the container runtime.

[CVE-2026-114149](https://github.com/dotnet/aspnetcore/security/advisories/GHSA-x33c-w48q-qrgc)
concerns multipart resource exhaustion. RateFreak calls `ReadFormAsync` in artwork
and avatar upload endpoints. That is a relevant processing surface; existing
size/value/header-length limits are not asserted to eliminate the advisory.
No exploit was executed and exploitability through the complete ingress was not
established. Runtime update is the immediate release priority.

Two other reported ASP.NET findings describe
[SignalR identity handling](https://github.com/dotnet/aspnetcore/security/advisories/GHSA-67wp-ghc2-fw2g)
and [SignalR Redis scale-out](https://github.com/dotnet/aspnetcore/security/advisories/GHSA-wr8v-9x5q-ggcm).
No explicit SignalR registration or Hub mapping was found in application source;
that reduces the apparent application exposure to those particular mechanisms,
but is not a reason to retain an outdated framework. Microsoft rates the identity
advisory critical, while this Trivy feed records high; both assessments are retained.

### Review the copied Node package-manager closure

The web image's **19 JavaScript package findings** are in npm's copied
`/usr/local/lib/node_modules/npm/node_modules/` tree, rather than the application's
production pnpm dependency graph. They include brace-expansion, glob, ip-address,
minimatch, postcss-selector-parser and Undici. For example, the installed Undici
6.28.0 matches its publisher's
[TLS-option validation advisory](https://github.com/nodejs/undici/security/advisories/GHSA-2m5v-pch6-74hx).
No claim is made that Next.js loads this npm-private copy for application requests.

Refresh the pinned Node image/package-manager closure or deliberately construct
a runtime without unnecessary tools. Inspect the resulting bytes before choosing
an approach: removing files in a later layer does not erase them from a
distributed image archive. Refresh source/notice decisions for changed components.

### Address the development minimatch finding separately

The complete frontend audit finds minimatch **10.2.6** through
`eslint-config-next → eslint-plugin-import → @typescript-eslint/parser →
@typescript-eslint/typescript-estree`. The
[publisher advisory](https://github.com/advisories/GHSA-g3hv-75wg-m249) identifies
11.0.1 as patched. This is separate from npm's copied minimatch 10.2.5 in the web
image. Review a compatible upstream update or narrowly scoped resolution, then
run lint/typecheck and relevant build checks; do not force a broad major-version
override or treat an application-only audit as covering the copied runtime tools.

### Triage OS and inherited Go findings against actual use

The Ubuntu API scan includes an APT update with a listed fix. Most Debian
Bookworm findings have no fixed version listed in this database; a blind package
upgrade cannot be assumed to clear them. Refresh authenticated package inputs,
then evaluate remaining findings individually against vendor status and actual
code paths. A distro migration would change the supported image/source baseline
and needs its own compatibility/source review.

Examples showing why scanner severity alone is insufficient:

- Debian's [zlib assessment for CVE-2023-45853](https://security-tracker.debian.org/tracker/CVE-2023-45853)
  says the affected MiniZip contribution is not built into these Bookworm binary
  packages. The critical zlib1g matches are recorded as not applicable to those
  binary packages on that vendor evidence; this does not exempt other native
  zlib/MiniZip copies or unrelated zlib advisories.
- [SQLite CVE-2025-7458](https://security-tracker.debian.org/tracker/CVE-2025-7458)
  is classified as a minor/no-DSA issue by Debian and requires attacker-controlled
  SQL. RateFreak uses PostgreSQL; no SQLite request-data path was established.
  The affected installed library remains inventoried, without a blanket waiver.
- [libxml2 CVE-2026-6653](https://security-tracker.debian.org/tracker/CVE-2026-6653)
  remains postponed/minor in Bookworm. Its XML parsing exposure in the maintenance
  image needs review; it is not automatically cleared by that vendor priority.
- The inherited gosu binary reports Go 1.24.6 and 62 stdlib/module matches.
  [CVE-2025-68121](https://pkg.go.dev/vuln/GO-2026-4337), for example, concerns TLS
  session resumption. The scan is version-based, not a call-graph proof that gosu
  reaches those functions. The tools image replaces PostgreSQL's original
  entrypoint and does not use gosu in its documented maintenance commands.
  Prefer a maintained/minimal tools payload or perform symbol/reachability review;
  no global exception for its Go findings was accepted.

Remaining OS/native findings are not silently accepted. This pass establishes
the inventory, representative vendor triage and remediation priorities, not
exhaustive exploitability decisions for all 326 advisory IDs. Embedded libvips,
Skia and other native aggregates also need explicit consideration because package
scanners do not identify every statically linked component.

## Fresh-install data verification

An additional isolated installation used the exact candidate API/tools images,
ran ordinary provisioning/migrations and started the API, **without calling any
acceptance seed function**. Every public-schema table and the artwork directory
were checked. Result: **zero accounts, catalog items and artwork files**.
All application-content tables were empty. The only nonempty tables were
24 migration-history entries, five built-in category definitions and one pending
installation-state row. The initial administrator still requires first-run setup.

The category definitions are Albums, Books, Movies, TV shows and Video games.
They are reference definitions, not example titles, ratings or reviews. The
owner was asked whether those definitions should remain under the no-seeded-data
requirement; no category/migration behavior was changed while that clarification
was pending. See the [fresh-install receipt](release-evidence/2026-11-23-security/empty-install.json).
The disposable database, services and volumes were removed afterward.

## Next release work

1. Update the shared .NET runtime/build inputs, then refresh the Node/npm and
   applicable OS/tool inputs and resolve the separate development-tool advisory.
2. Rebuild and rescan; record specific residual dispositions and vendor evidence,
   including native aggregates and unused inherited helpers.
3. Refresh changed inventories, exact source acquisitions, pinned notices and
   image-bound sidecars. Old coverage cannot simply be relabeled for new images.
4. Repeat relevant application/build and exact-image install/upgrade/browser
   acceptance, then qualify the stable source/version.

Provider permission, operator-specific operational duties, maintainer notification
delivery and actual publication authorization remain independent gates.
