using RateFreak.Infrastructure.Catalog;
using System.Data;
using Microsoft.EntityFrameworkCore;
using RateFreak.Application.Moderation;
using RateFreak.Domain.Moderation;
using RateFreak.Infrastructure.Persistence;

namespace RateFreak.Infrastructure.Moderation;

internal sealed partial class ModerationService(RateFreakDbContext db) : IModerationService
{
    public Task<bool> HasAccessAsync(Guid actor, bool adminOnly, CancellationToken ct) =>
        (from membership in db.UserRoles join role in db.Roles on membership.RoleId equals role.Id
         where membership.UserId == actor && (role.NormalizedName == "ADMIN" || (!adminOnly && role.NormalizedName == "MODERATOR")) select membership).AnyAsync(ct);

    private Task<StaffIdentity?> Identity(Guid? id, CancellationToken ct) => db.Users.AsNoTracking().Where(x => x.Id == id)
        .Select(x => new StaffIdentity(x.Id, x.UserName!, x.DisplayName)).SingleOrDefaultAsync(ct);

    public async Task<ModerationTarget?> TargetAsync(string type, Guid id, CancellationToken ct)
    {
        var target = await TargetCoreAsync(type, id, false, ct);
        if (target is not null && type == "item")
        {
            var asset = await db.Set<RateFreak.Domain.Catalog.ArtworkAsset>().Where(a => a.ItemId == id).Select(a => (Guid?)a.AssetId).SingleOrDefaultAsync(ct);
            if (asset is not null) target = target with { ImageReference = $"/api/v1/moderation/media/{id}/artwork/{asset}" };
        }
        return target;
    }
    private async Task<ModerationTarget?> TargetCoreAsync(string type, Guid id, bool retainedReport, CancellationToken ct)
    {
        var publicItems = db.MediaItems.Where(m => m.Visibility == "public");
        if (type == "review")
            return await (from r in db.Reviews.AsNoTracking() join u in db.Users.AsNoTracking() on r.UserId equals u.Id
                where r.Id == id && (retainedReport || publicItems.Any(m => m.Id == r.MediaItemId))
                select new ModerationTarget(r.Id,"review",new StaffIdentity(u.Id,u.UserName!,u.DisplayName),r.Body,r.IsSpoiler,r.IsHidden,r.UpdatedAt)).SingleOrDefaultAsync(ct);
        if (type == "comment")
            return await (from c in db.ReviewComments.AsNoTracking() join u in db.Users.AsNoTracking() on c.UserId equals u.Id
                where c.Id == id && (retainedReport || db.Reviews.Any(r => r.Id == c.ReviewId && publicItems.Any(m => m.Id == r.MediaItemId)))
                select new ModerationTarget(c.Id,"comment",new StaffIdentity(u.Id,u.UserName!,u.DisplayName),c.Body,c.IsSpoiler,c.IsHidden,c.UpdatedAt)).SingleOrDefaultAsync(ct);
        if (type == "item")
            return await (from m in db.MediaItems.AsNoTracking() join author in db.Users.AsNoTracking() on m.CreatorUserId equals author.Id into authors
                from u in authors.DefaultIfEmpty()
                where m.Id == id && m.Provenance == "user" && (retainedReport || m.Visibility == "public")
                select new ModerationTarget(m.Id,"item",u == null ? null : new StaffIdentity(u.Id,u.UserName!,u.DisplayName),m.Title,false,m.IsHidden,m.UpdatedAt,m.Description,null)).SingleOrDefaultAsync(ct);
        if (type == "category")
            return await (from c in db.Categories.AsNoTracking() join author in db.Users.AsNoTracking() on c.OwnerUserId equals author.Id into authors
                from u in authors.DefaultIfEmpty()
                where c.Id == id && c.Scope == "public" && !c.IsSeeded
                select new ModerationTarget(c.Id,"category",u == null ? null : new StaffIdentity(u.Id,u.UserName!,u.DisplayName),c.Name,false,c.IsHidden,c.UpdatedAt)).SingleOrDefaultAsync(ct);
        return null;
    }
    private static Guid? LiveId(ContentReport report) => report.TargetType switch
    { "review" => report.ReviewId, "comment" => report.CommentId, "item" => report.ItemId, "category" => report.CategoryId, _ => null };
    private Task<ModerationTarget?> LiveTarget(ContentReport report, CancellationToken ct) =>
        LiveId(report) is Guid id ? TargetCoreAsync(report.TargetType, id, true, ct) : Task.FromResult<ModerationTarget?>(null);

