using RateFreak.Application.MetadataImport;
using RateFreak.Infrastructure.Catalog;
using RateFreak.Application.Catalog;
using RateFreak.Infrastructure.Moderation;
using Microsoft.EntityFrameworkCore;
using RateFreak.Application.PersonalMedia;
using RateFreak.Domain.PersonalMedia;
using RateFreak.Infrastructure.Persistence;

namespace RateFreak.Infrastructure.PersonalMedia;

internal sealed class RatingService(RateFreakDbContext context) : IRatingService
{
    public async Task<RecentRatingsPage> RecentAsync(Guid userId, CancellationToken ct)
    {
        var items = await (from rating in context.Ratings.AsNoTracking().Where(r => r.UserId == userId)
                           join media in context.MediaItems.AsNoTracking().Where(CatalogAccess.Readable(userId)) on rating.MediaItemId equals media.Id
                           orderby rating.UpdatedAt descending, rating.MediaItemId
                           select new RecentRating(new MediaSummary(media.Id, media.MediaType, media.Title, media.ReleaseDate, MusicBrainzArtwork.Album(media.ImageReference,
                context.MediaExternalIds.Where(e => media.MediaType == "album" && e.MediaItemId == media.Id && e.Provider == "musicbrainz" && e.Namespace == "release-group").OrderBy(e => e.ExternalId).Select(e => e.ExternalId).FirstOrDefault())), rating.Score, rating.UpdatedAt))
            .Take(12).ToListAsync(ct);
        return new(items);
    }

    public Task<RatingDetails?> FindAsync(Guid userId, Guid mediaId, CancellationToken ct) =>
        context.Ratings.AsNoTracking().Where(x => x.UserId == userId && x.MediaItemId == mediaId && context.MediaItems.Where(CatalogAccess.Readable(userId)).Any(m => m.Id == x.MediaItemId))
            .Select(x => new RatingDetails(x.Id, x.MediaItemId, x.Score, x.CreatedAt, x.UpdatedAt, x.IsShared, x.SharedAt)).SingleOrDefaultAsync(ct);

    public async Task<CatalogResult<RatingDetails>> PutAsync(Guid userId, Guid mediaId, decimal score, bool? isShared, CancellationToken ct)
    {
        var rating = new Rating(userId, mediaId, score);
        await using var tx = await context.Database.BeginTransactionAsync(ct);
        await ModerationLock.AcquireAsync(context, ct);
        if (!await context.Users.AnyAsync(user => user.Id == userId, ct)) return new(CatalogStatus.NotFound);
        if (!await context.MediaItems.Where(CatalogAccess.Readable(userId)).AnyAsync(x => x.Id == mediaId, ct)) return new(CatalogStatus.NotFound);
        if (isShared == true && !await context.MediaItems.Where(CatalogAccess.Readable(null)).AnyAsync(x => x.Id == mediaId, ct))
            return new(CatalogStatus.Invalid, Errors: new() { ["isShared"] = ["Only ratings for publicly available items can be shared."] });
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO ratings (id, user_id, media_item_id, score, created_at, updated_at, is_shared, shared_at)
            VALUES ({rating.Id}, {userId}, {mediaId}, {score}, {rating.CreatedAt}, {rating.UpdatedAt},
                COALESCE({isShared}::boolean, FALSE), CASE WHEN {isShared}::boolean = TRUE THEN {rating.UpdatedAt} ELSE NULL END)
            ON CONFLICT (user_id, media_item_id) DO UPDATE SET score = EXCLUDED.score, updated_at = EXCLUDED.updated_at,
                is_shared = COALESCE({isShared}::boolean, ratings.is_shared),
                shared_at = CASE WHEN {isShared}::boolean IS NULL THEN ratings.shared_at
                    WHEN {isShared}::boolean = FALSE THEN NULL
                    ELSE COALESCE(ratings.shared_at, EXCLUDED.shared_at) END
            """, ct);
        var result = await FindAsync(userId, mediaId, ct);
        await tx.CommitAsync(ct);
        return new(CatalogStatus.Success, result);
    }

    public async Task<bool> DeleteAsync(Guid userId, Guid mediaId, CancellationToken ct)
    {
        await using var tx = await context.Database.BeginTransactionAsync(ct);
        await ModerationLock.AcquireAsync(context, ct);
        if (!await context.MediaItems.Where(CatalogAccess.Readable(userId)).AnyAsync(x => x.Id == mediaId, ct)) return false;
        await context.Ratings.Where(x => x.UserId == userId && x.MediaItemId == mediaId).ExecuteDeleteAsync(ct);
        await tx.CommitAsync(ct); return true;
    }

    public async Task<RatingAggregate?> AggregateAsync(Guid? viewerId, Guid mediaId, CancellationToken ct)
    {
        // Correlated aggregates execute in one statement/snapshot, including media existence.
        return await context.MediaItems.AsNoTracking().Where(CatalogAccess.Readable(viewerId)).Where(x => x.Id == mediaId)
            .Select(media => new RatingAggregate(context.Ratings.Count(x => x.MediaItemId == media.Id),
                context.Ratings.Where(x => x.MediaItemId == media.Id).Average(x => (decimal?)x.Score)))
            .SingleOrDefaultAsync(ct);
    }
}
