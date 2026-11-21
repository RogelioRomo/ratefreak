# Versioned releases and announcements

RateFreak is distributed for single-host Linux amd64 self-hosting. The maintainer
prepares a candidate, reviews its exact artifacts and publishes only after the
project owner authorizes publication and the independent release gates are closed.
The workflows never publish releases, push tags or upload registry images.

## Versions and development builds

Root `VERSION` is authoritative. Keep the base version in `web/package.json`
aligned. API/web/tools embed one version and full source revision; official
candidate preparation requires a matching clean existing `v<VERSION>` tag.
Developer builds are visibly nonofficial. Extracted source installation archives
include generated `SOURCE_REVISION` provenance and remain nonofficial when rebuilt
without Git metadata. Runtime environment settings cannot
change compiled installed identity. Read the pre-1.0 compatibility rules in
[release notes](releases/README.md).

Source builds continue to use `ratefreak-{api,web,tools}:local`. Downloaded bundles
load versioned local tags together; they require no registry credentials. Install
only downloads obtained from the project's verified GitHub release. Checksums
prove byte agreement with a manifest, not the identity of its publisher.

## Verification and candidate preparation

Run `python3 deploy/release/verify.py --report-directory /new/path/verification`.
It runs restored backend/real PostgreSQL suites, frontend tests/lint/typecheck/build,
operator/release/compliance suites, Release publish and model consistency. Backend
TRX files must show executed tests without skips. Raw logs/TRX stay local and may contain machine paths. Export only the allowlisted
public summary with `--public-report-directory /new/path/public-reports`; Actions
uploads and candidate report archives contain summaries, not raw logs or TRX.
Instance secrets, storage, keys and backups do not belong in reports.

For a clean existing tag with matching notes under `docs/releases/`:

```bash
python3 deploy/release/prepare.py --tag v0.1.0 --output /new/path/candidate --compliance /protected/reviewed-source-materials
python3 deploy/release/bundle.py verify /new/path/candidate/release
```

Image-bound source review can take place after a build. An incomplete candidate
retains its exact image archives and `candidate-manifest.json`; it has no
installable `release-manifest.json`. Supply the matching reviewed source materials
and rerun the same tag/output with `--resume`. Finalization verifies and loads the
retained archives without rebuilding, then writes a new `release/` directory.
An existing final `release/` is never replaced. In Actions, `candidate_artifact_id`
selects one immutable artifact from the original run for this second phase; `source_materials_artifact_id` selects
one immutable reviewed source-material artifact. Missing evidence leaves the original
candidate incomplete.

The manually dispatched **Prepare release candidate** workflow resolves its tag to
one commit. It accepts an optional same-repository artifact ID named
`release-source-materials`; the download needs only Actions read access.
Artifact provenance must match a completed manual main-branch workflow in this
repository; retry attempts are never merged by pattern. Source-material uploads
are expected from the separately reviewed `release-source-materials.yml` workflow
when that outstanding source-delivery work is ready. All third-party actions are pinned. The normal Verify workflow has only contents
read access, including for fork pull requests. Artifact access follows repository
rules: never put private instance data in Actions uploads.

Preparation verifies source, builds API/web/tools, checks reviewed image-bound
source materials, runs disposable HTTPS acceptance and collects compressed image
archives, a tracked-source installation archive, release notes, notices, source
materials, verification reports and a SHA-256/size manifest. The official build context is exported from the one selected Git commit; ignored
local files are never Docker build inputs. Output paths are new;
existing candidates are never overwritten. Retain the exact accepted bytes. Finalization requires separate fresh-install
and upgrade acceptance. For 0.1.0 the upgrade baseline is pinned source revision
`9cc808a`; later versions require the preceding published stable bundle supplied
with `--baseline-bundle` (or the exact `baseline_artifact_id` in Actions). The
maintainer verifies that this retained bundle matches the published release.

Missing source material or failed acceptance returns nonzero and must remain an
incomplete candidate. `built-images.json`, `candidate-manifest.json` and diagnostic archives are not an
installable release; distribute only the completed `release/` contents. Action artifacts are temporary review storage, not durable
public releases. Hosted workflow success must be verified on GitHub separately.

