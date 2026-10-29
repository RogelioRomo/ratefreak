namespace RateFreak.Domain.PersonalMedia;

public sealed class Review
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid UserId { get; private set; }
    public Guid MediaItemId { get; private set; }
    public string Body { get; private set; } = null!;
    public bool IsSpoiler { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; private set; } = DateTimeOffset.UtcNow;
    private Review() { }
    public Review(Guid userId, Guid mediaItemId, string body, bool isSpoiler)
    {
        body = body.Trim();
        if (!PersonalMediaRules.ValidBody(body)) throw new ArgumentException("Invalid review body.");
        UserId = userId; MediaItemId = mediaItemId; Body = body; IsSpoiler = isSpoiler;
    }
}
