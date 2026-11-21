# Remaining image source delivery research

Researched: 2026-11-21. This is an acquisition and review approach for the
self-hosted API, web and tools images. It does not approve their redistribution,
claim that source archives have been acquired, or close provider clearance.
The existing [native sidecar](native-source-package-verification.md) covers a
separate, verified web-native scope.

## Recommended delivery approach

For each released image and architecture, retain the exact binary inventory,
map every component to source materials or an individually reviewed exception,
and publish required source archives next to the matching binary artifacts.
Prefer collecting all distribution source packages over guessing which OS
packages need source. This conservative collection policy is an engineering
choice; it is not a claim that every package has identical obligations.

GPLv2 section 3 includes distribution with corresponding machine-readable source
and equivalent source access from the designated binary download location. Its
definition includes compilation and installation scripts. GPLv3 sections 1 and
6 similarly address corresponding source and network distribution, with explicit
rules for equivalent access and continued availability. Which provision applies
depends on each shipped component and distribution method. These are reasons to
deliver actual version-matched material, rather than a list of upstream homepages.
See the [GPLv2 text](https://www.gnu.org/licenses/old-licenses/gpl-2.0.en.html)
and [GPLv3 text](https://www.gnu.org/licenses/gpl-3.0.html).

Do not treat an OS library exclusion from another work's corresponding source
as a blanket exemption for binaries that the release itself distributes. The
GPLv2 system-component exception expressly addresses components accompanying the
executable; GPLv3 also distinguishes the covered work, system libraries and
separable object-code portions. Apply actual component licenses in the review,
not an image-wide MIT label. The references above establish those distinctions,
not a conclusion about every binary in RateFreak.

## Exact OS package acquisition

The final images mix Ubuntu noble, Debian bookworm and PostgreSQL's PGDG
repository. Record the image ID/digest and architecture first. From each image,
collect the following fields, keeping only installed packages for the merged
filesystem inventory:

```text
binary:Package, Version, Architecture, db:Status-Status,
source:Package, source:Version
```

`dpkg-query` supplies the binary/source virtual fields; binary versions and source
versions can differ. Preserve epochs and distribution revisions exactly.
See the [dpkg-query manual](https://manpages.debian.org/bookworm/dpkg/dpkg-query.1.en.html).

In a disposable collector with matching signed `deb-src` repositories, retrieve
each unique source-package/version pair using:

```bash
apt-get source --download-only 'SOURCE-PACKAGE=EXACT-SOURCE-VERSION'
```

APT supports exact source version selection; omitting the version selects the
newest available source and is unsuitable for this mapping. Source indexes are
separate from binary indexes. See the [APT manual](https://manpages.debian.org/bookworm/apt/apt-get.8.en.html).

Retain the `.dsc` and every file it names, including original upstream archives,
additional component archives, Debian packaging tarballs and historical diff
formats. Check source name/version and file hashes/sizes against retained signed
repository metadata and the descriptor; keep the authentication evidence. A
recomputed hash proves integrity against that recorded value, not authenticity by
itself. Debian's source format includes packaging and the build rules; a bare
upstream tarball omits distribution patches and instructions.
See [Debian source-package policy](https://www.debian.org/doc/debian-policy/ch-source.html).

Historical acquisition routes:

| Origin | Official source route | Qualification |
| --- | --- | --- |
| Debian | [Debian Snapshot](https://snapshot.debian.org/) indexes historical package names, versions and archive timestamps; it supports `deb-src` snapshots, including the separate security archive. | Record which snapshot contains the exact source version. Old `Valid-Until` handling is separate from signature validation. |
| Ubuntu | [Ubuntu Snapshot](https://snapshot.ubuntu.com/) provides time-selected archives after 2023-03-01; noble APT supports snapshot selection. | Its stated intended availability is at least two years. Preserve acquired bytes in the release source set rather than relying indefinitely on this service. |
| PostgreSQL PGDG | [PGDG historical archive](https://apt-archive.postgresql.org/) provides `DIST-pgdg-archive` suites whose Sources indexes contain historical versions. | Use `bookworm-pgdg-archive` for these tools-image packages when applicable; a normal `DIST-pgdg` archive only retains the latest version. Debian Snapshot is not the PGDG source repository. |

If the exact version is absent from a current index, select an appropriate
historical index and retry the exact version. Do not silently substitute the
latest source. An unavailable source version remains an explicit gap.

## Runtimes and other native payloads

The retained local candidate inventory at
`.local/release-versioning/real-candidate/source-inventory.json` records Node
`v24.22.2`, .NET runtime/shared framework `10.0.14`, PostgreSQL client
`17.11-1.pgdg12+2`, SkiaSharp `4.155.0`, and Next/SWC `16.4.6`.
These identify the inspected candidate, not future final images. The following
upstream evidence guides review; image-extracted copies must still be compared.

| Component | Evidence and review action |
| --- | --- |
| Node | The [v24.22.2 license](https://raw.githubusercontent.com/nodejs/node/v24.22.2/LICENSE) includes the MIT grant and bundled dependency terms, including OpenSSL under Apache-2.0. Preserve the complete file and applicable notices. A no-source-required decision needs a review of the bundled components, not merely Node's top-level MIT label. If collecting source, use its exact release source archive and verify signed checksums using the [official verification instructions](https://github.com/nodejs/node#verifying-binaries). |
| .NET | The [project MIT license](https://raw.githubusercontent.com/dotnet/dotnet/main/LICENSE.TXT) requires retaining the grant/copyright notice; the exact [runtime v10.0.14 third-party notices](https://raw.githubusercontent.com/dotnet/runtime/v10.0.14/THIRD-PARTY-NOTICES.TXT) identify separately licensed content. Preserve the runtime and ASP.NET framework notices actually shipped in API/tools, including the copied `/usr/share/dotnet` tree. A source exception must document those components and exact notice evidence. The moving project MIT page alone is not version-specific artifact evidence. |
| PostgreSQL | Its [license](https://www.postgresql.org/about/licence/) requires retaining its copyright and permission/disclaimer paragraphs in copies; that grant does not impose a general source-publication condition. OS dependencies and distribution patches retain separate obligations. Acquiring the exact PGDG source package is still a useful conservative policy. |
| SkiaSharp | The [v4.155.0 wrapper license](https://raw.githubusercontent.com/mono/SkiaSharp/v4.155.0/LICENSE.md) is MIT. It does not establish the terms of all code inside `libSkiaSharp.so`. Inspect the exact NuGet native package's full `THIRD-PARTY-NOTICES.txt`, embedded components and published native output before recording an exception. “NoDependencies” is a package name, not a license conclusion. |
| Next/SWC | The [Next v16.4.6 license](https://raw.githubusercontent.com/vercel/next.js/v16.4.6/license.md) is MIT; [SWC's license](https://raw.githubusercontent.com/swc-project/swc/main/LICENSE) is Apache-2.0, whose redistribution section addresses license copies and applicable notices. Neither alone reviews the Rust dependency closure embedded in the exact `@next/swc-linux-x64-gnu` binary. Review that exact package and linked/embedded dependencies before asserting a source exception. The SWC main-branch reference establishes general terms, not an exact binary build inventory. |

MIT and Apache grants permit object distribution under their stated conditions;
they do not make a complete source archive universally mandatory. Conversely,
mapping an entire native aggregate to a top-level permissive license without
checking its embedded dependencies is insufficient evidence. Individual source
exceptions should state why no source-delivery condition applies and identify
the exact retained license/notice files and reviewed component scope.

## Artifact boundary and acceptance

Inspect all distributed layers as well as the merged filesystem. The OCI format
represents filesystem changes in layers and deletion through whiteout markers;
later deletion hides lower-layer content when applying the layers. The engineering
inference is that an inventory taken only from the running merged filesystem can
miss earlier binaries still present in a distributed image archive. Record and
review that content or prove the chosen output excludes it.
See the [OCI layer specification](https://github.com/opencontainers/image-spec/blob/main/layer.md#whiteouts).

Before calling the source-delivery gate complete:

- Match every image/architecture and component/version to source files or a
  documented reviewed exception; preserve checksums and acquisition evidence.
- Include distribution build/patch materials, RateFreak Dockerfiles and source
  tree, source usage/build instructions and existing native rebuild evidence.
- Review files copied outside package management, generated/bundled browser code,
  the EF executable, fonts, native aggregates and all published image layers.
- Validate archive contents, safe extraction boundaries and component coverage;
  test representative source extraction/build instructions. Checksum validation
  alone does not prove build completeness or bit-for-bit reproduction.
- Publish required sources with the matching binaries and clear retrieval
  directions when publication is authorized. A local archive is preparatory
  evidence, not an actual public delivery event.

Provider terms, vulnerability review, deployment acceptance and publication
authorization remain separate gates. This research changes none of those states.
