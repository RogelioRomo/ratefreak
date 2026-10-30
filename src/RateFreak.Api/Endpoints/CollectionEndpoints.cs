using System.Globalization;
using System.Security.Claims;
using RateFreak.Api.Security;
using RateFreak.Application.Collections;

namespace RateFreak.Api.Endpoints;

internal sealed class CollectionNoStore;

internal static class CollectionEndpoints
{
    public static void MapCollectionEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/collections").WithTags("Collections").WithMetadata(new CollectionNoStore());
        Read(group.MapGet("", async (ClaimsPrincipal user, ICollectionQueries queries, CancellationToken ct,
            bool ownOnly = false, int page = 1, int pageSize = 20) =>
        {
            if (ownOnly && Viewer(user) is null) return Results.Unauthorized();
            var errors = PageErrors(page, pageSize);
            return errors.Count != 0 ? Results.ValidationProblem(errors)
                : Results.Ok(await queries.ListAsync(Viewer(user), ownOnly, page, pageSize, ct));
        })).Produces<CollectionPage<CollectionSummary>>().ProducesValidationProblem().ProducesProblem(401).WithSummary("List public and your own collections");
        Read(group.MapGet("/{id:guid}", async (Guid id, ClaimsPrincipal user, ICollectionQueries queries, CancellationToken ct) =>
        {
            var value = await queries.FindAsync(Viewer(user), id, ct);
            return value is null ? Results.NotFound() : Results.Ok(value);
        })).Produces<CollectionDetails>().ProducesProblem(404).WithSummary("Get an accessible collection and its fields");
        Write(group.MapPost("", async (CollectionCreate request, ClaimsPrincipal user, ICollectionCommands commands, HttpContext context, CancellationToken ct) =>
        {
            var result = await commands.CreateAsync(Owner(user), request, ct);
            return Response(result, context, result.Value is null ? null : $"/api/v1/collections/{result.Value.Collection.Id}");
        })).Produces<CollectionDetails>(201).WithSummary("Create a collection and its initial fields");
        Write(group.MapPatch("/{id:guid}", async (Guid id, CollectionUpdate request, ClaimsPrincipal user, ICollectionCommands commands, HttpContext context, CancellationToken ct) =>
            Response(await commands.UpdateAsync(Owner(user), id, request, ct), context)))
            .Produces<CollectionDetails>().WithSummary("Replace your collection metadata using its current revision");
        Read(group.MapGet("/{id:guid}/items", async (Guid id, ClaimsPrincipal user, ICollectionQueries queries, CancellationToken ct,
            int page = 1, int pageSize = 20) =>
        {
            var errors = PageErrors(page, pageSize);
            if (errors.Count != 0) return Results.ValidationProblem(errors);
            var value = await queries.ItemsAsync(Viewer(user), id, page, pageSize, ct);
            return value is null ? Results.NotFound() : Results.Ok(value);
        })).Produces<CollectionPage<ItemDetails>>().ProducesValidationProblem().ProducesProblem(404).WithSummary("List accessible collection entries");
        Read(group.MapGet("/{id:guid}/items/{itemId:guid}", async (Guid id, Guid itemId, ClaimsPrincipal user, ICollectionQueries queries, CancellationToken ct) =>
        {
            var value = await queries.ItemAsync(Viewer(user), id, itemId, ct);
            return value is null ? Results.NotFound() : Results.Ok(value);
        })).Produces<ItemDetails>().ProducesProblem(404).WithSummary("Get an accessible collection entry");
        Write(group.MapPost("/{id:guid}/items", async (Guid id, ItemCreate request, ClaimsPrincipal user, ICollectionItemCommands commands, HttpContext context, CancellationToken ct) =>
        {
            var result = await commands.AddAsync(Owner(user), id, request, ct);
            return Response(result, context, result.Value is null ? null : $"/api/v1/collections/{id}/items/{result.Value.Id}");
        })).Produces<ItemDetails>(201).WithSummary("Add a canonical media or custom entry");
        Write(group.MapPatch("/{id:guid}/items/{itemId:guid}", async (Guid id, Guid itemId, ItemUpdate request, ClaimsPrincipal user, ICollectionItemCommands commands, HttpContext context, CancellationToken ct) =>
            Response(await commands.UpdateAsync(Owner(user), id, itemId, request, ct), context)))
            .Produces<ItemDetails>().WithSummary("Replace an entry's custom data, title and position");
        Write(group.MapDelete("/{id:guid}/items/{itemId:guid}", async (Guid id, Guid itemId, ClaimsPrincipal user, ICollectionItemCommands commands, HttpContext context, CancellationToken ct,
            long? expectedRevision = null) => Response(await commands.DeleteAsync(Owner(user), id, itemId, expectedRevision, ct), context, delete: true)))
            .Produces(204).WithSummary("Delete your collection entry using its current revision");
    }

    private static RouteHandlerBuilder Read(RouteHandlerBuilder route) => route.RequireRateLimiting("catalog").ProducesProblem(429);
    private static RouteHandlerBuilder Write(RouteHandlerBuilder route) => route.RequireAuthorization().AddEndpointFilter<AntiforgeryFilter>()
        .RequireRateLimiting("personal-write").ProducesValidationProblem().ProducesProblem(401).ProducesProblem(404).ProducesProblem(409).ProducesProblem(429);
    private static Guid? Viewer(ClaimsPrincipal user) => Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
    private static Guid Owner(ClaimsPrincipal user) => Viewer(user)!.Value;
    private static IResult Response<T>(CollectionResult<T> result, HttpContext context, string? location = null, bool delete = false) where T : class
    {
        if (result.Status == CollectionStatus.Invalid) return Results.ValidationProblem(result.Errors!);
        if (result.Status == CollectionStatus.NotFound) return Results.NotFound();
        if (result.Status == CollectionStatus.Conflict) return Results.Problem(statusCode: 409, title: "The collection changed or this operation conflicts with its current data.");
        context.Response.Headers["X-Collection-Revision"] = result.Revision!.Value.ToString(CultureInfo.InvariantCulture);
        return delete ? Results.NoContent() : location is not null ? Results.Created(location, result.Value) : Results.Ok(result.Value);
    }
    private static Dictionary<string, string[]> PageErrors(int page, int pageSize)
    {
        var errors = new Dictionary<string, string[]>();
        if (page is < 1 or > 10000) errors["page"] = ["Page must be between 1 and 10000."];
        if (pageSize is < 1 or > 100) errors["pageSize"] = ["Page size must be between 1 and 100."];
        return errors;
    }
}
