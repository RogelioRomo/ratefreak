using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using RateFreak.Domain.Catalog;

namespace RateFreak.Application.MetadataImport;

public sealed record ExternalMediaResult(string Provider, string ExternalId, string Type, string Title,
    string? Description, DateOnly? ReleaseDate, string? ImageReference, JsonElement Metadata);
public sealed record ExternalSearchPage(string Provider, int Page, IReadOnlyList<ExternalMediaResult> Items);
public sealed record ProviderCapability(string Name, bool Enabled, IReadOnlyList<string> SupportedTypes,
    string Attribution, string AttributionUrl);

public interface IMediaProvider
{
    ProviderCapability Capability { get; }
    Task<ExternalSearchPage> SearchAsync(string query, string type, int page, CancellationToken cancellationToken);
    Task<ExternalMediaResult?> GetDetailsAsync(string externalId, string type, CancellationToken cancellationToken);
}

public enum ProviderFailure { Disabled, Unavailable, RateLimited, Timeout, InvalidResponse }
public sealed class MediaProviderException(ProviderFailure failure, int? retryAfterSeconds = null) : Exception("Metadata provider request failed.")
{
    public ProviderFailure Failure { get; } = failure;
    public int? RetryAfterSeconds { get; } = retryAfterSeconds;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ImportMediaRequest(string? Provider, string? ExternalId, string? MediaType);
public sealed record ImportMediaResult(Guid MediaId, bool Created);
public interface IMediaImportService
{
    Task<ImportMediaResult?> ImportAsync(string provider, string externalId, string type, CancellationToken cancellationToken);
}

public static class MediaProviderInput
{
    public static bool TryId(string? input, out string canonical)
    {
        canonical = "";
        if (string.IsNullOrEmpty(input) || input.Length > 10 || input.Any(c => c is < '0' or > '9') ||
            !int.TryParse(input, NumberStyles.None, CultureInfo.InvariantCulture, out var value) || value <= 0) return false;
        canonical = value.ToString(CultureInfo.InvariantCulture);
        return true;
    }

    public static bool ValidType(string? type) => type is MediaTypes.Movie or MediaTypes.TvShow;

    public static bool TryIdentity(string? provider, string? type, string? externalId, out ProviderIdentity identity)
    {
        identity = null!;
        if (provider == "tmdb" && ValidType(type) && TryId(externalId, out var tmdbId))
        {
            var ns = type == MediaTypes.Movie ? "movie" : "tv";
            identity = new(provider, ns, tmdbId, $"https://www.themoviedb.org/{ns}/{tmdbId}"); return true;
        }
        if (provider == "igdb" && type == MediaTypes.VideoGame && TryId(externalId, out var igdbId))
        {
            identity = new(provider, "game", igdbId, null); return true;
        }
        if (provider == "musicbrainz" && type == MediaTypes.Album && externalId is { Length: 36 }
            && Guid.TryParseExact(externalId, "D", out var uuid) && uuid != Guid.Empty)
        {
            var id = uuid.ToString("D");
            identity = new(provider, "release-group", id, $"https://musicbrainz.org/release-group/{id}"); return true;
        }
        return false;
    }
}
