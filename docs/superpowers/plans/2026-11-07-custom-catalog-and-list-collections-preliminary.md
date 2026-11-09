# Custom catalog items and list-only collections — preliminary plan

**Status:** Historical preliminary discussion, superseded by the approved
[2026-11-08 plan](2026-11-08-custom-catalog-and-list-collections.md) and
[ADR 0016](../../adr/0016-custom-catalog-and-list-collections.md). This document records the agreed direction,
proposed technical approach and remaining decisions. It is not an executable
implementation plan or approval to run a database reset now.

**Date:** 2026-11-07.

**Goal:** Let users create and rate anything, while collections organize existing
items into lists.

**Architecture:** Extend the existing catalog item identity and rating/review
system with ownership, visibility and dynamic categories. Reuse collection
metadata, ownership and concurrency controls; replace its custom-entry/field model
with references to catalog items.

**Stack:** Existing ASP.NET Core/Identity API, EF Core/PostgreSQL and Next frontend.
No new hosted authentication or catalog service is implied.

## 1. Agreed product direction

- Users can create catalog items beyond movies, TV, albums and games: flowers,
  restaurants, books or anything else they want to rate.
- Custom items use the existing common structure: title and description, with the
  current rating, review, review-comment and other applicable item features.
- Do not introduce user-defined item fields or category-specific schemas.
- The creator chooses whether an item is private or published to the public catalog.
- Categories are separate from collections. Users explicitly select an existing
  category or create a category; a collection name never creates or names one.
- Public categories are shared and user-created, with duplicate checks and admin
  management rather than mandatory admin approval of every new category.
- Collections become lists of catalog items, including provider-backed and
  user-created items. Ratings/reviews belong to the item, not its list membership.
- The user explicitly accepts losing existing collection entries and field data
  because the current database is for testing. No archive, export or conversion
  workflow is required for that test data.

Example: create **Blue hydrangea**, choose **Flowers**, select Private or Public,
rate/review it, then add the same item to **My garden** and **Gift ideas**. Both
lists refer to the same item and the same personal rating/review.

## 2. Current implementation and what we can reuse

Catalog entities and database constraints currently allow only `movie`, `tv_show`,
`album` and `video_game`. Ratings, reviews, library rows and catalog-backed collection
entries already reference the same catalog item ID. Keep those IDs and relationships.
Do not create a second rating/review system for custom items.

Collections currently implement a small custom database: typed field definitions,
JSON entry data, nullable catalog references and free-standing entry titles. That
is the functionality to remove, rather than extending it to support real ratings.

| Existing collection capability | Proposed treatment |
| --- | --- |
| Name, description and owner | Keep |
| Private/public collection visibility | Keep |
| List/browse/detail and paging | Keep; apply item visibility too |
| Add an existing item | Keep and extend to accessible custom catalog items |
| Entry ordering and removal | Keep; removing membership never deletes the catalog item |
| Parent transaction lock and expected revision | Keep for conflicting writes |
| Unique item per collection | Keep |
| Custom entry title without a catalog item | Remove; create a catalog item first |
| Field definitions/configuration and schema version | Remove from the final list model |
| Entry JSON custom data and field editors | Remove |
| Field deletion-impact preview/field endpoints | Retire along with field support |

Collection membership is not item ownership. A list owner cannot rename someone
else's item, alter their review or delete their catalog item by removing a list row.

## 3. Proposed catalog/category model

These are implementation recommendations, not finalized API names.

**Catalog item:** retain the existing UUID; add creator/ownership information,
visibility, category reference and provenance sufficient to distinguish user-created
items from provider/system content. Existing provider mappings and canonical
metadata remain intact. Existing items migrate as public/system-owned unless an
actual existing ownership rule says otherwise; lack of a provider mapping alone
must not invent an owner or classify an existing row as user-created.

Keep the existing four media-type values for compatibility. Dynamic categories
must not become new hard-coded media types or provider namespaces. A practical
transition is a generic custom type plus a category UUID, with new UI/category
filters independent of legacy media-type filters. Prefer additive changes over a
repository-wide rename of every MediaItem/MediaItemId in the first slice.

**Category:** stable UUID, display name, normalized name, ownership/scope and
moderation state. Seed the existing four categories. Users may reuse public
categories, or explicitly create one. Normalize case/whitespace for exact duplicate
checks; do not assume similar names identify the same thing. Existing private
category names must not become visible through duplicate errors or search.

