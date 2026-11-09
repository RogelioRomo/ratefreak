using System.Text.Json;

namespace RateFreak.Application.Catalog;

public sealed record CatalogQuery(string? Search, string? MediaType, int Page, int PageSize, Guid? CategoryId = null, bool OwnOnly = false);
public sealed record MediaSummary(Guid Id, string Type, string Title, DateOnly? ReleaseDate, string? ImageReference);
public sealed record ExternalIdentity(string Provider, string Namespace, string ExternalId, string? ExternalUrl, DateTimeOffset? LastSyncedAt);
public sealed record MediaDetails(Guid Id, string Type, string Title, string? Description,
    DateOnly? ReleaseDate, string? ImageReference, JsonElement Metadata, DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt, IReadOnlyList<ExternalIdentity> ExternalIds)
{
    public CategorySummary? Category { get; init; }
    public Guid? CreatorUserId { get; init; }
    public string Provenance { get; init; } = "system";
    public string Visibility { get; init; } = "public";
    public bool IsArchived { get; init; }
    public bool IsHidden { get; init; }
    public long Revision { get; init; } = 1;
}
public sealed record CatalogPage(IReadOnlyList<MediaSummary> Items, int Page, int PageSize, int TotalCount);

public interface ICatalogQueries
{
    Task<CatalogPage> SearchAsync(Guid? viewerId, CatalogQuery query, CancellationToken cancellationToken);
    Task<MediaDetails?> FindAsync(Guid? viewerId, Guid id, CancellationToken cancellationToken);
}
