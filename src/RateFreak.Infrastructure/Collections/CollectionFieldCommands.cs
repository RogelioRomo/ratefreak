using Microsoft.EntityFrameworkCore;
using RateFreak.Application.Collections;
using RateFreak.Domain.Collections;
using RateFreak.Infrastructure.Persistence;

namespace RateFreak.Infrastructure.Collections;

internal sealed class CollectionFieldCommands(RateFreakDbContext db) : ICollectionFieldCommands
{
    public async Task<CollectionResult<FieldDetails>> AddAsync(Guid ownerId, Guid id, FieldCreate request, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var collection = await CollectionMutation.LockAsync(db, ownerId, id, ct);
        var rejected = CollectionMutation.Check<FieldDetails>(collection, request.ExpectedRevision);
        if (rejected is not null) return rejected;
        if (request.Field is null) return CollectionMutation.Invalid<FieldDetails>("field", "A field definition is required.");
        var fields = await db.CollectionFields.Where(x => x.CollectionId == id).ToListAsync(ct);
        if (fields.Any(x => x.Key == request.Field.Key)) return new(CollectionStatus.Conflict);
        var errors = CollectionValidation.ValidateFields([.. fields.Select(CollectionMapping.Definition), request.Field]);
        if (errors.Count != 0) return new(CollectionStatus.Invalid, Errors: errors);
        if (request.Field.Required && await db.CollectionItems.AnyAsync(x => x.CollectionId == id, ct)) return new(CollectionStatus.Conflict);
        var input = request.Field;
        var field = new CollectionField(id, input.Key!, input.Label!, input.FieldType!, input.Required, CollectionMapping.Configuration(input.Configuration), input.Position ?? 0);
        db.CollectionFields.Add(field); collection!.Advance(schema: true);
        await db.SaveChangesAsync(ct); var result = CollectionMapping.Field(field);
        await tx.CommitAsync(ct); return new(CollectionStatus.Success, result, collection.Revision);
    }

    public async Task<CollectionResult<FieldDetails>> UpdateAsync(Guid ownerId, Guid id, Guid fieldId, FieldUpdate request, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var collection = await CollectionMutation.LockAsync(db, ownerId, id, ct);
        var rejected = CollectionMutation.Check<FieldDetails>(collection, request.ExpectedRevision);
        if (rejected is not null) return rejected;
        var fields = await db.CollectionFields.Where(x => x.CollectionId == id).ToListAsync(ct);
        var field = fields.SingleOrDefault(x => x.Id == fieldId);
        if (field is null) return new(CollectionStatus.NotFound);
        if (request.Required is null || request.Position is null || request.Configuration.ValueKind == System.Text.Json.JsonValueKind.Undefined)
            return CollectionMutation.Invalid<FieldDetails>("field", "Label, required, configuration and position are required for replacement.");
        var definition = new FieldDefinition(field.Key, request.Label, field.FieldType, request.Required.Value, request.Configuration, request.Position);
        var definitions = fields.Select(x => x.Id == fieldId ? definition : CollectionMapping.Definition(x)).ToList();
        var errors = CollectionValidation.ValidateFields(definitions);
        if (errors.Count != 0) return new(CollectionStatus.Invalid, Errors: errors);
        // Stream existing values rather than loading an unbounded collection into memory.
        await foreach (var data in db.CollectionItems.AsNoTracking().Where(x => x.CollectionId == id).Select(x => x.Data).AsAsyncEnumerable().WithCancellation(ct))
            if (CollectionValidation.ValidateData(CollectionMapping.Json(data), definitions).Count != 0) return new(CollectionStatus.Conflict);
        field.SetValues(request.Label!, request.Required.Value, CollectionMapping.Configuration(request.Configuration), request.Position.Value);
        collection!.Advance(schema: true); await db.SaveChangesAsync(ct);
        var result = CollectionMapping.Field(field); await tx.CommitAsync(ct);
        return new(CollectionStatus.Success, result, collection.Revision);
    }

    public async Task<CollectionResult<CollectionRevision>> DeleteAsync(Guid ownerId, Guid id, Guid fieldId, long? expectedRevision, bool confirm, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var collection = await CollectionMutation.LockAsync(db, ownerId, id, ct);
        var rejected = CollectionMutation.Check<CollectionRevision>(collection, expectedRevision);
        if (rejected is not null) return rejected;
        var field = await db.CollectionFields.SingleOrDefaultAsync(x => x.CollectionId == id && x.Id == fieldId, ct);
        if (field is null) return new(CollectionStatus.NotFound);
        if (!confirm) return CollectionMutation.Invalid<CollectionRevision>("confirm", "Explicit confirm=true is required; stored values will be permanently removed.");
        var now = DateTimeOffset.UtcNow;
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE collection_items SET data = data - {field.Key}, updated_at = {now}
            WHERE collection_id = {id} AND jsonb_exists(data, {field.Key})
            """, ct);
        db.CollectionFields.Remove(field); collection!.Advance(schema: true);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return new(CollectionStatus.Success, new(collection.Revision), collection.Revision);
    }
}
