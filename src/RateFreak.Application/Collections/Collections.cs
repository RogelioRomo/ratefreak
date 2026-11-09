using RateFreak.Application.Catalog;

namespace RateFreak.Application.Collections;

public sealed record CollectionPage<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
public sealed record CollectionAuthor(Guid Id, string Handle, string DisplayName);
public sealed record CollectionSummary(Guid Id, CollectionAuthor Owner, string Name, string? Description, string Visibility,
    long Revision, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
public sealed record CollectionDetails(CollectionSummary Collection);
public sealed record ItemDetails(Guid Id, Guid MediaItemId, MediaSummary Media, int Position, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
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
}
public interface ICollectionCommands
{
    Task<CollectionResult<CollectionDetails>> CreateAsync(Guid ownerId, CollectionCreate request, CancellationToken ct);
    Task<CollectionResult<CollectionDetails>> UpdateAsync(Guid ownerId, Guid id, CollectionUpdate request, CancellationToken ct);
}
public interface ICollectionItemCommands
{
    Task<CollectionResult<ItemDetails>> AddAsync(Guid ownerId, Guid id, ItemCreate request, CancellationToken ct);
    Task<CollectionResult<ItemDetails>> UpdateAsync(Guid ownerId, Guid id, Guid itemId, ItemUpdate request, CancellationToken ct);
    Task<CollectionResult<CollectionRevision>> ClearUnavailableAsync(Guid ownerId, Guid id, long? expectedRevision, CancellationToken ct);
    Task<CollectionResult<CollectionRevision>> DeleteAsync(Guid ownerId, Guid id, Guid itemId, long? expectedRevision, CancellationToken ct);
}
