using System.Data;
using RateFreak.Infrastructure.Moderation;
using Microsoft.EntityFrameworkCore;
using RateFreak.Application.PersonalMedia;
using RateFreak.Domain.PersonalMedia;
using RateFreak.Infrastructure.Persistence;

namespace RateFreak.Infrastructure.PersonalMedia;

internal sealed class ReviewService(RateFreakDbContext context) : IReviewService
{
    private IQueryable<ReviewDetails> Items(IQueryable<Review> reviews) =>
        from review in reviews
        join author in context.Users.AsNoTracking() on review.UserId equals author.Id
        select new ReviewDetails(review.Id, review.MediaItemId, new ReviewAuthor(author.Id, author.UserName!, author.DisplayName),
            review.Body, review.IsSpoiler, review.CreatedAt, review.UpdatedAt);

    public Task<ReviewDetails?> FindOwnAsync(Guid userId, Guid mediaId, CancellationToken ct) =>
        Items(context.Reviews.AsNoTracking().Where(x => x.UserId == userId && x.MediaItemId == mediaId && !x.IsHidden)).SingleOrDefaultAsync(ct);

    public async Task<PersonalPage<ReviewDetails>?> ListAsync(Guid mediaId, int page, int pageSize, CancellationToken ct)
    {
        await using var tx = await context.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        if (!await context.MediaItems.AnyAsync(x => x.Id == mediaId, ct)) return null;
        var items = context.Reviews.AsNoTracking().Where(x => x.MediaItemId == mediaId && !x.IsHidden);
        var total = await items.CountAsync(ct);
        var result = await Items(items.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)).ToListAsync(ct);
        await tx.CommitAsync(ct);
        return new(result, page, pageSize, total);
    }

    public Task<ReviewDetails?> FindAsync(Guid reviewId, CancellationToken ct) => Items(context.Reviews.AsNoTracking().Where(x => x.Id == reviewId && !x.IsHidden)).SingleOrDefaultAsync(ct);

    public async Task<ReviewWriteResult> CreateAsync(Guid userId, Guid mediaId, ReviewRequest request, CancellationToken ct)
    {
        var review = new Review(userId, mediaId, request.Body!, request.IsSpoiler);
        if (!await context.MediaItems.AnyAsync(x => x.Id == mediaId, ct)) return new(ReviewWriteStatus.NotFound);
        await using var tx = await context.Database.BeginTransactionAsync(ct);
        await ModerationLock.AcquireAsync(context, ct);
        var count = await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO reviews (id, user_id, media_item_id, body, is_spoiler, created_at, updated_at)
            VALUES ({review.Id}, {userId}, {mediaId}, {review.Body}, {review.IsSpoiler}, {review.CreatedAt}, {review.UpdatedAt})
            ON CONFLICT (user_id, media_item_id) DO NOTHING
            """, ct);
        if (count == 0) return new(ReviewWriteStatus.Conflict);
        var result = await Items(context.Reviews.AsNoTracking().Where(x => x.Id == review.Id)).SingleOrDefaultAsync(ct);
        await tx.CommitAsync(ct);
        return new(ReviewWriteStatus.Success, result);
    }

    public async Task<ReviewDetails?> UpdateAsync(Guid userId, Guid reviewId, ReviewRequest request, CancellationToken ct)
    {
        var body = request.Body!.Trim();
        if (!PersonalMediaRules.ValidBody(body)) throw new ArgumentException("Invalid review body.");
        await using var tx = await context.Database.BeginTransactionAsync(ct);
        await ModerationLock.AcquireAsync(context, ct);
        var changed = await context.Reviews.Where(x => x.Id == reviewId && x.UserId == userId).ExecuteUpdateAsync(
            setters => setters.SetProperty(x => x.Body, body).SetProperty(x => x.IsSpoiler, request.IsSpoiler)
                .SetProperty(x => x.UpdatedAt, DateTimeOffset.UtcNow), ct);
        if (changed == 0) return null;
        var result = await Items(context.Reviews.AsNoTracking().Where(x => x.Id == reviewId)).SingleOrDefaultAsync(ct);
        await tx.CommitAsync(ct);
        return result;
    }

    public async Task<bool> DeleteAsync(Guid userId, Guid reviewId, CancellationToken ct)
    {
        await using var tx = await context.Database.BeginTransactionAsync(ct);
        await ModerationLock.AcquireAsync(context, ct);
        var deleted = await context.Reviews.Where(x => x.Id == reviewId && x.UserId == userId).ExecuteDeleteAsync(ct) != 0;
        await tx.CommitAsync(ct); return deleted;
    }
}
