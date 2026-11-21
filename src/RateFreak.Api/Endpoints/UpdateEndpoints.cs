using RateFreak.Application.Updates;
using RateFreak.Application.Moderation;
using System.Security.Claims;
using RateFreak.Api.Security;
namespace RateFreak.Api.Endpoints;
internal static class UpdateEndpoints
{
    public static void MapUpdateEndpoints(this WebApplication app)
    {
        var updates=app.MapGroup("/api/v1/admin/updates").WithTags("Application updates").RequireAuthorization("admin");
        updates.MapGet("",async(IUpdateService service,IModerationService access,ClaimsPrincipal user,CancellationToken ct)=>await Read(service,access,user,false,ct))
            .RequireRateLimiting("moderation-read").Produces<UpdateStatus>();
        updates.MapPost("/check",async(IUpdateService service,IModerationService access,ClaimsPrincipal user,CancellationToken ct)=>await Read(service,access,user,true,ct))
            .AddEndpointFilter<AntiforgeryFilter>().RequireRateLimiting("update-check").Produces<UpdateStatus>();
    }
    private static async Task<IResult> Read(IUpdateService service,IModerationService access,ClaimsPrincipal user,bool refresh,CancellationToken ct)
    {
        var status=await service.GetAsync(refresh,ct);
        if(!Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier),out var actor)||!await access.HasAccessAsync(actor,true,ct))return Results.StatusCode(403);
        return Results.Ok(status);
    }
}
