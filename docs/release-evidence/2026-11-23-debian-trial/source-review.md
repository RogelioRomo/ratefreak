# Debian 13 trial corresponding-source review

This source set is bound to the nonofficial trial image IDs and source revision
in coverage.json, installation.tar.gz/SOURCE_REVISION and os-image-bindings.json.
It is not a stable/public release or security clearance. Deliver all matching
materials and complete notices with any authorized binary distribution.

The application, pinned Node 24.22.3, .NET 10.0.15, Skia and sharp/libvips payloads
and grant texts are unchanged from the reviewed security-update source set.
All three collected application notice manifests must match that set byte for
byte. Prior component/runtime decisions and provenance limitations continue only
for those identical components. Preserve all existing runtime, Skia, addon/header,
font, native LGPL/GPL and individual-package texts and replacement instructions.
Node/OpenSSL advisory work remains separate even though no non-OS scanner match
was reported. No native rebuild or addon binary reproduction was repeated.

All 74 new installed Debian/PGDG source versions are supplied in
'debian13-os-sources.tar': 233 complete source inputs verified against signed APT
source indexes and each descriptor's SHA256 list. Where main/security indexes
contain the same source version, every input/hash/size set was compared before
selecting a mirror location. Original signed metadata remains inside the archive.
The unchanged Ubuntu API sources remain in ratefreak-candidate-os-sources.tar
and os-source-updates.tar. Their old image IDs are historical: current mappings
are recorded in os-image-bindings.json and coverage.json.

The inherited Trixie base layers also retain six superseded binary versions
from four additional source versions, despite updated merged filesystems.
'lower-layer-os-sources.tar' supplies these complete descriptor sets, original
inherited copyright texts and exact layer/package bindings. Their descriptors
were fetched over official Debian HTTPS and separately checked with gpgv against
the Debian developer/maintainer keyrings installed from signed APT repositories
in a disposable verification container. These are not claimed to be covered by
the current signed Sources indexes. Preserve the retained signature evidence and
its exact keyring hashes; descriptor verification is not binary reproduction.

Restore source files to original filenames recorded in their manifests, verify
hashes, extract with dpkg-source -x ORIGINAL.dsc, and consult debian/control,
debian/rules and upstream build documentation. All 406 locked native sidecar
inputs and native binary hashes are verified and rebound to the new web image.

No PostgreSQL server/gosu, npm/Corepack/Yarn or optional Next OG implementation
was introduced in final layers. PostgreSQL client17 and libpq have exact current
PGDG source mappings. The separate PostgreSQL/Caddy service images, host kernel,
remaining OS vulnerabilities, provider permission and final publication gates
are not cleared by source reconciliation or acceptance tests.
