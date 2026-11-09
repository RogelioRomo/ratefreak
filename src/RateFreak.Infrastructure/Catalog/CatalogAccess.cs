using System.Linq.Expressions;
using RateFreak.Domain.Catalog;

namespace RateFreak.Infrastructure.Catalog;

internal static class CatalogAccess
{
    public static Expression<Func<MediaItem, bool>> Readable(Guid? viewerId) => item =>
        !item.IsArchived && (item.CreatorUserId == viewerId && viewerId != null ||
        item.Visibility == "public" && item.Category.Scope == "public" && !item.IsHidden && !item.Category.IsHidden);
    public static Expression<Func<MediaItem, bool>> Manageable(Guid ownerId) => item => item.CreatorUserId == ownerId;
}
