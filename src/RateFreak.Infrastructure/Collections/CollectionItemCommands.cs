using Microsoft.EntityFrameworkCore;
using Npgsql;
using RateFreak.Application.Catalog;
using RateFreak.Application.Collections;
using RateFreak.Domain.Collections;
using RateFreak.Infrastructure.Catalog;
using RateFreak.Infrastructure.Persistence;

namespace RateFreak.Infrastructure.Collections;

internal sealed class CollectionItemCommands(RateFreakDbContext db) : ICollectionItemCommands
{
    public async Task<CollectionResult<ItemDetails>> AddAsync(Guid ownerId, Guid id, ItemCreate request, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct); var collection = await CollectionMutation.LockAsync(db, ownerId, id, ct);
        var rejected = CollectionMutation.Check<ItemDetails>(collection, request.ExpectedRevision); if (rejected is not null) return rejected;
        if (request.MediaItemId is null || request.MediaItemId == Guid.Empty || request.Position is < 0) return CollectionMutation.Invalid<ItemDetails>("mediaItemId", "An existing item and nonnegative position are required.");
        var media = await db.MediaItems.AsNoTracking().Where(CatalogAccess.Readable(ownerId)).SingleOrDefaultAsync(m => m.Id == request.MediaItemId, ct);
        if (media is null) return new(CollectionStatus.NotFound);
        if (media.Visibility != "public" && (media.CreatorUserId != ownerId || collection!.Visibility != "private")) return CollectionMutation.Invalid<ItemDetails>("mediaItemId", "Private items require your own private list.");
        if (await db.CollectionItems.AnyAsync(i => i.CollectionId == id && i.MediaItemId == media.Id, ct)) return new(CollectionStatus.Conflict);
        var item = new CollectionItem(id, media.Id, request.Position ?? 0); db.Add(item); collection!.Advance();
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "ux_collection_items_media" }) { return new(CollectionStatus.Conflict); }
        var result = new ItemDetails(item.Id, media.Id, new MediaSummary(media.Id, media.MediaType, media.Title, media.ReleaseDate, media.ImageReference), item.Position, item.CreatedAt, item.UpdatedAt);
        await tx.CommitAsync(ct); return new(CollectionStatus.Success, result, collection.Revision);
    }
    public async Task<CollectionResult<ItemDetails>> UpdateAsync(Guid ownerId, Guid id, Guid itemId, ItemUpdate request, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct); var collection = await CollectionMutation.LockAsync(db, ownerId, id, ct);
        var rejected = CollectionMutation.Check<ItemDetails>(collection, request.ExpectedRevision); if (rejected is not null) return rejected;
        var item = await db.CollectionItems.SingleOrDefaultAsync(i => i.Id == itemId && i.CollectionId == id && db.MediaItems.Where(CatalogAccess.Readable(ownerId)).Any(m => m.Id == i.MediaItemId), ct);
        if (item is null) return new(CollectionStatus.NotFound);
        if (request.Position is null or < 0) return CollectionMutation.Invalid<ItemDetails>("position", "A nonnegative position is required.");
        item.Reorder(request.Position.Value); collection!.Advance(); await db.SaveChangesAsync(ct);
        var media = await db.MediaItems.AsNoTracking().Where(m => m.Id == item.MediaItemId).Select(m => new MediaSummary(m.Id, m.MediaType, m.Title, m.ReleaseDate, m.ImageReference)).SingleAsync(ct);
        await tx.CommitAsync(ct); return new(CollectionStatus.Success, new(item.Id, item.MediaItemId, media, item.Position, item.CreatedAt, item.UpdatedAt), collection.Revision);
    }
    public async Task<CollectionResult<CollectionRevision>> ClearUnavailableAsync(Guid ownerId, Guid id, long? expectedRevision, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var collection = await CollectionMutation.LockAsync(db, ownerId, id, ct);
        var rejected = CollectionMutation.Check<CollectionRevision>(collection, expectedRevision);
        if (rejected is not null) return rejected;
        await db.CollectionItems.Where(i => i.CollectionId == id && !db.MediaItems.Where(CatalogAccess.Readable(ownerId)).Any(m => m.Id == i.MediaItemId)).ExecuteDeleteAsync(ct);
        // Always advance; the response reveals neither hidden identities nor counts.
        collection!.Advance(); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return new(CollectionStatus.Success, new(collection.Revision), collection.Revision);
    }
    public async Task<CollectionResult<CollectionRevision>> DeleteAsync(Guid ownerId, Guid id, Guid itemId, long? expectedRevision, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct); var collection = await CollectionMutation.LockAsync(db, ownerId, id, ct);
        var rejected = CollectionMutation.Check<CollectionRevision>(collection, expectedRevision); if (rejected is not null) return rejected;
        var item = await db.CollectionItems.SingleOrDefaultAsync(i => i.Id == itemId && i.CollectionId == id && db.MediaItems.Where(CatalogAccess.Readable(ownerId)).Any(m => m.Id == i.MediaItemId), ct);
        if (item is null) return new(CollectionStatus.NotFound);
        db.Remove(item); collection!.Advance(); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return new(CollectionStatus.Success, new(collection.Revision), collection.Revision);
    }
}
