using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RateFreak.Application.Catalog;
using RateFreak.Domain.Catalog;
using RateFreak.Domain.Moderation;
using RateFreak.Infrastructure.Moderation;
using RateFreak.Infrastructure.Persistence;

namespace RateFreak.Infrastructure.Catalog;

internal sealed class CatalogItemCommands(RateFreakDbContext db) : ICatalogItemCommands
{
    private static CatalogResult<MediaDetails> Result(CatalogStatus status) => new(status);
    private async Task<Category?> CategoryAsync(Guid actor, Guid? id, bool publishing, CancellationToken ct)
    {
        if (id is null || await db.CategoryAliases.AnyAsync(a => a.SourceCategoryId == id, ct)) return null;
        return await db.Categories.SingleOrDefaultAsync(c => c.Id == id && !c.IsHidden && (c.Scope == "public" || !publishing && c.Scope == "private" && c.OwnerUserId == actor), ct);
    }
    private void Audit(Guid actor, Guid id, string action) => db.Add(new ModerationEvent { ActorId = actor, TargetType = "item", TargetId = id, Action = action, Reason = "Creator catalog " + action });
    private static bool Valid(string? title, string? description)
    {
        if (title is null || description?.Any(c => char.IsControl(c) && c is not ('\n' or '\r' or '\t')) == true) return false;
        try { _ = new MediaItem(MediaTypes.Movie, title, description); return true; } catch (ArgumentException) { return false; }
    }
    public async Task<MediaDetails?> ManageAsync(Guid actorId, Guid id, CancellationToken ct)
    {
        var item = await db.MediaItems.AsNoTracking().Include(m => m.Category).Where(CatalogAccess.Manageable(actorId)).SingleOrDefaultAsync(m => m.Id == id, ct);
        if (item is null) return null;
        using var metadata = JsonDocument.Parse(item.Metadata);
        return new(item.Id, item.MediaType, item.Title, item.Description, item.ReleaseDate, item.ImageReference, metadata.RootElement.Clone(), item.CreatedAt, item.UpdatedAt, [])
        { Category = new(item.Category.Id, item.Category.Name, item.Category.Scope, item.Category.Revision, item.Category.IsHidden, item.Category.IsSeeded, item.Category.OwnerUserId), CreatorUserId = item.CreatorUserId, Provenance = item.Provenance, Visibility = item.Visibility, IsArchived = item.IsArchived, IsHidden = item.IsHidden, Revision = item.Revision };
    }
    public async Task<CatalogResult<MediaDetails>> CreateAsync(Guid actorId, CustomItemCreate request, CancellationToken ct)
    {
        var visibility = request.Visibility ?? "private";
        if (!Valid(request.Title, request.Description) || visibility is not ("private" or "public")) return Result(CatalogStatus.Invalid);
        await using var tx = await db.Database.BeginTransactionAsync(ct); await ModerationLock.AcquireAsync(db, ct);
        if (!await db.Users.AnyAsync(u => u.Id == actorId, ct)) return Result(CatalogStatus.Forbidden);
        var category = await CategoryAsync(actorId, request.CategoryId, visibility == "public", ct); if (category is null) return Result(CatalogStatus.Invalid);
        var item = MediaItem.CreateCustom(actorId, request.Title!, request.Description, category.Id); db.Add(item); await db.SaveChangesAsync(ct);
        if (visibility == "public") await db.MediaItems.Where(m => m.Id == item.Id).ExecuteUpdateAsync(s => s.SetProperty(m => m.Visibility, "public"), ct);
        Audit(actorId, item.Id, "create"); await db.SaveChangesAsync(ct); var result = await ManageAsync(actorId, item.Id, ct); await tx.CommitAsync(ct); return new(CatalogStatus.Success, result);
    }
    public async Task<CatalogResult<MediaDetails>> UpdateAsync(Guid actorId, Guid id, CustomItemUpdate request, CancellationToken ct)
    {
        if (request.ExpectedRevision is null or <= 0 || !Valid(request.Title, request.Description)) return Result(CatalogStatus.Invalid);
        await using var tx = await db.Database.BeginTransactionAsync(ct); await ModerationLock.AcquireAsync(db, ct);
        if (!await db.Users.AnyAsync(u => u.Id == actorId, ct)) return Result(CatalogStatus.Forbidden);
        var item = await db.MediaItems.AsNoTracking().SingleOrDefaultAsync(m => m.Id == id && m.CreatorUserId == actorId && m.Provenance == "user", ct);
        if (item is null) return Result(CatalogStatus.NotFound); if (item.Revision != request.ExpectedRevision) return Result(CatalogStatus.Conflict);
        var category = await CategoryAsync(actorId, request.CategoryId, item.Visibility == "public", ct); if (category is null) return Result(CatalogStatus.Invalid);
        await db.MediaItems.Where(m => m.Id == id).ExecuteUpdateAsync(s => s.SetProperty(m => m.Title, request.Title!.Trim()).SetProperty(m => m.Description, request.Description).SetProperty(m => m.CategoryId, category.Id).SetProperty(m => m.Revision, m => m.Revision + 1).SetProperty(m => m.UpdatedAt, DateTimeOffset.UtcNow), ct);
        Audit(actorId, id, "edit"); await db.SaveChangesAsync(ct); var result = await ManageAsync(actorId, id, ct); await tx.CommitAsync(ct); return new(CatalogStatus.Success, result);
    }
    public async Task<CatalogResult<MediaDetails>> ChangeStateAsync(Guid actorId, Guid id, string action, ItemStateChange request, CancellationToken ct)
    {
        if (request.ExpectedRevision is null or <= 0 || action is not ("publish" or "unpublish" or "archive" or "restore")) return Result(CatalogStatus.Invalid);
        await using var tx = await db.Database.BeginTransactionAsync(ct); await ModerationLock.AcquireAsync(db, ct);
        if (!await db.Users.AnyAsync(u => u.Id == actorId, ct)) return Result(CatalogStatus.Forbidden);
        var item = await db.MediaItems.AsNoTracking().SingleOrDefaultAsync(m => m.Id == id && m.CreatorUserId == actorId && m.Provenance == "user", ct);
        if (item is null) return Result(CatalogStatus.NotFound); if (item.Revision != request.ExpectedRevision) return Result(CatalogStatus.Conflict);
        if (action == "publish" && (item.IsArchived || item.IsHidden || await CategoryAsync(actorId, item.CategoryId, true, ct) is null)) return Result(CatalogStatus.Invalid);
        var archived = action == "archive" || action != "restore" && item.IsArchived;
        var visibility = action == "publish" ? "public" : action is "unpublish" or "restore" ? "private" : item.Visibility;
        await db.MediaItems.Where(m => m.Id == id).ExecuteUpdateAsync(s => s.SetProperty(m => m.IsArchived, archived).SetProperty(m => m.Visibility, visibility).SetProperty(m => m.Revision, m => m.Revision + 1).SetProperty(m => m.UpdatedAt, DateTimeOffset.UtcNow), ct);
        Audit(actorId, id, action); await db.SaveChangesAsync(ct); var result = await ManageAsync(actorId, id, ct); await tx.CommitAsync(ct); return new(CatalogStatus.Success, result);
    }
}
