using Microsoft.EntityFrameworkCore;
using RateFreak.Application.Collections;
using RateFreak.Domain.Collections;
using RateFreak.Infrastructure.Persistence;

namespace RateFreak.Infrastructure.Collections;

internal sealed class CollectionCommands(RateFreakDbContext db) : ICollectionCommands
{
    public async Task<CollectionResult<CollectionDetails>> CreateAsync(Guid ownerId, CollectionCreate request, CancellationToken ct)
    {
        var visibility = request.Visibility ?? "private";
        var errors = CollectionValidation.Metadata(request.Name, request.Description, visibility);
        foreach (var error in CollectionValidation.ValidateFields(request.Fields ?? [])) errors[error.Key] = error.Value;
        if (errors.Count != 0) return new(CollectionStatus.Invalid, Errors: errors);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var collection = new Collection(ownerId, request.Name!, request.Description, visibility);
        db.Collections.Add(collection);
        foreach (var field in request.Fields ?? [])
            db.CollectionFields.Add(new CollectionField(collection.Id, field.Key!, field.Label!, field.FieldType!, field.Required,
                CollectionMapping.Configuration(field.Configuration), field.Position ?? 0));
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
        collection!.SetMetadata(request.Name!, request.Description, request.Visibility!);
        collection.Advance();
        await db.SaveChangesAsync(ct);
        var result = await new CollectionQueries(db).OwnedDetailsAsync(collection, ct);
        await tx.CommitAsync(ct);
        return new(CollectionStatus.Success, result, collection.Revision);
    }
}
