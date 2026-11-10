using RateFreak.Infrastructure.Catalog;
using System.Data;
using RateFreak.Infrastructure.Moderation;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using RateFreak.Application.Social;
using RateFreak.Domain.Social;
using RateFreak.Infrastructure.Persistence;

namespace RateFreak.Infrastructure.Social;

internal sealed class ReviewCommentService(RateFreakDbContext db) : IReviewCommentService
{
    private IQueryable<RateFreak.Domain.PersonalMedia.Review> AccessibleParents(Guid? viewerId) =>
        db.Reviews.Where(r => db.MediaItems.Where(CatalogAccess.Readable(viewerId)).Any(m => m.Id == r.MediaItemId));

    private IQueryable<ReviewCommentDetails> Items(IQueryable<ReviewComment> comments) =>
        from comment in comments
        join author in db.Users.AsNoTracking() on comment.UserId equals author.Id
        select new ReviewCommentDetails(comment.Id, comment.ReviewId, new CommentAuthor(author.Id, author.UserName!, author.IsProfileHidden ? author.UserName! : author.DisplayName),
            comment.Body, comment.IsSpoiler, comment.CreatedAt, comment.UpdatedAt);

    public async Task<ReviewCommentPage?> ListAsync(Guid? viewerId, Guid reviewId, int page, int pageSize, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        if (!await AccessibleParents(viewerId).AnyAsync(x => x.Id == reviewId && !x.IsHidden, ct)) return null;
        var comments = db.ReviewComments.AsNoTracking().Where(x => x.ReviewId == reviewId && !x.IsHidden);
        var count = await comments.CountAsync(ct);
        var items = await Items(comments.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize)).ToListAsync(ct);
        await tx.CommitAsync(ct);
        return new(items, page, pageSize, count);
    }
    public Task<ReviewCommentDetails?> FindAsync(Guid? viewerId, Guid commentId, CancellationToken ct) =>
        Items(db.ReviewComments.AsNoTracking().Where(x => x.Id == commentId && !x.IsHidden && AccessibleParents(viewerId).Any(r => r.Id == x.ReviewId && !r.IsHidden))).SingleOrDefaultAsync(ct);

    public async Task<ReviewCommentDetails?> CreateAsync(Guid userId, Guid reviewId, ReviewCommentRequest request, CancellationToken ct)
    {
        if (userId == Guid.Empty || reviewId == Guid.Empty) return null;
        var comment = new ReviewComment(userId, reviewId, request.Body!, request.IsSpoiler);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await ModerationLock.AcquireAsync(db, ct);
        // The shared transaction lock serializes moderation; FK handling covers concurrent parent deletion.
        var parent = await AccessibleParents(userId).AsNoTracking().SingleOrDefaultAsync(x => x.Id == reviewId, ct);
        if (parent is null || parent.IsHidden) return null;
        db.Add(comment);
        try
        {
            await db.SaveChangesAsync(ct);
            var result = await Items(db.ReviewComments.AsNoTracking().Where(x => x.Id == comment.Id)).SingleOrDefaultAsync(ct);
            await tx.CommitAsync(ct);
            return result;
        }
        catch (DbUpdateException error) when (error.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.ForeignKeyViolation, ConstraintName: "FK_review_comments_reviews_review_id" or "FK_review_comments_users_user_id" })
        {
            await tx.RollbackAsync(ct);
            db.Entry(comment).State = EntityState.Detached;
            return null;
        }
    }
    public async Task<ReviewCommentDetails?> UpdateAsync(Guid userId, Guid commentId, ReviewCommentRequest request, CancellationToken ct)
    {
        var body = request.Body!.Trim();
        if (!ReviewComment.ValidBody(body)) throw new ArgumentException("Invalid review comment body.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await ModerationLock.AcquireAsync(db, ct);
        var changed = await db.ReviewComments.Where(x => x.Id == commentId && x.UserId == userId && AccessibleParents(userId).Any(r => r.Id == x.ReviewId)).ExecuteUpdateAsync(set =>
            set.SetProperty(x => x.Body, body).SetProperty(x => x.IsSpoiler, request.IsSpoiler).SetProperty(x => x.UpdatedAt, DateTimeOffset.UtcNow), ct);
        if (changed == 0) return null;
        var result = await Items(db.ReviewComments.AsNoTracking().Where(x => x.Id == commentId)).SingleOrDefaultAsync(ct);
        await tx.CommitAsync(ct);
        return result;
    }
    public async Task<bool> DeleteAsync(Guid userId, Guid commentId, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await ModerationLock.AcquireAsync(db, ct);
        var deleted = await db.ReviewComments.Where(x => x.Id == commentId && x.UserId == userId && AccessibleParents(userId).Any(r => r.Id == x.ReviewId)).ExecuteDeleteAsync(ct) != 0;
        await tx.CommitAsync(ct); return deleted;
    }
}