A private item may use a public category. A public item must use a public category.
When publishing an item from a private category, explicitly select/reuse a public
category or publish/create one, resolving name conflicts. Do not accidentally
publish the creator's other private items or disclose their counts.

**Defaults proposed:** private item creation; public publication is explicit.
Creator edits their item; admins moderate public items/categories. Categories do
not determine image/provider credentials, ownership, visibility or rating scale.

## 4. Visibility must cover the whole existing feature set

Current catalog and many rating/review reads assume all catalog items are public.
Adding a visibility column without changing those readers would be incomplete.
Introduce a common item access policy and use it consistently across:

- Catalog search, category browsing, item detail and direct-ID access.
- Rating reads/writes/aggregates and review list/detail/creation/editing.
- Comments and reports reached through a review, including direct comment URLs.
- Personal library, public profiles, review history and activity surfaces.
- Collection pickers, entry reads, pagination totals and membership writes.
- Moderation projections, caching, invalidation and frontend query keys.

Anonymous users and other ordinary users must not discover a private item's title,
category, description, reviews or derived counts. Unauthorized direct IDs return a
consistent not-found response. A public collection must not expose private member
items; counts and pagination must use the same visible-item filter. Private items
can be added only to their creator's private lists in the initial proposal.

Existing provider-backed items stay public. Each person keeps the existing single
personal rating per item, 0–5 in half-point steps, and the current review rules.
“Comments” means the existing comments on reviews; a separate item-level comment
thread is not implicitly added.

Public user-created content also needs item/category reporting and moderation,
with authorization, CSRF, abuse budgets and credential-free audit records. Extend
existing moderation patterns; do not treat a public item creator as an admin.

## 5. Collections API changes

Simplify the existing collection API in place for this test-stage application,
provided the pre-implementation inventory confirms no deployed/external consumers
need the old field contracts. If there are such consumers, version/deprecate that
contract before removal instead of silently breaking it.

Final contract direction:

- Collection creation/update accepts collection metadata, not field definitions.
- Adding an entry accepts an existing accessible item ID and position, with the
  existing expected-revision protection. Exact public property names are deferred.
- Updating a membership changes list ordering, not the item's title or properties.
- Entry reads project the referenced item's visible summary; they store no second
  custom title, rating, review or schema-driven JSON data.
- Entry deletion removes only that list membership.
- Remove `/fields` routes, deletion-impact routes and their OpenAPI/proxy allowances.
  Strict request validation rejects legacy fields/title/data payloads rather than
  accepting and ignoring them.
- An optional **Create item and add to list** shortcut may follow later. Initially
  keep item creation and list membership explicit, with no empty/orphan item
  created when a membership request fails.

Reuse and simplify `ICollectionQueries`, `ICollectionCommands`,
`ICollectionItemCommands`, `CollectionMutation` and the existing endpoints. Remove
`ICollectionFieldCommands` and field-specific validation/UI/tests when the cutover
is complete. Replace obsolete field tests with list/access-policy coverage; keep
ownership, visibility, revision, transaction, ordering and removal coverage.

## 6. Test database transition and migration

The accepted data loss is specifically existing **collection entries and field
configuration/values** in the current test database, including catalog-backed list
memberships. Preserve collection records and their metadata, users, roles,
provider mappings, catalog items, ratings, reviews and other application data.

Proposed sequence:

1. Add catalog category/ownership/visibility storage and seed existing public
   categories/items. Preserve all existing item UUIDs and provider associations.
2. Implement and verify item access rules before exposing any private custom items.
3. Before the list cutover, stop incompatible writers and identify the intended
   test database. Record relevant before counts and schema version.
4. In a reviewed migration/reset step, clear test collection memberships and field
   definitions/values. Remove the now-unused JSON/title/field/schema-version
   structures and require every new membership to reference a catalog item.
5. Retain collections as empty lists, advance their revisions so stale editors
   cannot submit against a pre-reset revision, and invalidate client caches.
6. Deploy the matching list API/frontend together; verify before/after counts and
   create/add/reorder/remove using public and private custom items.

Do not put a blanket reset into a normal migration that could later run against
an unrelated production database. Keep destructive test-data clearing explicitly
scoped to the disposable/current test database. The schema migration should fail
clearly if incompatible data remains rather than silently deleting it elsewhere.
This preliminary-plan request does not execute any of these steps.

