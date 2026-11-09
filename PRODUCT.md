# Product

<!-- impeccable:product-schema 1 -->

## Platform

web

## Product Purpose

RateFreak brings movies, TV, albums and video games into one personal media
catalog, alongside user-created subjects, library tracking, ratings, reviews and lists.
The approved delivery roadmap is `docs/frontend-checklist.md` and its linked design.

## Users

Repository-derived audience: people who want to remember and organize the media
they enjoy across formats. More specific audience research remains open.

## Capabilities and Constraints

The frontend currently supports local registration, login and account/session
management, plus public catalog browsing, title/type search, pagination and
media details, provider discovery/preview and explicit authenticated catalog import.
Private library tracking, ratings and item-scoped reviews/comments use the common
catalog identity. People can create private or public custom items with title,
description, an explicit category and one optional uploaded image stored on the host
filesystem outside wwwroot. Own-item management includes corrections, explicit
publication, private changes and reversible archival. Public categories are shared
immediately, with scoped duplicate checks and admin rename/merge/hide/restore.
Collections organize existing accessible catalog items as lists, with ordering,
revision-safe editing and membership removal that preserves ratings/reviews.
Public list results/counts apply item visibility. Dynamic field builders and
free-standing custom entries are retired. Existing databases require the separately
scoped, reviewed test-data cutover before the list schema can apply.

Use the existing Next.js App Router, TypeScript, CSS Modules and TanStack Query.
Keep object interfaces in separate files. The .NET API owns business rules and
authentication. Do not invent social proof, catalog counts or member activity.

## Brand Commitments

The name is RateFreak. On 2026-10-31 the user rejected the existing homepage and
selected an editorial culture journal direction: warm ivory, near-black type,
deep red accents and a collage of film, music and game imagery.

The user approved the completed homepage, requested the same design for existing
account pages, and requested a dark theme. Light, Dark and System preferences
are available throughout the app; the user subsequently selected Dark as the first-visit
default and blue as its accent. This preference is independent of account identity.

The user subsequently requested icons instead of a theme dropdown and SVG
instead of the raster collage. Current artwork is original geometric SVG;
interface icons use Tabler. Preserve accessible names and selected theme states.

## Evidence on Hand

Product and implementation evidence is in `docs/frontend-checklist.md`.
No testimonials or usage statistics are supplied. Homepage illustrative content
must not imply an actual user's library or a live catalog feed.
