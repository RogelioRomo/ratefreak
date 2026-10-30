namespace RateFreak.Infrastructure.Providers.Igdb;

public sealed class IgdbOptions
{
    public bool Enabled { get; set; }
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public int TimeoutMilliseconds { get; set; } = 6000;
    public bool IsValid() => TimeoutMilliseconds is >= 500 and <= 15000 &&
        (!Enabled || SafeCredential(ClientId) && SafeCredential(ClientSecret));
    private static bool SafeCredential(string? value) => value is { Length: >= 1 and <= 256 } && value.All(char.IsAsciiLetterOrDigit);
}
