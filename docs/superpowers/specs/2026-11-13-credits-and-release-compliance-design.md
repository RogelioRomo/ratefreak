# Provider credits and release compliance

User request: add branding/credits for all providers, assemble dependency notices
and native-library compliance evidence, and resolve outstanding provider agreements.
The user selected **Credits page plus catalog attribution** on 2026-11-13.

## Approach

A Credits page alone was considered but leaves imported titles without contextual
credit. The selected approach adds `/credits`, a footer link, and credit on
imported title details derived from their persisted external identities. Discovery
and preview retain their existing credit. All three providers receive linked
credit; TMDB also receives its approved, unmodified logo and exact required notice.

The page inherits Fraunces/DM Sans, semantic theme tokens and the existing narrow
reading layout. Use stacked provider sections, readable links, responsive spacing
and both themes. Static credits do not depend on provider availability or secrets.
Custom items receive no inferred provider credit. No new API or schema is needed.
This structure and placement are the implementer's proposal within the selected
approach; the visual identity remains unchanged.

## Notices and evidence

Collect complete copyright/license/NOTICE texts from exact restored package
archives and installed npm packages; inventory hashes and declared licenses.
Include project LICENSE and fonts. Preserve upstream runtime and OS notices.
Package notices in API/web/tools images and expose web dependency notices from
Credits. Do not infer a complete native license from the wrapper's license label.

Record actual image/package/native versions, hashes and available upstream
source/build provenance. Check LGPL replacement/relinking/source requirements and
embedded libraries; unresolved source availability remains a release blocker.
Final artifacts must be checked rather than declaring package metadata sufficient.
This work does not publish images or accept any provider agreement.

## Provider agreements

Retrieve full official TMDB/Twitch terms where accessible, record date, URL and
section-specific paraphrases plus evidence hashes where feasible. Compare storage,
removal, termination, commercial eligibility, credentials and redistribution with
the implementation. Add an operator checklist separating project obligations from
instance-specific agreement acceptance. Explicitly retain blockers for inaccessible
or ambiguous terms; do not claim permission based on a FAQ alone.

## Verification

Test provider identity attribution (including multiple identities, unknown IDs,
custom items and disabled providers), credits navigation/content, and notice
collector failure on missing/unreviewed license evidence. Run frontend lint,
typecheck, relevant tests and production build; inspect desktop/mobile credits.
Check packaged notice presence and manifests against the built artifacts.
Record actual results and remaining obligations in STATUS and review documents.

No migration, provider activation, account changes, populated release data,
public deployment or publication is included.
