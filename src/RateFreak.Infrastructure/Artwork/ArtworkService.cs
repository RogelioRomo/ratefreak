using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RateFreak.Application.Catalog;
using RateFreak.Domain.Catalog;
using RateFreak.Infrastructure.Catalog;
using RateFreak.Infrastructure.Moderation;
using RateFreak.Infrastructure.Persistence;

namespace RateFreak.Infrastructure.Artwork;

internal sealed class ArtworkService(RateFreakDbContext db, IArtworkStorage storage, IArtworkDecoder decoder, ICatalogItemCommands items, RateFreak.Application.Moderation.IModerationService moderation, IOptions<ArtworkOptions> options, ILogger<ArtworkService> logger) : IArtworkService
{
    public async Task<CatalogResult<MediaDetails>> PutAsync(Guid actorId, Guid itemId, long expectedRevision, Stream input, CancellationToken ct)
    {
        if (!options.Value.Enabled) return new(CatalogStatus.Unavailable);
        if (expectedRevision <= 0) return new(CatalogStatus.Invalid);
        if (!await db.MediaItems.AnyAsync(m => m.Id == itemId && m.CreatorUserId == actorId, ct)) return new(CatalogStatus.NotFound);
        DecodedArtwork decoded;
        try { decoded = await decoder.DecodeAsync(input, ct); } catch (ArgumentException) { return new(CatalogStatus.Invalid); }
        string? oldKey = null;
        try
        {
            using var staged = await storage.StageAsync(new MemoryStream(decoded.Bytes), ct);
            await using (var tx = await db.Database.BeginTransactionAsync(ct))
            {
                await ModerationLock.AcquireAsync(db, ct);
                var item = await db.MediaItems.AsNoTracking().SingleOrDefaultAsync(m => m.Id == itemId && m.CreatorUserId == actorId, ct);
                if (item is null) return new(CatalogStatus.NotFound); if (item.Revision != expectedRevision) { await storage.DeleteAsync(staged.Key, ct); return new(CatalogStatus.Conflict); }
                var asset = await db.Set<ArtworkAsset>().SingleOrDefaultAsync(a => a.ItemId == itemId, ct); oldKey = asset?.StorageKey;
                asset ??= new ArtworkAsset { ItemId = itemId }; if (db.Entry(asset).State == EntityState.Detached) db.Add(asset);
                asset.AssetId = Guid.NewGuid(); asset.StorageKey = await storage.PromoteAsync(staged, asset.AssetId, ct); asset.ContentType = decoded.ContentType; asset.Width = decoded.Width; asset.Height = decoded.Height; asset.ByteLength = decoded.Bytes.Length;
                var url = $"/api/v1/media/{itemId}/artwork/{asset.AssetId}";
                await db.MediaItems.Where(m => m.Id == itemId).ExecuteUpdateAsync(s => s.SetProperty(m => m.ImageReference, url).SetProperty(m => m.Revision, m => m.Revision + 1).SetProperty(m => m.UpdatedAt, DateTimeOffset.UtcNow), ct);
                await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
            }
            if (oldKey is not null) await SafeDelete(oldKey);
            return new(CatalogStatus.Success, await items.ManageAsync(actorId, itemId, ct));
        }
        catch (IOException) { logger.LogWarning("Artwork storage operation failed for item {ItemId}", itemId); return new(CatalogStatus.Unavailable); }
        catch (UnauthorizedAccessException) { logger.LogWarning("Artwork storage access failed for item {ItemId}", itemId); return new(CatalogStatus.Unavailable); }
    }
    private async Task SafeDelete(string key)
    {
        try { await storage.DeleteAsync(key, CancellationToken.None); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { logger.LogWarning("Unreferenced artwork file awaits cleanup"); }
    }
    public async Task<CatalogResult<MediaDetails>> DeleteAsync(Guid actorId, Guid itemId, long expectedRevision, CancellationToken ct)
    {
        if (!options.Value.Enabled) return new(CatalogStatus.Unavailable);
        if (expectedRevision <= 0) return new(CatalogStatus.Invalid);
        string? key;
        await using (var tx = await db.Database.BeginTransactionAsync(ct))
        {
            await ModerationLock.AcquireAsync(db, ct);
            var item = await db.MediaItems.AsNoTracking().SingleOrDefaultAsync(m => m.Id == itemId && m.CreatorUserId == actorId, ct);
            if (item is null) return new(CatalogStatus.NotFound); if (item.Revision != expectedRevision) return new(CatalogStatus.Conflict);
            var asset = await db.Set<ArtworkAsset>().SingleOrDefaultAsync(a => a.ItemId == itemId, ct); key = asset?.StorageKey;
            if (asset is not null) db.Remove(asset);
            await db.MediaItems.Where(m => m.Id == itemId).ExecuteUpdateAsync(s => s.SetProperty(m => m.ImageReference, (string?)null).SetProperty(m => m.Revision, m => m.Revision + 1).SetProperty(m => m.UpdatedAt, DateTimeOffset.UtcNow), ct);
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        }
        if (key is not null) await SafeDelete(key);
        return new(CatalogStatus.Success, await items.ManageAsync(actorId, itemId, ct));
    }
    public Task<ArtworkRead?> ReadPublicForStaffAsync(Guid actorId, Guid itemId, Guid assetId, CancellationToken ct) => ReadStaffAsync(actorId, itemId, null, assetId, ct);
    public Task<ArtworkRead?> ReadForReportAsync(Guid actorId, Guid reportId, Guid assetId, CancellationToken ct) => ReadStaffAsync(actorId, null, reportId, assetId, ct);
    private async Task<ArtworkRead?> ReadStaffAsync(Guid actorId, Guid? itemId, Guid? reportId, Guid assetId, CancellationToken ct)
    {
        if (!options.Value.Enabled) return null;
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        if (!await moderation.HasAccessAsync(actorId, false, ct)) return null;
        var asset = await db.Set<ArtworkAsset>().AsNoTracking().SingleOrDefaultAsync(a => a.AssetId == assetId && (reportId != null
            ? db.ContentReports.Any(r => r.Id == reportId && r.TargetType == "item" && r.ItemId == a.ItemId)
            : a.ItemId == itemId && db.MediaItems.Any(m => m.Id == a.ItemId && m.Visibility == "public" && m.Provenance == "user")), ct);
        if (asset is null) return null;
        try
        {
            var stream = await storage.OpenReadAsync(asset.StorageKey, ct); await tx.CommitAsync(ct);
            return stream is null ? null : new(stream, asset.ContentType);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { logger.LogWarning("Reported artwork unavailable for asset {AssetId}", assetId); return null; }
    }
    public async Task<ArtworkRead?> ReadAsync(Guid? viewerId, Guid itemId, Guid assetId, CancellationToken ct)
    {
        if (!options.Value.Enabled) return null;
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        var asset = await db.Set<ArtworkAsset>().AsNoTracking().SingleOrDefaultAsync(a => a.ItemId == itemId && a.AssetId == assetId && db.MediaItems.Where(CatalogAccess.Readable(viewerId)).Any(m => m.Id == a.ItemId), ct);
        if (asset is null) return null;
        try
        {
            var stream = await storage.OpenReadAsync(asset.StorageKey, ct); await tx.CommitAsync(ct);
            return stream is null ? null : new(stream, asset.ContentType);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { logger.LogWarning("Artwork file unavailable for item {ItemId}", itemId); return null; }
    }
}
