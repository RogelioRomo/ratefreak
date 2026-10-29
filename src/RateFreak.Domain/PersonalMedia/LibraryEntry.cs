namespace RateFreak.Domain.PersonalMedia;

public sealed class LibraryEntry
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid UserId { get; private set; }
    public Guid MediaItemId { get; private set; }
    public string Status { get; private set; } = null!;
    public DateOnly? StartedAt { get; private set; }
    public DateOnly? CompletedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; private set; } = DateTimeOffset.UtcNow;
    private LibraryEntry() { }
    public LibraryEntry(Guid userId, Guid mediaItemId, string status, DateOnly? startedAt, DateOnly? completedAt)
    {
        if (!PersonalMediaRules.ValidStatus(status) || !PersonalMediaRules.ValidDates(startedAt, completedAt))
            throw new ArgumentException("Invalid tracking values.");
        UserId = userId; MediaItemId = mediaItemId; Status = status; StartedAt = startedAt; CompletedAt = completedAt;
    }
}
