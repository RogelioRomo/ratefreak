using RateFreak.Application.MetadataImport;

namespace RateFreak.Infrastructure.Providers.MusicBrainz;

public sealed class MusicBrainzRequestGate(TimeProvider clock) : IDisposable
{
    private readonly SemaphoreSlim slot = new(1,1);
    private long? lastStart;
    private long? cooldownStart;
    private TimeSpan cooldown;
    public bool TryEnter() => slot.Wait(0);
    public void Exit() => slot.Release();
    public void Cooldown(TimeSpan duration) { cooldownStart = clock.GetTimestamp(); cooldown = duration; }
    public int? RemainingCooldown()
    {
        if (cooldownStart is not { } start) return null;
        var remaining = cooldown - clock.GetElapsedTime(start);
        return remaining > TimeSpan.Zero ? (int)Math.Clamp(Math.Ceiling(remaining.TotalSeconds),1,86400) : null;
    }
    public async Task PaceAsync(CancellationToken ct)
    {
        // The exclusive operation slot protects timestamps and cooldown across clients.
        if (RemainingCooldown() is > 1 and var retry) throw new MediaProviderException(ProviderFailure.RateLimited,retry);
        var wait = lastStart is { } last ? TimeSpan.FromSeconds(1) - clock.GetElapsedTime(last) : TimeSpan.Zero;
        if (cooldownStart is { } start) wait = Max(wait,cooldown - clock.GetElapsedTime(start));
        if (wait > TimeSpan.Zero) await Task.Delay(wait,clock,ct);
        ct.ThrowIfCancellationRequested(); lastStart = clock.GetTimestamp();
    }
    private static TimeSpan Max(TimeSpan a,TimeSpan b) => a > b ? a : b;
    public void Dispose() => slot.Dispose();
}
