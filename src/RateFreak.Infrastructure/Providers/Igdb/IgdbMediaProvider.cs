using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RateFreak.Application.MetadataImport;
using RateFreak.Domain.Collections;
using static RateFreak.Infrastructure.Providers.Igdb.IgdbJson;

namespace RateFreak.Infrastructure.Providers.Igdb;

public sealed class IgdbMediaProvider(HttpClient games, HttpClient auth, IOptions<IgdbOptions> options,
    IgdbSession session, TimeProvider clock) : IMediaProvider
{
    private readonly IgdbOptions settings = options.Value;
    private const string Fields = "fields id,name,summary,game_type,version_parent,first_release_date,cover.image_id,platforms.name,genres.name; ";
    public ProviderCapability Capability => new("igdb", settings.Enabled, ["video_game"], "Game metadata provided by IGDB.", "https://www.igdb.com");

    public async Task<ExternalSearchPage> SearchAsync(string query, string type, int page, CancellationToken cancellationToken)
    {
        if (type != "video_game" || !CollectionRules.ValidText(query.Trim(), 200) || page is < 1 or > 500) throw new ArgumentException("Invalid provider search input.");
        var literal = query.Trim().Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
        using var json = await RequestAsync(Fields + $"search \"{literal}\"; where game_type = (0,8,9) & version_parent = null; limit 20; offset {((page - 1) * 20).ToString(CultureInfo.InvariantCulture)};", cancellationToken);
        var rows = Rows(json, 20);
        var ids = new HashSet<int>();
        var items = new List<ExternalMediaResult>();
        foreach (var row in rows.EnumerateArray())
        {
            if (!ids.Add(IgdbMapping.Identity(row))) throw Invalid();
            var mapped = IgdbMapping.Map(row);
            if (mapped is not null) items.Add(mapped);
        }
        return new("igdb", page, items);
    }
    public async Task<ExternalMediaResult?> GetDetailsAsync(string externalId, string type, CancellationToken cancellationToken)
    {
        if (!MediaProviderInput.TryIdentity("igdb", type, externalId, out var identity)) throw new ArgumentException("Invalid provider identity.");
        using var json = await RequestAsync(Fields + $"where id = {identity.ExternalId}; limit 1;", cancellationToken);
        var rows = Rows(json, 1);
        if (rows.GetArrayLength() == 0) return null;
        if (IgdbMapping.Identity(rows[0]).ToString(CultureInfo.InvariantCulture) != identity.ExternalId) throw Invalid();
        return IgdbMapping.Map(rows[0]);
    }
    private static JsonElement Rows(JsonDocument json, int max)
    {
        if (json.RootElement.ValueKind != JsonValueKind.Array || json.RootElement.GetArrayLength() > max) throw Invalid();
        return json.RootElement;
    }
    private async Task<JsonDocument> RequestAsync(string body, CancellationToken caller)
    {
        caller.ThrowIfCancellationRequested();
        if (!settings.Enabled) throw new MediaProviderException(ProviderFailure.Disabled);
        if (!session.TryEnter()) throw new MediaProviderException(ProviderFailure.Unavailable, 1);
        using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(settings.TimeoutMilliseconds), clock);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(caller, timeout.Token);
        try
        {
            session.RejectCooldown();
            for (var attempt = 0; attempt < 2; attempt++)
            {
                var token = session.CachedToken ?? await TokenAsync(deadline.Token);
                await session.PaceAsync(deadline.Token);
                using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.igdb.com/v4/games");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                request.Headers.Add("Client-ID", settings.ClientId);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                request.Content = new StringContent(body, Encoding.UTF8, "text/plain");
                HttpResponseMessage response;
                try { response = await games.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token); }
                catch (HttpRequestException) when (attempt == 0) { continue; }
                using (response)
                {
                    if (response.StatusCode == HttpStatusCode.Unauthorized)
                    {
                        session.InvalidateToken();
                        if (attempt == 0) continue;
                    }
                    var delay = ApplyCooldown(response);
                    var transient = response.StatusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.InternalServerError or HttpStatusCode.BadGateway or HttpStatusCode.GatewayTimeout;
                    if (attempt == 0 && (transient || delay is { } wait && wait <= TimeSpan.FromSeconds(1))) continue;
                    EnsureSuccess(response);
                    return await ReadAsync(response, 1024 * 1024, deadline.Token);
                }
            }
            throw new MediaProviderException(ProviderFailure.Unavailable);
        }
        catch (OperationCanceledException) when (!caller.IsCancellationRequested) { throw new MediaProviderException(ProviderFailure.Timeout); }
        catch (HttpRequestException) { throw new MediaProviderException(ProviderFailure.Unavailable); }
        catch (IOException) { throw new MediaProviderException(ProviderFailure.Unavailable); }
        catch (JsonException) { throw Invalid(); }
        finally { session.Exit(); }
    }
    private async Task<string> TokenAsync(CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://id.twitch.tv/oauth2/token");
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        { ["client_id"] = settings.ClientId!, ["client_secret"] = settings.ClientSecret!, ["grant_type"] = "client_credentials" });
        using var response = await auth.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        ApplyCooldown(response);
        EnsureSuccess(response);
        using var json = await ReadAsync(response, 16384, ct);
        var root = json.RootElement;
        if (root.ValueKind != JsonValueKind.Object) throw Invalid();
        var token = Text(root, "access_token", 4096);
        if (string.IsNullOrEmpty(token) || !token.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.' or '~' or '+' or '/' or '=')) throw Invalid();
        if (!string.Equals(Text(root, "token_type", 20), "bearer", StringComparison.OrdinalIgnoreCase)) throw Invalid();
        var expiresIn = Int(root, "expires_in");
        session.CacheToken(token, expiresIn);
        return token;
    }
    private TimeSpan? ApplyCooldown(HttpResponseMessage response)
    {
        if (response.StatusCode is not (HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable)) return null;
        var duration = response.Headers.RetryAfter?.Delta ?? response.Headers.RetryAfter?.Date - clock.GetUtcNow() ?? TimeSpan.FromSeconds(1);
        if (duration < TimeSpan.FromSeconds(1)) duration = TimeSpan.FromSeconds(1);
        session.Cooldown(duration);
        return duration;
    }
    private void EnsureSuccess(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;
        var limited = response.StatusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable;
        throw new MediaProviderException(limited ? ProviderFailure.RateLimited : ProviderFailure.Unavailable, limited ? session.RemainingCooldown() : null);
    }
}
