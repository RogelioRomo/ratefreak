namespace RateFreak.Domain.Social;

public sealed class UserFollow
{
    public Guid FollowerId { get; set; }
    public Guid FolloweeId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
