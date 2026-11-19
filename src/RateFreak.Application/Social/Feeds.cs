using RateFreak.Application.Catalog;
using RateFreak.Application.PersonalMedia;

namespace RateFreak.Application.Social;

public sealed record FeedQuery(string Tab, string? Type, string? Cursor);
public sealed record FeedReview(Guid Id, string Excerpt, bool IsSpoiler);
public sealed record FeedEntry(ReviewAuthor Author, MediaSummary Media, decimal? Score, FeedReview? Review, DateTimeOffset PublishedAt);
public sealed record FeedPage(IReadOnlyList<FeedEntry> Items, string? NextCursor, bool HasFollowees);
public sealed record FeedReadResult(FeedPage? Page, Dictionary<string, string[]>? Errors = null);
public interface IFeedService
{
    Task<FeedReadResult> ListAsync(Guid viewerId, FeedQuery query, CancellationToken ct);
}
