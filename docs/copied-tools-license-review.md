# Copied runtime tools: source and notice review

Reviewed 2026-11-21. This note covers gosu and the copied PostgreSQL/Node Docker scripts, not PostgreSQL itself, Node itself, OS packages, npm packages, or the complete images. Findings are tied to the disposable image identities in `.local/release-compliance/runtime-fix-2026-11-21/verification.json` and `source-inventory.json`.

Subsequent integration: [runtime delivery verification](runtime-redistribution-verification.md)
records the completed notice packaging and decision binding in new disposable
proof images. The acquisition-phase descriptions below retain their original scope.

The exact binaries/scripts have identifiable permissive upstream licenses. Their reviewed licenses do not impose a corresponding-source distribution requirement. **This supports specific source decisions for the hashes below; it does not establish that existing distributed notices meet their attribution obligations.** Source archives and candidate notice material are preserved under `.local/release-compliance/runtime-review-2026-11-21/tools/`. No production manifests or images were changed during this review.

## Exact source identity

| Runtime file | SHA-256 | Exact matching upstream source | Source decision |
| --- | --- | --- | --- |
| tools `/usr/local/bin/gosu` | `52c8749d0142edd234e9d6bd5237dff2d81e71f43537e2f4f66f75dd4b243dd0` | Official gosu 1.19 `gosu-amd64` release, source tag commit `6456aaa0f3c854d199d0f037f068eb97515b7513` | No source provision required by reviewed Apache/BSD/MIT terms; full pinned sources preserved voluntarily. |
| tools `/usr/local/bin/docker-entrypoint.sh` | `9c440299ae04a0a79d55b8bf03307036d890a40979d2fb698073c9050d4b20a5` | docker-library/postgres commit `4f9ced003ba58a854656ba150d146243d27ae3ac`, root script | MIT; no source provision required. Script is itself source; preserve accompanying MIT notice. |
| tools `/usr/local/bin/docker-ensure-initdb.sh` | `922ade6b23a312e65023349b36e873752b15ff38ae4c0ae461d74451e104f312` | Same PostgreSQL commit, `docker-ensure-initdb.sh`; also matches its last content-change commit `2c6fe8daca9d2ccc143afe6b1cdbc1eb80379d3f` | MIT; no source provision required. |
| tools `/usr/local/bin/docker-enforce-initdb.sh` | `922ade6b23a312e65023349b36e873752b15ff38ae4c0ae461d74451e104f312` | Byte-identical to the preceding ensure script; the script explicitly branches on its invoked name | Same specific MIT decision. |
| web `/usr/local/bin/docker-entrypoint.sh` | `a15ac9589c04baf9da95b08e0e79b5cf1d75ab8dc64e06a5e68e4ceb0ad7c8ea` | nodejs/docker-node commit `3101ce6b5b3a0308b58d464eef141e0043c3bf5b`, root script | MIT; no source provision required. |

