using System.Security.Claims;
using RateFreak.Api.Security;
using RateFreak.Application.Social;
using RateFreak.Domain.Social;

namespace RateFreak.Api.Endpoints;

internal sealed class CommentNoStore;

internal static class ReviewCommentEndpoints
{
    public static void MapReviewCommentEndpoints(this WebApplication app)
    {
        var list = app.MapGroup("/api/v1/reviews/{reviewId:guid}/comments").WithTags("Review comments").WithMetadata(new CommentNoStore());
        list.MapGet("", async (Guid reviewId, ClaimsPrincipal user, IReviewCommentService service, CancellationToken ct, int page = 1, int pageSize = 20) =>
        {
            var errors = new Dictionary<string, string[]>();
            if (page is < 1 or > 10000) errors["page"] = ["Page must be between 1 and 10000."];
            if (pageSize is < 1 or > 100) errors["pageSize"] = ["Page size must be between 1 and 100."];
            if (errors.Count != 0) return Results.ValidationProblem(errors);
            var result = await service.ListAsync(ViewerId(user), reviewId, page, pageSize, ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }).RequireRateLimiting("social-read").Produces<ReviewCommentPage>().ProducesValidationProblem().ProducesProblem(404).ProducesProblem(429)
            .WithSummary("List public review comments, oldest first");
        Write(list.MapPost("", async (Guid reviewId, ReviewCommentRequest request, ClaimsPrincipal user, IReviewCommentService service, CancellationToken ct) =>
        {
            if (Error(request) is { } error) return error;
            var result = await service.CreateAsync(UserId(user), reviewId, request, ct);
            return result is null ? Results.NotFound() : Results.Created($"/api/v1/comments/{result.Id}", result);
        })).Produces<ReviewCommentDetails>(201).ProducesProblem(404).WithSummary("Add a public plain-text comment to a review");
        var comments = app.MapGroup("/api/v1/comments/{id:guid}").WithTags("Review comments").WithMetadata(new CommentNoStore());
        comments.MapGet("", async (Guid id, ClaimsPrincipal user, IReviewCommentService service, CancellationToken ct) =>
        {
            var result = await service.FindAsync(ViewerId(user), id, ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }).RequireRateLimiting("social-read").Produces<ReviewCommentDetails>().ProducesProblem(404).ProducesProblem(429)
            .WithSummary("Get a public review comment");
        Write(comments.MapPatch("", async (Guid id, ReviewCommentRequest request, ClaimsPrincipal user, IReviewCommentService service, CancellationToken ct) =>
        {
            if (Error(request) is { } error) return error;
            var result = await service.UpdateAsync(UserId(user), id, request, ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        })).Produces<ReviewCommentDetails>().ProducesProblem(404).WithSummary("Replace your comment body and spoiler flag");
        Write(comments.MapDelete("", async (Guid id, ClaimsPrincipal user, IReviewCommentService service, CancellationToken ct) =>
            await service.DeleteAsync(UserId(user), id, ct) ? Results.NoContent() : Results.NotFound()))
            .Produces(204).ProducesProblem(404).WithSummary("Delete your own comment");
    }
    private static RouteHandlerBuilder Write(RouteHandlerBuilder builder) => builder.RequireAuthorization()
        .AddEndpointFilter<AntiforgeryFilter>().RequireRateLimiting("social-write").ProducesValidationProblem().ProducesProblem(401).ProducesProblem(429);
    private static Guid UserId(ClaimsPrincipal user) => Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private static IResult? Error(ReviewCommentRequest request) => ReviewComment.ValidBody(request.Body?.Trim()) ? null
        : Results.ValidationProblem(new Dictionary<string, string[]> { ["body"] = ["Body must contain 1–2000 valid plain-text characters; only tab and newline controls are allowed."] });
    private static Guid? ViewerId(ClaimsPrincipal user) => Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

}
