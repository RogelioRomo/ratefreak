# Release notes

Keep maintainer-written notes in `<VERSION>.md`, matching root `VERSION` exactly.
Start from TEMPLATE.md. Summarize user-visible changes and document configuration,
explicit database migrations, supported upgrade baseline, backup and recovery
limits. No pending provider or source-delivery gate is cleared by writing notes.

Use SemVer: before 1.0, incompatible documented HTTP/configuration/operator changes
increment the minor version; fixes increment patch. A prerelease suffix identifies
an unreleased candidate; a published 0.x stable-channel release still has pre-1.0
compatibility limits. Release tags are `v<VERSION>` and may never be reused for
changed artifacts. API, web, tools and their source sidecars are one release.
