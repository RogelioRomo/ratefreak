using System.Text.Json;

namespace RateFreak.Application.Catalog;

public sealed record CatalogQuery(string? Search, string? MediaType, int Page, int PageSize);
public sealed record MediaSummary(Guid Id, string Type, string Title, DateOnly? ReleaseDate, string? ImageReference);
public sealed record ExternalIdentity(string Provider, string Namespace, string ExternalId, string? ExternalUrl, DateTimeOffset? LastSyncedAt);
public sealed record MediaDetails(Guid Id, string Type, string Title, string? Description,
    DateOnly? ReleaseDate, string? ImageReference, JsonElement Metadata, DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt, IReadOnlyList<ExternalIdentity> ExternalIds);
public sealed record CatalogPage(IReadOnlyList<MediaSummary> Items, int Page, int PageSize, int TotalCount);

public interface ICatalogQueries
{
    Task<CatalogPage> SearchAsync(CatalogQuery query, CancellationToken cancellationToken);
    Task<MediaDetails?> FindAsync(Guid id, CancellationToken cancellationToken);
}