## Source-delivery evidence input

The supplied directory contains flat, regular source/review artifacts and a
`coverage.json` object with format `1` and an `images` object for `api`, `web`,
`tools`. Each image entry names its exact `image_id`, a `review_record`, `files`
(mapping artifact filename to SHA-256) and the reviewed `scopes`:

- API: `os`, `dotnet`, `nuget`.
- Web: `os`, `node`, `npm`, `native`.
- Tools: `os`, `dotnet`, `nuget`, `postgres`.

The review record must identify actual corresponding source/redistribution
coverage for the shipped components and their versions, including permitted
exceptions and delivery instructions. List that review record itself in `files`.
Source manifests must bind to these image IDs; an old image's sidecar cannot be
reused merely because a package version looks unchanged. Native source sidecars
are named by the web entry’s `native_sidecar` field, listed in `files`, and undergo
internal inventory, expected shipped package/component/native-binary fingerprint
and exact web-image-ID verification. Each image also supplies `components`, one
entry per scope/name/version from the generated `source-inventory.json`. Every
component needs inventoried `source_files` or an `exception` containing a reason
and an inventoried review `record`. A review markdown file alone is not source
coverage. Decisions about license obligations remain maintainer-reviewed inputs.

These checks enforce packaging identity and required review inputs; they do not
make a legal determination or prove that arbitrary supplied archives satisfy a
license. Finish the documented dependency/provider review and image source-delivery
review before supplying approved evidence. Current unresolved OS/API/tools source
work remains blocking; an empty manifest or a boolean clearance flag is insufficient.

## Publication checklist

1. Close the outstanding gates in [STATUS](STATUS.md), including provider rights,
   source delivery, security/operations acceptance and maintainer notification checks.
2. Review the exact candidate revision, notes, reports, source material and manifest.
   Audit tracked files and final payloads for secrets, user data and host configuration.
3. Verify all artifact hashes and the tag/commit/image identities. Re-run acceptance
   against these exact images if the candidate changes; never rebuild accepted bytes
   during publication. Keep the candidate and verification records durably.
4. Obtain explicit project-owner authorization to publish this exact release.
5. Create a GitHub release using the existing matching tag, readable release notes
   and every manifest-listed artifact plus `release-manifest.json`. Mark prereleases
   correctly; use GitHub's latest designation only for the intended stable release.
   Do not reuse a published version or silently replace its artifacts.
6. Check anonymous release/download access, checksum verification, installation
   instructions and subscriber notification behavior. Record publication evidence.

Publishing the GitHub release announces availability. Operators can subscribe on
the repository using **Watch → Custom → Releases**, then configure GitHub/email
notifications in their account. No RateFreak mailing list or user email delivery
is introduced. See [GitHub release documentation](https://docs.github.com/en/repositories/releasing-projects-on-github/about-releases).

## Administrator update awareness

Set `RATEFREAK_UPDATES_ENABLED=true` in the operator command's environment (or set
`Updates__Enabled: 'true'` in the protected API Compose override), then recreate
API containers through the reviewed maintenance procedure. An example env file is
documentation; it is not automatically loaded by Python or the API. Disabled is
the default, and manual checks are disabled too.

Current administrators see **Updates** in the moderation navigation once their
access is verified. The page shows installed identity, latest stable version,
check times and release/upgrade links. The server checks lazily, caches success
for 24 hours and coalesces concurrent requests. Explicit checks share a five-minute
minimum interval and longer upstream retry delays. A missing release, network
failure or stale result is explained without falsely claiming current software.
Development/custom builds do not receive a trustworthy upgrade verdict.

Only the API contacts the fixed public GitHub endpoint, without credentials. GitHub
sees the server network address and product version; no account identifiers,
instance origin or provider credentials are sent. There is no browser request to
GitHub, automatic package download, installer or notification-inbox event. The
operator follows the [manual upgrade procedure](self-hosting.md#upgrade).
