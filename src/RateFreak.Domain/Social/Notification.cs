namespace RateFreak.Domain.Social;

public sealed class Notification
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Kind { get; private set; } = null!;
    public Guid RecipientId { get; private set; }
    public Guid ActorId { get; private set; }
    public Guid? CommentId { get; private set; }
    public Guid? FollowActorId { get; private set; }
    public Guid? FollowRecipientId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ReadAt { get; private set; }
    private Notification() { }
    public Notification(Guid recipientId, Guid actorId, Guid? commentId = null)
    {
        if (recipientId == Guid.Empty || actorId == Guid.Empty || recipientId == actorId || commentId == Guid.Empty)
            throw new ArgumentException("Invalid notification.");
        RecipientId = recipientId; ActorId = actorId; CommentId = commentId;
        Kind = commentId.HasValue ? "comment" : "follow";
        if (Kind == "follow") { FollowActorId = actorId; FollowRecipientId = recipientId; }
    }
}