    private IQueryable<ReportSummary> Summaries(IQueryable<ContentReport> reports) =>
        from r in reports join u in db.Users.AsNoTracking() on r.ReporterId equals u.Id into users
        from u in users.DefaultIfEmpty()
        select new ReportSummary(r.Id,r.TargetType,r.TargetId,u == null ? null : new StaffIdentity(u.Id,u.UserName!,u.DisplayName),r.Reason,r.Status,r.CreatedAt);

    public async Task<ModerationPage<ReportSummary>> ReportsAsync(string status, int page, int pageSize, CancellationToken ct)
    {
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead,ct);
        var q=db.ContentReports.AsNoTracking().Where(x=>x.Status==status);var count=await q.CountAsync(ct);
        var items=await Summaries(q.OrderBy(x=>x.CreatedAt).ThenBy(x=>x.Id).Skip((page-1)*pageSize).Take(pageSize)).ToListAsync(ct);
        await tx.CommitAsync(ct);return new(items,page,pageSize,count);
    }
    public async Task<ReportDetail?> ReportAsync(Guid id,CancellationToken ct)
    {
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead,ct);
        var r=await db.ContentReports.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==id,ct);if(r is null)return null;
        var summary=new ReportSummary(r.Id,r.TargetType,r.TargetId,await Identity(r.ReporterId,ct),r.Reason,r.Status,r.CreatedAt);
        var target = await LiveTarget(r,ct);
        if (target is not null && r.TargetType == "item")
        {
            var asset = await db.Set<RateFreak.Domain.Catalog.ArtworkAsset>().Where(a => a.ItemId == r.ItemId).Select(a => (Guid?)a.AssetId).SingleOrDefaultAsync(ct);
            if (asset is not null) target = target with { ImageReference = $"/api/v1/moderation/reports/{id}/artwork/{asset}" };
        }
        var result=new ReportDetail(summary,r.Details,target,r.Decision,r.ResolutionReason,r.ResolvedAt);
        await tx.CommitAsync(ct);return result;
    }
    public async Task<ModerationPage<ModerationAuditEntry>> AuditAsync(string? type,Guid? id,int page,int pageSize,CancellationToken ct)
    {
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead,ct);
        var q=db.ModerationEvents.AsNoTracking().Where(x=>(type==null||x.TargetType==type)&&(id==null||x.TargetId==id));
        var count=await q.CountAsync(ct);
        var items=await q.OrderByDescending(x=>x.CreatedAt).ThenBy(x=>x.Id).Skip((page-1)*pageSize).Take(pageSize)
            .Select(x => new ModerationAuditEntry(x.Id, x.CreatedAt, x.ActorId, x.ActorKind, x.Action,
                x.TargetType, x.TargetId, x.ReportId, x.Reason, x.Before, x.After, x.ReviewedUpdatedAt))
            .ToListAsync(ct);
        await tx.CommitAsync(ct);return new(items,page,pageSize,count);
    }
    private async Task LockUser(Guid id,CancellationToken ct) =>
        _ = await db.Users.FromSqlInterpolated($"SELECT * FROM users WHERE id = {id} FOR UPDATE").AsNoTracking().ToListAsync(ct);
    private async Task LockTarget(string type,Guid id,CancellationToken ct)
    {
        if(type == "item")
            _ = await db.MediaItems.FromSqlInterpolated($"SELECT * FROM media_items WHERE id = {id} FOR UPDATE").AsNoTracking().ToListAsync(ct);
        else if(type == "category")
            _ = await db.Categories.FromSqlInterpolated($"SELECT * FROM catalog_categories WHERE id = {id} FOR UPDATE").AsNoTracking().ToListAsync(ct);
        else if(type=="review")
            _=await db.Reviews.FromSqlInterpolated($"SELECT * FROM reviews WHERE id = {id} FOR UPDATE").AsNoTracking().ToListAsync(ct);
        else
        {
            var parent=await db.ReviewComments.Where(x=>x.Id==id).Select(x=>(Guid?)x.ReviewId).SingleOrDefaultAsync(ct);
            if(parent.HasValue)_=await db.Reviews.FromSqlInterpolated($"SELECT * FROM reviews WHERE id = {parent.Value} FOR UPDATE").AsNoTracking().ToListAsync(ct);
            _=await db.ReviewComments.FromSqlInterpolated($"SELECT * FROM review_comments WHERE id = {id} FOR UPDATE").AsNoTracking().ToListAsync(ct);
        }
    }
    public async Task<ModerationResult> ReportAsync(Guid actor,string type,Guid id,ReportRequest request,CancellationToken ct)
    {
        if(!ModerationRules.Report(request))return new(ModerationStatus.Invalid);
        await using var tx=await db.Database.BeginTransactionAsync(ct);await ModerationLock.AcquireAsync(db,ct);await LockUser(actor,ct);
        if(!await db.Users.AnyAsync(x=>x.Id==actor,ct))return new(ModerationStatus.Forbidden);
        var readableItems = db.MediaItems.Where(CatalogAccess.Readable(actor));
        if (type == "item" && !await readableItems.AnyAsync(m => m.Id == id && m.Visibility == "public" && m.Provenance == "user", ct)) return new(ModerationStatus.NotFound);
        if (type == "category" && !await db.Categories.AnyAsync(c => c.Id == id && c.Scope == "public" && !c.IsHidden && !c.IsSeeded, ct)) return new(ModerationStatus.NotFound);
        if (type == "review" && !await db.Reviews.AnyAsync(r => r.Id == id && readableItems.Any(m => m.Id == r.MediaItemId), ct)) return new(ModerationStatus.NotFound);
        if (type == "comment" && !await db.ReviewComments.AnyAsync(c => c.Id == id && db.Reviews.Any(r => r.Id == c.ReviewId && readableItems.Any(m => m.Id == r.MediaItemId)), ct)) return new(ModerationStatus.NotFound);
        await LockTarget(type,id,ct);var target=await TargetAsync(type,id,ct);
        if(target is null||target.Hidden)return new(ModerationStatus.NotFound);
        if(type=="comment"&&!await db.ReviewComments.Where(x=>x.Id==id).AnyAsync(x=>db.Reviews.Any(r=>r.Id==x.ReviewId&&!r.IsHidden),ct))return new(ModerationStatus.NotFound);
        if(target.Author != null && target.Author.Id==actor)return new(ModerationStatus.Invalid);
        if(await db.ContentReports.AnyAsync(x=>x.OriginalReporterId==actor&&x.TargetType==type&&x.TargetId==id,ct))return new(ModerationStatus.Conflict);
        var report=new ContentReport { TargetType=type,TargetId=id,OriginalReporterId=actor,ReporterId=actor,OriginalAuthorId=target.Author == null ? null : target.Author.Id,ReviewId=type=="review"?id:null,CommentId=type=="comment"?id:null,ItemId=type=="item"?id:null,CategoryId=type=="category"?id:null,Reason=request.Reason!,Details=string.IsNullOrWhiteSpace(request.Details)?null:request.Details.Trim() };
        db.Add(report);await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);
        return new(ModerationStatus.Success,new ReportAcknowledgement(report.Id,report.Status,report.CreatedAt));
    }
}
