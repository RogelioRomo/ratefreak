using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RateFreak.Application.MetadataImport;
using RateFreak.Domain.Collections;
using RateFreak.Infrastructure.MetadataImport;

namespace RateFreak.Infrastructure.Providers.OpenLibrary;

public sealed class OpenLibraryMediaProvider(HttpClient client, IOptions<OpenLibraryOptions> options,
    OpenLibraryRequestGate gate, ProviderMetadataCache cache, TimeProvider clock, ILogger<OpenLibraryMediaProvider> logger) : IMediaProvider
{
    private readonly OpenLibraryOptions settings = options.Value;
    public ProviderCapability Capability => new("openlibrary", settings.Enabled, ["book"], "Book metadata provided by Open Library, an Internet Archive initiative.", "https://openlibrary.org");

    public async Task<ExternalSearchPage> SearchAsync(string query, string type, int page, CancellationToken cancellationToken)
    {
        if (type != "book" || !CollectionRules.ValidText(query.Trim(), 200) || page is < 1 or > 500) throw new ArgumentException("Invalid provider search input.");
        var literal = "\"" + query.Trim().Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
        using var json = await GetJsonAsync($"search.json?q={Uri.EscapeDataString(literal)}&fields=key,title,author_key,author_name,first_publish_year,cover_i&limit=20&page={page.ToString(CultureInfo.InvariantCulture)}", cancellationToken);
        if (json is null || json.RootElement.ValueKind != JsonValueKind.Object || !json.RootElement.TryGetProperty("docs", out var docs) || docs.ValueKind != JsonValueKind.Array || docs.GetArrayLength() > 20) throw OpenLibraryMapping.Invalid();
        return new("openlibrary", page, docs.EnumerateArray().Select(row => OpenLibraryMapping.Map(row, search: true).Result()).ToArray());
    }

    public async Task<ExternalMediaResult?> GetDetailsAsync(string externalId, string type, CancellationToken cancellationToken)
    {
        if (!MediaProviderInput.TryIdentity("openlibrary", type, externalId, out var identity)) throw new ArgumentException("Invalid provider identity.");
        using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(settings.TimeoutMilliseconds), clock);
        using var operation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        try
        {
            using var json = await GetJsonAsync($"works/{identity.ExternalId}.json", operation.Token);
            if (json is null) return null;
            var work = OpenLibraryMapping.Map(json.RootElement, search: false);
            if (work.Id != identity.ExternalId) throw OpenLibraryMapping.Invalid();
            var ids = work.Authors.Take(20).Select(a => a.Id).Order(StringComparer.Ordinal).ToArray();
            if (ids.Length > 0)
            {
                try
                {
                    var key = JsonSerializer.Serialize(new[] { "openlibrary", "authors", string.Join(',', ids) });
                    var names = await cache.GetAsync(key, TimeSpan.FromHours(1), async ct =>
                    {
                        var query = "key:(" + string.Join(" OR ", ids.Select(id => "/authors/" + id)) + ")";
                        using var authors = await GetJsonAsync("search/authors.json?q=" + Uri.EscapeDataString(query) + "&limit=20", ct);
                        return authors is null ? null : OpenLibraryMapping.MapAuthors(authors.RootElement, ids.ToHashSet(StringComparer.Ordinal)).ToArray();
                    }, operation.Token);
                    if (names is not null)
                    {
                        var byId = names.ToDictionary(a => a.Id, StringComparer.Ordinal);
                        work = work with { Authors = work.Authors.Select(a => byId.GetValueOrDefault(a.Id) ?? a).ToArray() };
                    }
                }
                catch (MediaProviderException error) when (error.Failure is ProviderFailure.Unavailable or ProviderFailure.RateLimited or ProviderFailure.Timeout) { }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { }
            }
            cancellationToken.ThrowIfCancellationRequested();
            return work.Result();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { throw new MediaProviderException(ProviderFailure.Timeout); }
    }

    private async Task<JsonDocument?> GetJsonAsync(string path, CancellationToken caller)
    {
        caller.ThrowIfCancellationRequested();
        if (!settings.Enabled) throw new MediaProviderException(ProviderFailure.Disabled);
        if (!gate.TryEnter()) throw new MediaProviderException(ProviderFailure.Unavailable, 1);
        using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(settings.TimeoutMilliseconds), clock);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(caller, timeout.Token);
        try
        {
            for (var attempt = 0; attempt < 2; attempt++)
            {
                await gate.PaceAsync(deadline.Token);
                using var request = new HttpRequestMessage(HttpMethod.Get, "https://openlibrary.org/" + path);
                request.Headers.UserAgent.ParseAdd(settings.UserAgent); request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                HttpResponseMessage response;
                try { response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token); }
                catch (HttpRequestException) when (attempt == 0) { continue; }
                using (response)
                {
                    if (response.StatusCode == HttpStatusCode.NotFound) return null;
                    if (response.StatusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable)
                    {
                        var seconds = response.Headers.RetryAfter?.Delta?.TotalSeconds ?? (response.Headers.RetryAfter?.Date - clock.GetUtcNow())?.TotalSeconds ?? 1;
                        var retry = (int)Math.Clamp(Math.Ceiling(seconds), 1, 86400); gate.Cooldown(TimeSpan.FromSeconds(retry));
                        throw new MediaProviderException(ProviderFailure.RateLimited, retry);
                    }
                    if (attempt == 0 && response.StatusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.InternalServerError or HttpStatusCode.BadGateway or HttpStatusCode.GatewayTimeout) continue;
                    if (!response.IsSuccessStatusCode)
                    {
                        logger.LogWarning("Open Library request failed with status {StatusCode}", (int)response.StatusCode);
                        throw new MediaProviderException(ProviderFailure.Unavailable);
                    }
                    if (response.Content.Headers.ContentLength > 1024 * 1024) throw OpenLibraryMapping.Invalid();
                    await using var stream = await response.Content.ReadAsStreamAsync(deadline.Token);
                    using var buffer = new MemoryStream(); var bytes = new byte[8192]; int read;
                    while ((read = await stream.ReadAsync(bytes, deadline.Token)) != 0)
                    { if (buffer.Length + read > 1024 * 1024) throw OpenLibraryMapping.Invalid(); buffer.Write(bytes, 0, read); }
                    return JsonDocument.Parse(buffer.ToArray(), new JsonDocumentOptions { MaxDepth = 32, AllowDuplicateProperties = false });
                }
            }
            throw new MediaProviderException(ProviderFailure.Unavailable);
        }
        catch (OperationCanceledException) when (!caller.IsCancellationRequested) { throw new MediaProviderException(ProviderFailure.Timeout); }
        catch (HttpRequestException) { throw new MediaProviderException(ProviderFailure.Unavailable); }
        catch (IOException) { throw new MediaProviderException(ProviderFailure.Unavailable); }
        catch (JsonException) { throw OpenLibraryMapping.Invalid(); }
        finally { gate.Exit(); }
    }
}
