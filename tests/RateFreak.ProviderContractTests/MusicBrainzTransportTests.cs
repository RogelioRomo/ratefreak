using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using RateFreak.Application.MetadataImport;
using RateFreak.Infrastructure.Providers.MusicBrainz;

namespace RateFreak.ProviderContractTests;

public sealed partial class MusicBrainzTests
{
    [Fact]
    public async Task Retries_and_successive_clients_are_paced_with_one_shared_gate()
    {
        var clock=new ManualClock(); var starts=new List<long>();
        using var handler=new Handler((_,_)=> { starts.Add(clock.GetTimestamp()); return Task.FromResult(starts.Count==1?Json("{}",HttpStatusCode.BadGateway):Json(Album())); });
        using var client=new HttpClient(handler); using var gate=new MusicBrainzRequestGate(clock);
        var first=Provider(client,gate,clock).GetDetailsAsync(Id,"album",Ct);
        Assert.False(first.IsCompleted); Assert.Single(starts);
        clock.Advance(TimeSpan.FromSeconds(1)); Assert.NotNull(await first);
        var second=Provider(client,gate,clock).GetDetailsAsync(Id,"album",Ct);
        Assert.False(second.IsCompleted); Assert.Equal(2,starts.Count);
        clock.Advance(TimeSpan.FromSeconds(1)); Assert.NotNull(await second);
        Assert.Equal(new long[] {0,TimeSpan.TicksPerSecond,2*TimeSpan.TicksPerSecond},starts);
    }
    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests)] [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task Long_retry_after_sets_shared_cooldown_without_outbound_retries(HttpStatusCode status)
    {
        var clock=new ManualClock(); var calls=0;
        using var handler=new Handler((_,_)=> { calls++; var response=Json("private upstream",status); response.Headers.RetryAfter=new RetryConditionHeaderValue(TimeSpan.FromSeconds(60)); return Task.FromResult(response); });
        using var client=new HttpClient(handler); using var gate=new MusicBrainzRequestGate(clock);
        var first=await Assert.ThrowsAsync<MediaProviderException>(()=>Provider(client,gate,clock).GetDetailsAsync(Id,"album",Ct));
        Assert.Equal(60,first.RetryAfterSeconds); Assert.Equal(ProviderFailure.RateLimited,first.Failure);
        var second=await Assert.ThrowsAsync<MediaProviderException>(()=>Provider(client,gate,clock).GetDetailsAsync(Id,"album",Ct));
        Assert.Equal(60,second.RetryAfterSeconds); Assert.Equal(1,calls); Assert.DoesNotContain("private upstream",first.ToString());
        clock.Advance(TimeSpan.FromSeconds(60));
        await Assert.ThrowsAsync<MediaProviderException>(()=>Provider(client,gate,clock).GetDetailsAsync(Id,"album",Ct)); Assert.Equal(2,calls);
    }
    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)] [InlineData(HttpStatusCode.Forbidden)] [InlineData(HttpStatusCode.Redirect)]
    public async Task Permanent_failures_are_not_retried(HttpStatusCode status)
    {
        var calls=0; using var handler=new Handler((_,_)=> { calls++; return Task.FromResult(Json("private",status)); });
        using var client=new HttpClient(handler); using var gate=new MusicBrainzRequestGate(TimeProvider.System);
        Assert.Equal(ProviderFailure.Unavailable,(await Assert.ThrowsAsync<MediaProviderException>(()=>Provider(client,gate).GetDetailsAsync(Id,"album",Ct))).Failure); Assert.Equal(1,calls);
    }
    [Fact]
    public async Task Timeout_cancellation_and_busy_rejection_release_the_gate()
    {
        var clock=new ManualClock(); var calls=0;
        using var handler=new Handler(async (_,ct)=> { calls++; await Task.Delay(Timeout.InfiniteTimeSpan,ct); return Json(Album()); });
        using var client=new HttpClient(handler); using var gate=new MusicBrainzRequestGate(clock); var provider=Provider(client,gate,clock);
        var pending=provider.GetDetailsAsync(Id,"album",Ct);
        var busy=await Assert.ThrowsAsync<MediaProviderException>(()=>provider.GetDetailsAsync(Id,"album",Ct)); Assert.Equal(ProviderFailure.Unavailable,busy.Failure); Assert.Equal(1,calls);
        clock.Advance(TimeSpan.FromSeconds(6)); Assert.Equal(ProviderFailure.Timeout,(await Assert.ThrowsAsync<MediaProviderException>(()=>pending)).Failure);
        Assert.True(gate.TryEnter()); gate.Exit();
        using var caller=new CancellationTokenSource(); var cancelled=provider.GetDetailsAsync(Id,"album",caller.Token); await caller.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>cancelled); Assert.True(gate.TryEnter()); gate.Exit();
    }
    [Theory]
    [InlineData("title","\"bad\\u0000\"")]
    [InlineData("title","\"\\uD800\"")]
    [InlineData("primary-type","null")]
    [InlineData("artist-credit","null")]
    [InlineData("artist-credit","[{}]")]
    [InlineData("secondary-types","[3]")]
    [InlineData("id","\"b7ffd2af-418f-4be2-bdd1-22f8b48613da\"")]
    public async Task Invalid_normalized_fields_are_provider_errors(string field,string raw)
    {
        using var source=JsonDocument.Parse(Album());
        var body="{"+string.Join(",",source.RootElement.EnumerateObject().Select(p=>JsonSerializer.Serialize(p.Name)+":"+(p.Name==field?raw:p.Value.GetRawText())))+"}";
        using var handler=new Handler((_,_)=>Task.FromResult(Json(body))); using var client=new HttpClient(handler); using var gate=new MusicBrainzRequestGate(TimeProvider.System);
        Assert.Equal(ProviderFailure.InvalidResponse,(await Assert.ThrowsAsync<MediaProviderException>(()=>Provider(client,gate).GetDetailsAsync(Id,"album",Ct))).Failure);
    }
    private sealed class UnboundedContent(byte[] data) : HttpContent
    {
        protected override bool TryComputeLength(out long length) { length=0; return false; }
        protected override Task SerializeToStreamAsync(Stream stream,TransportContext? context) => stream.WriteAsync(data).AsTask();
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Oversized_response_is_rejected_with_and_without_declared_length(bool streamed)
    {
        using var handler=new Handler((_,_)=>Task.FromResult(streamed?new HttpResponseMessage(HttpStatusCode.OK){Content=new UnboundedContent(new byte[1024*1024+1])}:Json(new string('x',1024*1024+1))));
        using var client=new HttpClient(handler); using var gate=new MusicBrainzRequestGate(TimeProvider.System);
        Assert.Equal(ProviderFailure.InvalidResponse,(await Assert.ThrowsAsync<MediaProviderException>(()=>Provider(client,gate).GetDetailsAsync(Id,"album",Ct))).Failure);
    }
    [Fact]
    public async Task Normalization_bounds_reject_oversized_credits_types_and_titles()
    {
        foreach(var body in new[] {
            Album().Replace("Year Zero",new string('x',301)),
            Album().Replace("\"secondary-types\":[]","\"secondary-types\":["+string.Join(',',Enumerable.Repeat("\"Live\"",21))+"]"),
            Album().Replace("\"artist-credit\":[","\"artist-credit\":["+string.Join(',',Enumerable.Repeat("{}",51))+","),
            Album().Replace("Nine Inch Nails",new string('x',301)),
            Album().Replace("\"joinphrase\":\"\"","\"joinphrase\":\""+new string('x',101)+"\"") })
        {
            using var handler=new Handler((_,_)=>Task.FromResult(Json(body))); using var client=new HttpClient(handler); using var gate=new MusicBrainzRequestGate(TimeProvider.System);
            Assert.Equal(ProviderFailure.InvalidResponse,(await Assert.ThrowsAsync<MediaProviderException>(()=>Provider(client,gate).GetDetailsAsync(Id,"album",Ct))).Failure);
        }
    }
    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests)] [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task Short_throttling_response_retries_once_after_a_full_second(HttpStatusCode status)
    {
        var clock=new ManualClock();var calls=0;
        using var handler=new Handler((_,_)=>Task.FromResult(++calls==1?Json("{}",status):Json(Album())));
        using var client=new HttpClient(handler);using var gate=new MusicBrainzRequestGate(clock);
        var task=Provider(client,gate,clock).GetDetailsAsync(Id,"album",Ct);Assert.Equal(1,calls);Assert.False(task.IsCompleted);
        clock.Advance(TimeSpan.FromSeconds(1));Assert.NotNull(await task);Assert.Equal(2,calls);
    }
    [Fact]
    public async Task Excessively_deep_json_is_rejected_before_mapping()
    {
        var body="{\"unused\":"+new string('[',33)+"0"+new string(']',33)+"}";
        using var handler=new Handler((_,_)=>Task.FromResult(Json(body)));using var client=new HttpClient(handler);using var gate=new MusicBrainzRequestGate(TimeProvider.System);
        Assert.Equal(ProviderFailure.InvalidResponse,(await Assert.ThrowsAsync<MediaProviderException>(()=>Provider(client,gate).GetDetailsAsync(Id,"album",Ct))).Failure);
    }

}
