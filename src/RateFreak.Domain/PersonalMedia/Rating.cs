namespace RateFreak.Domain.PersonalMedia;

public sealed class Rating
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid UserId { get; private set; }
    public Guid MediaItemId { get; private set; }
    public decimal Score { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; private set; } = DateTimeOffset.UtcNow;
    private Rating() { }
    public Rating(Guid userId, Guid mediaItemId, decimal score)
    {
        if (!PersonalMediaRules.ValidScore(score)) throw new ArgumentException("Invalid rating score.");
        UserId = userId; MediaItemId = mediaItemId; Score = score;
    }
}
