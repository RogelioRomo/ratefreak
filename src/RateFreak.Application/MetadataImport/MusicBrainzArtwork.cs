namespace RateFreak.Application.MetadataImport;

public static class MusicBrainzArtwork
{
    public static string? Album(string? reference, string? releaseGroupId)
    {
        if (reference is not null) return reference;
        return MediaProviderInput.TryIdentity("musicbrainz", "album", releaseGroupId, out var identity)
            ? $"https://coverartarchive.org/release-group/{identity.ExternalId}/front-500" : null;
    }
}
