namespace RateFreak.Infrastructure.Authentication;

internal sealed class AccountDataGrant
{
    public string Hash { get; set; } = "";
    public Guid UserId { get; set; }
    public string SecurityStamp { get; set; } = "";
    public string Operation { get; set; } = "";
    public DateTimeOffset ExpiresAt { get; set; }
}
internal sealed class AccountExportBudget
{
    public Guid UserId { get; set; }
    public DateTimeOffset WindowEndsAt { get; set; }
    public int Attempts { get; set; }
}
public sealed class AccountDataOptions { public string? ErasureJournalPath { get; set; } }