The upstream commit references identify exact matching content, not proof of which historical Docker image build first copied it. Hash comparisons against the inventoried bytes are the identity evidence. The [PostgreSQL script](https://github.com/docker-library/postgres/blob/4f9ced003ba58a854656ba150d146243d27ae3ac/docker-entrypoint.sh), [ensure script](https://github.com/docker-library/postgres/blob/4f9ced003ba58a854656ba150d146243d27ae3ac/docker-ensure-initdb.sh), and [Node script](https://github.com/nodejs/docker-node/blob/3101ce6b5b3a0308b58d464eef141e0043c3bf5b/docker-entrypoint.sh) are preserved as files and complete `git archive` source snapshots. Local `script-provenance.json` records the comparisons. The downloaded [official gosu release](https://github.com/tianon/gosu/releases/tag/1.19) and bytes read from the tools image both produce the inventoried gosu SHA-256.

## License and notice findings

PostgreSQL Docker scripts use the repository's MIT license, copyright 2014 Docker PostgreSQL Authors, referencing AUTHORS. Retain the copyright and permission notice with copies/substantial portions. Both LICENSE and AUTHORS are preserved in the candidate notice file and source archives. This is the Docker image tooling license, separately from the database license. [Pinned PostgreSQL Docker license](https://github.com/docker-library/postgres/blob/4f9ced003ba58a854656ba150d146243d27ae3ac/LICENSE).

The Node Docker script uses MIT with copyright 2015 Joyent, Inc. and Node.js contributors. Retain its copyright and permission notice. Node's own license file is a different artifact and should not be assumed to cover this script's copyright. [Pinned Docker Node license](https://github.com/nodejs/docker-node/blob/3101ce6b5b3a0308b58d464eef141e0043c3bf5b/LICENSE).

Gosu 1.19 uses Apache-2.0. Redistribution requires the license, prominent notices for modified files, preservation of applicable source attributions when distributing derivative source, and applicable upstream NOTICE attributions when supplied. The 1.19 source tree has no root NOTICE. Its `setup-user.go` explicitly identifies a copied and modified runc function, so the review also preserves that origin's license and NOTICE. The copied function does **not** make runc's entire dependency graph a gosu dependency. [Gosu license](https://github.com/tianon/gosu/blob/6456aaa0f3c854d199d0f037f068eb97515b7513/LICENSE), [copied-function provenance](https://github.com/tianon/gosu/blob/6456aaa0f3c854d199d0f037f068eb97515b7513/setup-user.go), [runc NOTICE at the cited original commit](https://github.com/opencontainers/runc/blob/18c313be729dd02b17934af41e32116a28b4b3bf/NOTICE).

Embedded Go build metadata confirms `go1.24.6`, `CGO_ENABLED=0`, `linux/amd64`, `GOAMD64=v1`, gosu `v1.19.0`, and exactly these external modules:

| Included component | License and retained material |
| --- | --- |
| `github.com/moby/sys/user v0.1.0`, module checksum `h1:WmZ93f5Ux6het5iituh9x2zAG7NFY9Aqi49jjE1PaQg=` | Apache-2.0; exact module zip and LICENSE. No NOTICE file in that module zip. [Upstream module](https://github.com/moby/sys/tree/user/v0.1.0/user). |
| `golang.org/x/sys v0.1.0`, module checksum `h1:kunALQeHf1/185U1i0GOB/fy1IPRDDpuoOOqRReG57U=` | BSD-3-Clause; exact module zip, LICENSE, PATENTS, selected source headers. [Versioned license](https://github.com/golang/sys/blob/v0.1.0/LICENSE). |
| Go 1.24.6 runtime and standard library | BSD-3-Clause base license; source archive, LICENSE, PATENTS, selected source headers. Binary redistribution must reproduce the copyright, conditions and disclaimer in accompanying material; endorsement restriction remains applicable. [Versioned Go license](https://github.com/golang/go/blob/go1.24.6/LICENSE). |
| runc copied `SetupUser` function | Apache-2.0; original `init_linux.go`, LICENSE and full NOTICE, including Docker 2012–2015 attribution. |
| Go `runtime/memmove_amd64.s` | Additional Lucent Technologies 1994–1999 / Vita Nuova 2000–2007 MIT-style permission and disclaimer appear in the source header. Preserve the full header. [Exact Go source](https://github.com/golang/go/blob/go1.24.6/src/runtime/memmove_amd64.s). |

The Go base license alone would omit the last attribution. Using the exact Go 1.24.6 compiler, `CGO_ENABLED=0 go list -deps -json ./...` against gosu 1.19 selected 66 packages and 652 source/assembly files. `gosu-selected-files.json` records file hashes; `gosu-selected-file-headers.txt` conservatively retains all selected initial comment headers, including the full Lucent/Vita Nuova permission text. This intentionally covers more source than the linker may retain. The source-selection evidence is not a claim of reproducible binary rebuilding. Gosu's upstream build uses synthetic VCS metadata, so its embedded `vcs.revision=1.19` is a release version, not a git object ID. [Exact build recipe](https://github.com/tianon/gosu/blob/6456aaa0f3c854d199d0f037f068eb97515b7513/Dockerfile).

## Preserved artifacts and remaining integration work

`download-manifest.json` records upstream URLs, artifact hashes and archive generation methods. `file-manifest.json` hashes preserved evidence and candidate notices; it excludes cloned repositories and the temporary extracted compiler installation. Complete sources are `gosu-1.19.tar.gz`, the two versioned Go module zips, `go1.24.6.src.tar.gz`, and commit-named PostgreSQL/Node source archives. The compiler's Linux binary archive is verification tooling only. `gosu-build-info.txt` records decoded binary metadata.

`COPIED-TOOLS-LICENSE-NOTICES.txt` is concrete candidate distribution material containing the script licenses, PostgreSQL AUTHORS, Apache licenses, runc NOTICE, Go/x/sys licenses and patent grants, and conservative selected source headers. It is local review evidence, **not yet wired into a release**.

The tools image's existing `/app/notices/runtime/runtime-license-texts.tar.gz` file list and concatenated contents were captured as `image-runtime-license-file-list.txt` and `image-runtime-license-texts.txt`. The file list contains OS/common-license material and does not identify dedicated gosu, Docker PostgreSQL or Docker Node notice files. A generic Apache text does not substitute for missing component-specific notices. This review does not assert their absence from every other image path: final release validation must confirm that the candidate notices, or equivalent complete material, actually accompany the relevant image/release.

Before clearing the copied-tool release items, attach these specific source decisions to each exact inventory entry, package the applicable notices for tools and web, and verify them in the final images. Keep the remaining runtime components under their separate reviews. No blanket exception for future scripts, gosu versions, architectures or Go toolchains is supported by this evidence.
