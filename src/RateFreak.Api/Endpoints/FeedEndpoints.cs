using System.Security.Claims;
using RateFreak.Application.Social;
using RateFreak.Domain.Catalog;

namespace RateFreak.Api.Endpoints;

internal sealed class FeedNoStore;
internal static class FeedEndpoints
{
    public static void MapFeedEndpoints(this WebApplication app) =>
        app.MapGet("/api/v1/feed", async (HttpRequest request, ClaimsPrincipal user, IFeedService service, CancellationToken ct) =>
        {
            var query = request.Query;
            if (query.Any(x => x.Key is not ("tab" or "type" or "cursor") || x.Value.Count != 1))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["query"] = ["Unknown or repeated feed parameter."] });
            var tab = query.ContainsKey("tab") ? query["tab"].ToString() : "following";
            string? type = query.ContainsKey("type") ? query["type"].ToString() : null;
            string? cursor = query.ContainsKey("cursor") ? query["cursor"].ToString() : null;
            if (tab is not ("following" or "community") || type is not null && !MediaTypes.IsCatalogType(type))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["query"] = ["Invalid feed tab or media type."] });
            var result = await service.ListAsync(Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!), new(tab, type, cursor), ct);
            return result.Errors is {} errors ? Results.ValidationProblem(errors) : Results.Ok(result.Page);
        }).WithTags("Feeds").WithMetadata(new FeedNoStore(), new StoredCatalogContent()).RequireAuthorization().RequireRateLimiting("social-read")
        .Produces<FeedPage>().ProducesValidationProblem().ProducesProblem(401).ProducesProblem(429)
        .WithSummary("Read current public reviews and explicitly shared ratings, newest first");
}
