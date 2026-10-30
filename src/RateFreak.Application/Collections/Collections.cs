using System.Text.Json;

namespace RateFreak.Application.Collections;

public sealed record CollectionPage<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
public sealed record CollectionAuthor(Guid Id, string Handle, string DisplayName);
public sealed record CollectionSummary(Guid Id, CollectionAuthor Owner, string Name, string? Description, string Visibility,
    long Revision, long SchemaVersion, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
public sealed record FieldDetails(Guid Id, string Key, string Label, string FieldType, bool Required, JsonElement Configuration, int Position);
public sealed record CollectionDetails(CollectionSummary Collection, IReadOnlyList<FieldDetails> Fields);
public sealed record ItemDetails(Guid Id, Guid? MediaItemId, string Title, JsonElement Data, int Position, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
public sealed record DeletionImpact(Guid FieldId, string Key, int PopulatedItemCount, long Revision);
public sealed record CollectionRevision(long Revision);
public enum CollectionStatus { Success, NotFound, Conflict, Invalid }
public sealed record CollectionResult<T>(CollectionStatus Status, T? Value = null, long? Revision = null,
    Dictionary<string, string[]>? Errors = null) where T : class;

public interface ICollectionQueries
{
    Task<CollectionPage<CollectionSummary>> ListAsync(Guid? viewerId, bool ownOnly, int page, int pageSize, CancellationToken ct);
    Task<CollectionDetails?> FindAsync(Guid? viewerId, Guid id, CancellationToken ct);
    Task<CollectionPage<ItemDetails>?> ItemsAsync(Guid? viewerId, Guid id, int page, int pageSize, CancellationToken ct);
    Task<ItemDetails?> ItemAsync(Guid? viewerId, Guid id, Guid itemId, CancellationToken ct);
    Task<DeletionImpact?> DeletionImpactAsync(Guid ownerId, Guid id, Guid fieldId, CancellationToken ct);
}
public interface ICollectionCommands
{
    Task<CollectionResult<CollectionDetails>> CreateAsync(Guid ownerId, CollectionCreate request, CancellationToken ct);
    Task<CollectionResult<CollectionDetails>> UpdateAsync(Guid ownerId, Guid id, CollectionUpdate request, CancellationToken ct);
}
public interface ICollectionFieldCommands
{
    Task<CollectionResult<FieldDetails>> AddAsync(Guid ownerId, Guid id, FieldCreate request, CancellationToken ct);
    Task<CollectionResult<FieldDetails>> UpdateAsync(Guid ownerId, Guid id, Guid fieldId, FieldUpdate request, CancellationToken ct);
    Task<CollectionResult<CollectionRevision>> DeleteAsync(Guid ownerId, Guid id, Guid fieldId, long? expectedRevision, bool confirm, CancellationToken ct);
}
public interface ICollectionItemCommands
{
    Task<CollectionResult<ItemDetails>> AddAsync(Guid ownerId, Guid id, ItemCreate request, CancellationToken ct);
    Task<CollectionResult<ItemDetails>> UpdateAsync(Guid ownerId, Guid id, Guid itemId, ItemUpdate request, CancellationToken ct);
    Task<CollectionResult<CollectionRevision>> DeleteAsync(Guid ownerId, Guid id, Guid itemId, long? expectedRevision, CancellationToken ct);
}
