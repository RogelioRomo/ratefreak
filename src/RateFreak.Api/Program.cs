using System.Diagnostics;
using RateFreak.Api.Endpoints;
using RateFreak.Api.Documentation;
using RateFreak.Api.Middleware;
using RateFreak.Api.Security;
using RateFreak.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options => options.IncludeScopes = true);
builder.Services.AddInfrastructure(builder.Configuration);
builder.AddApiSecurity();
builder.Services.AddOpenApi();
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
app.Use(async (context, next) =>
{
    if (context.GetEndpoint()?.Metadata.GetMetadata<CollectionNoStore>() is not null || context.Request.Path.StartsWithSegments("/api/v1/auth") || context.Request.Path.StartsWithSegments("/api/v1/users/me") ||
        (context.Request.Path.StartsWithSegments("/api/v1/media") && context.Request.Path.Value!.EndsWith("/rating", StringComparison.Ordinal)))
        context.Response.Headers.CacheControl = "no-store";
    await next(context);
});
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapDevelopmentDocumentation();
app.MapHealthEndpoints();
app.MapAccountEndpoints();
app.MapCatalogEndpoints();
app.MapMediaProviderEndpoints();
app.MapPersonalMediaEndpoints();
app.MapCollectionEndpoints();
app.Run();

public partial class Program;
