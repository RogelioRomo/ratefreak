using Microsoft.EntityFrameworkCore;
using RateFreak.Application.Collections;
using RateFreak.Domain.Collections;
using RateFreak.Infrastructure.Persistence;

namespace RateFreak.Infrastructure.Collections;

internal static class CollectionMutation
{
    // Every writer locks the same parent before inspecting schema, revision or items.
    // Call only inside the writer's explicit transaction.
    public static Task<Collection?> LockAsync(RateFreakDbContext db, Guid ownerId, Guid id, CancellationToken ct) =>
        db.Collections.FromSqlInterpolated($"SELECT * FROM collections WHERE id = {id} AND owner_user_id = {ownerId} FOR UPDATE")
            .SingleOrDefaultAsync(ct);

    public static CollectionResult<T>? Check<T>(Collection? collection, long? expectedRevision) where T : class =>
        collection is null ? new(CollectionStatus.NotFound)
        : expectedRevision is null or <= 0 ? Invalid<T>("expectedRevision", "A positive expectedRevision is required.")
        : expectedRevision != collection.Revision ? new(CollectionStatus.Conflict) : null;

    public static CollectionResult<T> Invalid<T>(string key, string message) where T : class =>
        new(CollectionStatus.Invalid, Errors: new() { [key] = [message] });
}
