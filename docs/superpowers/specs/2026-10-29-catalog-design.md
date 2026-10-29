# Phase 2 catalog design

Continue the approved architecture with canonical movie and TV-series records and
public read endpoints. No seasons, episodes, providers, import/write routes or demo
seed data. Existing account protections and pending verification/recovery remain.

Use a feature-specific application query interface implemented with no-tracking EF
queries. This keeps HTTP and EF outside Domain without adding a repository or
mediator. A direct EF endpoint would be shorter but would bypass the existing
layer boundary. A full search engine is unnecessary for the initial catalog.

Media uses UUIDs, UTC timestamps, nullable date-only release dates, bounded title,
description and artwork reference, and JSONB metadata restricted to an object.
External identity is unique by (provider, namespace, external ID), allowing TMDB's
movie and TV numeric IDs to overlap. Multiple mappings may point to one media item;
no title-based merging. Foreign keys restrict canonical deletion.

GET /api/v1/media accepts q (trimmed, <=200 characters), type (movie or tv_show),
page (default 1, 1..10000), pageSize (default 20, 1..100). Title search is literal,
case-insensitive substring matching; SQL LIKE metacharacters are escaped. Stable
ordering is title then UUID. Return items, page, pageSize and totalCount. Lists omit
large description/metadata; details include these plus external mappings, never
account data. Missing or malformed UUIDs return 404. Invalid query input returns
400 Problem Details. Anonymous catalog reads share a separate 60/minute IP budget.
Offset pagination and substring scans are deliberate initial limits; add targeted
search indexing when data volume justifies it.

These are implementation defaults under the specification's conservative-default
rule. Provider URLs/artwork are references only and never fetched by this slice.
