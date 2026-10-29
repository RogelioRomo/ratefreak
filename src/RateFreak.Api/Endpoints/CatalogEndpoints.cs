using RateFreak.Application.Catalog;
using RateFreak.Domain.Catalog;

namespace RateFreak.Api.Endpoints;

internal static class CatalogEndpoints
{
    public static void MapCatalogEndpoints(this WebApplication app)
    {
        var catalog = app.MapGroup("/api/v1/media").WithTags("Catalog").RequireRateLimiting("catalog");
        catalog.MapGet("", async (ICatalogQueries queries, CancellationToken cancellationToken,
            string? q = null, string? type = null, int page = 1, int pageSize = 20) =>
        {
            q = q?.Trim();
            var errors = new Dictionary<string, string[]>();
            if (q?.Length > 200) errors["q"] = ["Search must not exceed 200 characters."];
            if (q?.Contains('\0') == true) errors["q"] = ["Search must not contain null characters."];
            if (type is not null && !MediaTypes.IsSupported(type)) errors["type"] = ["Type must be movie or tv_show."];
            if (page is < 1 or > 10000) errors["page"] = ["Page must be between 1 and 10000."];
            if (pageSize is < 1 or > 100) errors["pageSize"] = ["Page size must be between 1 and 100."];
            if (errors.Count > 0) return Results.ValidationProblem(errors);
            return Results.Ok(await queries.SearchAsync(new CatalogQuery(q, type, page, pageSize), cancellationToken));
        }).Produces<CatalogPage>().ProducesValidationProblem().ProducesProblem(StatusCodes.Status429TooManyRequests)
            .WithSummary("Search the local movie and TV catalog");
        catalog.MapGet("/{id:guid}", async (Guid id, ICatalogQueries queries, CancellationToken cancellationToken) =>
        {
            var item = await queries.FindAsync(id, cancellationToken);
            return item is null ? Results.NotFound() : Results.Ok(item);
        }).Produces<MediaDetails>().ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests).WithSummary("Get canonical media details");
    }
}
