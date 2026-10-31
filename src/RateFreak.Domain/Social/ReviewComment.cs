using RateFreak.Domain.PersonalMedia;

namespace RateFreak.Domain.Social;

public sealed class ReviewComment
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid UserId { get; private set; }
    public Guid ReviewId { get; private set; }
    public string Body { get; private set; } = null!;
    public bool IsHidden { get; private set; }
    public bool IsSpoiler { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; private set; } = DateTimeOffset.UtcNow;
    private ReviewComment() { }
    public ReviewComment(Guid userId, Guid reviewId, string body, bool isSpoiler)
    {
        body = body.Trim();
        if (userId == Guid.Empty || reviewId == Guid.Empty || !ValidBody(body)) throw new ArgumentException("Invalid review comment.");
        UserId = userId; ReviewId = reviewId; Body = body; IsSpoiler = isSpoiler;
    }
    public static bool ValidBody(string? body) => body is { Length: <= 2000 } && PersonalMediaRules.ValidBody(body);
}
