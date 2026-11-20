using RateFreak.Infrastructure.Catalog;
using RateFreak.Infrastructure.Moderation;
using System.Data;
using Microsoft.EntityFrameworkCore;
using RateFreak.Application.Catalog;
using RateFreak.Application.PersonalMedia;
using RateFreak.Domain.PersonalMedia;
using RateFreak.Infrastructure.Persistence;

namespace RateFreak.Infrastructure.PersonalMedia;

internal sealed class LibraryService(RateFreakDbContext context) : ILibraryService
{
    private IQueryable<LibraryItem> Items(IQueryable<LibraryEntry> entries) =>
        from entry in entries
        join media in context.MediaItems.AsNoTracking() on entry.MediaItemId equals media.Id
        select new LibraryItem(new MediaSummary(media.Id, media.MediaType, media.Title, media.ReleaseDate, media.ImageReference),
            entry.Status, entry.StartedAt, entry.CompletedAt, entry.CreatedAt, entry.UpdatedAt,
            context.Ratings.Where(r => r.UserId == entry.UserId && r.MediaItemId == entry.MediaItemId).Select(r => (decimal?)r.Score).FirstOrDefault());

    public Task<LibraryItem?> FindAsync(Guid userId, Guid mediaId, CancellationToken ct) =>
        Items(context.LibraryEntries.AsNoTracking().Where(x => x.UserId == userId && x.MediaItemId == mediaId && context.MediaItems.Where(CatalogAccess.Readable(userId)).Any(m => m.Id == x.MediaItemId))).SingleOrDefaultAsync(ct);

    public async Task<PersonalPage<LibraryItem>> ListAsync(Guid userId, LibraryQuery query, CancellationToken ct)
    {
        await using var tx = await context.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        var items = context.LibraryEntries.AsNoTracking().Where(x => x.UserId == userId && context.MediaItems.Where(CatalogAccess.Readable(userId)).Any(m => m.Id == x.MediaItemId));
        if (query.Status is not null) items = items.Where(x => x.Status == query.Status);
        if (query.Type is not null) items = items.Where(x => context.MediaItems.Any(m => m.Id == x.MediaItemId && m.MediaType == query.Type));
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var literal = query.Search.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
            items = items.Where(x => context.MediaItems.Any(m => m.Id == x.MediaItemId && EF.Functions.ILike(m.Title, "%" + literal + "%", "\\")));
        }
        var total = await items.CountAsync(ct);
        var rows = from entry in items
                   join media in context.MediaItems.AsNoTracking() on entry.MediaItemId equals media.Id
                   select new { Entry = entry, Media = media,
                       OwnRating = context.Ratings.Where(r => r.UserId == userId && r.MediaItemId == entry.MediaItemId).Select(r => (decimal?)r.Score).FirstOrDefault() };
        var ordered = query.Sort switch
        {
            "recently_completed" => rows.OrderBy(x => x.Entry.CompletedAt == null).ThenByDescending(x => x.Entry.CompletedAt).ThenByDescending(x => x.Entry.CreatedAt).ThenBy(x => x.Media.Id),
            "title" => rows.OrderBy(x => x.Media.Title.ToLower()).ThenByDescending(x => x.Entry.CreatedAt).ThenBy(x => x.Media.Id),
            "highest_rated" => rows.OrderBy(x => x.OwnRating == null).ThenByDescending(x => x.OwnRating).ThenByDescending(x => x.Entry.CreatedAt).ThenBy(x => x.Media.Id),
            "lowest_rated" => rows.OrderBy(x => x.OwnRating == null).ThenBy(x => x.OwnRating).ThenByDescending(x => x.Entry.CreatedAt).ThenBy(x => x.Media.Id),
            _ => rows.OrderByDescending(x => x.Entry.CreatedAt).ThenBy(x => x.Media.Id)
        };
        var page = await ordered.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(x => new LibraryItem(new MediaSummary(x.Media.Id, x.Media.MediaType, x.Media.Title, x.Media.ReleaseDate, x.Media.ImageReference),
                x.Entry.Status, x.Entry.StartedAt, x.Entry.CompletedAt, x.Entry.CreatedAt, x.Entry.UpdatedAt, x.OwnRating)).ToListAsync(ct);
        await tx.CommitAsync(ct);
        return new(page, query.Page, query.PageSize, total);
    }

    public async Task<LibraryItem?> PutAsync(Guid userId, Guid mediaId, LibraryRequest request, CancellationToken ct)
    {
        var entry = new LibraryEntry(userId, mediaId, request.Status!, request.StartedAt, request.CompletedAt);
        // Hold the upsert lock until the response is loaded; concurrent DELETE must wait.
        await using var tx = await context.Database.BeginTransactionAsync(ct);
        await ModerationLock.AcquireAsync(context, ct);
        if (!await context.Users.AnyAsync(user => user.Id == userId, ct)) return null;
        if (!await context.MediaItems.Where(CatalogAccess.Readable(userId)).AnyAsync(x => x.Id == mediaId, ct)) return null;
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO user_media (id, user_id, media_item_id, status, started_at, completed_at, created_at, updated_at)
            VALUES ({entry.Id}, {userId}, {mediaId}, {entry.Status}, {entry.StartedAt}, {entry.CompletedAt}, {entry.CreatedAt}, {entry.UpdatedAt})
            ON CONFLICT (user_id, media_item_id) DO UPDATE
            SET status = EXCLUDED.status, started_at = EXCLUDED.started_at, completed_at = EXCLUDED.completed_at, updated_at = EXCLUDED.updated_at
            """, ct);
        var result = await Items(context.LibraryEntries.AsNoTracking().Where(x => x.UserId == userId && x.MediaItemId == mediaId && context.MediaItems.Where(CatalogAccess.Readable(userId)).Any(m => m.Id == x.MediaItemId))).SingleAsync(ct);
        await tx.CommitAsync(ct);
        return result;
    }

    public async Task<LibraryStatusWriteResult> UpdateStatusAsync(Guid userId, Guid mediaId, LibraryStatusRequest request, CancellationToken ct)
    {
        await using var tx = await context.Database.BeginTransactionAsync(ct);
        await ModerationLock.AcquireAsync(context, ct);
        var entry = await FindAsync(userId, mediaId, ct);
        if (entry is null) return new(LibraryStatusWriteStatus.NotFound);
        if (entry.Status != request.ExpectedStatus) return new(LibraryStatusWriteStatus.Conflict);
        await context.LibraryEntries.Where(x => x.UserId == userId && x.MediaItemId == mediaId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, request.Status!).SetProperty(x => x.UpdatedAt, DateTimeOffset.UtcNow), ct);
        var result = await FindAsync(userId, mediaId, ct);
        await tx.CommitAsync(ct);
        return new(LibraryStatusWriteStatus.Success, result);
    }

    public async Task<bool> DeleteAsync(Guid userId, Guid mediaId, CancellationToken ct)
    {
        await using var tx = await context.Database.BeginTransactionAsync(ct);
        await ModerationLock.AcquireAsync(context, ct);
        if (!await context.MediaItems.Where(CatalogAccess.Readable(userId)).AnyAsync(x => x.Id == mediaId, ct)) return false;
        await context.LibraryEntries.Where(x => x.UserId == userId && x.MediaItemId == mediaId).ExecuteDeleteAsync(ct);
        await tx.CommitAsync(ct); return true;
    }
}
