using System.Security.Claims;
using RateFreak.Api.Security;
using RateFreak.Application.Social;
namespace RateFreak.Api.Endpoints;
internal sealed class NotificationNoStore;
internal static class NotificationEndpoints
{
    private static Guid Recipient(ClaimsPrincipal user) => Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
    public static void MapNotificationEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/notifications").WithTags("Notifications").RequireAuthorization().WithMetadata(new NotificationNoStore());
        group.MapGet("", async (ClaimsPrincipal user, INotificationService service, CancellationToken ct, int page = 1, int pageSize = 20) => {
            if (page is < 1 or > 10000 || pageSize is < 1 or > 100) return Results.ValidationProblem(new Dictionary<string,string[]> { ["page"] = ["Page must be 1–10000 and page size 1–100."] });
            return Results.Ok(await service.ListAsync(Recipient(user), page, pageSize, ct));
        }).RequireRateLimiting("social-read").Produces<NotificationPage>().ProducesValidationProblem();
        group.MapGet("/unread-count", async (ClaimsPrincipal user, INotificationService service, CancellationToken ct) =>
            Results.Ok(new { unreadCount = await service.UnreadCountAsync(Recipient(user), ct) })).RequireRateLimiting("social-read");
        Write(group.MapPut("/{id:guid}/read", async (Guid id, ClaimsPrincipal user, INotificationService service, CancellationToken ct) =>
            await service.SetReadAsync(Recipient(user), id, true, ct) ? Results.NoContent() : Results.NotFound()));
        Write(group.MapDelete("/{id:guid}/read", async (Guid id, ClaimsPrincipal user, INotificationService service, CancellationToken ct) =>
            await service.SetReadAsync(Recipient(user), id, false, ct) ? Results.NoContent() : Results.NotFound()));
        Write(group.MapDelete("/{id:guid}", async (Guid id, ClaimsPrincipal user, INotificationService service, CancellationToken ct) =>
            await service.DeleteAsync(Recipient(user), id, ct) ? Results.NoContent() : Results.NotFound()));
        Write(group.MapPost("/read-all", async (ClaimsPrincipal user, INotificationService service, CancellationToken ct) => {
            await service.ReadAllAsync(Recipient(user), ct); return Results.NoContent();
        }));
        Write(group.MapDelete("", async (ClaimsPrincipal user, INotificationService service, CancellationToken ct) => {
            await service.ClearAsync(Recipient(user), ct); return Results.NoContent();
        }));
    }
    private static void Write(RouteHandlerBuilder builder) => builder.AddEndpointFilter<AntiforgeryFilter>().AddEndpointFilter<NotificationBodyFilter>()
        .RequireRateLimiting("social-write").Produces(204).ProducesProblem(401).ProducesProblem(404).ProducesProblem(429);
}
