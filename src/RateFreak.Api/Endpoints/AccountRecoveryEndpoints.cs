using System.Security.Claims;
using RateFreak.Api.Security;
using RateFreak.Application.Identity;

namespace RateFreak.Api.Endpoints;

internal static class AccountRecoveryEndpoints
{
    public static void MapAccountRecoveryEndpoints(this WebApplication app)
    {
        var auth = app.MapGroup("/api/v1/auth").WithTags("Authentication");
        auth.MapGet("/recovery-status", async (ClaimsPrincipal user, IAccountRecoveryService recovery, CancellationToken ct) =>
        {
            var status = await recovery.StatusAsync(Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!), ct);
            return status is null ? Results.Unauthorized() : Results.Ok(status);
        }).RequireAuthorization().RequireRateLimiting("personal-read")
            .Produces<RecoveryStatus>().WithSummary("Get local password recovery enrollment status");

        auth.MapPost("/recovery-code", async (GenerateRecoveryCodeRequest request, ClaimsPrincipal user,
            IAccountRecoveryService recovery, CancellationToken ct) => Result(await recovery.GenerateAsync(
                Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!),
                user.FindFirstValue("AspNet.Identity.SecurityStamp") ?? string.Empty, request, ct)))
            .RequireAuthorization().AddEndpointFilter<AntiforgeryFilter>().RequireRateLimiting("recovery-code")
            .Produces<RecoveryCodeResponse>().ProducesProblem(400).ProducesProblem(401)
            .WithSummary("Replace a saved recovery code after confirming the current password");

        auth.MapPost("/reset-password", async (ResetPasswordRequest request, IAccountRecoveryService recovery, CancellationToken ct) =>
            Result(await recovery.ResetAsync(request, ct)))
            .AddEndpointFilter<AntiforgeryFilter>().RequireRateLimiting("password-recovery")
            .Produces<RecoveryCodeResponse>().ProducesProblem(400)
            .WithSummary("Reset a local password with a saved single-use recovery code");
    }

    private static IResult Result(RecoveryResult result) => result.Outcome switch
    {
        RecoveryOutcome.Success => Results.Ok(new RecoveryCodeResponse(result.RecoveryCode!)),
        RecoveryOutcome.Unauthenticated => Results.Unauthorized(),
        _ when result.Errors is not null => Results.ValidationProblem(result.Errors),
        _ => Results.Problem(statusCode: 400, title: "Unable to recover account. Check your details or try again later.")
    };

    private sealed record RecoveryCodeResponse(string RecoveryCode);
}
