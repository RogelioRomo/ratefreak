using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using RateFreak.Api.Security;
using RateFreak.Application.Identity;

namespace RateFreak.Api.Endpoints;

internal static class AccountEndpoints
{
    public static void MapAccountEndpoints(this WebApplication app)
    {
        var auth = app.MapGroup("/api/v1/auth").WithTags("Authentication");
        auth.MapGet("/csrf", (HttpContext context, IAntiforgery antiforgery) =>
        {
            var tokens = antiforgery.GetAndStoreTokens(context);
            return Results.Ok(new { requestToken = tokens.RequestToken });
        }).RequireRateLimiting("csrf").WithSummary("Issue a CSRF token for the current session");

        auth.MapPost("/register", async (RegisterRequest request, IAccountService accounts, CancellationToken cancellationToken) =>
            Registration(await accounts.RegisterAsync(request, cancellationToken)))
            .AddEndpointFilter<AntiforgeryFilter>().RequireRateLimiting("registration")
            .Produces<AccountProfile>(StatusCodes.Status201Created).ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict).WithSummary("Register a local account");

        auth.MapPost("/login", async (LoginRequest request, IAccountService accounts, CancellationToken cancellationToken) =>
        {
            var result = await accounts.LoginAsync(request, cancellationToken);
            return result.Status switch
            {
                AccountStatus.Success => Results.Ok(result.Profile),
                AccountStatus.Invalid => Results.ValidationProblem(result.Errors!),
                _ => Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid email or password.")
            };
        }).AddEndpointFilter<AntiforgeryFilter>().RequireRateLimiting("login")
            .Produces<AccountProfile>().ProducesValidationProblem().ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithSummary("Sign in with email and password");

        auth.MapPost("/logout", async (ClaimsPrincipal user, IAccountService accounts, CancellationToken cancellationToken) =>
        {
            if (!UserId(user, out var id)) return Results.Unauthorized();
            await accounts.LogoutAsync(id, cancellationToken);
            return Results.NoContent();
        }).RequireAuthorization().AddEndpointFilter<AntiforgeryFilter>()
            .Produces(StatusCodes.Status204NoContent).ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithSummary("Sign out and revoke all sessions for this account");

        app.MapGet("/api/v1/users/me", async (ClaimsPrincipal user, IAccountService accounts, CancellationToken cancellationToken) =>
        {
            if (!UserId(user, out var id)) return Results.Unauthorized();
            var profile = await accounts.FindAsync(id, cancellationToken);
            return profile is null ? Results.Unauthorized() : Results.Ok(profile);
        }).RequireAuthorization().WithTags("Users").Produces<AccountProfile>()
            .ProducesProblem(StatusCodes.Status401Unauthorized).WithSummary("Get the current account");
    }

    private static bool UserId(ClaimsPrincipal user, out Guid id) =>
        Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out id);

    private static IResult Registration(AccountResult result) => result.Status switch
    {
        AccountStatus.Success => Results.Json(result.Profile, statusCode: StatusCodes.Status201Created),
        AccountStatus.Conflict => Results.Problem(statusCode: StatusCodes.Status409Conflict,
            title: "Email or handle is unavailable."),
        _ => Results.ValidationProblem(result.Errors!)
    };
}
