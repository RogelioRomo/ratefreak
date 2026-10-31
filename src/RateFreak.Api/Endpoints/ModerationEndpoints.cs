using System.Security.Claims;
using RateFreak.Api.Security;
using RateFreak.Application.Moderation;

namespace RateFreak.Api.Endpoints;

internal sealed class ReportingNoStore;

internal static class ModerationEndpoints
{
    public static void MapModerationEndpoints(this WebApplication app)
    {
        foreach (var (segment, type) in new[] { ("reviews", "review"), ("comments", "comment") })
        {
            app.MapPost($"/api/v1/{segment}/{{id:guid}}/reports", async (Guid id, ReportRequest request, ClaimsPrincipal user, IModerationService service, CancellationToken ct) =>
            {
                var result = await service.ReportAsync(Actor(user), type, id, request, ct);
                return result.Status == ModerationStatus.Success ? Results.Created((string?)null, result.Value) : Result(result);
            }).WithMetadata(new ReportingNoStore()).WithTags("Reporting").RequireAuthorization().AddEndpointFilter<AntiforgeryFilter>().RequireRateLimiting("reporting")
                .Produces<ReportAcknowledgement>(201).ProducesValidationProblem().ProducesProblem(401).ProducesProblem(404).ProducesProblem(409).ProducesProblem(429);
        }
        var staff = app.MapGroup("/api/v1/moderation").WithTags("Moderation").RequireAuthorization("moderation");
        staff.MapGet("/reports", async (IModerationService service, CancellationToken ct, string status = "open", int page = 1, int pageSize = 20) =>
            !Paging(page, pageSize) || status is not ("open" or "resolved") ? Invalid() : Results.Ok(await service.ReportsAsync(status, page, pageSize, ct)))
            .RequireRateLimiting("moderation-read").Produces<ModerationPage<ReportSummary>>();
        staff.MapGet("/reports/{id:guid}", async (Guid id, IModerationService service, CancellationToken ct) =>
            await service.ReportAsync(id, ct) is { } result ? Results.Ok(result) : Results.NotFound()).RequireRateLimiting("moderation-read").Produces<ReportDetail>();
        Write(staff.MapPost("/reports/{id:guid}/resolve", async (Guid id, ResolveRequest request, ClaimsPrincipal user, IModerationService service, CancellationToken ct) =>
            Result(await service.ResolveAsync(Actor(user), id, request, ct))));
        foreach (var (segment, type) in new[] { ("reviews", "review"), ("comments", "comment") })
        {
            staff.MapGet($"/{segment}/{{id:guid}}", async (Guid id, IModerationService service, CancellationToken ct) =>
                await service.TargetAsync(type, id, ct) is { } result ? Results.Ok(result) : Results.NotFound()).RequireRateLimiting("moderation-read").Produces<ModerationTarget>();
            Write(staff.MapPost($"/{segment}/{{id:guid}}/visibility", async (Guid id, VisibilityRequest request, ClaimsPrincipal user, IModerationService service, CancellationToken ct) =>
                Result(await service.VisibilityAsync(Actor(user), type, id, request, ct))));
        }
        staff.MapGet("/audit", async (IModerationService service, CancellationToken ct, string? targetType = null, Guid? targetId = null, int page = 1, int pageSize = 20) =>
            !Paging(page, pageSize) || targetType is not (null or "review" or "comment" or "account") ? Invalid() : Results.Ok(await service.AuditAsync(targetType, targetId, page, pageSize, ct)))
            .RequireRateLimiting("moderation-read").Produces<ModerationPage<ModerationAuditEntry>>();
        var admin = app.MapGroup("/api/v1/admin/moderators").WithTags("Moderator administration").RequireAuthorization("admin");
        foreach (var verb in new[] { "PUT", "DELETE" })
            admin.MapMethods("/{id:guid}", [verb], async (Guid id, [Microsoft.AspNetCore.Mvc.FromBody] RoleRequest request, ClaimsPrincipal user, IModerationService service, CancellationToken ct) =>
                Result(await service.ModeratorAsync(Actor(user), id, verb == "PUT", request.Reason ?? "", ct)))
                .AddEndpointFilter<AntiforgeryFilter>().RequireRateLimiting("role-write").Produces(204).ProducesValidationProblem().ProducesProblem(401).ProducesProblem(403).ProducesProblem(404).ProducesProblem(429);
    }
    private static RouteHandlerBuilder Write(RouteHandlerBuilder b) => b.AddEndpointFilter<AntiforgeryFilter>().RequireRateLimiting("moderation-write")
        .Produces(204).ProducesValidationProblem().ProducesProblem(401).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409).ProducesProblem(429);
    private static bool Paging(int p, int size) => p is >= 1 and <= 10000 && size is >= 1 and <= 100;
    private static Guid Actor(ClaimsPrincipal user) => Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private static IResult Invalid() => Results.ValidationProblem(new Dictionary<string, string[]> { { "request", ["Invalid request values."] } });
    private static IResult Result(ModerationResult r) => r.Status switch
    {
        ModerationStatus.Success => Results.NoContent(),
        ModerationStatus.NotFound => Results.NotFound(),
        ModerationStatus.Forbidden => Results.StatusCode(403),
        ModerationStatus.Conflict => Results.Conflict(),
        _ => Invalid()
    };
}
