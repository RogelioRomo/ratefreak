using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RateFreak.Application.Identity;
using RateFreak.Domain.Catalog;
using RateFreak.Infrastructure.Moderation;
namespace RateFreak.Infrastructure.Authentication;

internal sealed partial class AccountDataService
{
    public async Task DeleteAsync(Guid userId, string stamp, string? grant, string handle, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct); await ModerationLock.AcquireAsync(db, ct);
        var user = await LockAsync(userId, stamp, ct); if (user == null) throw new AccountDataException(401, "Your session has expired.");
        if (user.UserName != handle) throw new AccountDataException(400, "Type your exact current handle to delete this account.");
        await CheckAdminAsync(userId, ct); await ConsumeAsync(userId, stamp, grant, "delete", ct);
        await journal.AppendAsync(userId, false, ct);
        var keys = await EraseAsync(userId, ct); await tx.CommitAsync(ct);
        // Commit is final even if completion marking or artwork cleanup fails; pending intents replay.
        try { await journal.AppendAsync(userId, true, CancellationToken.None); } catch (Exception) { logger.LogError("Erasure journal completion requires retry."); }
        await CleanupAsync(keys);
    }
    private async Task CheckAdminAsync(Guid id, CancellationToken ct)
    {
        var admins = from membership in db.UserRoles join role in db.Roles on membership.RoleId equals role.Id where role.NormalizedName == "ADMIN" select membership.UserId;
        if (await admins.ContainsAsync(id, ct) && await admins.CountAsync(ct) <= 1) throw new AccountDataException(409, "Appoint another administrator with the offline administrator tooling before deleting this account.");
    }
    internal async Task<List<string>> EraseAsync(Guid id, CancellationToken ct)
    {
        var privateIds = db.MediaItems.Where(m => m.CreatorUserId == id && m.Visibility == "private").Select(m => m.Id);
        var keys = await db.Set<ArtworkAsset>().Where(a => privateIds.Contains(a.ItemId)).Select(a => a.StorageKey).ToListAsync(ct);
        // Remove dependent references, including those created by others while the item was public.
        await db.CollectionItems.Where(x => privateIds.Contains(x.MediaItemId)).ExecuteDeleteAsync(ct);
        await db.LibraryEntries.Where(x => privateIds.Contains(x.MediaItemId)).ExecuteDeleteAsync(ct);
        await db.Ratings.Where(x => privateIds.Contains(x.MediaItemId)).ExecuteDeleteAsync(ct);
        await db.Reviews.Where(x => privateIds.Contains(x.MediaItemId)).ExecuteDeleteAsync(ct);
        await db.MediaExternalIds.Where(x => privateIds.Contains(x.MediaItemId)).ExecuteDeleteAsync(ct);
        await db.Set<ArtworkAsset>().Where(x => privateIds.Contains(x.ItemId)).ExecuteDeleteAsync(ct);
        await db.MediaItems.Where(m => privateIds.Contains(m.Id)).ExecuteDeleteAsync(ct);
        var privateCategories = db.Categories.Where(c => c.OwnerUserId == id && c.Scope == "private").Select(c => c.Id);
        await db.CategoryAliases.Where(a => privateCategories.Contains(a.SourceCategoryId) || privateCategories.Contains(a.TargetCategoryId)).ExecuteDeleteAsync(ct);
        await db.Categories.Where(c => privateCategories.Contains(c.Id)).ExecuteDeleteAsync(ct);
        var now = DateTimeOffset.UtcNow;
        await db.MediaItems.Where(m => m.CreatorUserId == id).ExecuteUpdateAsync(s => s.SetProperty(m => m.CreatorUserId, (Guid?)null).SetProperty(m => m.Revision, m => m.Revision + 1).SetProperty(m => m.UpdatedAt, now), ct);
        await db.Categories.Where(c => c.OwnerUserId == id).ExecuteUpdateAsync(s => s.SetProperty(c => c.OwnerUserId, (Guid?)null).SetProperty(c => c.Revision, c => c.Revision + 1).SetProperty(c => c.UpdatedAt, now), ct);
        var pseudonym = Guid.NewGuid();
        await db.ContentReports.Where(r => r.OriginalReporterId == id).ExecuteUpdateAsync(s => s.SetProperty(r => r.OriginalReporterId, pseudonym).SetProperty(r => r.ReporterId, (Guid?)null).SetProperty(r => r.Details, "Reporter account deleted."), ct);
        await db.ContentReports.Where(r => r.OriginalAuthorId == id).ExecuteUpdateAsync(s => s.SetProperty(r => r.OriginalAuthorId, (Guid?)pseudonym), ct);
        await db.ContentReports.Where(r => r.ResolvedBy == id).ExecuteUpdateAsync(s => s.SetProperty(r => r.ResolvedBy, (Guid?)pseudonym), ct);
        await db.ModerationEvents.Where(e => e.ActorId == id).ExecuteUpdateAsync(s => s.SetProperty(e => e.ActorId, (Guid?)pseudonym), ct);
        await db.ModerationEvents.Where(e => e.TargetType == "account" && e.TargetId == id).ExecuteUpdateAsync(s => s.SetProperty(e => e.TargetId, pseudonym), ct);
        await db.Users.Where(u => u.Id == id).ExecuteDeleteAsync(ct);
        return keys;
    }
    internal async Task CleanupAsync(List<string> keys)
    {
        foreach (var key in keys)
        {
            try { if (!await db.Set<ArtworkAsset>().AnyAsync(a => a.StorageKey == key)) await artwork.DeleteAsync(key, CancellationToken.None); }
            catch (Exception) { logger.LogWarning("Private artwork cleanup will be retried by the orphan sweeper."); }
        }
    }
}
