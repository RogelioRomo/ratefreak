using System.Globalization;
using System.Text.Json;
using RateFreak.Application.MetadataImport;
using static RateFreak.Infrastructure.Providers.Igdb.IgdbJson;

namespace RateFreak.Infrastructure.Providers.Igdb;

internal static class IgdbMapping
{
    public static int Identity(JsonElement row) => row.ValueKind == JsonValueKind.Object ? Int(row, "id") : throw Invalid();
    public static ExternalMediaResult? Map(JsonElement row)
    {
        var id = Identity(row);
        var type = Int(row, "game_type", 0);
        var title = Text(row, "name", 300)?.Trim();
        if (string.IsNullOrWhiteSpace(title) || title.Any(char.IsControl)) throw Invalid();
        var edition = false;
        if (row.TryGetProperty("version_parent", out var parent) && parent.ValueKind != JsonValueKind.Null)
        { if (parent.ValueKind != JsonValueKind.Number || !parent.TryGetInt32(out var parentId) || parentId <= 0) throw Invalid(); edition = true; }
        if (type is not (0 or 8 or 9) || edition) return null;
        var description = Text(row, "summary", 20000);
        var metadata = new Dictionary<string, object> { ["gameType"] = type };
        if (row.TryGetProperty("first_release_date", out var date) && date.ValueKind != JsonValueKind.Null)
        {
            if (date.ValueKind != JsonValueKind.Number || !date.TryGetInt64(out var seconds) || seconds is < -62135596800L or > 253402300799L) throw Invalid();
            metadata["firstReleaseTimestamp"] = seconds;
        }
        MapNames(row, "platforms", metadata); MapNames(row, "genres", metadata);
        string? image = null;
        if (row.TryGetProperty("cover", out var cover) && cover.ValueKind != JsonValueKind.Null)
        {
            if (cover.ValueKind != JsonValueKind.Object) throw Invalid();
            var imageId = Text(cover, "image_id", 100);
            if (imageId is null || imageId.Length == 0 || !imageId.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-')) throw Invalid();
            image = "https://images.igdb.com/igdb/image/upload/t_cover_big/" + imageId + ".jpg";
        }
        var normalized = JsonSerializer.SerializeToElement(metadata);
        if (normalized.GetRawText().Length > 32768) throw Invalid();
        return new("igdb", id.ToString(CultureInfo.InvariantCulture), "video_game", title, description, null, image, normalized);
    }
    private static void MapNames(JsonElement row, string key, Dictionary<string, object> metadata)
    {
        if (!row.TryGetProperty(key, out var values) || values.ValueKind == JsonValueKind.Null) return;
        if (values.ValueKind != JsonValueKind.Array || values.GetArrayLength() > 20) throw Invalid();
        var ids = new HashSet<int>();
        var names = new List<object>();
        foreach (var value in values.EnumerateArray())
        {
            var id = Identity(value);
            var name = Text(value, "name", 100)?.Trim();
            if (!ids.Add(id) || string.IsNullOrWhiteSpace(name) || name.Any(char.IsControl)) throw Invalid();
            names.Add(new { id, name });
        }
        metadata[key] = names;
    }
}
