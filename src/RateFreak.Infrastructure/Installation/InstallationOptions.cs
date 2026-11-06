namespace RateFreak.Infrastructure.Installation;

public sealed class InstallationOptions
{
    public string? SetupSecret { get; set; }
    public static bool ValidSecret(string? value) => value is { Length: 64 } && value.All(Uri.IsHexDigit);
}
