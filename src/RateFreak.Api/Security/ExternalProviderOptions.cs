namespace RateFreak.Api.Security;

public sealed class ExternalProviderOptions
{
    public const string Section = "Authentication:External";
    public string? PublicOrigin { get; set; }
    public ExternalProvider Google { get; set; } = new();
    public ExternalProvider GitHub { get; set; } = new();
    public ExternalProvider? Find(string provider) => provider switch { "google" => Google, "github" => GitHub, _ => null };
    public bool IsValid(bool development)
    {
        if (!Google.IsValid() || !GitHub.IsValid()) return false;
        if (!Google.Enabled && !GitHub.Enabled) return true;
        return Uri.TryCreate(PublicOrigin, UriKind.Absolute, out var uri) && uri.AbsolutePath == "/" &&
            string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment) && string.IsNullOrEmpty(uri.UserInfo) &&
            (uri.Scheme == "https" || development && uri.Scheme == "http" && uri.IsLoopback);
    }
    public bool MatchesOrigin(HttpRequest request) => Uri.TryCreate(PublicOrigin, UriKind.Absolute, out var uri) &&
        string.Equals(uri.Scheme, request.Scheme, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(uri.Authority, request.Host.Value, StringComparison.OrdinalIgnoreCase);
}
public sealed class ExternalProvider
{
    public bool Enabled { get; set; }
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public bool IsValid() => !Enabled || Safe(ClientId) && Safe(ClientSecret);
    private static bool Safe(string? value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 1024 && !value.Any(char.IsControl);
}
