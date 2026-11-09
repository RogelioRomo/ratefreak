# Collection lists surface

Updated 2026-11-09. Mode: Operate. Extend the editorial system in `DESIGN.md`:
Fraunces headings, DM Sans reading/controls, flat divided sections and semantic
Dark blue/Light oxblood accents. Reuse existing buttons, feedback and forms.
Current product status is in `docs/STATUS.md`; ADR 0016 replaces field-based entries.

## Browsing and metadata

`/collections` retains All accessible/My collections, explicit Apply, URL-owned
scope/page and semantic rows with real title, owner, description and visibility.
Keep native labeled controls, empty/error/login recovery and responsive wrapping.
`/collections/new` uses the existing desktop heading/form grid, stacking at 48rem.
Metadata edits preserve revision-bound drafts and explicit reload after conflicts.

## List memberships

Details show Items and owner-only Edit metadata/Add item actions. Place 1.5rem
below that button row before the next divider. Add existing accessible catalog
items through explicit search/selection; creation of a new subject happens in
`/catalog/new`. Each membership points to the original item and personal activity.
No dynamic fields, free-standing titles, arbitrary values or schema editors remain.

Owners reorder memberships and explicitly confirm removal. Explain that removing
membership preserves the catalog item, ratings and reviews. Unavailable items
have a separate confirmed clear action without revealing private item identities.
Keep only one inline editor active; failed writes preserve drafts, unknown outcomes
block replay and confirmed saves with failed reads offer read-only refresh recovery.

## Sessions and layout

Routine background checks retain the last verified identity and leave navigation,
content and drafts visible and usable. Initial discovery, explicit transitions and
failed checks still conceal/disable private content. Confirmed account changes
remount viewer state. The API enforces access independently of cached UI identity.

Keep wrapping action groups, visible labels/focus and 3rem form controls. Summary
rows stack at 32rem and heading/grid layouts at 48rem. No new visual tokens or
animation are introduced by these changes.

## Historical evidence

The following evidence describes the original metadata/field-based surface on
2026-11-01; it is not browser verification of the replacement list UI. Later
catalog/list evidence is in `docs/frontend-release-verification.md` (2026-11-08).

Grounded in `web/features/collections/components/*.tsx`,
`collections.module.css`, `web/app/globals.css`, `PRODUCT.md` and the approved
`docs/superpowers/specs/2026-11-01-frontend-collections-design.md`.
The visual reviewer inspected all 39 stored captures through
`.impeccable/review/collections/dark-contact.png`, `light-contact.png`,
`dark-mid-contact.png`, `light-mid-contact.png` and `states-contact.png`.
Both themes cover list/create/detail/edit at 320/768/1024/1440px, plus anonymous,
conflict, create-error, empty, error, missing and saved states. The reviewer found
no material visual fixes. These captures establish visual/state evidence, not
live authenticated persistence or collection-entry functionality.

Subsequent user-requested live verification passed 58 real PostgreSQL integration
tests and 19 browser assertions through the running frontend/backend/database,
without API interception. Live evidence is in `.impeccable/review/collections-live/`
and `docs/frontend-checklist.md`; only temporary verification accounts/collections
were written and all three were removed afterward. Product code is unchanged.


Collection-contents extension evidence (2026-11-01): grounded in the collection
workspace, field manager/editor/configuration, entry editor/value controls/value
readout, entry detail and field/entry removal components, their CSS module and
global theme tokens; approved scope is
`docs/superpowers/specs/2026-11-01-frontend-collection-contents-design.md`.
Fresh visual review returned SHIP with no material issues for 46 captures in
`.impeccable/review/collection-contents/`: five primary contact sheets covering
collection, field-editor, entry-create, entry-detail and entry-edit across both
themes and 320/768/1024/1440px, plus six state captures. Real browser verification
passed 17 checks; 442 frontend tests, 58 real PostgreSQL integration tests, lint,
typecheck and production build passed. This extends, rather than replaces, the
metadata evidence above. Supplemental live stress results are recorded in
`docs/frontend-checklist.md`.
