namespace RateFreak.Api.Security;

public sealed class BrowserProxyOptions
{
    public string? SharedSecret { get; set; }
    public bool AllowPrivateHttp { get; set; }
    public string? PublicOrigin { get; set; }

    public bool HasValidPrivateOrigin() => !AllowPrivateHttp ||
        !string.IsNullOrWhiteSpace(SharedSecret) && Uri.TryCreate(PublicOrigin, UriKind.Absolute, out var origin) &&
        origin.Scheme == "https" && origin.AbsolutePath == "/" && string.IsNullOrEmpty(origin.UserInfo) &&
        string.IsNullOrEmpty(origin.Query) && string.IsNullOrEmpty(origin.Fragment);
}
