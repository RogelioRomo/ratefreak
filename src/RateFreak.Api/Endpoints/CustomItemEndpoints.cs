using System.Security.Claims;
using RateFreak.Api.Security;
using RateFreak.Application.Catalog;

namespace RateFreak.Api.Endpoints;

internal static class CustomItemEndpoints
{
    public static void MapCustomItemEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/media").WithTags("Custom catalog").RequireAuthorization();
        group.MapGet("/{id:guid}/manage", async (Guid id, ClaimsPrincipal user, ICatalogItemCommands service, CancellationToken ct) =>
            await service.ManageAsync(CategoryEndpoints.Viewer(user)!.Value, id, ct) is { } item ? Results.Ok(item) : Results.NotFound()).RequireRateLimiting("personal-read");
        Write(group.MapPost("", async (CustomItemCreate request, ClaimsPrincipal user, ICatalogItemCommands service, CancellationToken ct) =>
        {
            var result = await service.CreateAsync(CategoryEndpoints.Viewer(user)!.Value, request, ct);
            return result.Status == CatalogStatus.Success ? Results.Created($"/api/v1/media/{result.Value!.Id}", result.Value) : CategoryEndpoints.Result(result);
        }));
        Write(group.MapPatch("/{id:guid}", async (Guid id, CustomItemUpdate request, ClaimsPrincipal user, ICatalogItemCommands service, CancellationToken ct) => CategoryEndpoints.Result(await service.UpdateAsync(CategoryEndpoints.Viewer(user)!.Value, id, request, ct))));
        foreach (var action in new[] { "publish", "unpublish", "archive", "restore" })
            Write(group.MapPost($"/{{id:guid}}/{action}", async (Guid id, ItemStateChange request, ClaimsPrincipal user, ICatalogItemCommands service, CancellationToken ct) => CategoryEndpoints.Result(await service.ChangeStateAsync(CategoryEndpoints.Viewer(user)!.Value, id, action, request, ct))));
    }
    private static RouteHandlerBuilder Write(RouteHandlerBuilder builder) => builder.AddEndpointFilter<AntiforgeryFilter>().RequireRateLimiting("personal-write");
}
