namespace RateFreak.Domain.Moderation;

public sealed class ContentReport
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string TargetType { get; set; } = null!;
    public Guid TargetId { get; set; }
    public Guid OriginalReporterId { get; set; }
    public Guid OriginalAuthorId { get; set; }
    public Guid? ReporterId { get; set; }
    public Guid? ReviewId { get; set; }
    public Guid? ItemId { get; set; }
    public Guid? CategoryId { get; set; }
    public Guid? CommentId { get; set; }
    public string Reason { get; set; } = null!;
    public string? Details { get; set; }
    public string Status { get; set; } = "open";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ResolvedAt { get; set; }
    public string? Decision { get; set; }
    public string? ResolutionReason { get; set; }
    public Guid? ResolvedBy { get; set; }
}
