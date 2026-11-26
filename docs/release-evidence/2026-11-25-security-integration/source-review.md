# Security integration source review, 2026-11-25

Nonofficial image/source bindings are in coverage.json, os-image-bindings.json
and installation.tar.gz/SOURCE_REVISION. This is not stable release clearance.
The prior Debian trial source archives are reused by exact source version and
hash, not by their historical image labels. Four changed source versions are
supplied in security-os-sources.tar with authenticated source indexes and
complete descriptor/checksum sets. The archived source-index image identities
precede the JavaScript-only decoder mitigation; installed package versions are
independently checked against these final image inventories.

The unchanged Node/.NET, Sharp/libvips and Skia payloads retain their existing
complete notices and source/provenance limitations. Declaring the already
shipped Sharp version as a direct dependency changes no package version or
native binary. All application notice manifests match the prior trial exactly.
The 406-input native sidecar is verified and rebound to the current web image;
no native rebuild or stronger addon-header/font provenance claim is made.

Retain all prior installed and inherited-layer source archives and notices,
including lower-layer-os-sources.tar. Superseded libzstd 1.5.7+dfsg-1 remains in
the inherited Trixie layer and is covered by the prior debian13-os-sources.tar;
the updated merged-filesystem libzstd is covered by security-os-sources.tar.
Source acquisition/coverage does not establish vulnerability applicability.
Separate PostgreSQL/Caddy images are referenced official services, not bundled
inside the application images or covered by this three-image source mapping.
Deliver the complete matching source/notices if binaries are later published.
