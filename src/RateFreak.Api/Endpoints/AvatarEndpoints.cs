using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Http.Features;
using RateFreak.Api.Security;
using RateFreak.Application.Identity;
using RateFreak.Application.Catalog;

namespace RateFreak.Api.Endpoints;

internal static class AvatarEndpoints
{
    private const long FileLimit = 5 * 1024 * 1024;
    public static void MapAvatarEndpoints(this WebApplication app)
    {
        var users = app.MapGroup("/api/v1/users").WithTags("Profiles").WithMetadata(new ProfileNoStore());
        users.MapGet("/{userId:guid}/avatar/{assetId:guid}", async (Guid userId, Guid assetId, IAvatarService service, HttpContext context, CancellationToken ct) =>
            Image(context, await service.ReadAsync(userId, assetId, ct))).RequireRateLimiting("profile-read");
        users.MapGet("/me/avatar/{assetId:guid}", async (Guid assetId, ClaimsPrincipal user, IAvatarService service, HttpContext context, CancellationToken ct) =>
            Image(context, await service.ReadOwnAsync(Actor(user), assetId, ct))).RequireAuthorization().RequireRateLimiting("profile-read");
        app.MapGet("/api/v1/moderation/reports/{reportId:guid}/avatar/{assetId:guid}", async (Guid reportId, Guid assetId, ClaimsPrincipal user, IAvatarService service, HttpContext context, CancellationToken ct) =>
            Image(context, await service.ReadForReportAsync(Actor(user), reportId, assetId, ct))).RequireAuthorization("moderation").RequireRateLimiting("moderation-read");
        app.MapGet("/api/v1/moderation/profiles/{userId:guid}/avatar/{assetId:guid}", async (Guid userId, Guid assetId, ClaimsPrincipal user, IAvatarService service, HttpContext context, CancellationToken ct) =>
            Image(context, await service.ReadForProfileAsync(Actor(user), userId, assetId, ct))).RequireAuthorization("moderation").RequireRateLimiting("moderation-read");
        users.MapPut("/me/avatar", async (ClaimsPrincipal user, IAvatarService service, HttpContext context, CancellationToken ct) =>
        {
            if (!ExpectedAccount(context, user)) return Results.Conflict();
            var limit = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
            if (limit is { IsReadOnly: false }) limit.MaxRequestBodySize = FileLimit + 65536;
            if (context.Request.ContentLength > FileLimit + 65536) return Results.StatusCode(413);
            if (!context.Request.HasFormContentType || context.Request.ContentType?.StartsWith("multipart/form-data", StringComparison.OrdinalIgnoreCase) != true) return Results.StatusCode(415);
            IFormCollection form;
            try { form = await context.Request.ReadFormAsync(new FormOptions { MultipartBodyLengthLimit = FileLimit + 65536, ValueLengthLimit = 64, ValueCountLimit = 2, MultipartHeadersLengthLimit = 4096, MemoryBufferThreshold = 65536 }, ct); }
            catch (InvalidDataException) { return Results.StatusCode(413); }
            if (form.Count != 1 || form.Keys.Single() != "expectedRevision" || form["expectedRevision"].Count != 1 ||
                !long.TryParse(form["expectedRevision"], NumberStyles.None, CultureInfo.InvariantCulture, out var revision) || revision < 0 ||
                form.Files.Count != 1 || form.Files[0].Name != "file") return Results.BadRequest();
            if (form.Files[0].Length > FileLimit) return Results.StatusCode(413);
            await using var stream = form.Files[0].OpenReadStream();
            return Result(await service.PutAsync(Actor(user), revision, stream, ct));
        }).RequireAuthorization().AddEndpointFilter<AntiforgeryFilter>().RequireRateLimiting("profile-write").Produces<AvatarState>();
        users.MapDelete("/me/avatar", async ([Microsoft.AspNetCore.Mvc.FromBody] RemoveAvatarRequest request, ClaimsPrincipal user, IAvatarService service, HttpContext context, CancellationToken ct) =>
            !ExpectedAccount(context, user) ? Results.Conflict() : request.ExpectedRevision is null or < 0 ? Results.BadRequest() : Result(await service.DeleteAsync(Actor(user), request.ExpectedRevision.Value, ct)))
            .RequireAuthorization().AddEndpointFilter<AntiforgeryFilter>().RequireRateLimiting("profile-write").Produces<AvatarState>();
    }
    private static bool ExpectedAccount(HttpContext context, ClaimsPrincipal user)
    {
        var values = context.Request.Headers["X-RateFreak-Account-Id"];
        return values.Count == 1 && Guid.TryParseExact(values[0], "D", out var expected) && expected == Actor(user);
    }
    private static Guid Actor(ClaimsPrincipal user) => Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private static IResult Image(HttpContext context, ArtworkRead? read)
    {
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers.XContentTypeOptions = "nosniff";
        return read is null ? Results.NotFound() : Results.Stream(read.Content, read.ContentType);
    }
    private static IResult Result(AvatarResult result) => result.Status switch
    {
        AvatarStatus.Success => Results.Ok(result.Value), AvatarStatus.Conflict => Results.Conflict(),
        AvatarStatus.NotFound => Results.NotFound(), AvatarStatus.Unavailable => Results.Problem(statusCode: 503, title: "Avatar storage is unavailable."),
        _ => Results.ValidationProblem(new Dictionary<string, string[]> { ["file"] = ["Choose a static JPEG, PNG or WebP image, at most 5 MiB and within supported dimensions."] })
    };
}
