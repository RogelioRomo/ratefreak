# Phase 6d: public review comments

User requested merging IGDB and continuing next steps. Review comments are the next
bounded social slice; book-provider selection and live activation remain separate.
Optional collection-target preference was asked; default is reviews only. All
existing reviews are public, so explicit ReviewId avoids a polymorphic target or
private collection visibility races. Reuse the existing author-owned review model
and social rate budgets; do not introduce a generic discussion framework.

## Contract

- GET /api/v1/reviews/{reviewId:guid}/comments: public page, defaults page1/pageSize20,
  page1..10000/pageSize1..100, items/page/pageSize/totalCount. Oldest CreatedAt then
  UUID order; repeatable-read transaction keeps existence/count/page consistent.
  Existing review with no comments200; missing review404.
- POST same route: session+CSRF, {body,isSpoiler=false};201 with Location
  /api/v1/comments/{id}. Multiple independent comments per author/review permitted;
  POST retries create independent IDs (no idempotency key). No replies/thread trees.
- GET /api/v1/comments/{id:guid}: public detail or404.
- PATCH /api/v1/comments/{id:guid}: session+CSRF, replaces body/spoiler on owned comment;
  200 detail, preserves ID/author/target/CreatedAt, updates UpdatedAt. Last write wins.
- DELETE same: session+CSRF, author only,204; missing/non-owned404 including repeat
  DELETE. Review author gains no right to edit/delete another person's comment.

Comment details: id,reviewId,author{id,handle,displayName},body,isSpoiler,createdAt,
updatedAt. No email/security data. All responses use no-store. Reads share social-read
60/IP/min and writes share social-write30/account/min with follows;429 Retry-After.
Unknown JSON properties, duplicate fields, malformed JSON/types return400. Spoiler
flag is presentation metadata; text is returned literally, without HTML rendering.
Body trimmed then validated:1..2000 UTF-16 code units, non-whitespace, valid surrogate
pairs, only tab/CR/LF control characters. Same Unicode rules as reviews. No private
library exposure or following requirement. Authentication actor always server derived.

## Persistence and races

Typed ReviewComment in Domain/Social, Application/Social contracts and scoped EF
service; one additive review_comments table, UUID/UTC identity/timestamps, body
CHECK for1..2000 and nonblank after common whitespace trim. Cascading review and
account FKs. Index(review_id,created_at,id) and author FK index. No unique per-pair
constraint. New migration must preserve all existing records/schema and reapply.
Create transaction inserts and projects before commit; missing/deleted target/actor
FK failures map to404 and roll back. Update uses actor predicate and transaction
through projection; zero affected maps404. Reads are no-tracking projections.
Migration Down only drops this new comments table; document comment-data loss.

## Scope and proof

No collection targets, replies, reactions, mentions/notifications, feed, reports,
admin/moderation endpoints, account blocking, private profiles or deployment. The
original plan's moderation state/audit policies remain a separate design; no inert
state columns or unprotected admin routes added. Any future hiding must apply to
all list/detail queries and be auditable.

Prove HTTP lifecycle, pagination ties, author isolation (including review author),
authentication/CSRF on every mutation, input boundaries/Unicode/unknown properties,
no-store/privacy, missing parent, rate-budget isolation, database body/FKs/cascade,
concurrent parent deletion during creation, migration apply/reapply/model consistency.
Full suite, Release build, audit, independent review, local additive migration and
read-only smoke; record evidence, commit feature branch. No remote push/merge inferred.
