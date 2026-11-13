# Dependency and image redistribution review

Reviewed: 2026-11-13. Source revision: `b9d3ca3`. This review records evidence and
remaining release work; it does not establish final artifact clearance or replace
the licenses shipped by each dependency. Provider data is reviewed separately in
[provider terms](provider-terms-review.md).

**Implementation follow-up, 2026-11-13:** [Credits/compliance evidence](release-compliance-verification.md)
supersedes the initial packaging gaps below: all three candidate images now carry
project LICENSE, package notice texts/hashes, existing runtime/OS copyright files,
and native source/build evidence. The web serves its notice bundle from Credits.
Source distribution/relinking, exhaustive final artifact review and the recorded
scroll-bar supplement provenance limitation remain open. The rest of this note
preserves the initial review and its rationale; it is not a claim that all gaps
listed below still describe the current Dockerfiles.

## Result and scope

RateFreak's MIT license applies to project code. It does not relicense dependencies,
fonts, container operating systems, provider data or artwork. The dependency
metadata inspected here does not reveal a requirement to change the project's
license, but redistribution is **not ready**: a complete notice bundle, native
library/source compliance and final artifact inventories remain open.

The [inventory snapshot](dependency-inventory.md) records 63 locally installed web
packages reachable from production dependencies and the restored API package
graph. Package metadata is an initial classification, not proof of every embedded
component's terms. The API graph includes development tools and other platforms;
the web graph includes Next.js build dependencies. Neither is an image SBOM.
No final containers were rebuilt, exported or scanned in this review. Test-only
packages, the EF tool's own graph, OS package graphs and every optional platform
binary have not received an exhaustive license review.

## Findings