Rollback: additive catalog changes can be rolled back only while new custom data
can still be represented safely by the old app. Once custom items/private content
exist, do not run the old public-only API against them. The old collection schema
can be recreated, but intentionally deleted test entries/field values cannot be
recovered without a separately retained backup. No rollback promises data recovery.

## 7. Delivery phases and affected areas

Implement in dependent slices; do not expose unfinished privacy paths.

- [x] **Phase 1 — finalize policy and contracts.** Resolve the decisions below;
  write an approved design/ADR and executable implementation plan. Update
  ADR 0006's collection direction when the replacement design is approved.
  Completed 2026-11-08: [approved replacement design](../specs/2026-11-08-custom-catalog-and-list-collections-design.md)
  and [executable plan](2026-11-08-custom-catalog-and-list-collections.md).
  Native implementation and independent review completed in the isolated worktree;
  real cutover remains gated and no existing database reset was executed.
- [x] **Phase 2 — catalog/category foundation and access policy.** Domain/catalog
  models, `RateFreakDbContext` configurations/migrations, category/item contracts,
  access checks across personal media, social and collection readers/writers.
  Prove owner/private/public behavior before public routing/UI activation.
- [x] **Phase 3 — user-created item/category API and moderation.** Create/edit,
  publish/visibility changes, category reuse/creation and conflict handling,
  reporting/moderation, audit and abuse controls. Provider import keeps its existing
  trusted behavior and does not accept arbitrary user categories as provider types.
- [x] **Phase 4 — custom item frontend.** Create item, category picker/create,
  visibility choice, own-item management and common item detail using existing
  rating/review/comment controls. Update discovery/filter labels and applicable
  library/profile surfaces; no dynamic field builder.
- [x] **Phase 5 — list-only collection cutover.** Scoped test-data reset, simpler
  relational memberships and API, remove field/free-title editors, and use one
  accessible-item picker for existing and user-created catalog items.
- [x] **Phase 6 — complete acceptance.** Fresh PostgreSQL migrations, existing
  provider regression suite, private-data leak/concurrency tests, API/proxy
  contracts, frontend tests, build, keyboard and responsive Light/Dark browser
  journeys. Record results and obtain independent review before merge.

## 8. Decisions still needed before implementation

These do not block this preliminary document; they do affect the final design.

1. **Public-to-private changes and deletion:** what should happen to other users'
   reviews and list memberships? Proposed: restrict visibility consistently,
   preserve stored contributions, and archive rather than cascade-delete shared
   items. Do not choose a destructive behavior silently.
2. **Public item edits:** how much may the creator change after others have rated
   it? Proposed: correct details without replacing what the item represents;
   disputes and material changes need moderation rules.
3. **Category management:** public name normalization/uniqueness, rename/merge
   behavior, private category ownership and whether new public categories are
   immediately visible. User creation without mandatory approval is the agreed
   direction; exact admin operations remain to be designed.
4. **Common item fields:** title/description are confirmed. Decide whether manual
   artwork is part of the first slice; release dates/provider-specific fields
   should not become mandatory for flowers or other generic subjects.
5. **Library language:** reuse existing behavior where applicable, but decide how
   media-oriented tracking labels should read for non-media items.

## Source references

- `src/RateFreak.Domain/Catalog/MediaItem.cs`
- `src/RateFreak.Infrastructure/Persistence/Configurations/CatalogConfiguration.cs`
- `src/RateFreak.Infrastructure/PersonalMedia/RatingService.cs` and `ReviewService.cs`
- `src/RateFreak.Api/Endpoints/CollectionEndpoints.cs`
- `src/RateFreak.Application/Collections/Collections.cs` and `CollectionRequests.cs`
- `src/RateFreak.Infrastructure/Collections/CollectionItemCommands.cs`,
  `CollectionQueries.cs` and `CollectionFieldCommands.cs`
- [Existing collection ADR](../../adr/0006-collections.md) (current implementation;
  this preliminary proposal does not yet supersede its accepted contract).


## Implementation completion — 2026-11-08

Phases above describe local implementation and verification. Phase 5 includes the
guarded reset tool and disposable-database proof, **not** a live reset. See the
[execution evidence](2026-11-08-custom-catalog-and-list-collections.md#execution-evidence--2026-11-08)
and [ADR 0016](../../adr/0016-custom-catalog-and-list-collections.md).
External consumers and real cutover require operator confirmation. Historical open
decisions in section 8 were resolved in the approved replacement design.
