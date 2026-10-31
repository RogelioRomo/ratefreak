using System.Security.Claims;
using RateFreak.Api.Security;
using RateFreak.Application.Identity;

namespace RateFreak.Api.Endpoints;

internal sealed class ProfileNoStore;

internal static class UserProfileEndpoints
{
    public static void MapUserProfileEndpoints(this WebApplication app)
    {
        var users = app.MapGroup("/api/v1/users").WithTags("Profiles").WithMetadata(new ProfileNoStore());
        users.MapGet("/{userId:guid}", async (Guid userId, IUserProfileService profiles, CancellationToken ct) =>
            await profiles.FindAsync(userId, ct) is { } profile ? Results.Ok(profile) : Results.NotFound())
            .RequireRateLimiting("profile-read").Produces<PublicProfile>().ProducesProblem(404).ProducesProblem(429)
            .WithSummary("Get a public profile without private account data");
        users.MapGet("/by-handle/{handle}", async (string handle, IUserProfileService profiles, CancellationToken ct) =>
        {
            if (!UserProfileRules.ValidHandle(handle)) return Results.ValidationProblem(new Dictionary<string, string[]>
                { ["handle"] = ["Handle must contain 3–30 ASCII letters, digits or underscores."] });
            return await profiles.FindByHandleAsync(handle, ct) is { } profile ? Results.Ok(profile) : Results.NotFound();
        }).RequireRateLimiting("profile-read").Produces<PublicProfile>().ProducesValidationProblem().ProducesProblem(404).ProducesProblem(429)
            .WithSummary("Get a public profile by case-insensitive handle");
        users.MapPatch("/me", async (UpdateProfileRequest request, ClaimsPrincipal user, IUserProfileService profiles, CancellationToken ct) =>
        {
            var id = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await profiles.UpdateAsync(id, request, ct);
            if (result.Errors is { } errors) return Results.ValidationProblem(errors);
            return result.Profile is { } profile ? Results.Ok(profile) : Results.Unauthorized();
        }).RequireAuthorization().AddEndpointFilter<AntiforgeryFilter>().RequireRateLimiting("profile-write")
            .Produces<AccountProfile>().ProducesValidationProblem().ProducesProblem(401).ProducesProblem(429)
            .WithSummary("Update your display name and bio, preserving omitted fields");
    }
}
