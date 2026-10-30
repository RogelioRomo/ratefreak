using System.Text.Json;
using RateFreak.Application.MetadataImport;

namespace RateFreak.Infrastructure.Providers.Igdb;

internal static class IgdbJson
{
    public static MediaProviderException Invalid() => new(ProviderFailure.InvalidResponse);
    public static async Task<JsonDocument> ReadAsync(HttpResponseMessage response, int limit, CancellationToken ct)
    {
        if (response.Content.Headers.ContentLength > limit) throw Invalid();
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var buffer = new MemoryStream();
        var bytes = new byte[8192];
        int read;
        while ((read = await stream.ReadAsync(bytes, ct)) != 0)
        {
            if (buffer.Length + read > limit) throw Invalid();
            buffer.Write(bytes, 0, read);
        }
        return JsonDocument.Parse(buffer.ToArray(), new JsonDocumentOptions { MaxDepth = 32, AllowDuplicateProperties = false });
    }
    public static string? Text(JsonElement row, string name, int max)
    {
        if (!row.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null) return null;
        if (value.ValueKind != JsonValueKind.String) throw Invalid();
        string text;
        try { text = value.GetString()!; }
        catch (InvalidOperationException) { throw Invalid(); }
        if (text.Length > max || text.Any(c => char.IsControl(c) && c is not ('\n' or '\r' or '\t'))) throw Invalid();
        return text;
    }
    public static int Int(JsonElement row, string name, int minimum = 1)
    {
        if (!row.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var number) || number < minimum) throw Invalid();
        return number;
    }
}
