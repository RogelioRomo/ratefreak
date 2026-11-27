# Song release artwork patch

Owner requested song artwork through associated albums on 2026-11-27. This
supersedes the original music-discovery design's exclusion of song artwork.

Use only releases returned for the exact MusicBrainz recording. Prefer official
albums, then non-compilation releases, known front artwork, earlier valid dates
and stable IDs. Preserve recording identity and all existing ratings/library data.
Request releases/release-groups with song details; search already embeds releases.
Do not issue one extra metadata request per search row. Retain selected release
ID, release-group ID and title in metadata; show a validated MusicBrainz source
link. Prefer release-group covers, with exact release covers when no group is
available or a release explicitly reports a front cover. Missing/malformed optional
release information must not make otherwise valid song metadata fail.

No new dependencies, schema changes or automatic album imports. Existing imported
images are preserved; new imports store the associated cover. The current local
database contains no songs needing backfill. Already imported songs on other
installations retain stored values (no background refresh is introduced).

- [x] Add provider regressions for selection, identity preservation, release-only
  artwork, malformed/missing associations, deterministic ordering and bounded data.
- [x] Observe failing tests, then implement provider mapping and detail includes.
- [x] Add source-link and renderer tests, then implement strict release-cover
  validation and release context in the existing music metadata component.
- [x] Verify new song imports persist artwork through catalog/library/recent reads.
- [x] Run relevant suites, full test commands, lint/typecheck; review diff.
- [x] Restart local app, verify provider results and readiness, commit only this work.


Verification so far: observed four provider regressions and three UI regressions
fail before implementation. Focused song import regression passes. Frontend suite
1093 tests across 128 files, lint and typecheck pass; design detector has no
findings. Independent read-only review found no actionable issues. Full .NET suite
passed: 62 unit, 524 integration and 235 provider tests (821 total). Live search returns 20 recordings, 19 with associated cover URLs; a
real search cover and its preview cover both returned HTTP 200 image/jpeg. API
readiness is HTTP 200 and the local API was restarted with this patch. Live
read-only evidence is under .local/localhost/musicbrainz-artwork-investigation/.
No live imports or changes to stored user media were made.

Committed locally after green verification; pre-existing STATUS changes and the
original investigation file remain excluded. The local app remains running.
