using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
namespace RateFreak.Infrastructure.Updates;
public sealed record ReleaseResult(string Kind,string? Version=null,string? ETag=null,DateTimeOffset? RetryAt=null);
public sealed class GitHubReleaseClient(HttpClient client,TimeProvider clock)
{
    public const string Repository="https://github.com/RogelioRomo/ratefreak";
    public async Task<ReleaseResult> FetchAsync(string? etag)
    {
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var request=new HttpRequestMessage(HttpMethod.Get,"https://api.github.com/repos/RogelioRomo/ratefreak/releases/latest");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.Add("X-GitHub-Api-Version","2026-03-10");
        var identity=BuildIdentity.Embedded();
        request.Headers.UserAgent.ParseAdd("RateFreak/"+(SemanticVersion.TryParse(identity.Version,out _)?identity.Version:"development"));
        if(etag is not null)request.Headers.IfNoneMatch.Add(EntityTagHeaderValue.Parse(etag));
        using var response=await client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,timeout.Token);
        if(response.StatusCode==HttpStatusCode.NotModified)return new("not-modified");
        if(response.StatusCode==HttpStatusCode.NotFound)return new("no-release");
        if(response.StatusCode!=HttpStatusCode.OK)
        {
            DateTimeOffset? retry=response.Headers.RetryAfter?.Date;
            if(response.Headers.RetryAfter?.Delta is {} delta)retry=clock.GetUtcNow()+delta;
            if(response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests &&
                response.Headers.TryGetValues("X-RateLimit-Reset",out var values)&&long.TryParse(values.FirstOrDefault(),CultureInfo.InvariantCulture,out var seconds)&&seconds is >=0 and <=253402300799)
            {var reset=DateTimeOffset.FromUnixTimeSeconds(seconds);if(retry is null||reset>retry)retry=reset;}
            return new("unavailable",RetryAt:retry);
        }
        const int limit=256*1024;
        if(response.Content.Headers.ContentLength>limit)throw new InvalidDataException("Oversized release response.");
        await using var stream=await response.Content.ReadAsStreamAsync(timeout.Token);
        using var bytes=new MemoryStream();var buffer=new byte[8192];int count;
        while((count=await stream.ReadAsync(buffer,timeout.Token))>0)
        {if(bytes.Length+count>limit)throw new InvalidDataException("Oversized release response.");bytes.Write(buffer,0,count);}
        using var document=JsonDocument.Parse(bytes.ToArray());var root=document.RootElement;
        if(root.ValueKind!=JsonValueKind.Object||root.EnumerateObject().GroupBy(p=>p.Name,StringComparer.Ordinal).Any(g=>g.Count()!=1)||
            root.GetProperty("draft").GetBoolean()||root.GetProperty("prerelease").GetBoolean()||
            !DateTimeOffset.TryParse(root.GetProperty("published_at").GetString(),CultureInfo.InvariantCulture,DateTimeStyles.None,out _))
            throw new InvalidDataException("Invalid published release.");
        var tag=root.GetProperty("tag_name").GetString();
        if(tag is null||!tag.StartsWith('v')||!SemanticVersion.TryParse(tag[1..],out var version)||version!.IsPrerelease)
            throw new InvalidDataException("Invalid stable tag.");
        return new("release",tag[1..],response.Headers.ETag?.ToString());
    }
}
