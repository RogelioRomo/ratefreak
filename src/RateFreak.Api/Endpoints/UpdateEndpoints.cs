using RateFreak.Application.Updates;
using RateFreak.Api.Security;
namespace RateFreak.Api.Endpoints;
internal static class UpdateEndpoints
{
    public static void MapUpdateEndpoints(this WebApplication app)
    {
        var updates=app.MapGroup("/api/v1/admin/updates").WithTags("Application updates").RequireAuthorization("admin");
        updates.MapGet("",async(IUpdateService service,CancellationToken ct)=>Results.Ok(await service.GetAsync(false,ct)))
            .RequireRateLimiting("moderation-read").Produces<UpdateStatus>();
        updates.MapPost("/check",async(IUpdateService service,CancellationToken ct)=>Results.Ok(await service.GetAsync(true,ct)))
            .AddEndpointFilter<AntiforgeryFilter>().RequireRateLimiting("update-check").Produces<UpdateStatus>();
    }
}
