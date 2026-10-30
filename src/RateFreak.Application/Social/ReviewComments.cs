using System.Text.Json.Serialization;

namespace RateFreak.Application.Social;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ReviewCommentRequest(string? Body, bool IsSpoiler = false);
public sealed record CommentAuthor(Guid Id, string Handle, string DisplayName);
public sealed record ReviewCommentDetails(Guid Id, Guid ReviewId, CommentAuthor Author, string Body, bool IsSpoiler,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
public sealed record ReviewCommentPage(IReadOnlyList<ReviewCommentDetails> Items, int Page, int PageSize, int TotalCount);

public interface IReviewCommentService
{
    Task<ReviewCommentPage?> ListAsync(Guid reviewId, int page, int pageSize, CancellationToken ct);
    Task<ReviewCommentDetails?> FindAsync(Guid commentId, CancellationToken ct);
    Task<ReviewCommentDetails?> CreateAsync(Guid userId, Guid reviewId, ReviewCommentRequest request, CancellationToken ct);
    Task<ReviewCommentDetails?> UpdateAsync(Guid userId, Guid commentId, ReviewCommentRequest request, CancellationToken ct);
    Task<bool> DeleteAsync(Guid userId, Guid commentId, CancellationToken ct);
}
