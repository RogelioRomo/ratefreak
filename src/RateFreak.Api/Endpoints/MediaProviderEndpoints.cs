using System.Globalization;
using RateFreak.Api.Security;
using RateFreak.Application.MetadataImport;

namespace RateFreak.Api.Endpoints;

internal static class MediaProviderEndpoints
{
    public static void MapMediaProviderEndpoints(this WebApplication app)
    {
        var media = app.MapGroup("/api/v1/media").WithTags("Metadata providers");
        media.MapGet("/providers", (IMediaProvider provider) => Results.Ok(new[] { provider.Capability }))
            .RequireRateLimiting("catalog").Produces<ProviderCapability[]>().WithSummary("List configured provider capabilities");
        media.MapGet("/external/search", async (IMediaProvider provider, HttpContext context, CancellationToken cancellationToken,
            string? q = null, string? type = null, int page = 1) =>
        {
            q = q?.Trim();
            var errors = new Dictionary<string, string[]>();
            if (string.IsNullOrWhiteSpace(q) || q.Length > 200 || q.Any(char.IsControl))
                errors["q"] = ["Search must contain 1–200 characters without control characters."];
            if (!MediaProviderInput.ValidType(type)) errors["type"] = ["Type must be movie or tv_show."];
            if (page is < 1 or > 500) errors["page"] = ["Page must be between 1 and 500."];
            if (errors.Count > 0) return Results.ValidationProblem(errors);
            return await ProviderResult(async () => Results.Ok(await provider.SearchAsync(q!, type!, page, cancellationToken)), context);
        }).RequireRateLimiting("external-search").Produces<ExternalSearchPage>().ProducesValidationProblem()
            .ProducesProblem(502).ProducesProblem(503).ProducesProblem(504).WithSummary("Search TMDB movies or TV series");
        media.MapGet("/external/{provider}/{type}/{externalId}", async (string provider, string type, string externalId,
            IMediaProvider adapter, HttpContext context, CancellationToken cancellationToken) =>
        {
            if (provider != adapter.Capability.Name) return Results.NotFound();
            var validation = Validate(type, externalId, out var id);
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
            if (request.Provider != "tmdb") return Results.ValidationProblem(new Dictionary<string, string[]> { ["provider"] = ["Provider must be tmdb."] });
            var validation = Validate(request.MediaType, request.ExternalId, out var id);
            if (validation is not null) return validation;
            return await ProviderResult(async () =>
            {
                var result = await imports.ImportAsync(id, request.MediaType!, cancellationToken);
                if (result is null) return Results.NotFound();
                return result.Created ? Results.Created($"/api/v1/media/{result.MediaId}", result) : Results.Ok(result);
            }, context);
        }).RequireAuthorization().AddEndpointFilter<AntiforgeryFilter>().RequireRateLimiting("media-import")
            .Produces<ImportMediaResult>(201).Produces<ImportMediaResult>().ProducesValidationProblem()
            .ProducesProblem(401).ProducesProblem(404).ProducesProblem(429).ProducesProblem(502).ProducesProblem(503).ProducesProblem(504)
            .WithSummary("Import provider media into the canonical catalog");
    }

    private static IResult? Validate(string? type, string? externalId, out string id)
    {
        var validId = MediaProviderInput.TryId(externalId, out id);
        var errors = new Dictionary<string, string[]>();
        if (!MediaProviderInput.ValidType(type)) errors["mediaType"] = ["Type must be movie or tv_show."];
        if (!validId) errors["externalId"] = ["External ID must be a positive 32-bit decimal integer."];
        return errors.Count == 0 ? null : Results.ValidationProblem(errors);
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
