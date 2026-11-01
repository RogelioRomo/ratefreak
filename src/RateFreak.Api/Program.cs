using Microsoft.AspNetCore.Identity;
using System.Diagnostics;
using RateFreak.Api.Endpoints;
using RateFreak.Api.Documentation;
using RateFreak.Api.Middleware;
using RateFreak.Api.Security;
using RateFreak.Infrastructure;

var appointAdmin = args.Contains("--appoint-admin", StringComparer.Ordinal);
if (appointAdmin && (args.Length != 4 || args[0] != "--appoint-admin" || args[2] != "--reason" ||
    !Guid.TryParse(args[1], out var requestedAccount) || requestedAccount == Guid.Empty ||
    !RateFreak.Application.Moderation.ModerationRules.Text(args[3])))
{
    Console.Error.WriteLine("Usage: --appoint-admin <existing-account-uuid> --reason <reason>");
    Environment.ExitCode = 2;
    return;
}
if (appointAdmin)
{
    // Build a maintenance host without starting it: no HTTP listener or automatic migration.
    var maintenance = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
    {
        Args = args,
        EnvironmentName = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
    });
    maintenance.Logging.ClearProviders();
    maintenance.Services.AddInfrastructure(maintenance.Configuration);
    maintenance.Services.AddAuthentication(Microsoft.AspNetCore.Identity.IdentityConstants.ApplicationScheme).AddIdentityCookies();
    using var host = maintenance.Build();
    await using var scope = host.Services.CreateAsyncScope();
    try
    {
        var result = await scope.ServiceProvider.GetRequiredService<RateFreak.Application.Moderation.IModerationService>()
            .AppointAdminAsync(Guid.Parse(args[1]), args[3], CancellationToken.None);
        Environment.ExitCode = result.Status == RateFreak.Application.Moderation.ModerationStatus.Success ? 0 : 2;
        Console.WriteLine(Environment.ExitCode == 0 ? "Admin appointment complete." : "Admin appointment rejected.");
    }
    catch
    {
        Console.Error.WriteLine("Admin appointment failed. Check database configuration and schema.");
        Environment.ExitCode = 1;
    }
    return;
}
var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options => options.IncludeScopes = true);
builder.Services.AddInfrastructure(builder.Configuration);
builder.AddApiSecurity();
builder.AddExternalAuthentication();
builder.Services.AddOpenApi();
// Reject ambiguous DTO properties before last-value binding can hide invalid data.
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.AllowDuplicateProperties = false);
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
    context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier);
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

var app = builder.Build();

app.Use(async (context, next) =>
{
    using var scope = app.Logger.BeginScope(new Dictionary<string, object?>
    {
        ["TraceId"] = context.TraceIdentifier,
        ["ActivityId"] = Activity.Current?.TraceId.ToString()
    });
    await next(context);
});
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseBrowserProxyIdentity();
app.Use(async (context, next) =>
{
    if (context.GetEndpoint()?.Metadata.GetMetadata<ProfileNoStore>() is not null || context.GetEndpoint()?.Metadata.GetMetadata<CollectionNoStore>() is not null || context.GetEndpoint()?.Metadata.GetMetadata<FollowNoStore>() is not null || context.GetEndpoint()?.Metadata.GetMetadata<CommentNoStore>() is not null || context.Request.Path.StartsWithSegments("/api/v1/moderation") || context.Request.Path.StartsWithSegments("/api/v1/admin") || context.GetEndpoint()?.Metadata.GetMetadata<ReportingNoStore>() is not null || context.Request.Path.StartsWithSegments("/api/v1/auth") || context.Request.Path.StartsWithSegments("/api/v1/users/me") ||
        (context.Request.Path.StartsWithSegments("/api/v1/media") && (context.Request.Path.Value!.EndsWith("/rating", StringComparison.Ordinal) || context.Request.Path.Value!.EndsWith("/review", StringComparison.Ordinal))))
        context.Response.Headers.CacheControl = "no-store";
    await next(context);
});
app.UseExternalAuthenticationGuard();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapDevelopmentDocumentation();
app.MapHealthEndpoints();
app.MapAccountEndpoints();
app.MapUserProfileEndpoints();
app.MapExternalAccountEndpoints();
app.MapCatalogEndpoints();
app.MapMediaProviderEndpoints();
app.MapPersonalMediaEndpoints();
app.MapCollectionEndpoints();
app.MapFollowEndpoints();
app.MapReviewCommentEndpoints();
app.MapModerationEndpoints();
app.Run();

public partial class Program;
