namespace RateFreak.Domain.Moderation;

public sealed class ModerationEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid? ActorId { get; set; }
    public string ActorKind { get; set; } = "account";
    public string Action { get; set; } = null!;
    public string TargetType { get; set; } = null!;
    public Guid TargetId { get; set; }
    public Guid? ReportId { get; set; }
    public string Reason { get; set; } = null!;
    public bool? Before { get; set; }
    public bool? After { get; set; }
    public DateTimeOffset? ReviewedUpdatedAt { get; set; }
}
