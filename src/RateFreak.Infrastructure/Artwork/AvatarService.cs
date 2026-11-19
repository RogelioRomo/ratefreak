using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RateFreak.Application.Catalog;
using RateFreak.Application.Identity;
using RateFreak.Application.Moderation;
using RateFreak.Infrastructure.Authentication;
using RateFreak.Infrastructure.Moderation;
using RateFreak.Infrastructure.Persistence;

namespace RateFreak.Infrastructure.Artwork;

internal sealed class AvatarService(RateFreakDbContext db, FileAvatarStorage storage, IArtworkDecoder decoder,
    IModerationService moderation, IOptions<ArtworkOptions> options, ILogger<AvatarService> logger) : IAvatarService
{
    public async Task<AvatarResult> PutAsync(Guid actorId, long expectedRevision, Stream input, CancellationToken ct)
    {
        if (!options.Value.Enabled) return new(AvatarStatus.Unavailable);
        if (expectedRevision < 0) return new(AvatarStatus.Invalid);
        if (!await db.Users.AnyAsync(u => u.Id == actorId, ct)) return new(AvatarStatus.NotFound);
        DecodedArtwork decoded;
        try { decoded = await decoder.DecodeAvatarAsync(input, ct); }
        catch (ArgumentException) { return new(AvatarStatus.Invalid); }
        try
        {
            using var staged = await storage.StageAsync(new MemoryStream(decoded.Bytes), ct);
            var result = await ChangeAsync(actorId, expectedRevision, staged, decoded, ct);
            // A failed/conflicting write owns no live reference to its stage.
            await SafeDeleteAsync(staged.Key);
            return result;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { logger.LogWarning("Avatar storage unavailable"); return new(AvatarStatus.Unavailable); }
    }
    public async Task<AvatarResult> DeleteAsync(Guid actorId, long expectedRevision, CancellationToken ct)
    {
        if (expectedRevision < 0) return new(AvatarStatus.Invalid);
        return await ChangeAsync(actorId, expectedRevision, null, null, ct);
    }
    private async Task<AvatarResult> ChangeAsync(Guid actorId, long expectedRevision, StagedArtwork? staged, DecodedArtwork? decoded, CancellationToken ct)
    {
        string? oldKey;
        AvatarState state;
        await using (var tx = await db.Database.BeginTransactionAsync(ct))
        {
            await ModerationLock.AcquireAsync(db, ct);
            var user = await db.Users.FromSqlInterpolated($"SELECT * FROM users WHERE id = {actorId} FOR UPDATE").AsNoTracking().SingleOrDefaultAsync(ct);
            if (user is null) return new(AvatarStatus.NotFound);
            if (user.AvatarRevision != expectedRevision) return new(AvatarStatus.Conflict);
            var asset = await db.Set<AvatarAsset>().SingleOrDefaultAsync(a => a.UserId == actorId, ct);
            oldKey = asset?.StorageKey;
            Guid? assetId = null;
            if (staged is not null && decoded is not null)
            {
                asset ??= new AvatarAsset { UserId = actorId };
                if (db.Entry(asset).State == EntityState.Detached) db.Add(asset);
                asset.AssetId = Guid.NewGuid();
                asset.StorageKey = await storage.PromoteAsync(staged, asset.AssetId, ct);
                asset.ContentType = decoded.ContentType; asset.Width = decoded.Width; asset.Height = decoded.Height; asset.ByteLength = decoded.Bytes.LongLength;
                assetId = asset.AssetId;
            }
            else if (asset is not null) db.Remove(asset);
            var now = DateTimeOffset.UtcNow;
            if (now <= user.ProfileUpdatedAt.AddTicks(10)) now = user.ProfileUpdatedAt.AddTicks(10);
            await db.Users.Where(u => u.Id == actorId).ExecuteUpdateAsync(s => s
                .SetProperty(u => u.AvatarId, assetId).SetProperty(u => u.AvatarRevision, u => u.AvatarRevision + 1)
                .SetProperty(u => u.ProfileUpdatedAt, now).SetProperty(u => u.UpdatedAt, now), ct);
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
            state = new(assetId is null ? null : $"/api/v1/users/me/avatar/{assetId}", user.AvatarRevision + 1);
        }
        if (oldKey is not null) await SafeDeleteAsync(oldKey);
        return new(AvatarStatus.Success, state);
    }
    private async Task SafeDeleteAsync(string key)
    {
        try { await storage.DeleteAsync(key, CancellationToken.None); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { logger.LogWarning("Avatar file awaits orphan cleanup"); }
    }
    public Task<ArtworkRead?> ReadAsync(Guid userId, Guid assetId, CancellationToken ct) => ReadCoreAsync(userId, assetId, false, ct);
    public Task<ArtworkRead?> ReadOwnAsync(Guid actorId, Guid assetId, CancellationToken ct) => ReadCoreAsync(actorId, assetId, true, ct);
    public async Task<ArtworkRead?> ReadForProfileAsync(Guid actorId, Guid userId, Guid assetId, CancellationToken ct)
    {
        if (!await moderation.HasAccessAsync(actorId, false, ct)) return null;
        // Direct inspection of a hidden avatar is tied to retained moderation scope,
        // including the audited direct-hide flow which need not have a user report.
        if (!await db.ModerationEvents.AnyAsync(e => e.TargetType == "profile" && e.TargetId == userId && e.Action == "hide", ct)) return null;
        return await ReadCoreAsync(userId, assetId, true, ct);
    }
    public async Task<ArtworkRead?> ReadForReportAsync(Guid actorId, Guid reportId, Guid assetId, CancellationToken ct)
    {
        if (!await moderation.HasAccessAsync(actorId, false, ct)) return null;
        var userId = await db.ContentReports.Where(r => r.Id == reportId && r.TargetType == "profile").Select(r => r.ProfileId).SingleOrDefaultAsync(ct);
        return userId is {} id ? await ReadCoreAsync(id, assetId, true, ct) : null;
    }
    private async Task<ArtworkRead?> ReadCoreAsync(Guid userId, Guid assetId, bool privileged, CancellationToken ct)
    {
        if (!options.Value.Enabled) return null;
        var asset = await db.Set<AvatarAsset>().AsNoTracking().Where(a => a.UserId == userId && a.AssetId == assetId &&
            db.Users.Any(u => u.Id == userId && u.AvatarId == assetId && (privileged || !u.IsProfileHidden))).SingleOrDefaultAsync(ct);
        if (asset is null) return null;
        try { var stream = await storage.OpenReadAsync(asset.StorageKey, ct); return stream is null ? null : new(stream, asset.ContentType); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { logger.LogWarning("Avatar image unavailable"); return null; }
    }
}
