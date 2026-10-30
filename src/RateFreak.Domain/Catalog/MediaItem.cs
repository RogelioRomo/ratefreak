namespace RateFreak.Domain.Catalog;

public static class MediaTypes
{
    public const string Album = "album";
    public const string Movie = "movie";
    public const string TvShow = "tv_show";
    public static bool IsSupported(string value) => value is Movie or TvShow or Album;
}

public sealed class MediaItem
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string MediaType { get; private set; } = null!;
    public string Title { get; private set; } = null!;
    public string? Description { get; private set; }
    public DateOnly? ReleaseDate { get; private set; }
    public string? ImageReference { get; private set; }
    public string Metadata { get; private set; } = "{}";
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; private set; } = DateTimeOffset.UtcNow;

    private MediaItem() { }

    public MediaItem(string mediaType, string title, string? description = null,
        DateOnly? releaseDate = null, string? imageReference = null, string metadata = "{}")
    {
        if (!MediaTypes.IsSupported(mediaType)) throw new ArgumentException("Unsupported media type.", nameof(mediaType));
        title = title.Trim();
        if (title.Length is < 1 or > 300 || title.Any(char.IsControl))
            throw new ArgumentException("Title must contain 1–300 printable characters.", nameof(title));
        if (description?.Length > 20000) throw new ArgumentException("Description exceeds 20000 characters.", nameof(description));
        if (imageReference?.Length > 2048) throw new ArgumentException("Artwork reference exceeds 2048 characters.", nameof(imageReference));
        using var document = System.Text.Json.JsonDocument.Parse(metadata);
        if (document.RootElement.ValueKind != System.Text.Json.JsonValueKind.Object || metadata.Length > 32768)
            throw new ArgumentException("Metadata must be a JSON object of at most 32768 characters.", nameof(metadata));
        MediaType = mediaType;
        Title = title;
        Description = description;
        ReleaseDate = releaseDate;
        ImageReference = imageReference;
        Metadata = metadata;
    }
}
