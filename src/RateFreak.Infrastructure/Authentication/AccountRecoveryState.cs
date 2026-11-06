namespace RateFreak.Infrastructure.Authentication;

internal sealed class AccountRecoveryState
{
    public Guid UserId { get; set; }
    public string? Verifier { get; set; }
    public int FailedAttempts { get; set; }
    public DateTimeOffset? AttemptWindowEndsAt { get; set; }
}

internal sealed class AccountRecoveryEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
