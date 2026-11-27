namespace RateFreak.Infrastructure.Providers.Igdb;

internal sealed class IgdbLifecycleState
{
    public Guid MappingId { get; set; }
    public DateTimeOffset? NextAttemptAt { get; set; }
    public DateTimeOffset? LastAttemptAt { get; set; }
    public int Failures { get; set; }
    public string? LastOutcome { get; set; }
    public bool ContentRemoved { get; set; }
}

internal sealed class IgdbPosterIdentity
{
    public Guid MappingId { get; set; }
    public string Filename { get; set; } = null!;
}

internal sealed class IgdbRefreshSchedule
{
    public int Id { get; set; }
    public DateTimeOffset? LastBatchAt { get; set; }
    public DateTimeOffset? NextAllowedAt { get; set; }
}
