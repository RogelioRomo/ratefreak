using System.Security.Claims;
using RateFreak.Api.Security;
using RateFreak.Application.PersonalMedia;
using RateFreak.Domain.Catalog;
using RateFreak.Domain.PersonalMedia;

namespace RateFreak.Api.Endpoints;

internal static class PersonalMediaEndpoints
{
    public static void MapPersonalMediaEndpoints(this WebApplication app)
    {
        MapLibrary(app);
        MapRatings(app);
        MapReviews(app);
    }

    private static void MapLibrary(WebApplication app)
    {
        var library = app.MapGroup("/api/v1/users/me/library").WithTags("Personal library").RequireAuthorization();
        library.MapGet("", async (ClaimsPrincipal user, ILibraryService service, CancellationToken ct,
            string? status = null, string? type = null, int page = 1, int pageSize = 20) =>
        {
            var errors = PageErrors(page, pageSize);
            if (status is not null && !PersonalMediaRules.ValidStatus(status)) errors["status"] = ["Unsupported tracking status."];
            if (type is not null && !MediaTypes.IsSupported(type)) errors["type"] = ["Type must be movie or tv_show."];
            if (errors.Count != 0) return Results.ValidationProblem(errors);
            return Results.Ok(await service.ListAsync(UserId(user), new(status, type, page, pageSize), ct));
        }).RequireRateLimiting("personal-read").Produces<PersonalPage<LibraryItem>>().ProducesValidationProblem()
            .ProducesProblem(401).ProducesProblem(429).WithSummary("List your private tracking library");
        Write(library.MapPut("/{mediaId:guid}", async (Guid mediaId, LibraryRequest request, ClaimsPrincipal user,
            ILibraryService service, CancellationToken ct) =>
        {
            var errors = new Dictionary<string, string[]>();
            if (!PersonalMediaRules.ValidStatus(request.Status)) errors["status"] = ["Status must be planned, in_progress, completed, paused or dropped."];
            if (!PersonalMediaRules.ValidDates(request.StartedAt, request.CompletedAt)) errors["completedAt"] = ["Completion must not precede start."];
            if (errors.Count != 0) return Results.ValidationProblem(errors);
            var result = await service.PutAsync(UserId(user), mediaId, request, ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        })).Produces<LibraryItem>().ProducesValidationProblem().ProducesProblem(404)
            .WithSummary("Replace your tracking status and optional dates");
        Write(library.MapDelete("/{mediaId:guid}", async (Guid mediaId, ClaimsPrincipal user, ILibraryService service, CancellationToken ct) =>
        {
            await service.DeleteAsync(UserId(user), mediaId, ct); return Results.NoContent();
        })).Produces(204).WithSummary("Remove a library entry without deleting ratings or reviews");
    }

    private static void MapRatings(WebApplication app)
    {
        var media = app.MapGroup("/api/v1/media/{mediaId:guid}").WithTags("Ratings");
        media.MapGet("/ratings", async (Guid mediaId, IRatingService service, CancellationToken ct) =>
        {
            var result = await service.AggregateAsync(mediaId, ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }).RequireRateLimiting("catalog").Produces<RatingAggregate>().ProducesProblem(404).ProducesProblem(429)
            .WithSummary("Get the count and average of local community ratings (0–5)");
        media.MapGet("/rating", async (Guid mediaId, ClaimsPrincipal user, IRatingService service, CancellationToken ct) =>
        {
            var result = await service.FindAsync(UserId(user), mediaId, ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }).RequireAuthorization().RequireRateLimiting("personal-read").Produces<RatingDetails>().ProducesProblem(401).ProducesProblem(404).ProducesProblem(429)
            .WithSummary("Get your own rating; 404 when unrated");
        Write(media.MapPut("/rating", async (Guid mediaId, RatingRequest request, ClaimsPrincipal user, IRatingService service, CancellationToken ct) =>
        {
            if (!PersonalMediaRules.ValidScore(request.Score)) return Results.ValidationProblem(new Dictionary<string, string[]>
                { ["score"] = ["Score is required and must be 0–5 in increments of 0.5."] });
            var result = await service.PutAsync(UserId(user), mediaId, request.Score!.Value, ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        })).Produces<RatingDetails>().ProducesValidationProblem().ProducesProblem(404).WithSummary("Set your rating from 0 to 5 in half-point steps");
        Write(media.MapDelete("/rating", async (Guid mediaId, ClaimsPrincipal user, IRatingService service, CancellationToken ct) =>
        {
            await service.DeleteAsync(UserId(user), mediaId, ct); return Results.NoContent();
        })).Produces(204).WithSummary("Remove your rating");
    }

    private static void MapReviews(WebApplication app)
    {
        var media = app.MapGroup("/api/v1/media/{mediaId:guid}/reviews").WithTags("Reviews");
        media.MapGet("", async (Guid mediaId, IReviewService service, CancellationToken ct, int page = 1, int pageSize = 20) =>
        {
            var errors = PageErrors(page, pageSize);
            if (errors.Count != 0) return Results.ValidationProblem(errors);
            var result = await service.ListAsync(mediaId, page, pageSize, ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }).RequireRateLimiting("catalog").Produces<PersonalPage<ReviewDetails>>().ProducesValidationProblem().ProducesProblem(404).ProducesProblem(429)
            .WithSummary("List public plain-text reviews, newest first");
        Write(media.MapPost("", async (Guid mediaId, ReviewRequest request, ClaimsPrincipal user, IReviewService service, CancellationToken ct) =>
        {
            if (ReviewError(request) is { } error) return error;
            var result = await service.CreateAsync(UserId(user), mediaId, request, ct);
            return result.Status switch
            {
                ReviewWriteStatus.NotFound => Results.NotFound(),
                ReviewWriteStatus.Conflict => Results.Problem(statusCode: 409, title: "You already reviewed this media item."),
                _ => Results.Created($"/api/v1/reviews/{result.Review!.Id}", result.Review)
            };
        })).Produces<ReviewDetails>(201).ProducesValidationProblem().ProducesProblem(404).ProducesProblem(409)
            .WithSummary("Create your one public review for a media item");
        var reviews = app.MapGroup("/api/v1/reviews").WithTags("Reviews");
        reviews.MapGet("/{id:guid}", async (Guid id, IReviewService service, CancellationToken ct) =>
        {
            var result = await service.FindAsync(id, ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }).RequireRateLimiting("catalog").Produces<ReviewDetails>().ProducesProblem(404).ProducesProblem(429)
            .WithSummary("Get a public plain-text review");
        Write(reviews.MapPatch("/{id:guid}", async (Guid id, ReviewRequest request, ClaimsPrincipal user, IReviewService service, CancellationToken ct) =>
        {
            if (ReviewError(request) is { } error) return error;
            var result = await service.UpdateAsync(UserId(user), id, request, ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        })).Produces<ReviewDetails>().ProducesValidationProblem().ProducesProblem(404)
            .WithSummary("Replace the body and spoiler flag of your review");
        Write(reviews.MapDelete("/{id:guid}", async (Guid id, ClaimsPrincipal user, IReviewService service, CancellationToken ct) =>
            await service.DeleteAsync(UserId(user), id, ct) ? Results.NoContent() : Results.NotFound()))
            .Produces(204).ProducesProblem(404).WithSummary("Delete your own review");
    }

    private static RouteHandlerBuilder Write(RouteHandlerBuilder builder) => builder.RequireAuthorization()
        .AddEndpointFilter<AntiforgeryFilter>().RequireRateLimiting("personal-write").ProducesProblem(400).ProducesProblem(401).ProducesProblem(429);
    private static Guid UserId(ClaimsPrincipal user) => Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private static IResult? ReviewError(ReviewRequest request) => PersonalMediaRules.ValidBody(request.Body?.Trim()) ? null
        : Results.ValidationProblem(new Dictionary<string, string[]> { ["body"] = ["Body must contain 1–10000 valid plain-text characters; only tab and newline controls are allowed."] });
    private static Dictionary<string, string[]> PageErrors(int page, int pageSize)
    {
        var errors = new Dictionary<string, string[]>();
        if (page is < 1 or > 10000) errors["page"] = ["Page must be between 1 and 10000."];
        if (pageSize is < 1 or > 100) errors["pageSize"] = ["Page size must be between 1 and 100."];
        return errors;
    }
}
