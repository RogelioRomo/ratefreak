using Microsoft.EntityFrameworkCore;
using Npgsql;
using RateFreak.Application.Collections;
using RateFreak.Domain.Collections;
using RateFreak.Infrastructure.Persistence;

namespace RateFreak.Infrastructure.Collections;

internal sealed class CollectionItemCommands(RateFreakDbContext db) : ICollectionItemCommands
{
    public async Task<CollectionResult<ItemDetails>> AddAsync(Guid ownerId, Guid id, ItemCreate request, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var collection = await CollectionMutation.LockAsync(db, ownerId, id, ct);
        var rejected = CollectionMutation.Check<ItemDetails>(collection, request.ExpectedRevision);
        if (rejected is not null) return rejected;
        var error = await Validate(id, request.MediaItemId, request.Title, request.Data, request.Position ?? 0, ct);
        if (error is not null) return error;
        string? mediaTitle = null;
        if (request.MediaItemId is { } mediaId)
        {
            mediaTitle = await db.MediaItems.Where(x => x.Id == mediaId).Select(x => x.Title).SingleOrDefaultAsync(ct);
            if (mediaTitle is null) return new(CollectionStatus.NotFound);
            if (await db.CollectionItems.AnyAsync(x => x.CollectionId == id && x.MediaItemId == mediaId, ct)) return new(CollectionStatus.Conflict);
        }
        var item = new CollectionItem(id, request.MediaItemId, request.Title, request.Data.GetRawText(), request.Position ?? 0);
        db.CollectionItems.Add(item); collection!.Advance();
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation, ConstraintName: "FK_collection_items_media_items_media_item_id" })
        { return new(CollectionStatus.NotFound); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "ux_collection_items_media" })
        { return new(CollectionStatus.Conflict); }
        var result = CollectionMapping.Item(item, mediaTitle ?? item.Title!);
        await tx.CommitAsync(ct); return new(CollectionStatus.Success, result, collection.Revision);
    }

    public async Task<CollectionResult<ItemDetails>> UpdateAsync(Guid ownerId, Guid id, Guid itemId, ItemUpdate request, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var collection = await CollectionMutation.LockAsync(db, ownerId, id, ct);
        var rejected = CollectionMutation.Check<ItemDetails>(collection, request.ExpectedRevision);
        if (rejected is not null) return rejected;
        var item = await db.CollectionItems.SingleOrDefaultAsync(x => x.CollectionId == id && x.Id == itemId, ct);
        if (item is null) return new(CollectionStatus.NotFound);
        if (request.Position is null) return CollectionMutation.Invalid<ItemDetails>("position", "Position is required for replacement.");
        var error = await Validate(id, item.MediaItemId, request.Title, request.Data, request.Position.Value, ct);
        if (error is not null) return error;
        item.SetValues(request.Title, request.Data.GetRawText(), request.Position.Value); collection!.Advance();
        await db.SaveChangesAsync(ct);
        var title = item.MediaItemId is { } mediaId ? await db.MediaItems.Where(x => x.Id == mediaId).Select(x => x.Title).SingleAsync(ct) : item.Title!;
        var result = CollectionMapping.Item(item, title);
        await tx.CommitAsync(ct); return new(CollectionStatus.Success, result, collection.Revision);
    }

    public async Task<CollectionResult<CollectionRevision>> DeleteAsync(Guid ownerId, Guid id, Guid itemId, long? expectedRevision, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var collection = await CollectionMutation.LockAsync(db, ownerId, id, ct);
        var rejected = CollectionMutation.Check<CollectionRevision>(collection, expectedRevision);
        if (rejected is not null) return rejected;
        var item = await db.CollectionItems.SingleOrDefaultAsync(x => x.Id == itemId && x.CollectionId == id, ct);
        if (item is null) return new(CollectionStatus.NotFound);
        db.CollectionItems.Remove(item); collection!.Advance();
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return new(CollectionStatus.Success, new(collection.Revision), collection.Revision);
    }

    private async Task<CollectionResult<ItemDetails>?> Validate(Guid id, Guid? mediaId, string? title, System.Text.Json.JsonElement data, int position, CancellationToken ct)
    {
        var fields = await db.CollectionFields.AsNoTracking().Where(x => x.CollectionId == id).ToListAsync(ct);
        var errors = CollectionValidation.ValidateData(data, fields.Select(CollectionMapping.Definition).ToList());
        if (mediaId is not null && title is not null) errors["title"] = ["Canonical entries use the catalog title."];
        if (mediaId is null && !CollectionRules.ValidText(title?.Trim(), 200)) errors["title"] = ["A custom title of 1–200 valid characters is required."];
        if (position < 0) errors["position"] = ["Position must be nonnegative."];
        return errors.Count == 0 ? null : new(CollectionStatus.Invalid, Errors: errors);
    }
}
