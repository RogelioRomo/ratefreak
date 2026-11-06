using System.Data.Common;
using RateFreak.Api.Security;
using RateFreak.Application.Identity;
using RateFreak.Application.Installation;

namespace RateFreak.Api.Endpoints;

internal static class InstallationEndpoints
{
    public static void MapInstallationEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v1/setup/status", async (IInstallationService service, CancellationToken ct) =>
        {
            try { return await service.StatusAsync(ct) is { } state ? Results.Ok(state) : Unavailable(); }
            catch (Exception exception) when (exception is DbException or InvalidOperationException { InnerException: DbException }) { return Unavailable(); }
        }).RequireRateLimiting("setup-read").WithTags("Installation").Produces<InstallationStatus>().ProducesProblem(503);
        app.MapPost("/api/v1/setup", async (SetupRequest request, IInstallationService service, CancellationToken ct) =>
        {
            SetupResult result;
            try { result = await service.CompleteAsync(request,ct); }
            catch (Exception exception) when (exception is DbException or InvalidOperationException { InnerException: DbException }) { return Unavailable(); }
            return result.Outcome switch
            {
                SetupOutcome.Success => Results.Json(result.Account,statusCode:201),
                SetupOutcome.Completed => Results.Problem(statusCode:409,title:"Installation setup is already complete."),
                SetupOutcome.Unavailable => Unavailable(),
                _ when result.Errors is not null => Results.ValidationProblem(result.Errors),
                _ => Results.Problem(statusCode:400,title:"Unable to complete setup. Check your details.")
            };
        }).AddEndpointFilter<AntiforgeryFilter>().RequireRateLimiting("setup-write").WithTags("Installation")
            .Produces<LocalRegistrationResponse>(201).ProducesProblem(400).ProducesProblem(409).ProducesProblem(503);
    }
    private static IResult Unavailable() => Results.Problem(statusCode:503,title:"Installation setup is unavailable. Check server configuration.");
}
