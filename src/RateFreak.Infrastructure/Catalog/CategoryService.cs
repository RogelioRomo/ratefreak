using System.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using RateFreak.Application.Catalog;
using RateFreak.Application.Moderation;
using RateFreak.Domain.Catalog;
using RateFreak.Domain.Moderation;
using RateFreak.Infrastructure.Moderation;
using RateFreak.Infrastructure.Persistence;

namespace RateFreak.Infrastructure.Catalog;

internal sealed class CategoryService(RateFreakDbContext db, IModerationService moderation) : ICategoryService
{
    private static CategorySummary Summary(Category c) => new(c.Id, c.Name, c.Scope, c.Revision, c.IsHidden, c.IsSeeded, c.OwnerUserId);
    private static CatalogResult<CategorySummary> Result(CatalogStatus status) => new(status);
    private static string? Name(string? input)
    {
        if (input is null) return null;
        try { return Category.DisplayName(input); } catch (ArgumentException) { return null; }
    }
    public async Task<CatalogResult<CategoryPage>> ListForAdminAsync(Guid actorId, string? search, int page, int pageSize, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        if (!await moderation.HasAccessAsync(actorId, true, ct)) return new(CatalogStatus.Forbidden);
        var query = db.Categories.AsNoTracking().Where(c => c.Scope == "public" && !db.CategoryAliases.Any(a => a.SourceCategoryId == c.Id));
        if (!string.IsNullOrWhiteSpace(search))
        {
            var name = Name(search); if (name is null) return new(CatalogStatus.Invalid);
            var normalized = name.ToUpperInvariant(); query = query.Where(c => c.NormalizedName.Contains(normalized) || c.Scope == "public" && db.CategoryAliases.Any(a => a.TargetCategoryId == c.Id && db.Categories.Any(source => source.Id == a.SourceCategoryId && source.Scope == "public" && source.NormalizedName.Contains(normalized))));
        }
        var count = await query.CountAsync(ct); var items = await query.OrderBy(c => c.Name).ThenBy(c => c.Id).Skip((page - 1) * pageSize).Take(pageSize).Select(c => new CategorySummary(c.Id, c.Name, c.Scope, c.Revision, c.IsHidden, c.IsSeeded, c.OwnerUserId)).ToListAsync(ct);
        await tx.CommitAsync(ct); return new(CatalogStatus.Success, new(items, page, pageSize, count));
    }
    public async Task<CategoryPage> ListAsync(Guid? viewerId, string? search, bool ownOnly, int page, int pageSize, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        var query = db.Categories.AsNoTracking().Where(c => !c.IsHidden && !db.CategoryAliases.Any(a => a.SourceCategoryId == c.Id) && (c.Scope == "public" || c.OwnerUserId == viewerId && viewerId != null));
        if (ownOnly) query = query.Where(c => c.OwnerUserId == viewerId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var name = Name(search);
            if (name is null) return new([], page, pageSize, 0);
            var normalized = name.ToUpperInvariant(); query = query.Where(c => c.NormalizedName.Contains(normalized) || c.Scope == "public" && db.CategoryAliases.Any(a => a.TargetCategoryId == c.Id && db.Categories.Any(source => source.Id == a.SourceCategoryId && source.Scope == "public" && source.NormalizedName.Contains(normalized))));
        }
        var count = await query.CountAsync(ct);
        var items = await query.OrderBy(c => c.Name).ThenBy(c => c.Id).Skip((page - 1) * pageSize).Take(pageSize).Select(c => new CategorySummary(c.Id, c.Name, c.Scope, c.Revision, c.IsHidden, c.IsSeeded, c.OwnerUserId)).ToListAsync(ct);
        await tx.CommitAsync(ct); return new(items, page, pageSize, count);
    }
    public async Task<CatalogResult<CategorySummary>> CreateAsync(Guid actorId, CategoryCreate request, CancellationToken ct)
    {
        var name = Name(request.Name); var scope = request.Scope ?? "private";
        if (name is null || scope is not ("private" or "public")) return Result(CatalogStatus.Invalid);
        await using var tx = await db.Database.BeginTransactionAsync(ct); await ModerationLock.AcquireAsync(db, ct);
        if (!await db.Users.AnyAsync(u => u.Id == actorId, ct)) return Result(CatalogStatus.Forbidden);
        var normalized = name.ToUpperInvariant();
        if (await db.Categories.AnyAsync(c => c.NormalizedName == normalized && c.Scope == scope && (scope == "public" || c.OwnerUserId == actorId), ct)) return Result(CatalogStatus.Conflict);
        var category = new Category(name, scope, actorId); db.Add(category);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "ux_category_public_name" or "ux_category_private_name" }) { return Result(CatalogStatus.Conflict); }
        await tx.CommitAsync(ct); return new(CatalogStatus.Success, Summary(category));
    }
    private void Audit(Guid actor, string action, Guid id, string reason) => db.Add(new ModerationEvent { ActorId = actor, Action = action, TargetType = "category", TargetId = id, Reason = reason.Trim() });
    public async Task<CatalogResult<CategorySummary>> RenameAsync(Guid actorId, Guid id, CategoryRename request, bool admin, CancellationToken ct)
    {
        var name = Name(request.Name);
        if (name is null || request.ExpectedRevision is null or <= 0 || admin && !ModerationRules.Text(request.Reason)) return Result(CatalogStatus.Invalid);
        await using var tx = await db.Database.BeginTransactionAsync(ct); await ModerationLock.AcquireAsync(db, ct);
        if (!await db.Users.AnyAsync(u => u.Id == actorId, ct)) return Result(CatalogStatus.Forbidden);
        if (admin && !await moderation.HasAccessAsync(actorId, true, ct)) return Result(CatalogStatus.Forbidden);
        var category = await db.Categories.SingleOrDefaultAsync(c => c.Id == id && (admin ? c.Scope == "public" : c.Scope == "private" && c.OwnerUserId == actorId), ct);
        if (category is null) return Result(CatalogStatus.NotFound);
        if (admin && category.OwnerUserId == actorId) return Result(CatalogStatus.Forbidden);
        if (category.Revision != request.ExpectedRevision) return Result(CatalogStatus.Conflict);
        if (await db.CategoryAliases.AnyAsync(a => a.SourceCategoryId == id, ct)) return Result(CatalogStatus.Invalid);
        var normalized = name.ToUpperInvariant();
        if (await db.Categories.AnyAsync(c => c.Id != id && c.Scope == category.Scope && c.NormalizedName == normalized && (c.Scope == "public" || c.OwnerUserId == actorId), ct)) return Result(CatalogStatus.Conflict);
        await db.Categories.Where(c => c.Id == id).ExecuteUpdateAsync(s => s.SetProperty(c => c.Name, name).SetProperty(c => c.NormalizedName, normalized).SetProperty(c => c.Revision, c => c.Revision + 1).SetProperty(c => c.UpdatedAt, DateTimeOffset.UtcNow), ct);
        if (admin) Audit(actorId, "rename", id, request.Reason!);
        await db.SaveChangesAsync(ct); await db.Entry(category).ReloadAsync(ct); await tx.CommitAsync(ct); return new(CatalogStatus.Success, Summary(category));
    }
    public async Task<CatalogResult<CategorySummary>> SetHiddenAsync(Guid actorId, Guid id, CategoryStateChange request, bool hidden, CancellationToken ct)
    {
        if (request.ExpectedRevision is null or <= 0 || !ModerationRules.Text(request.Reason)) return Result(CatalogStatus.Invalid);
        await using var tx = await db.Database.BeginTransactionAsync(ct); await ModerationLock.AcquireAsync(db, ct);
        if (!await db.Users.AnyAsync(u => u.Id == actorId, ct)) return Result(CatalogStatus.Forbidden);
        if (!await moderation.HasAccessAsync(actorId, true, ct)) return Result(CatalogStatus.Forbidden);
        var category = await db.Categories.SingleOrDefaultAsync(c => c.Id == id && c.Scope == "public", ct);
        if (category is null) return Result(CatalogStatus.NotFound);
        if (category.IsSeeded) return Result(CatalogStatus.Invalid);
        if (category.OwnerUserId == actorId) return Result(CatalogStatus.Forbidden);
        if (category.Revision != request.ExpectedRevision) return Result(CatalogStatus.Conflict);
        await db.Categories.Where(c => c.Id == id).ExecuteUpdateAsync(s => s.SetProperty(c => c.IsHidden, hidden).SetProperty(c => c.Revision, c => c.Revision + 1).SetProperty(c => c.UpdatedAt, DateTimeOffset.UtcNow), ct);
        Audit(actorId, hidden ? "hide" : "restore", id, request.Reason!); await db.SaveChangesAsync(ct); await db.Entry(category).ReloadAsync(ct); await tx.CommitAsync(ct); return new(CatalogStatus.Success, Summary(category));
    }
    public async Task<CatalogResult<CategorySummary>> MergeAsync(Guid actorId, Guid id, CategoryMerge request, CancellationToken ct)
    {
        if (request.ExpectedRevision is null or <= 0 || request.TargetExpectedRevision is null or <= 0 || request.TargetId is null || request.TargetId == id || !ModerationRules.Text(request.Reason)) return Result(CatalogStatus.Invalid);
        await using var tx = await db.Database.BeginTransactionAsync(ct); await ModerationLock.AcquireAsync(db, ct);
        if (!await db.Users.AnyAsync(u => u.Id == actorId, ct)) return Result(CatalogStatus.Forbidden);
        if (!await moderation.HasAccessAsync(actorId, true, ct)) return Result(CatalogStatus.Forbidden);
        var source = await db.Categories.SingleOrDefaultAsync(c => c.Id == id && c.Scope == "public", ct);
        var target = await db.Categories.SingleOrDefaultAsync(c => c.Id == request.TargetId && c.Scope == "public", ct);
        if (source is null || target is null) return Result(CatalogStatus.NotFound);
        if (source.OwnerUserId == actorId || target.OwnerUserId == actorId) return Result(CatalogStatus.Forbidden);
        if (source.Revision != request.ExpectedRevision || target.Revision != request.TargetExpectedRevision) return Result(CatalogStatus.Conflict);
        if (source.IsSeeded || target.IsSeeded || target.IsHidden || await db.CategoryAliases.AnyAsync(a => a.SourceCategoryId == id || a.SourceCategoryId == target.Id, ct)) return Result(CatalogStatus.Invalid);
        await db.MediaItems.Where(m => m.CategoryId == id).ExecuteUpdateAsync(s => s.SetProperty(m => m.CategoryId, target.Id).SetProperty(m => m.Revision, m => m.Revision + 1).SetProperty(m => m.UpdatedAt, DateTimeOffset.UtcNow), ct);
        await db.CategoryAliases.Where(a => a.TargetCategoryId == id).ExecuteUpdateAsync(s => s.SetProperty(a => a.TargetCategoryId, target.Id), ct);
        db.Add(new CategoryAlias(id, target.Id));
        await db.Categories.Where(c => c.Id == id || c.Id == target.Id).ExecuteUpdateAsync(s => s.SetProperty(c => c.Revision, c => c.Revision + 1).SetProperty(c => c.UpdatedAt, DateTimeOffset.UtcNow), ct);
        Audit(actorId, "merge", id, request.Reason!); await db.SaveChangesAsync(ct); await db.Entry(target).ReloadAsync(ct); await tx.CommitAsync(ct); return new(CatalogStatus.Success, Summary(target));
    }
}
