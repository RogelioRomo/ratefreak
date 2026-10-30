using System.Data;
using Microsoft.EntityFrameworkCore;
using RateFreak.Application.Collections;
using RateFreak.Domain.Collections;
using RateFreak.Infrastructure.Persistence;

namespace RateFreak.Infrastructure.Collections;

internal sealed class CollectionQueries(RateFreakDbContext db) : ICollectionQueries
{
    private IQueryable<Collection> Accessible(Guid? viewerId) => db.Collections.AsNoTracking()
        .Where(x => x.Visibility == "public" || x.OwnerUserId == viewerId);
    private IQueryable<CollectionSummary> Summaries(IQueryable<Collection> source) =>
        from collection in source
        join owner in db.Users.AsNoTracking() on collection.OwnerUserId equals owner.Id
        select new CollectionSummary(collection.Id, new CollectionAuthor(owner.Id, owner.UserName!, owner.DisplayName),
            collection.Name, collection.Description, collection.Visibility, collection.Revision, collection.SchemaVersion, collection.CreatedAt, collection.UpdatedAt);

    public async Task<CollectionPage<CollectionSummary>> ListAsync(Guid? viewerId, bool ownOnly, int page, int pageSize, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        var source = Accessible(viewerId);
        if (ownOnly) source = source.Where(x => x.OwnerUserId == viewerId);
        var count = await source.CountAsync(ct);
        var items = await Summaries(source.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize)).ToListAsync(ct);
        await tx.CommitAsync(ct); return new(items, page, pageSize, count);
    }

    public async Task<CollectionDetails?> FindAsync(Guid? viewerId, Guid id, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        var summary = await Summaries(Accessible(viewerId).Where(x => x.Id == id)).SingleOrDefaultAsync(ct);
        if (summary is null) return null;
        var fields = await db.CollectionFields.AsNoTracking().Where(x => x.CollectionId == id).OrderBy(x => x.Position).ThenBy(x => x.Id).ToListAsync(ct);
        await tx.CommitAsync(ct); return new(summary, fields.Select(CollectionMapping.Field).ToList());
    }

    public async Task<CollectionPage<ItemDetails>?> ItemsAsync(Guid? viewerId, Guid id, int page, int pageSize, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        if (!await Accessible(viewerId).AnyAsync(x => x.Id == id, ct)) return null;
        var source = db.CollectionItems.AsNoTracking().Where(x => x.CollectionId == id);
        var count = await source.CountAsync(ct);
        var items = await ItemRows(source.OrderBy(x => x.Position).ThenBy(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize)).ToListAsync(ct);
        await tx.CommitAsync(ct); return new(items.Select(x => CollectionMapping.Item(x.Item, x.Title)).ToList(), page, pageSize, count);
    }

    private sealed record ItemRow(CollectionItem Item, string Title);
    private IQueryable<ItemRow> ItemRows(IQueryable<CollectionItem> source) =>
        from item in source
        join media in db.MediaItems.AsNoTracking() on item.MediaItemId equals media.Id into mediaRows
        from media in mediaRows.DefaultIfEmpty()
        select new ItemRow(item, item.MediaItemId == null ? item.Title! : media.Title);

    public async Task<ItemDetails?> ItemAsync(Guid? viewerId, Guid id, Guid itemId, CancellationToken ct)
    {
        var rows = await ItemRows(db.CollectionItems.AsNoTracking().Where(x => x.CollectionId == id && x.Id == itemId
            && Accessible(viewerId).Any(c => c.Id == id))).SingleOrDefaultAsync(ct);
        return rows is null ? null : CollectionMapping.Item(rows.Item, rows.Title);
    }

    public async Task<DeletionImpact?> DeletionImpactAsync(Guid ownerId, Guid id, Guid fieldId, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        var collection = await db.Collections.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.OwnerUserId == ownerId, ct);
        if (collection is null) return null;
        var field = await db.CollectionFields.AsNoTracking().SingleOrDefaultAsync(x => x.Id == fieldId && x.CollectionId == id, ct);
        if (field is null) return null;
        var values = await db.CollectionItems.AsNoTracking().Where(x => x.CollectionId == id).Select(x => x.Data).ToListAsync(ct);
        var count = values.Count(x => CollectionMapping.Json(x).TryGetProperty(field.Key, out var value) && value.ValueKind != System.Text.Json.JsonValueKind.Null);
        await tx.CommitAsync(ct); return new(field.Id, field.Key, count, collection.Revision);
    }

    // Used inside an existing locked write transaction; do not start another transaction.
    internal async Task<CollectionDetails> OwnedDetailsAsync(Collection collection, CancellationToken ct)
    {
        var summary = await Summaries(db.Collections.Where(x => x.Id == collection.Id)).SingleAsync(ct);
        var fields = await db.CollectionFields.Where(x => x.CollectionId == collection.Id).OrderBy(x => x.Position).ThenBy(x => x.Id).ToListAsync(ct);
        return new(summary, fields.Select(CollectionMapping.Field).ToList());
    }
}
