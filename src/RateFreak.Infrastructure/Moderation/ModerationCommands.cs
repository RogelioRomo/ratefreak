using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RateFreak.Application.Moderation;
using RateFreak.Domain.Moderation;

namespace RateFreak.Infrastructure.Moderation;

internal sealed partial class ModerationService
{
    private void Event(Guid? actor,string action,string type,Guid id,string reason,bool? before=null,bool? after=null,Guid? report=null,DateTimeOffset? version=null) =>
        db.Add(new ModerationEvent {ActorId=actor,ActorKind=actor.HasValue?"account":"operator",Action=action,TargetType=type,TargetId=id,Reason=reason.Trim(),Before=before,After=after,ReportId=report,ReviewedUpdatedAt=version});

    private async Task ChangeVisibility(string type,Guid id,bool hidden,DateTimeOffset previous,CancellationToken ct)
    {
        // PostgreSQL timestamp precision is microseconds; guarantee distinct versions even on fast repeats.
        var now=DateTimeOffset.UtcNow; if(now<=previous.AddTicks(10))now=previous.AddTicks(10);
        if(type=="review")await db.Reviews.Where(x=>x.Id==id).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.IsHidden,hidden).SetProperty(x=>x.UpdatedAt,now),ct);
        else if(type=="item")await db.MediaItems.Where(x=>x.Id==id).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.IsHidden,hidden).SetProperty(x=>x.Revision,x=>x.Revision+1).SetProperty(x=>x.UpdatedAt,now),ct);
        else if(type=="category")await db.Categories.Where(x=>x.Id==id && !x.IsSeeded).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.IsHidden,hidden).SetProperty(x=>x.Revision,x=>x.Revision+1).SetProperty(x=>x.UpdatedAt,now),ct);
        else await db.ReviewComments.Where(x=>x.Id==id).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.IsHidden,hidden).SetProperty(x=>x.UpdatedAt,now),ct);
    }
    public async Task<ModerationResult> VisibilityAsync(Guid actor,string type,Guid id,VisibilityRequest request,CancellationToken ct)
    {
        if(!ModerationRules.Text(request.Reason)||request.Hidden is null||request.ExpectedTargetUpdatedAt is null)return new(ModerationStatus.Invalid);
        await using var tx=await db.Database.BeginTransactionAsync(ct);await ModerationLock.AcquireAsync(db,ct);await LockUser(actor,ct);
        if(!await HasAccessAsync(actor,type == "category",ct))return new(ModerationStatus.Forbidden);
        await LockTarget(type,id,ct);var target=await TargetAsync(type,id,ct);if(target is null)return new(ModerationStatus.NotFound);
        if(target.Author != null && target.Author.Id==actor)return new(ModerationStatus.Forbidden);
        if(target.UpdatedAt!=request.ExpectedTargetUpdatedAt)return new(ModerationStatus.Conflict);
        if(target.Hidden!=request.Hidden.Value)
        {
            await ChangeVisibility(type,id,request.Hidden.Value,target.UpdatedAt,ct);
            Event(actor,request.Hidden.Value?"hide":"restore",type,id,request.Reason!,target.Hidden,request.Hidden.Value,version:target.UpdatedAt);
            await db.SaveChangesAsync(ct);
        }
        await tx.CommitAsync(ct);return new(ModerationStatus.Success);
    }
    public async Task<ModerationResult> ResolveAsync(Guid actor,Guid id,ResolveRequest request,CancellationToken ct)
    {
        if(!ModerationRules.Text(request.Reason)||request.Decision is not ("hide" or "dismiss"))return new(ModerationStatus.Invalid);
        await using var tx=await db.Database.BeginTransactionAsync(ct);await ModerationLock.AcquireAsync(db,ct);await LockUser(actor,ct);
        if(!await HasAccessAsync(actor,false,ct))return new(ModerationStatus.Forbidden);
        var r=await db.ContentReports.SingleOrDefaultAsync(x=>x.Id==id,ct);if(r is null)return new(ModerationStatus.NotFound);
        if(r.TargetType == "category" && !await HasAccessAsync(actor,true,ct))return new(ModerationStatus.Forbidden);
        if(r.OriginalAuthorId==actor)return new(ModerationStatus.Forbidden);
        if(r.Status!="open")return new(ModerationStatus.Conflict);
        if (LiveId(r) is Guid liveId) await LockTarget(r.TargetType,liveId,ct);
        var target=await LiveTarget(r,ct);
        if(target is null ? request.Decision=="hide"||request.ExpectedTargetUpdatedAt is not null : target.UpdatedAt!=request.ExpectedTargetUpdatedAt)return new(ModerationStatus.Conflict);
        if(request.Decision=="hide"&&!target!.Hidden)await ChangeVisibility(r.TargetType,r.TargetId,true,target.UpdatedAt,ct);
        r.Status="resolved";r.Decision=request.Decision;r.ResolutionReason=request.Reason!.Trim();r.ResolvedBy=actor;r.ResolvedAt=DateTimeOffset.UtcNow;
        Event(actor,"resolve_"+request.Decision,r.TargetType,r.TargetId,request.Reason!,target?.Hidden,request.Decision=="hide"?true:target?.Hidden,id,target?.UpdatedAt);
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return new(ModerationStatus.Success);
    }
    public async Task<ModerationResult> ModeratorAsync(Guid actor,Guid user,bool grant,string reason,CancellationToken ct)
    {
        if(!ModerationRules.Text(reason))return new(ModerationStatus.Invalid);
        await using var tx=await db.Database.BeginTransactionAsync(ct);await ModerationLock.AcquireAsync(db,ct);
        // All commands acquire the shared lock first; order account locks by UUID.
        foreach(var id in new[]{actor,user}.Distinct().Order())await LockUser(id,ct);
        if(!await HasAccessAsync(actor,true,ct))return new(ModerationStatus.Forbidden);
        if(!await db.Users.AnyAsync(x=>x.Id==user,ct))return new(ModerationStatus.NotFound);
        await SetRole(actor,user,"Moderator",grant,reason,ct);await tx.CommitAsync(ct);return new(ModerationStatus.Success);
    }
    public async Task<ModerationResult> AppointAdminAsync(Guid user,string reason,CancellationToken ct)
    {
        if(!ModerationRules.Text(reason))return new(ModerationStatus.Invalid);
        await using var tx=await db.Database.BeginTransactionAsync(ct);await ModerationLock.AcquireAsync(db,ct);await LockUser(user,ct);
        if(!await db.Users.AnyAsync(x=>x.Id==user,ct))return new(ModerationStatus.NotFound);
        await SetRole(null,user,"Admin",true,reason,ct);await tx.CommitAsync(ct);return new(ModerationStatus.Success);
    }
    private async Task SetRole(Guid? actor,Guid user,string name,bool grant,string reason,CancellationToken ct)
    {
        var role=await db.Roles.SingleOrDefaultAsync(x=>x.NormalizedName==name.ToUpperInvariant(),ct);
        if(role is null)
        {
            if(!grant)return;
            role=new IdentityRole<Guid>(name){Id=Guid.NewGuid(),NormalizedName=name.ToUpperInvariant()};db.Roles.Add(role);
            await db.SaveChangesAsync(ct);
        }
        var membership=await db.UserRoles.SingleOrDefaultAsync(x=>x.UserId==user&&x.RoleId==role.Id,ct);
        if((membership is not null)==grant)return;
        if(grant)db.UserRoles.Add(new IdentityUserRole<Guid>{UserId=user,RoleId=role.Id});else db.UserRoles.Remove(membership!);
        Event(actor,grant?"grant_"+name.ToLowerInvariant():"revoke_"+name.ToLowerInvariant(),"account",user,reason,!grant,grant);
        await db.SaveChangesAsync(ct);
    }
}
