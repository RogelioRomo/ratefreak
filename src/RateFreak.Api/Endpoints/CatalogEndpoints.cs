using System.Security.Claims;
using RateFreak.Application.Catalog;
using RateFreak.Domain.Catalog;

namespace RateFreak.Api.Endpoints;

internal static class CatalogEndpoints
{
    public static void MapCatalogEndpoints(this WebApplication app)
    {
        var catalog = app.MapGroup("/api/v1/media").WithTags("Catalog").RequireRateLimiting("catalog");
        catalog.MapGet("", async (ClaimsPrincipal user, ICatalogQueries queries, CancellationToken cancellationToken,
            string? q = null, string? type = null, int page = 1, int pageSize = 20, Guid? categoryId = null, bool ownOnly = false) =>
        {
            if (ownOnly && ViewerId(user) is null) return Results.Unauthorized();
            q = q?.Trim();
            var errors = new Dictionary<string, string[]>();
            if (q?.Length > 200) errors["q"] = ["Search must not exceed 200 characters."];
            if (q?.Contains('\0') == true) errors["q"] = ["Search must not contain null characters."];
            if (type is not null && !MediaTypes.IsCatalogType(type)) errors["type"] = ["Type must be movie, tv_show, album or video_game."];
            if (page is < 1 or > 10000) errors["page"] = ["Page must be between 1 and 10000."];
            if (pageSize is < 1 or > 100) errors["pageSize"] = ["Page size must be between 1 and 100."];
            if (errors.Count > 0) return Results.ValidationProblem(errors);
            return Results.Ok(await queries.SearchAsync(ViewerId(user), new CatalogQuery(q, type, page, pageSize, categoryId, ownOnly), cancellationToken));
        }).Produces<CatalogPage>().ProducesValidationProblem().ProducesProblem(StatusCodes.Status429TooManyRequests)
            .WithSummary("Search the local movie and TV catalog");
        catalog.MapGet("/{id:guid}", async (Guid id, ClaimsPrincipal user, ICatalogQueries queries, CancellationToken cancellationToken) =>
        {
            var item = await queries.FindAsync(ViewerId(user), id, cancellationToken);
            return item is null ? Results.NotFound() : Results.Ok(item);
        }).Produces<MediaDetails>().ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests).WithSummary("Get canonical media details");
    }
    private static Guid? ViewerId(ClaimsPrincipal user) => Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

}
