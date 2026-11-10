using System.Data;
using Microsoft.EntityFrameworkCore;
using RateFreak.Application.Catalog;
using RateFreak.Application.Collections;
using RateFreak.Domain.Collections;
using RateFreak.Infrastructure.Catalog;
using RateFreak.Infrastructure.Persistence;

namespace RateFreak.Infrastructure.Collections;

internal sealed class CollectionQueries(RateFreakDbContext db) : ICollectionQueries
{
    private IQueryable<Collection> Accessible(Guid? viewer) => db.Collections.AsNoTracking().Where(c => c.Visibility == "public" || c.OwnerUserId == viewer && viewer != null);
    private IQueryable<CollectionSummary> Summaries(IQueryable<Collection> query) =>
        from c in query join owner in db.Users.AsNoTracking() on c.OwnerUserId equals owner.Id
        select new CollectionSummary(c.Id, new(owner.Id, owner.UserName!, owner.IsProfileHidden ? owner.UserName! : owner.DisplayName), c.Name, c.Description, c.Visibility, c.Revision, c.CreatedAt, c.UpdatedAt);
    private IQueryable<CollectionItem> VisibleItems(Guid? viewer, Guid id) => db.CollectionItems.AsNoTracking().Where(i => i.CollectionId == id && db.MediaItems.Where(CatalogAccess.Readable(viewer)).Any(m => m.Id == i.MediaItemId));
    private IQueryable<ItemDetails> Details(IQueryable<CollectionItem> query) =>
        from i in query join m in db.MediaItems.AsNoTracking() on i.MediaItemId equals m.Id
        select new ItemDetails(i.Id, i.MediaItemId, new MediaSummary(m.Id, m.MediaType, m.Title, m.ReleaseDate, m.ImageReference), i.Position, i.CreatedAt, i.UpdatedAt);
    public async Task<CollectionPage<CollectionSummary>> ListAsync(Guid? viewerId, bool ownOnly, int page, int pageSize, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct); var query = Accessible(viewerId);
        if (ownOnly) query = query.Where(c => c.OwnerUserId == viewerId);
        var count = await query.CountAsync(ct); var items = await Summaries(query.OrderByDescending(c => c.CreatedAt).ThenBy(c => c.Id).Skip((page - 1) * pageSize).Take(pageSize)).ToListAsync(ct);
        await tx.CommitAsync(ct); return new(items, page, pageSize, count);
    }
    public async Task<CollectionDetails?> FindAsync(Guid? viewerId, Guid id, CancellationToken ct)
    { var summary = await Summaries(Accessible(viewerId).Where(c => c.Id == id)).SingleOrDefaultAsync(ct); return summary is null ? null : new(summary); }
    public async Task<CollectionPage<ItemDetails>?> ItemsAsync(Guid? viewerId, Guid id, int page, int pageSize, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        if (!await Accessible(viewerId).AnyAsync(c => c.Id == id, ct)) return null;
        var query = VisibleItems(viewerId, id); var count = await query.CountAsync(ct);
        var items = await Details(query.OrderBy(i => i.Position).ThenBy(i => i.Id).Skip((page - 1) * pageSize).Take(pageSize)).ToListAsync(ct); await tx.CommitAsync(ct); return new(items, page, pageSize, count);
    }
    public Task<ItemDetails?> ItemAsync(Guid? viewerId, Guid id, Guid itemId, CancellationToken ct) =>
        Details(VisibleItems(viewerId, id).Where(i => i.Id == itemId && Accessible(viewerId).Any(c => c.Id == id))).SingleOrDefaultAsync(ct);
    internal async Task<CollectionDetails> OwnedDetailsAsync(Collection collection, CancellationToken ct) => new(await Summaries(db.Collections.Where(c => c.Id == collection.Id)).SingleAsync(ct));
}
