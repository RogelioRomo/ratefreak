# ADR 0007: MusicBrainz album identity and provider selection

Status: Accepted, 2026-10-30. User approved album-first Phase 6 scope and the written
MusicBrainz design, then delegated implementation planning/execution.

Add the canonical album type and MusicBrainz release-group identities, deferring
edition/track entities. Primary-type Album only; secondary-type nonmusical entries,
EPs and singles excluded. Exact partial first-release precision remains metadata;
no fabricated DateOnly values. No artwork/annotation/tags/provider rating import.

Explicit provider registry selects implemented adapters with provider-specific ID
validation. Search defaults to TMDB for backwards compatibility; MusicBrainz requires
explicit selection. Shared transactional import preserves mapped records and
returns the winner of duplicate races. Each provider has a distinct named HTTP
client so handler/header/connection settings cannot affect the other adapter.

MusicBrainz is disabled until chosen maintainer contact is configured. No API key
or user account needed for read-only access. No inference or disclosure of the
user's Git email. A singleton monotonic-time gate spaces all MusicBrainz request
starts by at least one second, permits one operation/no queue, and shares Retry-After
cooldown across adapter instances. At most one retry and an overall timeout.
This is process-local; coordinate multi-instance/shared-IP access before deployment.

Widen the existing catalog type constraint only. Down explicitly refuses if albums
exist; never automatically delete user/catalog data to downgrade. Existing movie/TV
IDs and mappings remain unchanged. Personal-media and collection lifecycles remain
independent; albums reuse the existing contracts.

Alternatives: separate provider-specific endpoint/import pipelines duplicate
security and transaction behavior; arbitrary configured API adapters cannot express
normalization rules. Release-edition catalog entities would split album ratings.
Both are unnecessary for this slice. Social features and further providers remain
Phase 6 follow-ups, not silently included in this implementation.

See design, README and checklist for detailed API bounds and verification evidence.
Live MusicBrainz verification awaits user-chosen maintainer contact. Review provider
terms/commercial-service arrangements and all existing launch safeguards separately.
