using System.Security.Claims;
using RateFreak.Api.Security;
using RateFreak.Application.Catalog;

namespace RateFreak.Api.Endpoints;

internal static class CategoryEndpoints
{
    internal static Guid? Viewer(ClaimsPrincipal user) => Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
    internal static IResult Result<T>(CatalogResult<T> result) => result.Status switch
    {
        CatalogStatus.Success => Results.Ok(result.Value), CatalogStatus.NotFound => Results.NotFound(),
        CatalogStatus.Conflict => Results.Conflict(), CatalogStatus.Forbidden => Results.StatusCode(403),
        CatalogStatus.Unavailable => Results.Problem(statusCode: 503, title: "This feature is unavailable."),
        _ => Results.ValidationProblem(result.Errors ?? new() { ["request"] = ["Invalid request values."] })
    };
    public static void MapCategoryEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/categories").WithTags("Categories");
        group.MapGet("", async (ClaimsPrincipal user, ICategoryService service, CancellationToken ct, string? q = null, bool ownOnly = false, int page = 1, int pageSize = 20) =>
        {
            if (ownOnly && Viewer(user) is null) return Results.Unauthorized();
            if (page is < 1 or > 10000 || pageSize is < 1 or > 100 || q?.Length > 100) return Results.BadRequest();
            return Results.Ok(await service.ListAsync(Viewer(user), q, ownOnly, page, pageSize, ct));
        }).RequireRateLimiting("catalog");
        group.MapPost("", async (CategoryCreate request, ClaimsPrincipal user, ICategoryService service, CancellationToken ct) =>
        {
            var result = await service.CreateAsync(Viewer(user)!.Value, request, ct);
            return result.Status == CatalogStatus.Success ? Results.Created((string?)null, result.Value) : Result(result);
        }).RequireAuthorization().AddEndpointFilter<AntiforgeryFilter>().RequireRateLimiting("personal-write");
        group.MapPatch("/{id:guid}", async (Guid id, CategoryRename request, ClaimsPrincipal user, ICategoryService service, CancellationToken ct) => Result(await service.RenameAsync(Viewer(user)!.Value, id, request, false, ct)))
            .RequireAuthorization().AddEndpointFilter<AntiforgeryFilter>().RequireRateLimiting("personal-write");
        var admin = app.MapGroup("/api/v1/moderation/categories").RequireAuthorization("admin").WithTags("Category administration");
        admin.MapGet("", async (ClaimsPrincipal user, ICategoryService service, CancellationToken ct, string? q = null, int page = 1, int pageSize = 20) =>
            page is < 1 or > 10000 || pageSize is < 1 or > 100 || q?.Length > 100 ? Results.BadRequest() : Result(await service.ListForAdminAsync(Viewer(user)!.Value, q, page, pageSize, ct))).RequireRateLimiting("moderation-read");
        admin.MapPost("/{id:guid}/rename", async (Guid id, CategoryRename request, ClaimsPrincipal user, ICategoryService service, CancellationToken ct) => Result(await service.RenameAsync(Viewer(user)!.Value, id, request, true, ct))).AddEndpointFilter<AntiforgeryFilter>().RequireRateLimiting("moderation-write");
        admin.MapPost("/{id:guid}/merge", async (Guid id, CategoryMerge request, ClaimsPrincipal user, ICategoryService service, CancellationToken ct) => Result(await service.MergeAsync(Viewer(user)!.Value, id, request, ct))).AddEndpointFilter<AntiforgeryFilter>().RequireRateLimiting("moderation-write");
        foreach (var (action, hidden) in new[] { ("hide", true), ("restore", false) })
            admin.MapPost($"/{{id:guid}}/{action}", async (Guid id, CategoryStateChange request, ClaimsPrincipal user, ICategoryService service, CancellationToken ct) => Result(await service.SetHiddenAsync(Viewer(user)!.Value, id, request, hidden, ct))).AddEndpointFilter<AntiforgeryFilter>().RequireRateLimiting("moderation-write");
    }
}
