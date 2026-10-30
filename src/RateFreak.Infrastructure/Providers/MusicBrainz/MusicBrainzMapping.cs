using System.Globalization;
using System.Text.Json;
using RateFreak.Application.MetadataImport;
using RateFreak.Domain.Collections;

namespace RateFreak.Infrastructure.Providers.MusicBrainz;

internal static class MusicBrainzMapping
{
    private static MediaProviderException Invalid() => new(ProviderFailure.InvalidResponse);
    public static ExternalMediaResult? Map(JsonElement item)
    {
        if (item.ValueKind != JsonValueKind.Object) throw Invalid();
        var rawId = Text(item,"id",36,true)!;
        if (!MediaProviderInput.TryIdentity("musicbrainz","album",rawId,out var identity)) throw Invalid();
        var title = Text(item,"title",300,true)!.Trim();
        if (!CollectionRules.ValidText(title,300)) throw Invalid();
        var primary = Text(item,"primary-type",80,true)!;
        if (!string.Equals(primary,"Album",StringComparison.OrdinalIgnoreCase)) return null;
        var secondary = new List<string>();
        if (item.TryGetProperty("secondary-types",out var types))
        {
            if (types.ValueKind != JsonValueKind.Array || types.GetArrayLength()>20) throw Invalid();
            foreach (var type in types.EnumerateArray()) secondary.Add(String(type,80));
        }
        if (secondary.Any(x => new[] { "audiobook","audio drama","spokenword","interview" }.Contains(x,StringComparer.OrdinalIgnoreCase))) return null;
        if (!item.TryGetProperty("artist-credit",out var credits) || credits.ValueKind != JsonValueKind.Array || credits.GetArrayLength()>50) throw Invalid();
        var artists = new List<object>(); var combined = "";
        foreach (var credit in credits.EnumerateArray())
        {
            if (credit.ValueKind != JsonValueKind.Object || !credit.TryGetProperty("artist",out var artist) || artist.ValueKind != JsonValueKind.Object) throw Invalid();
            var artistId = Text(artist,"id",36,true)!;
            if (artistId.Length != 36 || !Guid.TryParseExact(artistId,"D",out var uuid) || uuid == Guid.Empty) throw Invalid();
            var name = Text(credit,"name",300,true)!;
            if (string.IsNullOrWhiteSpace(name)) throw Invalid();
            var join = Text(credit,"joinphrase",100) ?? "";
            combined += name + join;
            if (combined.Length>2000) throw Invalid();
            artists.Add(new { id=uuid.ToString("D"),name,joinPhrase=join });
        }
        var date = Text(item,"first-release-date",10);
        DateOnly? releaseDate = null;
        if (!string.IsNullOrEmpty(date))
        {
            if (date.Length == 10 && DateOnly.TryParseExact(date,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var full)) releaseDate=full;
            else if (date.Length == 4 && int.TryParse(date,NumberStyles.None,CultureInfo.InvariantCulture,out var year) && year is >=1 and <=9999) { }
            else if (date.Length == 7 && DateOnly.TryParseExact(date+"-01","yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out _)) { }
            else throw Invalid();
        }
        var metadata = new Dictionary<string,object> { ["artistCredit"]=combined,["artists"]=artists,["primaryType"]="Album",["secondaryTypes"]=secondary };
        if (!string.IsNullOrEmpty(date)) metadata["firstReleaseDate"]=date;
        var json = JsonSerializer.SerializeToElement(metadata);
        if (json.GetRawText().Length>32768) throw Invalid();
        return new("musicbrainz",identity.ExternalId,"album",title,null,releaseDate,null,json);
    }
    private static string? Text(JsonElement item,string name,int max,bool required=false)
    {
        if (!item.TryGetProperty(name,out var value) || value.ValueKind == JsonValueKind.Null)
        { if (required) throw Invalid(); return null; }
        return String(value,max);
    }
    private static string String(JsonElement value,int max)
    {
        if (value.ValueKind != JsonValueKind.String) throw Invalid();
        string text;
        try { text=value.GetString()!; } catch (InvalidOperationException) { throw Invalid(); }
        if (!CollectionRules.ValidText(text,max,allowBlank:true)) throw Invalid();
        return text;
    }
}
