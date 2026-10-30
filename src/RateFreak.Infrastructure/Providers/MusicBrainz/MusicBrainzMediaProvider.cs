using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RateFreak.Application.MetadataImport;
using RateFreak.Domain.Collections;

namespace RateFreak.Infrastructure.Providers.MusicBrainz;

public sealed class MusicBrainzMediaProvider(HttpClient client,IOptions<MusicBrainzOptions> options,
    MusicBrainzRequestGate gate,TimeProvider clock,ILogger<MusicBrainzMediaProvider> logger) : IMediaProvider
{
    private readonly MusicBrainzOptions settings=options.Value;
    public ProviderCapability Capability => new("musicbrainz",settings.Enabled,["album"],"Music metadata provided by MusicBrainz.","https://musicbrainz.org");
    public async Task<ExternalSearchPage> SearchAsync(string query,string type,int page,CancellationToken cancellationToken)
    {
        if (type!="album" || !CollectionRules.ValidText(query.Trim(),200) || page is <1 or >500) throw new ArgumentException("Invalid provider search input.");
        var literal=query.Trim().Replace("\\","\\\\",StringComparison.Ordinal).Replace("\"","\\\"",StringComparison.Ordinal);
        var search=$"(releasegroup:\"{literal}\" OR artist:\"{literal}\") AND primarytype:album";
        using var json=await GetJsonAsync($"release-group?query={Uri.EscapeDataString(search)}&limit=20&offset={((page-1)*20).ToString(CultureInfo.InvariantCulture)}&fmt=json",cancellationToken);
        if (json is null || json.RootElement.ValueKind!=JsonValueKind.Object || !json.RootElement.TryGetProperty("release-groups",out var rows) || rows.ValueKind!=JsonValueKind.Array || rows.GetArrayLength()>20)
            throw new MediaProviderException(ProviderFailure.InvalidResponse);
        var items=new List<ExternalMediaResult>();
        foreach (var row in rows.EnumerateArray()) { var mapped=MusicBrainzMapping.Map(row); if(mapped is not null) items.Add(mapped); }
        return new("musicbrainz",page,items);
    }
    public async Task<ExternalMediaResult?> GetDetailsAsync(string externalId,string type,CancellationToken cancellationToken)
    {
        if (!MediaProviderInput.TryIdentity("musicbrainz",type,externalId,out var identity)) throw new ArgumentException("Invalid provider identity.");
        using var json=await GetJsonAsync($"release-group/{identity.ExternalId}?inc=artist-credits&fmt=json",cancellationToken);
        if (json is null) return null;
        var mapped=MusicBrainzMapping.Map(json.RootElement);
        // Check identity even when a valid non-album response is excluded.
        if (json.RootElement.ValueKind!=JsonValueKind.Object || !json.RootElement.TryGetProperty("id",out var id)
            || id.ValueKind!=JsonValueKind.String || !Guid.TryParseExact(id.GetString(),"D",out var uuid) || uuid.ToString("D")!=identity.ExternalId)
            throw new MediaProviderException(ProviderFailure.InvalidResponse);
        return mapped;
    }
    private async Task<JsonDocument?> GetJsonAsync(string path,CancellationToken caller)
    {
        caller.ThrowIfCancellationRequested();
        if (!settings.Enabled) throw new MediaProviderException(ProviderFailure.Disabled);
        if (!gate.TryEnter()) throw new MediaProviderException(ProviderFailure.Unavailable,1);
        using var timeout=new CancellationTokenSource(TimeSpan.FromMilliseconds(settings.TimeoutMilliseconds),clock);
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(caller,timeout.Token);
        try
        {
            for(var attempt=0;attempt<2;attempt++)
            {
                await gate.PaceAsync(deadline.Token);
                using var request=new HttpRequestMessage(HttpMethod.Get,"https://musicbrainz.org/ws/2/"+path);
                request.Headers.UserAgent.ParseAdd(settings.UserAgent); request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                HttpResponseMessage response;
                try { response=await client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,deadline.Token); }
                catch(HttpRequestException) when(attempt==0) { continue; }
                using(response)
                {
                    if(response.StatusCode==HttpStatusCode.NotFound) return null;
                    var delay=RetryDelay(response.Headers.RetryAfter);
                    var retry=delay is { } duration ? (int?)Math.Clamp(Math.Ceiling(duration.TotalSeconds),1,86400) : null;
                    var limited=response.StatusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable;
                    if(limited) gate.Cooldown(delay ?? TimeSpan.FromSeconds(1));
                    var transient=response.StatusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.InternalServerError or HttpStatusCode.BadGateway or HttpStatusCode.GatewayTimeout;
                    if(attempt==0 && (transient || limited && (delay ?? TimeSpan.FromSeconds(1))<=TimeSpan.FromSeconds(1))) continue;
                    if(!response.IsSuccessStatusCode)
                    {
                        logger.LogWarning("MusicBrainz request failed with status {StatusCode}",(int)response.StatusCode);
                        throw new MediaProviderException(limited?ProviderFailure.RateLimited:ProviderFailure.Unavailable,limited?retry??1:null);
                    }
                    if(response.Content.Headers.ContentLength>1024*1024) throw new MediaProviderException(ProviderFailure.InvalidResponse);
                    await using var stream=await response.Content.ReadAsStreamAsync(deadline.Token);
                    using var buffer=new MemoryStream(); var bytes=new byte[8192]; int read;
                    while((read=await stream.ReadAsync(bytes,deadline.Token))!=0)
                    { if(buffer.Length+read>1024*1024) throw new MediaProviderException(ProviderFailure.InvalidResponse); buffer.Write(bytes,0,read); }
                    return JsonDocument.Parse(buffer.ToArray(),new JsonDocumentOptions { MaxDepth=32,AllowDuplicateProperties=false });
                }
            }
            throw new MediaProviderException(ProviderFailure.Unavailable);
        }
        catch(OperationCanceledException) when(!caller.IsCancellationRequested) { throw new MediaProviderException(ProviderFailure.Timeout); }
        catch(HttpRequestException) { throw new MediaProviderException(ProviderFailure.Unavailable); }
        catch(IOException) { throw new MediaProviderException(ProviderFailure.Unavailable); }
        catch(JsonException) { throw new MediaProviderException(ProviderFailure.InvalidResponse); }
        finally { gate.Exit(); }
    }
    private TimeSpan? RetryDelay(RetryConditionHeaderValue? header)
    {
        var duration=header?.Delta ?? header?.Date-clock.GetUtcNow();
        return duration is null ? null : duration < TimeSpan.FromSeconds(1) ? TimeSpan.FromSeconds(1) : duration;
    }
}
