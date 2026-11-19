using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using RateFreak.Application.Social;

namespace RateFreak.Infrastructure.Social;

internal sealed record FeedCursor(int Version, Guid Viewer, string Tab, string? Type,
    DateTimeOffset Time, Guid Author, Guid Media)
{
    internal static bool TryRead(string? value, Guid viewer, FeedQuery query, out FeedCursor? cursor)
    {
        cursor = null;
        if (value is null) return true;
        if (value.Length is 0 or > 1024) return false;
        try
        {
            cursor = JsonSerializer.Deserialize<FeedCursor>(WebEncoders.Base64UrlDecode(value));
            return cursor is { Version: 1 } && cursor.Viewer == viewer && cursor.Tab == query.Tab && cursor.Type == query.Type
                && cursor.Time.Offset == TimeSpan.Zero && cursor.Time > DateTimeOffset.MinValue
                && cursor.Author != Guid.Empty && cursor.Media != Guid.Empty;
        }
        catch (Exception error) when (error is FormatException or JsonException or ArgumentException) { cursor = null; return false; }
    }
    internal string Encode() => WebEncoders.Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(this));
}
