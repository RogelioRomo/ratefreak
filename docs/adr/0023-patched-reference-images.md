# ADR 0023: Deliver patched PostgreSQL and Caddy reference images

Date: 2026-11-27. Status: accepted by the owner; qualification and publication pending.

## Context

The reference installation already uses five images: API, web, maintenance tools,
PostgreSQL and Caddy. Initial binary packaging delivered three application images
and referenced upstream PostgreSQL/Caddy images. Fresh security review found
compatible distro fixes not yet present in those upstream service images. The
current risk policy requires available compatible fixes to be applied.

## Decision

Deliver all five images as one reviewed release set. Build reference PostgreSQL17
and Caddy2 images from pinned vendor bases, apply compatible distro package fixes,
and preserve upstream runtime/entrypoint behavior. The Caddy runtime uses the
exact upstream static Go binary on the existing Debian13 base. The vendor Alpine
image is only a build input; its package-manager and OpenSSL-linked helper
payloads are not distributed. Their source/license review could not establish
the needed linking grant, so the reference image avoids those components. Keep the existing three-role
application manifest and add a separate, strictly validated `reference_images`
map for PostgreSQL/Caddy. New source builds select all five identities atomically;
legacy three-image manifest parsing remains supported for retained evidence.

Every image is version/revision-labelled and archive-bound. Selection uses exact
image IDs; instance overrides cannot replace one selected image. A failed load or
partial source build must preserve the previous selected set. Loading a new set
requires explicit prepare/migration acceptance. No automatic upgrade is added.

Reference package/binary/module inventory and original notice material are
collected independently of application notices. Finalization requires exact
reference source/license coverage and fresh/upgrade acceptance naming both
reference IDs. Preserve corresponding sources and original grant texts for
newly redistributed OS/Go/native contents, including inherited layers.

## Consequences

The running service topology stays the same. The maintainer now owns patching,
source/license delivery and qualification for the two additional distributed
images. This avoids waiting for vendor image republishing while retaining normal
vendor package fixes. It does not introduce custom database/server code, clear
unrelated vulnerabilities, or authorize publishing/changing a persistent instance.
