namespace RateFreak.Domain.Catalog;

public sealed class MediaExternalId
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid MediaItemId { get; private set; }
    public string Provider { get; private set; } = null!;
    public string Namespace { get; private set; } = null!;
    public string ExternalId { get; private set; } = null!;
    public string? ExternalUrl { get; private set; }
    public DateTimeOffset? LastSyncedAt { get; private set; }

    private MediaExternalId() { }

    public MediaExternalId(Guid mediaItemId, string provider, string identityNamespace, string externalId,
        string? externalUrl = null, DateTimeOffset? lastSyncedAt = null)
    {
        if (mediaItemId == Guid.Empty) throw new ArgumentException("Media ID is required.", nameof(mediaItemId));
        MediaItemId = mediaItemId;
        Provider = Key(provider, 50);
        Namespace = Key(identityNamespace, 50);
        ExternalId = externalId.Trim();
        if (ExternalId.Length is < 1 or > 200 || ExternalId.Any(char.IsControl))
            throw new ArgumentException("External ID must contain 1–200 printable characters.", nameof(externalId));
        if (externalUrl?.Length > 2048) throw new ArgumentException("External URL exceeds 2048 characters.", nameof(externalUrl));
        if (lastSyncedAt is { Offset: var offset } && offset != TimeSpan.Zero)
            throw new ArgumentException("Sync timestamp must be UTC.", nameof(lastSyncedAt));
        ExternalUrl = externalUrl;
        LastSyncedAt = lastSyncedAt;
    }

    private static string Key(string value, int max)
    {
        value = value.Trim().ToLowerInvariant();
        if (value.Length < 1 || value.Length > max || value.Any(c => !(c is >= 'a' and <= 'z' or >= '0' and <= '9' or '_' or '-')))
            throw new ArgumentException("Identity namespace and provider must use ASCII letters, digits, underscores or hyphens.");
        return value;
    }
}
