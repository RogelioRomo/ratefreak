using RateFreak.Application.MetadataImport;

namespace RateFreak.Infrastructure.Providers.OpenLibrary;

/// <summary>Single-instance, shared-egress budget for every Open Library metadata request.</summary>
public sealed class OpenLibraryRequestGate(TimeProvider clock) : IDisposable
{
    private readonly SemaphoreSlim slot = new(1, 1);
    private long? lastStart;
    private long? cooldownStart;
    private TimeSpan cooldown;
    public bool TryEnter() => slot.Wait(0);
    public void Exit() => slot.Release();
    public void Cooldown(TimeSpan duration) { cooldownStart = clock.GetTimestamp(); cooldown = duration; }
    public async Task PaceAsync(CancellationToken ct)
    {
        var remaining = cooldownStart is { } start ? cooldown - clock.GetElapsedTime(start) : TimeSpan.Zero;
        if (remaining > TimeSpan.FromSeconds(1)) throw new MediaProviderException(ProviderFailure.RateLimited, (int)Math.Clamp(Math.Ceiling(remaining.TotalSeconds), 1, 86400));
        var wait = lastStart is { } last ? TimeSpan.FromSeconds(1) - clock.GetElapsedTime(last) : TimeSpan.Zero;
        if (remaining > wait) wait = remaining;
        if (wait > TimeSpan.Zero) await Task.Delay(wait, clock, ct);
        ct.ThrowIfCancellationRequested(); lastStart = clock.GetTimestamp();
    }
    public void Dispose() => slot.Dispose();
}
