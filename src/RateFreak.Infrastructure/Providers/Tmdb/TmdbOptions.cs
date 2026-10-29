using System.Text.RegularExpressions;

namespace RateFreak.Infrastructure.Providers.Tmdb;

public sealed class TmdbOptions
{
    public bool Enabled { get; set; }
    public string? AccessToken { get; set; }
    public string Language { get; set; } = "en-US";
    public int TimeoutMilliseconds { get; set; } = 4000;

    public bool IsValid() => TimeoutMilliseconds is >= 500 and <= 15000 &&
        Regex.IsMatch(Language ?? "", "^[a-z]{2}-[A-Z]{2}$", RegexOptions.CultureInvariant) &&
        (!Enabled || (!string.IsNullOrWhiteSpace(AccessToken) && AccessToken.Length <= 4096 &&
            AccessToken.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.' or '~' or '+' or '/' or '=')));
}

public sealed class TmdbRequestGate : IDisposable
{
    private readonly SemaphoreSlim slots = new(4, 4);
    public bool TryEnter() => slots.Wait(0);
    public void Exit() => slots.Release();
    public void Dispose() => slots.Dispose();
}
