using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RateFreak.Application.Catalog;
using RateFreak.Infrastructure.Persistence;

namespace RateFreak.Infrastructure.Catalog;

internal sealed class CatalogQueries(RateFreakDbContext context) : ICatalogQueries
{
    public async Task<CatalogPage> SearchAsync(Guid? viewerId, CatalogQuery query, CancellationToken cancellationToken)
    {
        await using var tx = await context.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        var items = context.MediaItems.AsNoTracking().Where(CatalogAccess.Readable(viewerId));
        if (query.OwnOnly && viewerId is Guid ownerId) items = context.MediaItems.AsNoTracking().Where(CatalogAccess.Manageable(ownerId));
        if (query.CategoryId is Guid categoryId)
        {
            var target = await context.CategoryAliases.Where(a => a.SourceCategoryId == categoryId).Select(a => (Guid?)a.TargetCategoryId).SingleOrDefaultAsync(cancellationToken) ?? categoryId;
            items = items.Where(x => x.CategoryId == target);
        }
        if (query.MediaType is not null) items = items.Where(x => x.MediaType == query.MediaType);
        if (!string.IsNullOrEmpty(query.Search))
        {
            var pattern = "%" + query.Search.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
            items = items.Where(x => EF.Functions.ILike(x.Title, pattern, "\\"));
        }
        var total = await items.CountAsync(cancellationToken);
        var page = await items.OrderBy(x => x.Title).ThenBy(x => x.Id)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(x => new MediaSummary(x.Id, x.MediaType, x.Title, x.ReleaseDate, x.ImageReference))
            .ToListAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return new CatalogPage(page, query.Page, query.PageSize, total);
    }

    public async Task<MediaDetails?> FindAsync(Guid? viewerId, Guid id, CancellationToken cancellationToken)
    {
        await using var tx = await context.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        var item = await context.MediaItems.AsNoTracking().Include(x => x.Category).Where(CatalogAccess.Readable(viewerId)).SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (item is null) return null;
        var mappings = await context.MediaExternalIds.AsNoTracking().Where(x => x.MediaItemId == id)
            .OrderBy(x => x.Provider).ThenBy(x => x.Namespace).ThenBy(x => x.ExternalId)
            .Select(x => new ExternalIdentity(x.Provider, x.Namespace, x.ExternalId, x.ExternalUrl, x.LastSyncedAt))
            .ToListAsync(cancellationToken);
        using var metadata = JsonDocument.Parse(item.Metadata);
        var result = new MediaDetails(item.Id, item.MediaType, item.Title, item.Description, item.ReleaseDate,
            item.ImageReference, metadata.RootElement.Clone(), item.CreatedAt, item.UpdatedAt, mappings)
        { Category = new(item.Category.Id, item.Category.Name, item.Category.Scope, item.Category.Revision, item.Category.IsHidden, item.Category.IsSeeded, item.Category.OwnerUserId), CreatorUserId = item.CreatorUserId, Provenance = item.Provenance, Visibility = item.Visibility, IsArchived = item.IsArchived, IsHidden = item.IsHidden, Revision = item.Revision };
        await tx.CommitAsync(cancellationToken); return result;
    }
}
