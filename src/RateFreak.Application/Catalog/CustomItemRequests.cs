using System.Text.Json.Serialization;

namespace RateFreak.Application.Catalog;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CustomItemCreate(string? Title, string? Description, Guid? CategoryId, string? Visibility);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CustomItemUpdate(long? ExpectedRevision, string? Title, string? Description, Guid? CategoryId);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ItemStateChange(long? ExpectedRevision);
public interface ICatalogItemCommands
{
    Task<CatalogResult<MediaDetails>> CreateAsync(Guid actorId, CustomItemCreate request, CancellationToken ct);
    Task<CatalogResult<MediaDetails>> UpdateAsync(Guid actorId, Guid id, CustomItemUpdate request, CancellationToken ct);
    Task<CatalogResult<MediaDetails>> ChangeStateAsync(Guid actorId, Guid id, string action, ItemStateChange request, CancellationToken ct);
    Task<MediaDetails?> ManageAsync(Guid actorId, Guid id, CancellationToken ct);
}
