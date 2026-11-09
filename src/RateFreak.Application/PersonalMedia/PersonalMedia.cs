using System.Text.Json.Serialization;
using RateFreak.Application.Catalog;

namespace RateFreak.Application.PersonalMedia;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LibraryRequest(string? Status, DateOnly? StartedAt, DateOnly? CompletedAt);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RatingRequest(decimal? Score);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ReviewRequest(string? Body, bool IsSpoiler = false);
public sealed record PersonalPage<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
public sealed record LibraryQuery(string? Status, string? Type, int Page, int PageSize);
public sealed record LibraryItem(MediaSummary Media, string Status, DateOnly? StartedAt, DateOnly? CompletedAt,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
public sealed record RatingDetails(Guid Id, Guid MediaId, decimal Score, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
public sealed record RatingAggregate(int Count, decimal? Average);
public sealed record ReviewAuthor(Guid Id, string Handle, string DisplayName);
public sealed record ReviewDetails(Guid Id, Guid MediaId, ReviewAuthor Author, string Body, bool IsSpoiler,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
public enum ReviewWriteStatus { Success, NotFound, Conflict }
public sealed record ReviewWriteResult(ReviewWriteStatus Status, ReviewDetails? Review = null);

public interface ILibraryService
{
    Task<LibraryItem?> FindAsync(Guid userId, Guid mediaId, CancellationToken ct);
    Task<PersonalPage<LibraryItem>> ListAsync(Guid userId, LibraryQuery query, CancellationToken ct);
    Task<LibraryItem?> PutAsync(Guid userId, Guid mediaId, LibraryRequest request, CancellationToken ct);
    Task<bool> DeleteAsync(Guid userId, Guid mediaId, CancellationToken ct);
}
public interface IRatingService
{
    Task<RatingDetails?> FindAsync(Guid userId, Guid mediaId, CancellationToken ct);
    Task<RatingDetails?> PutAsync(Guid userId, Guid mediaId, decimal score, CancellationToken ct);
    Task<bool> DeleteAsync(Guid userId, Guid mediaId, CancellationToken ct);
    Task<RatingAggregate?> AggregateAsync(Guid? viewerId, Guid mediaId, CancellationToken ct);
}
public interface IReviewService
{
    Task<ReviewDetails?> FindOwnAsync(Guid userId, Guid mediaId, CancellationToken ct);
    Task<PersonalPage<ReviewDetails>?> ListAsync(Guid? viewerId, Guid mediaId, int page, int pageSize, CancellationToken ct);
    Task<ReviewDetails?> FindAsync(Guid? viewerId, Guid reviewId, CancellationToken ct);
    Task<ReviewWriteResult> CreateAsync(Guid userId, Guid mediaId, ReviewRequest request, CancellationToken ct);
    Task<ReviewDetails?> UpdateAsync(Guid userId, Guid reviewId, ReviewRequest request, CancellationToken ct);
    Task<bool> DeleteAsync(Guid userId, Guid reviewId, CancellationToken ct);
}
