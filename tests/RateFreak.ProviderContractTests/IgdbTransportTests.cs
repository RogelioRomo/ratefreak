using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RateFreak.Application.MetadataImport;
using RateFreak.Infrastructure.Providers.Igdb;

namespace RateFreak.ProviderContractTests;

public sealed partial class IgdbTests
{
    [Fact]
    public async Task Cached_token_reused_across_instances_then_renewed_before_expiry()
    {
        var clock=new ManualClock();var tokens=0;var gamesCalls=0;
        using var authHandler=new Handler((_,_)=> { tokens++;return Task.FromResult(Json(TokenJson)); });
        using var handler=new Handler((_,_)=> { gamesCalls++;return Task.FromResult(Json("["+Game+"]")); });
        using var games=new HttpClient(handler);using var auth=new HttpClient(authHandler);using var session=new IgdbSession(clock);
        Assert.NotNull(await Provider(games,auth,session,clock).GetDetailsAsync("1942","video_game",Ct));
        clock.Advance(TimeSpan.FromMilliseconds(250));
        Assert.NotNull(await Provider(games,auth,session,clock).GetDetailsAsync("1942","video_game",Ct));Assert.Equal(1,tokens);
        clock.Advance(TimeSpan.FromMinutes(55));
        Assert.NotNull(await Provider(games,auth,session,clock).GetDetailsAsync("1942","video_game",Ct));Assert.Equal(2,tokens);Assert.Equal(3,gamesCalls);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Unauthorized_renews_token_once_with_two_games_attempts_maximum(bool permanent)
    {
        var tokens=0;var sends=0;
        using var authHandler=new Handler((_,_)=>Task.FromResult(Json(TokenJson.Replace("fake-app-token","token"+ ++tokens))));
        using var handler=new Handler((request,_)=>
        {
            sends++;Assert.Equal("token"+sends,request.Headers.Authorization!.Parameter);
            return Task.FromResult(sends==1||permanent?Json("private upstream",HttpStatusCode.Unauthorized):Json("["+Game+"]"));
        });
        using var games=new HttpClient(handler);using var auth=new HttpClient(authHandler);using var session=new IgdbSession(TimeProvider.System);
        var pending=Provider(games,auth,session).GetDetailsAsync("1942","video_game",Ct);
        if(permanent)Assert.Equal(ProviderFailure.Unavailable,(await Assert.ThrowsAsync<MediaProviderException>(()=>pending)).Failure);
        else Assert.NotNull(await pending);
        Assert.Equal(2,tokens);Assert.Equal(2,sends);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Long_cooldown_is_shared_and_blocks_even_final_fractional_second(bool dateHeader)
    {
        var clock=new ManualClock();var sends=0;var tokens=0;
        using var authHandler=new Handler((_,_)=> { tokens++;return Task.FromResult(Json(TokenJson)); });
        using var handler=new Handler((_,_)=>
        {
            sends++;var result=Json("private upstream",HttpStatusCode.TooManyRequests);
            result.Headers.RetryAfter=dateHeader?new RetryConditionHeaderValue(clock.GetUtcNow()+TimeSpan.FromDays(2)):new RetryConditionHeaderValue(TimeSpan.FromDays(2));return Task.FromResult(result);
        });
        using var games=new HttpClient(handler);using var auth=new HttpClient(authHandler);using var session=new IgdbSession(clock);
        var first=await Assert.ThrowsAsync<MediaProviderException>(()=>Provider(games,auth,session,clock).GetDetailsAsync("1942","video_game",Ct));
        Assert.Equal(86400,first.RetryAfterSeconds);Assert.DoesNotContain("private upstream",first.ToString());
        clock.Advance(TimeSpan.FromDays(2)-TimeSpan.FromMilliseconds(500));
        var second=await Assert.ThrowsAsync<MediaProviderException>(()=>Provider(games,auth,session,clock).GetDetailsAsync("1942","video_game",Ct));
        Assert.Equal(1,second.RetryAfterSeconds);Assert.Equal(1,sends);Assert.Equal(1,tokens);
        clock.Advance(TimeSpan.FromMilliseconds(500));
        await Assert.ThrowsAsync<MediaProviderException>(()=>Provider(games,auth,session,clock).GetDetailsAsync("1942","video_game",Ct));Assert.Equal(2,sends);
    }

    [Fact]
    public async Task Retry_and_next_operation_are_paced_across_adapter_instances()
    {
        var clock=new ManualClock();var starts=new List<long>();
        using var authHandler=new Handler((_,_)=>Task.FromResult(Json(TokenJson)));
        using var handler=new Handler((_,_)=> { starts.Add(clock.GetTimestamp());return Task.FromResult(starts.Count==1?Json("private",HttpStatusCode.BadGateway):Json("["+Game+"]")); });
        using var games=new HttpClient(handler);using var auth=new HttpClient(authHandler);using var session=new IgdbSession(clock);
        var first=Provider(games,auth,session,clock).GetDetailsAsync("1942","video_game",Ct);Assert.False(first.IsCompleted);Assert.Single(starts);
        clock.Advance(TimeSpan.FromMilliseconds(250));Assert.NotNull(await first);
        var second=Provider(games,auth,session,clock).GetDetailsAsync("1942","video_game",Ct);Assert.False(second.IsCompleted);
        clock.Advance(TimeSpan.FromMilliseconds(250));Assert.NotNull(await second);
        Assert.Equal(new long[] {0,2500000,5000000},starts);
    }

    [Fact]
    public async Task Whole_operation_timeout_caller_cancellation_and_busy_rejection_release_slot()
    {
        var clock=new ManualClock();var tokens=0;
        using var authHandler=new Handler(async (_,ct)=> { tokens++;await Task.Delay(Timeout.InfiniteTimeSpan,ct);return Json(TokenJson); });
        using var handler=new Handler((_,_)=>throw new InvalidOperationException("No games request expected"));
        using var games=new HttpClient(handler);using var auth=new HttpClient(authHandler);using var session=new IgdbSession(clock);var provider=Provider(games,auth,session,clock);
        var first=provider.GetDetailsAsync("1942","video_game",Ct);
        Assert.Equal(ProviderFailure.Unavailable,(await Assert.ThrowsAsync<MediaProviderException>(()=>provider.GetDetailsAsync("1942","video_game",Ct))).Failure);
        clock.Advance(TimeSpan.FromSeconds(6));Assert.Equal(ProviderFailure.Timeout,(await Assert.ThrowsAsync<MediaProviderException>(()=>first)).Failure);
        Assert.True(session.TryEnter());session.Exit();
        using var caller=new CancellationTokenSource();var next=provider.GetDetailsAsync("1942","video_game",caller.Token);await caller.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>next);Assert.True(session.TryEnter());session.Exit();Assert.Equal(2,tokens);
    }

    [Theory]
    [InlineData("{}")] [InlineData("{\"access_token\":\"private\\r\\nheader\",\"expires_in\":3600,\"token_type\":\"bearer\"}")]
    [InlineData("{\"access_token\":\"fake\",\"expires_in\":0,\"token_type\":\"bearer\"}")]
    [InlineData("{\"access_token\":\"fake\",\"expires_in\":3600,\"token_type\":\"other\"}")]
    [InlineData("{\"access_token\":\"fake\",\"expires_in\":3600,\"expires_in\":1,\"token_type\":\"bearer\"}")]
    public async Task Invalid_token_response_is_sanitized_without_game_request(string body)
    {
        using var authHandler=new Handler((_,_)=>Task.FromResult(Json(body)));using var handler=new Handler((_,_)=>throw new InvalidOperationException("No games HTTP"));
        using var games=new HttpClient(handler);using var auth=new HttpClient(authHandler);using var session=new IgdbSession(TimeProvider.System);
        Assert.Equal(ProviderFailure.InvalidResponse,(await Assert.ThrowsAsync<MediaProviderException>(()=>Provider(games,auth,session).GetDetailsAsync("1942","video_game",Ct))).Failure);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Disabled_and_invalid_inputs_do_not_access_either_transport(bool enabled)
    {
        using var handler=new Handler((_,_)=>throw new InvalidOperationException("No HTTP"));using var games=new HttpClient(handler);using var auth=new HttpClient(handler);using var session=new IgdbSession(TimeProvider.System);
        var provider=Provider(games,auth,session,enabled:enabled);
        await Assert.ThrowsAsync<ArgumentException>(()=>provider.GetDetailsAsync("bad","video_game",Ct));
        await Assert.ThrowsAsync<ArgumentException>(()=>provider.SearchAsync("Game","album",1,Ct));
        if(!enabled)Assert.Equal(ProviderFailure.Disabled,(await Assert.ThrowsAsync<MediaProviderException>(()=>provider.GetDetailsAsync("1942","video_game",Ct))).Failure);
    }

    [Theory]
    [InlineData(false,null,null,6000,true)] [InlineData(true,null,null,6000,false)] [InlineData(true,"client","secret",6000,true)]
    [InlineData(true,"client","bad\r\n",6000,false)] [InlineData(true,"client","secret",499,false)] [InlineData(false,null,null,15001,false)]
    public void Options_require_safe_enabled_credentials_and_bounded_deadline(bool enabled,string? id,string? secret,int timeout,bool valid) =>
        Assert.Equal(valid,new IgdbOptions { Enabled=enabled,ClientId=id,ClientSecret=secret,TimeoutMilliseconds=timeout }.IsValid());

    [Theory]
    [InlineData("cover","{\"image_id\":\"../../secret\"}")]
    [InlineData("name","\"\\u0000bad\"")] [InlineData("name","\"\\n\"")]
    [InlineData("summary","\"private\\u0000\"")] [InlineData("first_release_date","253402300800")]
    [InlineData("platforms","[6]")] [InlineData("genres","[{\"id\":1,\"name\":\"a\"},{\"id\":1,\"name\":\"b\"}]")]
    [InlineData("version_parent","-1")]
    public async Task Invalid_nested_metadata_never_reaches_storage(string field,string raw)
    {
        using var source=JsonDocument.Parse(Game);
        var body="[{"+string.Join(',',source.RootElement.EnumerateObject().Select(p=>JsonSerializer.Serialize(p.Name)+":"+(p.Name==field?raw:p.Value.GetRawText())))+(field=="version_parent"?",\"version_parent\":"+raw:"")+"}]";
        using var handler=new Handler((_,_)=>Task.FromResult(Json(body)));using var authHandler=new Handler((_,_)=>Task.FromResult(Json(TokenJson)));
        using var games=new HttpClient(handler);using var auth=new HttpClient(authHandler);using var session=new IgdbSession(TimeProvider.System);
        Assert.Equal(ProviderFailure.InvalidResponse,(await Assert.ThrowsAsync<MediaProviderException>(()=>Provider(games,auth,session).GetDetailsAsync("1942","video_game",Ct))).Failure);
    }

    [Fact]
    public async Task Response_row_and_nested_array_bounds_and_duplicate_search_ids_are_enforced()
    {
        foreach(var body in new[] { "["+string.Join(',',Enumerable.Repeat(Game,21))+"]", "["+Game+","+Game+"]",
            "["+Game.Replace("The Witcher 3",new string('x',301))+"]",
            "["+Game.Replace("\"platforms\":[{\"id\":6,\"name\":\"PC\"}]","\"platforms\":["+string.Join(',',Enumerable.Range(1,21).Select(id=>"{\"id\":"+id+",\"name\":\"PC\"}"))+"]")+"]" })
        {
            using var handler=new Handler((_,_)=>Task.FromResult(Json(body)));using var authHandler=new Handler((_,_)=>Task.FromResult(Json(TokenJson)));
            using var games=new HttpClient(handler);using var auth=new HttpClient(authHandler);using var session=new IgdbSession(TimeProvider.System);
            Assert.Equal(ProviderFailure.InvalidResponse,(await Assert.ThrowsAsync<MediaProviderException>(()=>Provider(games,auth,session).SearchAsync("Game","video_game",1,Ct))).Failure);
        }
    }
    private sealed class UnboundedContent(byte[] data) : HttpContent
    {
        protected override bool TryComputeLength(out long length) { length=0;return false; }
        protected override Task SerializeToStreamAsync(Stream stream,TransportContext? context)=>stream.WriteAsync(data).AsTask();
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Response_body_limits_apply_to_streamed_and_declared_lengths(bool streamed)
    {
        using var handler=new Handler((_,_)=>Task.FromResult(streamed?new HttpResponseMessage(HttpStatusCode.OK){Content=new UnboundedContent(new byte[1024*1024+1])}:Json(new string('x',1024*1024+1))));
        using var authHandler=new Handler((_,_)=>Task.FromResult(Json(TokenJson)));
        using var games=new HttpClient(handler);using var auth=new HttpClient(authHandler);using var session=new IgdbSession(TimeProvider.System);
        Assert.Equal(ProviderFailure.InvalidResponse,(await Assert.ThrowsAsync<MediaProviderException>(()=>Provider(games,auth,session).GetDetailsAsync("1942","video_game",Ct))).Failure);
    }
    [Theory]
    [InlineData(HttpStatusCode.Forbidden,1)] [InlineData(HttpStatusCode.Redirect,1)]
    [InlineData(HttpStatusCode.BadRequest,1)] [InlineData(HttpStatusCode.BadGateway,2)]
    [InlineData(HttpStatusCode.InternalServerError,2)] [InlineData(HttpStatusCode.RequestTimeout,2)]
    public async Task Permanent_failures_and_transient_retries_have_bounded_attempts(HttpStatusCode status,int attempts)
    {
        var sends=0;using var handler=new Handler((_,_)=> { sends++;return Task.FromResult(Json("private upstream",status)); });
        using var authHandler=new Handler((_,_)=>Task.FromResult(Json(TokenJson)));
        using var games=new HttpClient(handler);using var auth=new HttpClient(authHandler);using var session=new IgdbSession(TimeProvider.System);
        Assert.Equal(ProviderFailure.Unavailable,(await Assert.ThrowsAsync<MediaProviderException>(()=>Provider(games,auth,session).GetDetailsAsync("1942","video_game",Ct))).Failure);
        Assert.Equal(attempts,sends);
    }
    [Fact]
    public async Task Network_failure_retries_once_without_refetching_cached_token()
    {
        var sends=0;var tokens=0;using var handler=new Handler((_,_)=> { sends++;throw new HttpRequestException("private error"); });
        using var authHandler=new Handler((_,_)=> { tokens++;return Task.FromResult(Json(TokenJson)); });
        using var games=new HttpClient(handler);using var auth=new HttpClient(authHandler);using var session=new IgdbSession(TimeProvider.System);
        var error=await Assert.ThrowsAsync<MediaProviderException>(()=>Provider(games,auth,session).GetDetailsAsync("1942","video_game",Ct));
        Assert.Equal(ProviderFailure.Unavailable,error.Failure);Assert.DoesNotContain("private error",error.ToString());Assert.Equal(2,sends);Assert.Equal(1,tokens);
    }
    [Fact]
    public async Task Short_retry_after_waits_within_operation_and_obeys_two_attempt_budget()
    {
        var clock=new ManualClock();var sends=0;
        using var handler=new Handler((_,_)=>
        {
            sends++;var response=Json("private",HttpStatusCode.ServiceUnavailable);
            response.Headers.RetryAfter=new RetryConditionHeaderValue(TimeSpan.FromSeconds(1));return Task.FromResult(response);
        });
        using var authHandler=new Handler((_,_)=>Task.FromResult(Json(TokenJson)));
        using var games=new HttpClient(handler);using var auth=new HttpClient(authHandler);using var session=new IgdbSession(clock);
        var first=Provider(games,auth,session,clock).GetDetailsAsync("1942","video_game",Ct);Assert.False(first.IsCompleted);Assert.Equal(1,sends);
        clock.Advance(TimeSpan.FromSeconds(1));Assert.Equal(ProviderFailure.RateLimited,(await Assert.ThrowsAsync<MediaProviderException>(()=>first)).Failure);Assert.Equal(2,sends);
    }
    [Fact]
    public async Task Token_rate_limit_cools_down_without_games_and_without_repeating_authentication()
    {
        var clock=new ManualClock();var tokens=0;
        using var handler=new Handler((_,_)=>throw new InvalidOperationException("No games HTTP"));
        using var authHandler=new Handler((_,_)=> { tokens++;var response=Json("secret token body",HttpStatusCode.TooManyRequests);response.Headers.RetryAfter=new RetryConditionHeaderValue(TimeSpan.FromSeconds(60));return Task.FromResult(response); });
        using var games=new HttpClient(handler);using var auth=new HttpClient(authHandler);using var session=new IgdbSession(clock);
        for(var i=0;i<2;i++)
            Assert.Equal(60,(await Assert.ThrowsAsync<MediaProviderException>(()=>Provider(games,auth,session,clock).GetDetailsAsync("1942","video_game",Ct))).RetryAfterSeconds);
        Assert.Equal(1,tokens);
    }
    [Fact]
    public async Task Oversized_token_body_is_rejected_before_any_games_request()
    {
        using var handler=new Handler((_,_)=>throw new InvalidOperationException("No games HTTP"));
        using var authHandler=new Handler((_,_)=>Task.FromResult(Json(new string('x',16385))));
        using var games=new HttpClient(handler);using var auth=new HttpClient(authHandler);using var session=new IgdbSession(TimeProvider.System);
        Assert.Equal(ProviderFailure.InvalidResponse,(await Assert.ThrowsAsync<MediaProviderException>(()=>Provider(games,auth,session).GetDetailsAsync("1942","video_game",Ct))).Failure);
    }

}
