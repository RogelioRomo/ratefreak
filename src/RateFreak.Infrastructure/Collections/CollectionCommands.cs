using Microsoft.EntityFrameworkCore;
using RateFreak.Application.Collections;
using RateFreak.Domain.Collections;
using RateFreak.Infrastructure.Persistence;
using RateFreak.Infrastructure.Moderation;

namespace RateFreak.Infrastructure.Collections;

internal sealed class CollectionCommands(RateFreakDbContext db) : ICollectionCommands
{
    public async Task<CollectionResult<CollectionDetails>> CreateAsync(Guid ownerId, CollectionCreate request, CancellationToken ct)
    {
        var visibility = request.Visibility ?? "private";
        var errors = CollectionValidation.Metadata(request.Name, request.Description, visibility);
        if (errors.Count != 0) return new(CollectionStatus.Invalid, Errors: errors);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await ModerationLock.AcquireAsync(db, ct);
        if (!await db.Users.AnyAsync(user => user.Id == ownerId, ct)) return new(CollectionStatus.NotFound);
        var collection = new Collection(ownerId, request.Name!, request.Description, visibility);
        db.Collections.Add(collection);
        await db.SaveChangesAsync(ct);
        var result = await new CollectionQueries(db).OwnedDetailsAsync(collection, ct);
        await tx.CommitAsync(ct);
        return new(CollectionStatus.Success, result, collection.Revision);
    }

    public async Task<CollectionResult<CollectionDetails>> UpdateAsync(Guid ownerId, Guid id, CollectionUpdate request, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var collection = await CollectionMutation.LockAsync(db, ownerId, id, ct);
        var rejected = CollectionMutation.Check<CollectionDetails>(collection, request.ExpectedRevision);
        if (rejected is not null) return rejected;
        var errors = CollectionValidation.Metadata(request.Name, request.Description, request.Visibility);
        if (errors.Count != 0) return new(CollectionStatus.Invalid, Errors: errors);
        if (collection!.Visibility != "public" && request.Visibility == "public" && await db.CollectionItems.Where(x => x.CollectionId == id).AnyAsync(x => db.MediaItems.Any(m => m.Id == x.MediaItemId && (m.Visibility != "public" || m.Category.Scope != "public" || m.IsArchived || m.IsHidden || m.Category.IsHidden)), ct))
            return CollectionMutation.Invalid<CollectionDetails>("visibility", "Remove unavailable or private items before publishing this list.");
        collection!.SetMetadata(request.Name!, request.Description, request.Visibility!);
        collection.Advance();
        await db.SaveChangesAsync(ct);
        var result = await new CollectionQueries(db).OwnedDetailsAsync(collection, ct);
        await tx.CommitAsync(ct);
        return new(CollectionStatus.Success, result, collection.Revision);
    }
}
