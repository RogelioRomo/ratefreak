# Collection metadata, fields and entries surface

Implemented 2026-11-01. Mode: Operate. Extend the approved independent culture
journal in `DESIGN.md`: Fraunces headings, DM Sans reading and controls, flat
divided sections, dark blue/light oxblood accents and shared buttons/feedback.
This ordinary extension introduces no new visual tokens, artwork or animation.

## Browsing

`/collections` begins with Collections, a short explanation and New collection.
The labeled native Show collections select offers All accessible and My
collections, submitted with Apply. Explain that accessible includes public
collections plus the signed-in viewer's own private collections. Submitted scope
and page belong in the URL; applying scope resets page one. Anonymous My
collections offers login. Invalid filters offer reset rather than results.

Render real summaries as a semantic list with a wrapping Fraunces title link,
owner display name/handle, optional plain-text description and muted visibility.
Separate rows with theme borders; retain returned totals, page number and
Previous/Next links. Distinguish loading, retry, empty and later-page recovery.
Do not invent entry counts, artwork or profile links.

Headings use weight 500 and `clamp(3rem, 6vw, 5rem)` with 1.1 line height.
Summary titles use 1.75rem with 1.3 line height; descriptions preserve newlines,
wrap long content and stop at 75ch. Rows place content beside visibility until
32rem, then stack. Filters also stack at 32rem; heading actions stack at 48rem.

## Creation and owner editing

`/collections/new` places its heading beside a form in a 1:1.5 grid with a 4rem
gap, stacking in reading order at 48rem. Forms are capped at 48rem with 1.25rem
gaps. Visible Name, Description and Visibility labels accompany native input,
six-row textarea and select controls. Controls use 1rem text, a 3rem minimum
height, semantic input colors and the existing small radius. Keep the global
accent focus outline. Creation defaults to Private and explains Public access
to the collection and its existing entries before explicit submission.

Details show Back to collections, wrapping heading, owner, visibility and any
description. Only the verified owner sees Edit metadata, which opens a divided
inline section with Save metadata and Cancel editing. Field errors remain beside
controls; failed writes preserve drafts. A revision conflict preserves the draft,
blocks saving and offers Reload latest metadata with a warning that it replaces
the draft. Saved feedback uses a status message. Missing and inaccessible
collections share a neutral unavailable state.

Unknown collection-create outcomes direct the person to My collections before
another attempt. Collection deletion and confirmation modals are outside this
surface; field and entry removal use inline confirmation.

## Fields and entries

Collection detail follows metadata with Entries and Fields, using flat semantic
lists and theme dividers. Entries show a wrapping title link and Custom entry or
Catalog title; catalog entries also link to their catalog detail. Entry pagination
uses URL-owned itemsPage and 20 entries per page. Invalid paging offers reset;
empty later pages offer the first page. Visitors read accessible entries without
owner management controls. Only one metadata, field, removal or add-entry editor
can be active at a time.

Field rows show label, type, required/optional state and explicit position, ordered
by position then ID. Owners can add up to 30 fields. New fields have native Key,
Label, Type, Required and Position controls; saved key and type are immutable text.
Text configuration uses Maximum length; numbers use optional Minimum/Maximum
text inputs with decimal keyboard hints; select configuration uses individually
labeled option inputs with Add/Remove actions. Boolean and date need no extra
configuration. Explain that changes must keep saved entries valid and disable
adding a required field when entries already exist.

Entry creation offers custom title or an explicitly searched/selected local
catalog title; editing preserves entry kind and catalog identity. Every current
field gets a labeled native control: text textarea, exact-decimal text input,
Yes/No select, calendar date input or configured option select. Optional fields
have a Set a value checkbox. Unset, empty text, false and zero stay distinct.
The entry detail uses a definition list capped at 75ch with muted labels and
literal wrapping values; display missing/null as No value, empty text as Empty
text and booleans as Yes/No. Preserve calendar dates and exact decimal values
through reading, editing and unchanged-value replacement.

Forms retain the existing 48rem cap, 1.25rem gaps, 1rem input text, 3rem minimum
control height, small radius and accent focus outline. Field/value rows divide
with theme borders; action groups wrap. No cards, new tokens, artwork or motion
are introduced.

## Revision and destructive-action recovery

Editors initialize from fresh stable collection/item snapshots, with matching
collection revisions bracketing item reads. Draft revisions never silently rebase
on background refresh. Preserve failed drafts; generic conflicts explain changed
collection, duplicate identity or incompatible values without assuming one cause.
Reload latest collection explicitly warns that it replaces the draft. A successful
write followed by failed refresh reports Saved/Removed with Refresh needed and
retries only the read before another mutation is allowed. Unknown outcomes require
checking current state rather than replaying the write.

Field removal first previews the field label/key and populated-entry count, then
requires Confirm remove field, even for zero populated entries. Explain permanent
stored-value loss. Consent uses the exact preview revision; changes or session
uncertainty invalidate it and require a new preview and confirmation. Entry
removal freshly reviews the named entry, explains permanent stored-value loss
while catalog/personal activity remain, and requires Confirm remove entry.
Cancel stays available. Neither flow silently retries deletion.

## Session and interaction

Check identity before viewer-sensitive reads. During session checks conceal and
disable mounted content while retaining same-account drafts; account changes
remount viewer content. Use distinct session retry and login recovery states.
Disable repeated writes while pending and never replay a write after login or
uncertain completion. Interaction uses native forms, links and document flow.

## Evidence and limits

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
