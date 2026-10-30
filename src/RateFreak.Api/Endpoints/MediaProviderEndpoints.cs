using System.Globalization;
using RateFreak.Api.Security;
using RateFreak.Application.MetadataImport;

namespace RateFreak.Api.Endpoints;

internal static class MediaProviderEndpoints
{
    public static void MapMediaProviderEndpoints(this WebApplication app)
    {
        var media = app.MapGroup("/api/v1/media").WithTags("Metadata providers");
        media.MapGet("/providers", (IMediaProviderRegistry providers) => Results.Ok(providers.Capabilities))
            .RequireRateLimiting("catalog").Produces<ProviderCapability[]>().WithSummary("List configured provider capabilities");
        media.MapGet("/external/search", async (IMediaProviderRegistry providers, HttpContext context, CancellationToken cancellationToken,
            string? q = null, string? type = null, int page = 1, string provider = "tmdb") =>
        {
            q = q?.Trim();
            var errors = new Dictionary<string, string[]>();
            if (!RateFreak.Domain.Collections.CollectionRules.ValidText(q, 200))
                errors["q"] = ["Search must contain 1–200 characters without control characters."];
            var adapter = providers.Find(provider);
            if (adapter is null) errors["provider"] = ["Unknown metadata provider."];
            else if (type is null || !adapter.Capability.SupportedTypes.Contains(type)) errors["type"] = ["Type is not supported by this provider."];
            if (page is < 1 or > 500) errors["page"] = ["Page must be between 1 and 500."];
            if (errors.Count > 0) return Results.ValidationProblem(errors);
            return await ProviderResult(async () => Results.Ok(await adapter!.SearchAsync(q!, type!, page, cancellationToken)), context);
        }).RequireRateLimiting("external-search").Produces<ExternalSearchPage>().ProducesValidationProblem()
            .ProducesProblem(502).ProducesProblem(503).ProducesProblem(504).WithSummary("Search the selected metadata provider (default TMDB)");
        media.MapGet("/external/{provider}/{type}/{externalId}", async (string provider, string type, string externalId,
            IMediaProviderRegistry providers, HttpContext context, CancellationToken cancellationToken) =>
        {
            var adapter = providers.Find(provider);
            if (adapter is null) return Results.NotFound();
            var validation = Validate(provider, type, externalId, out var id);
            if (validation is not null) return validation;
            return await ProviderResult(async () =>
            {
                var result = await adapter.GetDetailsAsync(id, type, cancellationToken);
                return result is null ? Results.NotFound() : Results.Ok(result);
            }, context);
        }).RequireRateLimiting("external-search").Produces<ExternalMediaResult>().ProducesValidationProblem()
            .ProducesProblem(404).ProducesProblem(502).ProducesProblem(503).ProducesProblem(504)
            .WithSummary("Preview authoritative external media details");
        media.MapPost("/import", async (ImportMediaRequest request, IMediaImportService imports,
            HttpContext context, CancellationToken cancellationToken) =>
        {
            if (request.Provider is not ("tmdb" or "musicbrainz" or "igdb")) return Results.ValidationProblem(new Dictionary<string, string[]> { ["provider"] = ["Unknown metadata provider."] });
            var validation = Validate(request.Provider, request.MediaType, request.ExternalId, out var id);
            if (validation is not null) return validation;
            return await ProviderResult(async () =>
            {
                var result = await imports.ImportAsync(request.Provider!, id, request.MediaType!, cancellationToken);
                if (result is null) return Results.NotFound();
                return result.Created ? Results.Created($"/api/v1/media/{result.MediaId}", result) : Results.Ok(result);
            }, context);
        }).RequireAuthorization().AddEndpointFilter<AntiforgeryFilter>().RequireRateLimiting("media-import")
            .Produces<ImportMediaResult>(201).Produces<ImportMediaResult>().ProducesValidationProblem()
            .ProducesProblem(401).ProducesProblem(404).ProducesProblem(429).ProducesProblem(502).ProducesProblem(503).ProducesProblem(504)
            .WithSummary("Import provider media into the canonical catalog");
    }

    private static IResult? Validate(string? provider, string? type, string? externalId, out string id)
    {
        if (MediaProviderInput.TryIdentity(provider, type, externalId, out var identity)) { id=identity.ExternalId; return null; }
        id="";
        return Results.ValidationProblem(new Dictionary<string,string[]> { ["externalId"] = ["Invalid media type or external ID for the selected provider."] });
    }

    private static async Task<IResult> ProviderResult(Func<Task<IResult>> operation, HttpContext? context = null)
    {
        try { return await operation(); }
        catch (MediaProviderException error)
        {
            if (error.RetryAfterSeconds is { } retry && context is not null)
                context.Response.Headers.RetryAfter = retry.ToString(CultureInfo.InvariantCulture);
            var (status, title) = error.Failure switch
            {
                ProviderFailure.InvalidResponse => (502, "Metadata provider returned an invalid response."),
                ProviderFailure.Timeout => (504, "Metadata provider request timed out."),
                ProviderFailure.Disabled => (503, "Metadata provider is not enabled."),
                _ => (503, "Metadata provider is temporarily unavailable.")
            };
            return Results.Problem(statusCode: status, title: title);
        }
    }
}
