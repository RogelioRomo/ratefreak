# Release notice collection

Python 3 (standard library only) collects actual license/notice texts from restored
packages. It never grants rights or declares an image legally cleared.

From the repository root, after frozen frontend install and .NET restore:

```bash
pnpm --dir web notices
python3 deploy/compliance/notices.py nuget "$HOME/.nuget/packages" /tmp/ratefreak-api-notices --assets src/RateFreak.Api/obj/project.assets.json --supplements deploy/compliance/supplements --project-license LICENSE --evidence deploy/compliance/native --check
python3 -m unittest discover -s deploy/compliance -p 'test_*.py'
```

`pnpm dev` and `pnpm build` generate web notices automatically. The web production
dependency closure includes installed optional dependencies; unavailable optional
platform binaries are omitted. Required unresolved dependencies stop collection.
NuGet collection with `--assets` uses the restored API graph and explicitly requested additional
packages (`--include-package dotnet-ef/10.0.2` in the tools image); build-only dependencies and other-platform native assets are
included as a conservative notice superset. Test dependencies are excluded unless
their project assets are explicitly supplied. The tool package's bundled content
is covered by recursively collected texts; this does not audit its binary closure.

`manifest.json` records versions, declared licenses, metadata/text hashes and
supplement provenance. `--check` fails for a missing primary license text or a
supplement hash mismatch. **Text presence is not license applicability or complete
compliance.** In particular, the scroll-bar supplement records its version
provenance limit and native corresponding-source obligations remain separate.
The generated concatenated text is `THIRD-PARTY-NOTICES.txt`; the project license
is `LICENSE.txt`. Generated web output is ignored by Git and must not be edited.

All three production Dockerfiles collect notices in their build stages, preserve
them in runtime images and capture runtime evidence after OS package installation:

| Image | Notice directory |
| --- | --- |
| API/tools | `/app/notices/` |
| Web | `/app/public/notices/` (served under `/notices/`) |

`runtime_evidence.py` records OS package/source-package versions, native binary
hashes and host-architecture `ldd` results, embedded libvips component versions,
and existing runtime/OS copyright files in `runtime-license-texts.tar.gz`.
This archive supplements package notices; preserve both. It does not download OS
corresponding sources or prove a binary can be rebuilt/relinked.

`supplements/sources.json` pins retrieved license evidence by hash and source URL.
`native/sources.json` pins the selected libvips packaging recipe/version file;
`native/source-acquisition.json` records source archive/patch hashes. Acquired
archives are retained locally in `.local/release-compliance/native-sources/`,
outside Git. They are not published automatically. Missing Rust crate sources,
platform toolchains, source-delivery obligations and reproducibility are described
in [the release evidence](../../docs/release-compliance-verification.md).

On dependency updates, recollect notices, review changed provenance/licenses,
refresh native source evidence, build images and verify their actual payloads.
Do not copy an old manifest onto a new image or interpret a passed text-presence
check as resolving the remaining release gates.
