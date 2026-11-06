using System.Security.Cryptography;
using System.Text;

namespace RateFreak.Infrastructure.Authentication;

internal static class RecoveryCode
{
    internal static string Generate()
    {
        var hex = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        return string.Join('-', Enumerable.Range(0, 4).Select(i => hex.Substring(i * 8, 8)));
    }

    internal static string? Normalize(string? code)
    {
        if (code is null || code.Length > 64) return null;
        var compact = code.Trim().Replace("-", "", StringComparison.Ordinal).ToUpperInvariant();
        return compact.Length == 32 && compact.All(Uri.IsHexDigit) ? compact : null;
    }

    internal static string Hash(Guid userId, string code) => Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes($"RateFreak:password-recovery:v1:{userId:D}:{Normalize(code)}")));

    internal static bool Matches(Guid userId, string code, string? verifier) => verifier is { Length: 64 } &&
        CryptographicOperations.FixedTimeEquals(Convert.FromHexString(Hash(userId, code)), Convert.FromHexString(verifier));
}
