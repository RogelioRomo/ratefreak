namespace RateFreak.Infrastructure.Installation;

internal sealed class InstallationState
{
    public int Id { get; set; } = 1;
    public DateTimeOffset? CompletedAt { get; set; }
    // Historical attribution only: account deletion must never remove/reopen installation state.
    public Guid? InitialAdminId { get; set; }
}
