# Frontend follows implementation plan

Goal: expose repeat-safe follow actions and public connection lists on profiles.
Architecture: feature-owned interfaces/API/hooks/components, same-origin guarded
proxy, TanStack Query server state and URL-owned list selection.
Spec: ../specs/2026-11-04-frontend-follows-design.md

- [x] Add exact follow proxy policy and paging validation; test invalid paths,
  methods, query keys/duplicates/bounds and existing mutation security.
- [x] Add follow interfaces, API functions, query hooks and strict URL codec;
  test filter transitions and privacy-safe request contracts.
- [x] Add session-aware explicit Follow/Unfollow controls and connection list;
  test self/anonymous behavior, pending/error/uncertain/save/read separation,
  identity changes, pagination and stale feedback isolation.
- [x] Integrate profiles and safe login continuation, retaining current styling.
- [x] Run frontend suite/lint/typecheck/build, real PostgreSQL FollowTests and live
  browser/API checks. Review final diff, fix findings, update docs/checklist and
  remove dedicated verification accounts by exact identity. Preserve prior work.

Review focus: absence of point status must not become an inferred toggle; stale
account completions must not affect new sessions; read failures must not replay
writes; pagination must reset when direction changes; unknown outcomes must be
honest and only deliberate retries may write.
