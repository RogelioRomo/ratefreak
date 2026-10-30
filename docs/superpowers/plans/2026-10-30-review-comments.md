# Review Comments Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans. Steps use checkbox syntax.

**Goal:** Add public author-owned comments to existing public reviews.
**Architecture:** Typed domain row, application service contract, EF implementation,
Minimal API endpoints following current review/follow patterns.
**Tech Stack:** .NET10, EF/Npgsql, xUnit/Testcontainers; no packages added.
**Spec:** docs/superpowers/specs/2026-10-30-review-comments-design.md

## Global Constraints

Reviews only, flat comments, body1..2000, default spoiler false. Public projections
never expose email; only comment author edits/deletes. CSRF/session on all writes,
existing social budgets/no-store. Additive migration, cascading FKs, no startup Migrate.

## Review Focus

Parent deletion races cannot leak500; review owner cannot hijack another author;
tied CreatedAt pages must be stable; invalid bodies cannot mutate timestamps/text;
deleting an account must preserve unrelated comments/reviews.

### Task 1: Comments vertical slice

Create Domain/Social/ReviewComment.cs, Application/Social/ReviewComments.cs,
Infrastructure/Social/ReviewCommentService.cs, Persistence/Configurations/
ReviewCommentConfiguration.cs, Api/Endpoints/ReviewCommentEndpoints.cs and
IntegrationTests/ReviewCommentTests.cs (split security/DB tests if necessary).
Modify DbContext/DI/Program; generate AddReviewComments migration.

Interface IReviewCommentService: ListAsync(Guid reviewId,int page,int pageSize,ct)
->ReviewCommentPage?; FindAsync(Guid commentId,ct)->ReviewCommentDetails?;
CreateAsync(Guid userId,Guid reviewId,ReviewCommentRequest request,ct)->Details?;
UpdateAsync(Guid userId,Guid commentId,request,ct)->Details?;
DeleteAsync(Guid userId,Guid commentId,ct)->bool.

- [x] Write real HTTP lifecycle/auth/validation tests; run and observe missing routes.
- [x] Implement domain validation, typed request/projection contracts, FK/index config,
  transactions and ownership queries; register routes/service and no-store metadata.
- [x] Generate/inspect migration; focused tests GREEN.
- [x] Add pagination, rate limits, constraints/cascades and deterministic parent-deletion
  race tests; verify behavior and model consistency using PostgreSQL.
- [x] Run full suite, Release build, transitive audit and independent code review.
- [x] Fix required findings with regressions. Apply/reapply locally; read-only smoke.
- [x] Update ADR/README/checklist with actual evidence, commit completed slice.

## Execution evidence

Completed inline on feat/review-comments based on518de06. User's existing proceed
instruction covers implementation; reviews-only default stated after optional target
question remained unanswered. Main IGDB merge verified444 tests; final comments
suite476 pass (64unit/154provider/258integration), no failures/skips. Initial15 HTTP
cases RED→GREEN; all-zero parent500→404. Generated additive migration inspected,
applied/reapplied locally; EF model matches. Release clean, dependency audit no known
vulnerabilities. Independent review clear. Added real concurrent snapshot/cascade
and write/projection tests; pinned historical game-rollback test to its own migration
boundary after reproducing later-migration history changes. Read-only5081 smoke
passed; temporary server stopped. No local records/activation/push/comments merge.
