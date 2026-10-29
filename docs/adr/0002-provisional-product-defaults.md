# ADR 0002: Provisional product defaults

Status: Proposed defaults, not individually confirmed product requirements.
Date: 2026-10-28. Revisit before the phase that implements each rule.

- Canonical types: movie, TV series, album, video game, book. Movie/TV first.
  Seasons, episodes and album editions are deferred until explicitly modeled.
- Ratings: superseded by approved Phase 4 decision in ADR 0005: 0–5 inclusive
  in 0.5 increments; no row means unrated.
- One editable review per user/media pair (approved in Phase 4; see ADR 0005).
- Authenticated users may import, subject to rate limits; moderators correct catalog data.
- Collections permit mixed media and custom entries; public/private, default private.
  Owner-only editing; duplicate-media and schema-deletion policies remain pending Phase 5.
- Store external artwork references; copying/proxying requires licensing review.
- TMDB first, in Phase 3; no provider credentials or adapters in the foundation.

These rules do not block Phase 1 because it creates no business tables or routes.
