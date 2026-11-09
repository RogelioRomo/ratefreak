# ADR 0016: Custom catalog items and list-only collections

Status: Accepted by the user on 2026-11-08; implemented locally, deployment gated.
Supersedes the field/custom-entry direction of [ADR 0006](0006-collections.md).

User-created subjects share the existing catalog UUID, ratings, reviews, comments
and library model. Dynamic categories have independent UUIDs and scoped normalized
names; the four provider media types and provider identity mappings remain stable.
Categories support retained aliases and staff holds. Items default private, with
explicit publication, unpublication, archive and restore-to-private. Contributions
and memberships survive lifecycle changes, with common access predicates and
serialized visibility-sensitive writes. Ordinary staff reads have no private bypass;
reported targets have explicit staff-scoped inspection.

Each custom item may have one image stored on the hosting filesystem outside
wwwroot. Database rows contain only references and metadata. Bounded static image
decoding, immutable promotion before reference commit, post-commit old-file deletion
and delayed leased orphan cleanup handle failures. Access-checked no-store delivery
keeps image visibility aligned with the item. A static public upload directory was
rejected because private or withdrawn images must not remain publicly readable.

Collections hold existing catalog references only. Field schemas, copied titles
and JSON values are retired; collection metadata, ordering, uniqueness and revision
checks remain. Reads filter unavailable memberships and totals, publication requires
eligible members, and explicit owner-confirmed unavailable-membership removal
permits recovery without exposing private identities. Ordinary public-list metadata
edits remain possible after a member becomes unavailable. No automatic pruning.

The schema migration refuses legacy rows rather than deleting them. An offline,
exact-target, dry-run-first test-data reset is separate and requires an explicit
acknowledgement. External consumer confirmation and operator-approved data handling
remain deployment gates. Implementation tests did not reset existing databases;
a subsequent user-authorized local test-data cutover completed on 2026-11-08.
See [current status](../STATUS.md) for integration and deployment state.
See the [operations runbook](../custom-catalog-operations.md) and approved
[design](../superpowers/specs/2026-11-08-custom-catalog-and-list-collections-design.md).
