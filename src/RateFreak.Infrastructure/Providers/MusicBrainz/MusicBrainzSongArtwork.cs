using System.Globalization;
using System.Text.Json;
using RateFreak.Application.MetadataImport;
using RateFreak.Domain.Collections;

namespace RateFreak.Infrastructure.Providers.MusicBrainz;

internal sealed record MusicBrainzSongArtwork(string ImageReference, string Id, string Title, string? ReleaseGroupId)
{
    // Only relationships on this exact recording are candidates. Optional artwork
    // metadata must not prevent searching or importing the recording itself.
    public static MusicBrainzSongArtwork? Select(JsonElement recording)
    {
        if (!recording.TryGetProperty("releases", out var releases) || releases.ValueKind != JsonValueKind.Array) return null;
        var candidates = new List<(MusicBrainzSongArtwork Artwork, int Status, int Type, int Compilation, int Front, string Date)>();
        foreach (var release in releases.EnumerateArray().Take(100))
        {
            if (release.ValueKind != JsonValueKind.Object) continue;
            var id = Identity(Text(release,"id",36));
            var title = Text(release,"title",300);
            if (id is null || string.IsNullOrWhiteSpace(title)) continue;
            var status = Text(release,"status",40);
            if (status is "Cancelled" or "Withdrawn" or "Pseudo-Release") continue;
            var groupId = (string?)null;
            var primary = (string?)null;
            var compilation = false;
            if (release.TryGetProperty("release-group",out var group) && group.ValueKind == JsonValueKind.Object)
            {
                groupId = Identity(Text(group,"id",36));
                primary = Text(group,"primary-type",80);
                if (groupId != null) title = Text(group,"title",300) is { Length: > 0 } groupTitle ? groupTitle : title;
                if (group.TryGetProperty("secondary-types",out var secondary) && secondary.ValueKind == JsonValueKind.Array)
                    compilation = secondary.EnumerateArray().Take(20).Any(value => string.Equals(TextValue(value,80),"Compilation",StringComparison.OrdinalIgnoreCase));
            }
            var front = release.TryGetProperty("cover-art-archive",out var archive) && archive.ValueKind == JsonValueKind.Object
                && archive.TryGetProperty("front",out var value) && value.ValueKind == JsonValueKind.True;
            var image = !front && groupId != null ? MusicBrainzArtwork.Album(null,groupId)!
                : $"https://coverartarchive.org/release/{id}/front-500";
            var date = Text(release,"date",10);
            candidates.Add((new(image,id,title,groupId), status == "Official" ? 0 : string.IsNullOrEmpty(status) ? 1 : 2,
                primary == "Album" ? 0 : primary is "EP" or "Single" ? 1 : 2, compilation ? 1 : 0, front ? 0 : 1, SortDate(date)));
        }
        return candidates.OrderBy(x=>x.Status).ThenBy(x=>x.Type).ThenBy(x=>x.Compilation).ThenBy(x=>x.Front)
            .ThenBy(x=>x.Date,StringComparer.Ordinal).ThenBy(x=>x.Artwork.ReleaseGroupId,StringComparer.Ordinal)
            .ThenBy(x=>x.Artwork.Id,StringComparer.Ordinal).Select(x=>x.Artwork).FirstOrDefault();
    }

    private static string? Identity(string? value) => MediaProviderInput.TryIdentity("musicbrainz","album",value,out var identity) ? identity.ExternalId : null;
    private static string? Text(JsonElement item,string name,int max)
    {
        if (!item.TryGetProperty(name,out var value) || value.ValueKind != JsonValueKind.String) return null;
        return TextValue(value,max);
    }
    private static string? TextValue(JsonElement value,int max)
    {
        if (value.ValueKind != JsonValueKind.String) return null;
        string? text;
        try { text=value.GetString(); } catch (InvalidOperationException) { return null; }
        return text != null && CollectionRules.ValidText(text,max,allowBlank:true) ? text : null;
    }
    private static string SortDate(string? value)
    {
        if (value is not null)
        {
            // Padding is only for ordering; no fabricated date is stored.
            var full = value.Length == 4 ? value+"-01-01" : value.Length == 7 ? value+"-01" : value;
            if (DateOnly.TryParseExact(full,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out _)) return full;
        }
        return "9999-12-31";
    }
}
