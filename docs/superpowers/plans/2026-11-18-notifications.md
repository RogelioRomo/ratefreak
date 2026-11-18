# Notifications Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [x]`) syntax for tracking.

**Goal:** Ship a private durable notifications inbox with read state, deletion and clearing.

**Architecture:** Create notification records atomically alongside follows/comments. A single eligibility query supplies private inbox/count/read/delete operations. Reuse browser proxy and account-scoped React Query state.

**Tech Stack:** .NET, EF Core, PostgreSQL, Next.js, React Query, Vitest.

**Spec:** `docs/superpowers/specs/2026-11-18-notifications-design.md`

## Global Constraints

- Only new followers and comments on the recipient's review; no self-notifications or historical backfill.
- Poll every 60 seconds while visible; page size 20; badge cap 99+.
- Authentication, CSRF for writes, no-store and social rate policies.
- No copied text; current target visibility and hidden-profile handle fallback.
- Deletion/clear never change source relationships; clear includes hidden entries and preserves later arrivals.
- Disposable databases only; no persistent migration, push or deployment.

## Review Focus

- Follow PUT after notification deletion cannot resurrect the entry (Task 1).
- Hidden source restoration after clearing cannot restore deleted entries (Task 2).
- Concurrent new events after clear statement snapshot survive (Task 2).
- Identity transitions conceal and clear inbox/badge state (Task 3).
- Deleting the last row on a later page returns to valid pagination (Task 3).

### Task 1: Durable event production

**Files:** Domain/Social/Notification.cs, Application/Social/Notifications.cs; Infrastructure/Persistence/Configurations/NotificationConfiguration.cs, RateFreakDbContext.cs, migrations; Infrastructure/Social/FollowService.cs and ReviewCommentService.cs; tests/RateFreak.IntegrationTests/NotificationTests.cs.
**Interfaces:** Notification (Id, Kind, RecipientId, ActorId, CommentId?, CreatedAt, ReadAt?); event producers write only upon source creation, in the source transaction.

- [x] Add API integration tests asserting new/replayed/concurrent follows, comment/self/edit behavior and source cleanup.
- [x] Run focused integration tests and observe absent notification routes/records.
- [x] Add storage/configuration and additive migration; create events atomically; unfollow removes follow events transactionally.
- [x] Run focused tests; confirm uniqueness, source integrity, and no resurrection after deleting an entry and replaying follow PUT.

### Task 2: Private inbox API and lifecycle

**Files:** Infrastructure/Social/NotificationService.cs; Api/Endpoints/NotificationEndpoints.cs; Program.cs; Infrastructure/DependencyInjection.cs; Authentication/AccountDataExport.cs; tests/RateFreak.IntegrationTests/NotificationTests.cs and NotificationPrivacyTests.cs.
**Interfaces:** INotificationService.ListAsync(recipient,page,pageSize,ct), UnreadCountAsync(recipient,ct), SetReadAsync(recipient,id,read,ct), DeleteAsync(recipient,id,ct), ReadAllAsync(recipient,ct), ClearAsync(recipient,ct). DTO actor/public identifiers, optional review/media target, timestamps; page includes total/unread counts.

- [x] Add failing tests for authorization/CSRF, read/unread/all-read, ownership, paging, delete/clear, visibility, export and cascading erasure.
- [x] Implement shared recipient-scoped eligibility, consistent list/count snapshot, set-based mutations and API contracts in spec.
- [x] Integrate accessible notifications in existing export snapshot; account/source FKs remove notifications on erasure/replay.
- [x] Verify tests including hidden-row clear, restored visibility, new event after clear, and both directions of user deletion.

### Task 3: Browser inbox and navigation

**Files:** web/server/api/notification-policy.ts and proxy/route-policy integrations/tests; web/features/notifications/{api,interfaces,hooks,components}; web/app/notifications/page.tsx; auth-navigation.tsx.
**Interfaces:** getNotifications(page,signal), getUnreadCount(signal), setNotificationRead(id,read), deleteNotification(id), clearNotifications(), readAllNotifications(); private account-scoped query keys.

- [x] Read installed Next.js and relevant UI guidance. Add failing route/proxy and UI tests before implementation.
- [x] Add exact route policy with paging validation; typed API wrappers; shared polling query with visibility/session gating.
- [x] Add responsive inbox, read controls, links, delete and accessible clear confirmation, navigation badge, error/loading/empty feedback.
- [x] Verify confirmation cancel/focus, valid page after deletion, account transitions and count invalidation; run frontend tests/lint/typecheck/build.

### Task 4: Acceptance and documentation

**Files:** docs/STATUS.md, account-data-operations.md, README.md, notifications design and this plan.

- [x] Run backend suites/model consistency; frontend complete suite/lint/typecheck/build.
- [x] Exercise two-account real browser workflow on disposable database in both themes/desktop/mobile; remove temporary services/database.
- [x] Run independent whole-change review and fix material findings with regression tests.
- [x] Document implemented behavior, retention and evidence limits; leave changes reviewable without publishing.

Execution record: implementation and review fixes verified on feature branch
`feat/notifications`. The user subsequently authorized applying the persistent local
migration, committing and merging into `main`. The migration passed a private
backup/restore drill and data-preservation checks. Integration target: `main`;
see Git history for the final commit. No push or deployment is requested. See
[verification](../../notifications-verification.md) for evidence and limitations.
