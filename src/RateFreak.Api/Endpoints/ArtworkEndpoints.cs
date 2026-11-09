using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Http.Features;
using RateFreak.Api.Security;
using RateFreak.Application.Catalog;

namespace RateFreak.Api.Endpoints;

internal static class ArtworkEndpoints
{
    public static void MapArtworkEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v1/media/{id:guid}/artwork/{assetId:guid}", async (Guid id, Guid assetId, ClaimsPrincipal user, IArtworkService service, HttpContext context, CancellationToken ct) =>
        {
            context.Response.Headers.XContentTypeOptions = "nosniff";
            var artwork = await service.ReadAsync(CategoryEndpoints.Viewer(user), id, assetId, ct);
            return artwork is null ? Results.NotFound() : Results.Stream(artwork.Content, artwork.ContentType);
        }).RequireRateLimiting("catalog");
        var staff = app.MapGroup("/api/v1/moderation").RequireAuthorization("moderation").RequireRateLimiting("moderation-read");
        staff.MapGet("/media/{id:guid}/artwork/{assetId:guid}", async (Guid id, Guid assetId, ClaimsPrincipal user, IArtworkService service, HttpContext context, CancellationToken ct) =>
        {
            context.Response.Headers.XContentTypeOptions = "nosniff";
            var artwork = await service.ReadPublicForStaffAsync(CategoryEndpoints.Viewer(user)!.Value, id, assetId, ct);
            return artwork is null ? Results.NotFound() : Results.Stream(artwork.Content, artwork.ContentType);
        });
        staff.MapGet("/reports/{reportId:guid}/artwork/{assetId:guid}", async (Guid reportId, Guid assetId, ClaimsPrincipal user, IArtworkService service, HttpContext context, CancellationToken ct) =>
        {
            context.Response.Headers.XContentTypeOptions = "nosniff";
            var artwork = await service.ReadForReportAsync(CategoryEndpoints.Viewer(user)!.Value, reportId, assetId, ct);
            return artwork is null ? Results.NotFound() : Results.Stream(artwork.Content, artwork.ContentType);
        });
        app.MapPut("/api/v1/media/{id:guid}/artwork", async (Guid id, ClaimsPrincipal user, IArtworkService service, HttpContext context, CancellationToken ct) =>
        {
            var limit = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
            if (limit is { IsReadOnly: false }) limit.MaxRequestBodySize = 5 * 1024 * 1024 + 65536;
            if (context.Request.ContentLength > 5 * 1024 * 1024 + 65536) return Results.StatusCode(413);
            if (!context.Request.HasFormContentType || context.Request.ContentType?.StartsWith("multipart/form-data", StringComparison.OrdinalIgnoreCase) != true) return Results.StatusCode(415);
            IFormCollection form;
            try { form = await context.Request.ReadFormAsync(new FormOptions { MultipartBodyLengthLimit = 5 * 1024 * 1024 + 65536, ValueLengthLimit = 64, ValueCountLimit = 2, MultipartHeadersLengthLimit = 4096, MemoryBufferThreshold = 65536 }, ct); }
            catch (InvalidDataException) { return Results.StatusCode(413); }
            if (form.Count != 1 || form.Keys.Single() != "expectedRevision" || form["expectedRevision"].Count != 1 || !long.TryParse(form["expectedRevision"], NumberStyles.None, CultureInfo.InvariantCulture, out var revision) || revision <= 0 || form.Files.Count != 1 || form.Files[0].Name != "file") return Results.BadRequest();
            if (form.Files[0].Length > 5 * 1024 * 1024) return Results.StatusCode(413);
            await using var stream = form.Files[0].OpenReadStream();
            return CategoryEndpoints.Result(await service.PutAsync(CategoryEndpoints.Viewer(user)!.Value, id, revision, stream, ct));
        }).RequireAuthorization().AddEndpointFilter<AntiforgeryFilter>().RequireRateLimiting("personal-write");
        app.MapDelete("/api/v1/media/{id:guid}/artwork", async (Guid id, [Microsoft.AspNetCore.Mvc.FromBody] ItemStateChange request, ClaimsPrincipal user, IArtworkService service, CancellationToken ct) =>
            request.ExpectedRevision is null or <= 0 ? Results.BadRequest() : CategoryEndpoints.Result(await service.DeleteAsync(CategoryEndpoints.Viewer(user)!.Value, id, request.ExpectedRevision.Value, ct)))
            .RequireAuthorization().AddEndpointFilter<AntiforgeryFilter>().RequireRateLimiting("personal-write");
    }
}
