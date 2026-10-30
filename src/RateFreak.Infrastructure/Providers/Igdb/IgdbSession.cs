using RateFreak.Application.MetadataImport;

namespace RateFreak.Infrastructure.Providers.Igdb;

// The exclusive operation slot owns all mutable state, including token refresh.
public sealed class IgdbSession(TimeProvider clock) : IDisposable
{
    private readonly SemaphoreSlim slot = new(1, 1);
    private long? lastStart;
    private long? cooldownStart;
    private TimeSpan cooldown;
    private string? token;
    private long tokenStart;
    private TimeSpan tokenLifetime;

    public bool TryEnter() => slot.Wait(0);
    public void Exit() => slot.Release();
    public string? CachedToken => token is not null && clock.GetElapsedTime(tokenStart) < tokenLifetime ? token : null;
    public void InvalidateToken() => token = null;
    public void CacheToken(string value, int expiresIn)
    {
        token = value;
        tokenStart = clock.GetTimestamp();
        tokenLifetime = TimeSpan.FromSeconds(Math.Clamp((long)expiresIn - 60, 0, 55 * 60));
    }
    public void Cooldown(TimeSpan duration) { cooldownStart = clock.GetTimestamp(); cooldown = duration; }
    public int? RemainingCooldown()
    {
        if (cooldownStart is not { } start) return null;
        var remaining = cooldown - clock.GetElapsedTime(start);
        return remaining > TimeSpan.Zero ? (int)Math.Clamp(Math.Ceiling(remaining.TotalSeconds), 1, 86400) : null;
    }
    public void RejectCooldown()
    {
        if (RemainingCooldown() is { } seconds) throw new MediaProviderException(ProviderFailure.RateLimited, seconds);
    }
    public async Task PaceAsync(CancellationToken ct)
    {
        var wait = lastStart is { } last ? TimeSpan.FromMilliseconds(250) - clock.GetElapsedTime(last) : TimeSpan.Zero;
        if (cooldownStart is { } start && cooldown - clock.GetElapsedTime(start) > wait)
            wait = cooldown - clock.GetElapsedTime(start);
        if (wait > TimeSpan.Zero) await Task.Delay(wait, clock, ct);
        ct.ThrowIfCancellationRequested();
        lastStart = clock.GetTimestamp();
    }
    public void Dispose() => slot.Dispose();
}
