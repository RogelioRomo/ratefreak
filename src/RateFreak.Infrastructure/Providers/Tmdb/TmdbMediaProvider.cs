using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RateFreak.Application.MetadataImport;
using RateFreak.Domain.Catalog;

namespace RateFreak.Infrastructure.Providers.Tmdb;

public sealed class TmdbMediaProvider(HttpClient client, IOptions<TmdbOptions> options,
    TmdbRequestGate gate, ILogger<TmdbMediaProvider> logger) : IMediaProvider
{
    private const int MaxResponseBytes = 1024 * 1024;
    private readonly TmdbOptions settings = options.Value;
    public ProviderCapability Capability => new("tmdb", settings.Enabled, ["movie", "tv_show"],
        "This product uses the TMDB API but is not endorsed or certified by TMDB.", "https://www.themoviedb.org");

    public async Task<ExternalSearchPage> SearchAsync(string query, string type, int page, CancellationToken cancellationToken)
    {
        if (!MediaProviderInput.ValidType(type) || query.Trim().Length is < 1 or > 200 || query.Any(char.IsControl) || page is < 1 or > 500)
            throw new ArgumentException("Invalid provider search input.");
        using var json = await GetJsonAsync($"search/{Namespace(type)}?query={Uri.EscapeDataString(query.Trim())}&page={page.ToString(CultureInfo.InvariantCulture)}&include_adult=false&language={settings.Language}", cancellationToken);
        if (json is null || json.RootElement.ValueKind != JsonValueKind.Object || !json.RootElement.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array || results.GetArrayLength() > 20)
            throw new MediaProviderException(ProviderFailure.InvalidResponse);
        var items = new List<ExternalMediaResult>();
        foreach (var item in results.EnumerateArray())
        {
            var mapped = Map(item, type);
            if (mapped is not null) items.Add(mapped);
        }
        return new ExternalSearchPage("tmdb", page, items);
    }

    public async Task<ExternalMediaResult?> GetDetailsAsync(string externalId, string type, CancellationToken cancellationToken)
    {
        if (!MediaProviderInput.TryId(externalId, out var id) || !MediaProviderInput.ValidType(type))
            throw new ArgumentException("Invalid provider identity.");
        using var json = await GetJsonAsync($"{Namespace(type)}/{id}?language={settings.Language}", cancellationToken);
        if (json is null) return null;
        var result = Map(json.RootElement, type);
        if (result is not null && result.ExternalId != id) throw new MediaProviderException(ProviderFailure.InvalidResponse);
        return result;
    }

    private async Task<JsonDocument?> GetJsonAsync(string path, CancellationToken cancellationToken)
    {
        if (!settings.Enabled) throw new MediaProviderException(ProviderFailure.Disabled);
        cancellationToken.ThrowIfCancellationRequested();
        if (!gate.TryEnter()) throw new MediaProviderException(ProviderFailure.Unavailable, 1);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(settings.TimeoutMilliseconds);
        try
        {
            for (var attempt = 0; ; attempt++)
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.themoviedb.org/3/" + path);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.AccessToken);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                HttpResponseMessage response;
                try { response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token); }
                catch (HttpRequestException) when (attempt == 0)
                {
                    await Task.Delay(200, deadline.Token);
                    continue;
                }
                using (response)
                {
                    if (response.StatusCode == HttpStatusCode.NotFound) return null;
                    var transient = response.StatusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.BadGateway or
                        HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout or HttpStatusCode.InternalServerError;
                    var retryAfter = RetrySeconds(response.Headers.RetryAfter);
                    var limited = response.StatusCode == HttpStatusCode.TooManyRequests;
                    if (attempt == 0 && (transient || (limited && retryAfter is >= 0 and <= 1)))
                    {
                        await Task.Delay(limited ? retryAfter!.Value * 1000 : 200, deadline.Token);
                        continue;
                    }
                    if (!response.IsSuccessStatusCode)
                    {
                        logger.LogWarning("TMDB request failed with status {StatusCode}", (int)response.StatusCode);
                        throw new MediaProviderException(limited ? ProviderFailure.RateLimited : ProviderFailure.Unavailable,
                            limited ? Math.Max(1, retryAfter ?? 1) : null);
                    }
                    if (response.Content.Headers.ContentLength > MaxResponseBytes)
                        throw new MediaProviderException(ProviderFailure.InvalidResponse);
                    await using var stream = await response.Content.ReadAsStreamAsync(deadline.Token);
                    using var buffer = new MemoryStream();
                    var chunk = new byte[8192];
                    int read;
                    while ((read = await stream.ReadAsync(chunk, deadline.Token)) != 0)
                    {
                        if (buffer.Length + read > MaxResponseBytes) throw new MediaProviderException(ProviderFailure.InvalidResponse);
                        buffer.Write(chunk, 0, read);
                    }
                    return JsonDocument.Parse(buffer.ToArray(), new JsonDocumentOptions { MaxDepth = 32 });
                }
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { throw new MediaProviderException(ProviderFailure.Timeout); }
        catch (HttpRequestException) { throw new MediaProviderException(ProviderFailure.Unavailable); }
        catch (IOException) { throw new MediaProviderException(ProviderFailure.Unavailable); }
        catch (JsonException) { throw new MediaProviderException(ProviderFailure.InvalidResponse); }
        finally { gate.Exit(); }
    }

    private static int? RetrySeconds(RetryConditionHeaderValue? header)
    {
        var seconds = header?.Delta?.TotalSeconds ?? (header?.Date - DateTimeOffset.UtcNow)?.TotalSeconds;
        return seconds is null ? null : (int)Math.Clamp(Math.Ceiling(seconds.Value), 0, 86400);
    }

    internal static string Namespace(string type) => type == MediaTypes.Movie ? "movie" : "tv";

    private static ExternalMediaResult? Map(JsonElement item, string type)
    {
        if (item.ValueKind != JsonValueKind.Object) throw new MediaProviderException(ProviderFailure.InvalidResponse);
        if (item.TryGetProperty("adult", out var adult))
        {
            if (adult.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) throw new MediaProviderException(ProviderFailure.InvalidResponse);
            if (adult.GetBoolean()) return null;
        }
        if (!item.TryGetProperty("id", out var identity) || identity.ValueKind != JsonValueKind.Number || !identity.TryGetInt32(out var id) || id <= 0)
            throw new MediaProviderException(ProviderFailure.InvalidResponse);
        var title = Text(item, type == MediaTypes.Movie ? "title" : "name", 300)?.Trim();
        if (string.IsNullOrWhiteSpace(title) || title.Any(char.IsControl)) throw new MediaProviderException(ProviderFailure.InvalidResponse);
        var description = Text(item, "overview", 20000);
        var dateText = Text(item, type == MediaTypes.Movie ? "release_date" : "first_air_date", 30);
        DateOnly? date = DateOnly.TryParseExact(dateText, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed) ? parsed : null;
        var poster = Text(item, "poster_path", 200);
        string? image = null;
        if (!string.IsNullOrEmpty(poster))
        {
            if (!Regex.IsMatch(poster, "^/[A-Za-z0-9_-]+\\.[A-Za-z0-9]+$", RegexOptions.CultureInvariant))
                throw new MediaProviderException(ProviderFailure.InvalidResponse);
            image = "https://image.tmdb.org/t/p/w500" + poster;
        }
        var metadata = new Dictionary<string, object>();
        foreach (var field in type == MediaTypes.Movie ? new[] { "runtime" } : new[] { "number_of_seasons", "number_of_episodes" })
        {
            if (!item.TryGetProperty(field, out var value) || value.ValueKind == JsonValueKind.Null) continue;
            if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var number) || number < 0) throw new MediaProviderException(ProviderFailure.InvalidResponse);
            metadata[field] = number;
        }
        return new ExternalMediaResult("tmdb", id.ToString(CultureInfo.InvariantCulture), type, title, description,
            date, image, JsonSerializer.SerializeToElement(metadata));
    }

    private static string? Text(JsonElement item, string name, int max)
    {
        if (!item.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null) return null;
        if (value.ValueKind != JsonValueKind.String) throw new MediaProviderException(ProviderFailure.InvalidResponse);
        string text;
        try { text = value.GetString()!; }
        catch (InvalidOperationException) { throw new MediaProviderException(ProviderFailure.InvalidResponse); }
        if (text.Length > max || text.Contains('\0')) throw new MediaProviderException(ProviderFailure.InvalidResponse);
        return text;
    }
}