| Component | Evidence | Release action |
| --- | --- | --- |
| Web libraries | Installed Next.js 16.4.3, React/React DOM 19.3.0, Radix Select 2.4.1, TanStack Query 5.105.0 and Tabler 3.50.0 declare MIT in their package manifests. The closure also contains ISC, BSD, Apache, CC BY and LGPL declarations; see the inventory. | Retain exact package copyrights, license texts and applicable notices in distributions, including browser bundles. Verify notices survive minification and standalone tracing. |
| sharp/libvips | sharp 0.35.5 declares Apache-2.0. Installed `@img/sharp-libvips-linux-x64` 1.3.4 declares LGPL-3.0-or-later and contains `libvips-cpp.so.8.18.7` plus a `versions.json` listing embedded libraries. Upstream describes prebuilt shared libraries and publishes a separate multi-license notice list. [sharp license](https://raw.githubusercontent.com/lovell/sharp/main/LICENSE), [packaging source](https://github.com/lovell/sharp-libvips), [native notices](https://raw.githubusercontent.com/lovell/sharp-libvips/main/THIRD-PARTY-NOTICES.md). | Determine whether these binaries reach the final standalone image. If shipped, collect exact-version licenses/notices and corresponding source/build information for libvips and embedded components; review LGPL replacement/relinking obligations for the actual linkage and distribution. A package's Apache label does not cover all native code. |
| caniuse-lite | Installed 1.0.30001820 declares CC-BY-4.0. The upstream license requires attribution when licensed material is shared. [License](https://raw.githubusercontent.com/browserslist/caniuse-lite/main/LICENSE). | Determine whether its data is redistributed in each artifact; preserve supplied attribution and license information where applicable. Do not infer a visible UI credit requirement merely from a build-time dependency. |
| .NET packages | The restored API graph declares MIT for Microsoft, Swashbuckle and SkiaSharp packages; Npgsql packages declare PostgreSQL. [Npgsql license](https://raw.githubusercontent.com/npgsql/npgsql/main/LICENSE), [.NET license](https://raw.githubusercontent.com/dotnet/dotnet/main/LICENSE.TXT). | Build notices from exact restored packages and actual publish output, including the tools/migration bundle. PrivateAssets does not by itself prove absence from binaries. |
| SkiaSharp native payload | `SkiaSharp.NativeAssets.Linux.NoDependencies` 4.155.0 includes a separate `THIRD-PARTY-NOTICES.txt` in its NuGet package. It lists embedded third-party material beyond the MIT wrapper. | Preserve the exact package's complete native notices and confirm all shipped native payloads are represented. “NoDependencies” is not an absence-of-licenses claim. |
| .NET/Node runtimes | Both runtimes carry third-party material beyond their primary license. [.NET third-party notices](https://raw.githubusercontent.com/dotnet/runtime/main/THIRD-PARTY-NOTICES.TXT), [Node license and notices](https://raw.githubusercontent.com/nodejs/node/main/LICENSE). | Preserve the notices from the actual pinned runtime versions. The tools Dockerfile copies `/usr/share/dotnet` between images; check that required runtime notices accompany that copy. |
| Fonts | `web/public/fonts/` contains DM Sans and Fraunces WOFF2 subsets and their copyright/OFL texts. The web image copies the complete public directory. [OFL 1.1](https://openfontlicense.org/open-font-license-official-text/). | Retain both OFL files. Confirm exact font provenance/subsetting history and any reserved-name conditions before replacing or modifying the files. They remain OFL, separately from MIT project code. |

Upstream links above establish license families and areas to inspect. Moving
default branches are not immutable evidence for our package versions; final
notices must use the exact package archives and image contents.

## Container distribution boundaries

The Dockerfiles pin base image digests, but install additional packages with
unversioned `apt-get`. A base digest alone therefore does not identify the final
OS dependency graph. Record the final image digest and package inventory for every
released architecture.

| Artifact | Current inputs | Remaining review |
| --- | --- | --- |
| API | .NET ASP.NET `10.0-noble`, Python 3, curl, published API/native libraries | Ubuntu packages, runtime/native notices and package source obligations. |
| Web | Node `24-bookworm-slim`, Python 3, ca-certificates, Next standalone/static/public | Debian packages, actual traced native libraries, browser bundle notices and fonts. |
| Tools | PostgreSQL `17-bookworm`, copied .NET runtime, Python 3, libicu72, API publish and EF migration bundle | Both runtime families, Debian packages, native payload and EF bundle/tool dependencies. |
| Optional ingress | Caddy `2` pinned by digest in Compose | Upstream image and embedded Go dependency notices; verify whether RateFreak distributes it or operators pull it directly. |

Compose references to upstream images are distinct from republishing those images
inside a RateFreak release. They still need documented upstream terms. PostgreSQL
requires its license notices in copies; Caddy is Apache-2.0. OS packages have their
own licenses, including potential source obligations. Preserve package copyright
files and review the distribution method for corresponding sources; an MIT label
on the application does not cover the OS. Sources: [PostgreSQL license](https://www.postgresql.org/about/licence/),
[Caddy license](https://github.com/caddyserver/caddy/blob/master/LICENSE),
[Debian license information](https://www.debian.org/legal/licenses/),
[Ubuntu open-source licenses](https://canonical.com/legal/open-source-licences).

The current Dockerfiles do not explicitly copy the root RateFreak `LICENSE` or
assemble application dependency notices. Existing upstream files may survive in
base layers or packages, but this review did not verify their completeness in
final artifacts. A research note or list of SPDX identifiers is not a substitute
for the required texts and copyright notices.

## Required follow-up before artifact publication

1. Produce per-artifact, per-architecture inventories from the final web/API/tools
   images, browser bundles and migration executable. Include base OS, native
   components, fonts, and any vendored/generated payloads; distinguish build-only
   dependencies from redistributed files.
2. Assemble a versioned third-party notice/license bundle from exact archives,
   retaining copyrights and applicable Apache NOTICE files. Include RateFreak's
   LICENSE in its images and release archives and expose bundled client notices
   in a form recipients can access.
3. Resolve native LGPL and embedded-library source/build/replacement requirements
   and OS source obligations for the chosen image distribution method. Document
   the actual compliance mechanism rather than relying on a moving upstream URL.
4. Add release checks that inspect the built artifacts for these files; record
   their digests and notice/source bundle together. Repeat when dependencies or
   platforms change.
5. Run current vulnerability checks separately before release. This review did
   not run a security audit or establish that these versions are safe/current.

No runtime behavior, provider configuration, dependency version or image was
changed by this review.
