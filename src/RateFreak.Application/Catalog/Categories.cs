using System.Text.Json.Serialization;

namespace RateFreak.Application.Catalog;

public sealed record CategorySummary(Guid Id, string Name, string Scope, long Revision, bool IsHidden = false, bool IsSeeded = false, Guid? CreatorUserId = null);
public sealed record CategoryPage(IReadOnlyList<CategorySummary> Items, int Page, int PageSize, int TotalCount);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CategoryCreate(string? Name, string? Scope);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CategoryRename(long? ExpectedRevision, string? Name, string? Reason);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CategoryMerge(long? ExpectedRevision, Guid? TargetId, long? TargetExpectedRevision, string? Reason);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CategoryStateChange(long? ExpectedRevision, string? Reason);
public interface ICategoryService
{
    Task<CatalogResult<CategoryPage>> ListForAdminAsync(Guid actorId, string? search, int page, int pageSize, CancellationToken ct);
    Task<CategoryPage> ListAsync(Guid? viewerId, string? search, bool ownOnly, int page, int pageSize, CancellationToken ct);
    Task<CatalogResult<CategorySummary>> CreateAsync(Guid actorId, CategoryCreate request, CancellationToken ct);
    Task<CatalogResult<CategorySummary>> RenameAsync(Guid actorId, Guid id, CategoryRename request, bool admin, CancellationToken ct);
    Task<CatalogResult<CategorySummary>> MergeAsync(Guid actorId, Guid id, CategoryMerge request, CancellationToken ct);
    Task<CatalogResult<CategorySummary>> SetHiddenAsync(Guid actorId, Guid id, CategoryStateChange request, bool hidden, CancellationToken ct);
}
