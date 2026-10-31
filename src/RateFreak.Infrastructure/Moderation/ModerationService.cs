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
        if (type == "review")
            return await (from r in db.Reviews.AsNoTracking() join u in db.Users.AsNoTracking() on r.UserId equals u.Id
                where r.Id == id select new ModerationTarget(r.Id,"review",new StaffIdentity(u.Id,u.UserName!,u.DisplayName),r.Body,r.IsSpoiler,r.IsHidden,r.UpdatedAt)).SingleOrDefaultAsync(ct);
        if (type == "comment")
            return await (from c in db.ReviewComments.AsNoTracking() join u in db.Users.AsNoTracking() on c.UserId equals u.Id
                where c.Id == id select new ModerationTarget(c.Id,"comment",new StaffIdentity(u.Id,u.UserName!,u.DisplayName),c.Body,c.IsSpoiler,c.IsHidden,c.UpdatedAt)).SingleOrDefaultAsync(ct);
        return null;
    }
    private Task<ModerationTarget?> LiveTarget(ContentReport report, CancellationToken ct) =>
        (report.TargetType == "review" ? report.ReviewId : report.CommentId) is Guid id
            ? TargetAsync(report.TargetType, id, ct) : Task.FromResult<ModerationTarget?>(null);

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
        var result=new ReportDetail(summary,r.Details,await LiveTarget(r,ct),r.Decision,r.ResolutionReason,r.ResolvedAt);
        await tx.CommitAsync(ct);return result;
    }
    public async Task<ModerationPage<ModerationEvent>> AuditAsync(string? type,Guid? id,int page,int pageSize,CancellationToken ct)
    {
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead,ct);
        var q=db.ModerationEvents.AsNoTracking().Where(x=>(type==null||x.TargetType==type)&&(id==null||x.TargetId==id));
        var count=await q.CountAsync(ct);var items=await q.OrderByDescending(x=>x.CreatedAt).ThenBy(x=>x.Id).Skip((page-1)*pageSize).Take(pageSize).ToListAsync(ct);
        await tx.CommitAsync(ct);return new(items,page,pageSize,count);
    }
    private async Task LockUser(Guid id,CancellationToken ct) =>
        _ = await db.Users.FromSqlInterpolated($"SELECT * FROM users WHERE id = {id} FOR UPDATE").AsNoTracking().ToListAsync(ct);
    private async Task LockTarget(string type,Guid id,CancellationToken ct)
    {
        if(type=="review")
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
        await LockTarget(type,id,ct);var target=await TargetAsync(type,id,ct);
        if(target is null||target.Hidden)return new(ModerationStatus.NotFound);
        if(type=="comment"&&!await db.ReviewComments.Where(x=>x.Id==id).AnyAsync(x=>db.Reviews.Any(r=>r.Id==x.ReviewId&&!r.IsHidden),ct))return new(ModerationStatus.NotFound);
        if(target.Author!.Id==actor)return new(ModerationStatus.Invalid);
        if(await db.ContentReports.AnyAsync(x=>x.OriginalReporterId==actor&&x.TargetType==type&&x.TargetId==id,ct))return new(ModerationStatus.Conflict);
        var report=new ContentReport { TargetType=type,TargetId=id,OriginalReporterId=actor,ReporterId=actor,OriginalAuthorId=target.Author.Id,ReviewId=type=="review"?id:null,CommentId=type=="comment"?id:null,Reason=request.Reason!,Details=string.IsNullOrWhiteSpace(request.Details)?null:request.Details.Trim() };
        db.Add(report);await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);
        return new(ModerationStatus.Success,new ReportAcknowledgement(report.Id,report.Status,report.CreatedAt));
    }
}
