using System.Security.Claims;
using RateFreak.Api.Security;
using RateFreak.Application.Social;

namespace RateFreak.Api.Endpoints;

internal sealed class FollowNoStore;

internal static class FollowEndpoints
{
    public static void MapFollowEndpoints(this WebApplication app)
    {
        var users = app.MapGroup("/api/v1/users/{userId:guid}").WithTags("Follows").WithMetadata(new FollowNoStore());
        MapList(users, "followers", true);
        MapList(users, "following", false);
        Write(users.MapPut("/follow", (Guid userId, ClaimsPrincipal user, IFollowService service, CancellationToken ct) =>
            Set(userId, user, service, true, ct))).WithSummary("Follow a user (repeat-safe)");
        Write(users.MapDelete("/follow", (Guid userId, ClaimsPrincipal user, IFollowService service, CancellationToken ct) =>
            Set(userId, user, service, false, ct))).WithSummary("Unfollow a user (repeat-safe)");
    }

    private static void MapList(RouteGroupBuilder users, string path, bool followers) =>
        users.MapGet("/" + path, async (Guid userId, IFollowService service, CancellationToken ct, int page = 1, int pageSize = 20) =>
        {
            var errors = new Dictionary<string, string[]>();
            if (page is < 1 or > 10000) errors["page"] = ["Page must be between 1 and 10000."];
            if (pageSize is < 1 or > 100) errors["pageSize"] = ["Page size must be between 1 and 100."];
            if (errors.Count != 0) return Results.ValidationProblem(errors);
            var result = await service.ListAsync(userId, followers, page, pageSize, ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }).RequireRateLimiting("social-read").Produces<FollowPage>().ProducesValidationProblem().ProducesProblem(404).ProducesProblem(429)
            .WithSummary("List public " + path + " ordered by follow time and user ID");

    private static RouteHandlerBuilder Write(RouteHandlerBuilder builder) => builder.RequireAuthorization()
        .AddEndpointFilter<AntiforgeryFilter>().RequireRateLimiting("social-write")
        .Produces(204).ProducesValidationProblem().ProducesProblem(401).ProducesProblem(404).ProducesProblem(429);

    private static async Task<IResult> Set(Guid targetId, ClaimsPrincipal user, IFollowService service, bool following, CancellationToken ct) =>
        await service.SetAsync(Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!), targetId, following, ct) switch
        {
            FollowStatus.Success => Results.NoContent(),
            FollowStatus.Missing => Results.NotFound(),
            _ => Results.ValidationProblem(new Dictionary<string, string[]> { ["userId"] = ["You cannot follow or unfollow yourself."] })
        };
}
