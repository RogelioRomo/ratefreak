# Frontend review comments implementation plan

Goal: complete Phase6 with public review discussion and author-owned comments.
Spec: ../specs/2026-11-04-frontend-comments-design.md
Architecture: exact guarded proxy; feature interfaces, API/hooks and focused
components; public Query reads, local editor state and URL-owned paging.

- [x] Test then implement exact discussion/comment proxy allowlists and bounded
  comment paging. Preserve existing mutation protections and closed methods.
- [x] Test then implement feature contracts/API/query/URL helpers and cancellable
  mutation lifecycle separating known saves from uncertain outcomes/read refresh.
- [x] Test then implement discussion/list/spoiler controls, author-only form and
  delete confirmation; session uncertainty preserves drafts but disables writes.
- [x] Add Suspense route and review links, safe login continuation and public-query
  initialization support. Invalidate comment-author names after profile saves.
- [x] Run focused/full frontend, lint/typecheck/build, PostgreSQL integration and
  real browser checks. Fresh review, fix findings, document actual evidence and
  guarded cleanup. Preserve all earlier work and running preview services.

Review focus: duplicate POST after unknown result; cross-account draft/completion
leaks; review author versus comment author; spoiler text exposed by confirmations;
successful mutations misreported as failed when refreshing reads fails.
