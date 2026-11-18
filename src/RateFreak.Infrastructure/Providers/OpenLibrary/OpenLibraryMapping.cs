using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using RateFreak.Application.MetadataImport;

namespace RateFreak.Infrastructure.Providers.OpenLibrary;

internal static partial class OpenLibraryMapping
{
    internal sealed record Author(string Id, string? Name);
    internal sealed record Work(string Id, string Title, string? Description, int? Year, int? CoverId, IReadOnlyList<Author> Authors)
    {
        public ExternalMediaResult Result() => new("openlibrary", Id, "book", Title, Description, null,
            CoverId is { } cover ? $"https://covers.openlibrary.org/b/id/{cover.ToString(CultureInfo.InvariantCulture)}-L.jpg?default=false" : null,
            JsonSerializer.SerializeToElement(new { firstPublicationYear = Year, authors = Authors.Select(a => new { id = a.Id, name = a.Name }), coverId = CoverId }));
    }

    public static MediaProviderException Invalid() => new(ProviderFailure.InvalidResponse);
    [GeneratedRegex("^OL[1-9][0-9]{0,14}A$", RegexOptions.CultureInvariant)]
    private static partial Regex AuthorId();

    public static Work Map(JsonElement row, bool search)
    {
        if (row.ValueKind != JsonValueKind.Object) throw Invalid();
        var key = Text(row, "key", 100) ?? throw Invalid();
        var id = key.StartsWith("/works/", StringComparison.Ordinal) ? key[7..] : search ? key : "";
        if (!MediaProviderInput.TryIdentity("openlibrary", "book", id, out _)) throw Invalid();
        var title = Text(row, "title", 300)?.Trim();
        if (string.IsNullOrWhiteSpace(title) || title.Any(char.IsControl)) throw Invalid();
        string? description = null;
        if (row.TryGetProperty("description", out var desc) && desc.ValueKind != JsonValueKind.Null)
            description = desc.ValueKind == JsonValueKind.String ? Text(row, "description", 20000)
                : desc.ValueKind == JsonValueKind.Object ? Text(desc, "value", 20000) : throw Invalid();
        int? year = null, cover = null;
        var authors = new List<Author>();
        if (search)
        {
            if (row.TryGetProperty("first_publish_year", out var y) && y.ValueKind != JsonValueKind.Null)
            {
                if (y.ValueKind != JsonValueKind.Number || !y.TryGetInt32(out var number) || number is < 1 or > 9999) throw Invalid(); year = number;
            }
            if (row.TryGetProperty("cover_i", out var c) && c.ValueKind != JsonValueKind.Null)
            { if (c.ValueKind != JsonValueKind.Number || !c.TryGetInt32(out var number)) throw Invalid(); if (number > 0) cover = number; }
            var keys = StringArray(row, "author_key", 100, 100);
            var names = StringArray(row, "author_name", 100, 300);
            for (var i = 0; i < keys.Count; i++)
            {
                if (!AuthorId().IsMatch(keys[i])) throw Invalid();
                authors.Add(new(keys[i], i < names.Count ? PrintableName(names[i]) : null));
            }
        }
        else
        {
            var date = Text(row, "first_publish_date", 100);
            if (date is { Length: 4 } && int.TryParse(date, NumberStyles.None, CultureInfo.InvariantCulture, out var number) && number is >= 1 and <= 9999) year = number;
            else if (DateOnly.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)) year = parsed.Year;
            if (row.TryGetProperty("covers", out var covers) && covers.ValueKind != JsonValueKind.Null)
            {
                if (covers.ValueKind != JsonValueKind.Array || covers.GetArrayLength() > 500) throw Invalid();
                foreach (var value in covers.EnumerateArray())
                { if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var n)) throw Invalid(); if (n > 0) cover ??= n; }
            }
            if (row.TryGetProperty("authors", out var array) && array.ValueKind != JsonValueKind.Null)
            {
                if (array.ValueKind != JsonValueKind.Array || array.GetArrayLength() > 100) throw Invalid();
                foreach (var author in array.EnumerateArray())
                {
                    if (author.ValueKind != JsonValueKind.Object || !author.TryGetProperty("author", out var a) || a.ValueKind != JsonValueKind.Object) throw Invalid();
                    var authorKey = Text(a, "key", 100);
                    if (authorKey is null || !authorKey.StartsWith("/authors/", StringComparison.Ordinal) || !AuthorId().IsMatch(authorKey[9..])) throw Invalid();
                    authors.Add(new(authorKey[9..], null));
                }
            }
        }
        return new(id, title, description, year, cover, authors.DistinctBy(a => a.Id).Take(100).ToArray());
    }

    public static IReadOnlyList<Author> MapAuthors(JsonElement root, IReadOnlySet<string> requested)
    {
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("docs", out var docs) || docs.ValueKind != JsonValueKind.Array || docs.GetArrayLength() > 20) throw Invalid();
        var results = new List<Author>();
        foreach (var doc in docs.EnumerateArray())
        {
            if (doc.ValueKind != JsonValueKind.Object) throw Invalid();
            var key = Text(doc, "key", 100) ?? throw Invalid();
            var id = key.StartsWith("/authors/", StringComparison.Ordinal) ? key[9..] : key;
            if (!AuthorId().IsMatch(id) || !requested.Contains(id)) throw Invalid();
            var name = Text(doc, "name", 300);
            results.Add(new(id, name is null ? null : PrintableName(name)));
        }
        if (results.DistinctBy(a => a.Id).Count() != results.Count) throw Invalid();
        return results;
    }

    private static string PrintableName(string value)
    { if (string.IsNullOrWhiteSpace(value) || value.Any(char.IsControl)) throw Invalid(); return value.Trim(); }

    private static IReadOnlyList<string> StringArray(JsonElement row, string name, int limit, int length)
    {
        if (!row.TryGetProperty(name, out var values) || values.ValueKind == JsonValueKind.Null) return [];
        if (values.ValueKind != JsonValueKind.Array || values.GetArrayLength() > limit) throw Invalid();
        var results = new List<string>();
        foreach (var v in values.EnumerateArray())
        { if (ReadString(v) is not { } s || s.Length > length || s.Any(char.IsControl)) throw Invalid(); results.Add(s); }
        return results;
    }

    public static string? Text(JsonElement row, string name, int limit)
    {
        if (!row.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null) return null;
        if (ReadString(value) is not { } text || text.Length > limit || text.Any(c => char.IsControl(c) && c is not ('\n' or '\r' or '\t'))) throw Invalid();
        return text;
    }

    private static string? ReadString(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.String) throw Invalid();
        try { return value.GetString(); }
        catch (InvalidOperationException) { throw Invalid(); }
    }
}
