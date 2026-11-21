using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Options;
using RateFreak.Infrastructure.Updates;
namespace RateFreak.UnitTests;
public sealed class UpdateServiceTests
{
    private sealed class Clock:TimeProvider {public DateTimeOffset Now=new(2026,11,20,0,0,0,TimeSpan.Zero);public override DateTimeOffset GetUtcNow()=>Now;}
    private sealed class Handler:HttpMessageHandler
    {
        public int Calls; public Func<HttpRequestMessage,Task<HttpResponseMessage>> Respond = _=>Task.FromResult(Json("1.10.0"));
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct){Calls++;return Respond(request);}
    }
    private static HttpResponseMessage Json(string version)=>new(HttpStatusCode.OK){Content=new StringContent("{\"tag_name\":\"v"+version+"\",\"draft\":false,\"prerelease\":false,\"published_at\":\"2026-11-19T00:00:00Z\"}")};
    private static UpdateService Service(Handler handler,Clock clock,bool enabled=true,bool official=true)=>new(new GitHubReleaseClient(new HttpClient(handler),clock),Options.Create(new UpdateOptions{Enabled=enabled}),new BuildIdentity("1.9.0",new string('a',40),official),clock);
    [Fact] public async Task DisabledNeverContactsUpstream(){var h=new Handler();var s=Service(h,new(),false);Assert.Equal("disabled",(await s.GetAsync(true,TestContext.Current.CancellationToken)).State);Assert.Equal(0,h.Calls);}
    [Fact] public async Task SuccessCachesForTwentyFourHoursAndManualChecksShareCooldown()
    {
        var h=new Handler();var clock=new Clock();var s=Service(h,clock);
        var result=await s.GetAsync(false,TestContext.Current.CancellationToken);Assert.Equal("available",result.State);Assert.Equal("1.10.0",result.LatestVersion);Assert.False(result.CanCheck);
        await s.GetAsync(true,TestContext.Current.CancellationToken);Assert.Equal(1,h.Calls);
        clock.Now=clock.Now.AddMinutes(6);await s.GetAsync(false,TestContext.Current.CancellationToken);Assert.Equal(1,h.Calls);
        await s.GetAsync(true,TestContext.Current.CancellationToken);Assert.Equal(2,h.Calls);
        clock.Now=clock.Now.AddHours(24);await s.GetAsync(false,TestContext.Current.CancellationToken);Assert.Equal(3,h.Calls);
    }
    [Fact] public async Task ConcurrentChecksShareRequestAndCallerCancellationDoesNotCancelIt()
    {
        var release=new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var h=new Handler{Respond=_=>release.Task};var s=Service(h,new());using var cts=new CancellationTokenSource();
        var a=s.GetAsync(true,cts.Token);var b=s.GetAsync(true,TestContext.Current.CancellationToken);cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>a);release.SetResult(Json("1.10.0"));
        Assert.Equal("available",(await b).State);Assert.Equal(1,h.Calls);
    }
    [Fact] public async Task FailedRefreshPreservesStaleSuccessAndNeverSaysCurrent()
    {
        var h=new Handler();var clock=new Clock();var s=Service(h,clock);await s.GetAsync(false,TestContext.Current.CancellationToken);
        clock.Now=clock.Now.AddHours(25);h.Respond=_=>throw new HttpRequestException("private detail");
        var result=await s.GetAsync(false,TestContext.Current.CancellationToken);Assert.Equal("unavailable",result.State);Assert.True(result.Stale);Assert.Equal("1.10.0",result.LatestVersion);
        await s.GetAsync(true,TestContext.Current.CancellationToken);Assert.Equal(2,h.Calls);
    }
    [Fact] public async Task NotFoundClearsPreviouslyPublishedRelease()
    {
        var h=new Handler();var clock=new Clock();var s=Service(h,clock);await s.GetAsync(false,TestContext.Current.CancellationToken);clock.Now=clock.Now.AddDays(1);
        h.Respond=_=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        var result=await s.GetAsync(false,TestContext.Current.CancellationToken);Assert.Equal("no-release",result.State);Assert.Null(result.LatestVersion);
    }
    [Fact] public async Task ConditionalNotModifiedRequiresAndRetainsSuccessfulCache()
    {
        var h=new Handler();h.Respond=_=>{var response=Json("1.10.0");response.Headers.ETag=new EntityTagHeaderValue("\"one\"");return Task.FromResult(response);};
        var clock=new Clock();var s=Service(h,clock);await s.GetAsync(false,TestContext.Current.CancellationToken);clock.Now=clock.Now.AddDays(1);
        h.Respond=request=>{Assert.Equal("\"one\"",request.Headers.IfNoneMatch.Single().Tag);return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotModified));};
        Assert.Equal("available",(await s.GetAsync(false,TestContext.Current.CancellationToken)).State);
        var empty=Service(new Handler{Respond=_=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotModified))},new());
        Assert.Equal("unavailable",(await empty.GetAsync(false,TestContext.Current.CancellationToken)).State);
    }
    [Theory][InlineData("1.10.0-rc.1")][InlineData("01.0.0")]
    public async Task RejectsInvalidOrPrereleaseVersions(string version){var h=new Handler{Respond=_=>Task.FromResult(Json(version))};Assert.Equal("unavailable",(await Service(h,new()).GetAsync(false,TestContext.Current.CancellationToken)).State);}
    [Theory][InlineData(302)][InlineData(500)]
    public async Task RejectsRedirectsAndUpstreamErrors(int status){var h=new Handler{Respond=_=>Task.FromResult(new HttpResponseMessage((HttpStatusCode)status))};Assert.Equal("unavailable",(await Service(h,new()).GetAsync(false,TestContext.Current.CancellationToken)).State);}
    [Theory][InlineData("{\"draft\":true}")][InlineData("{bad")][InlineData("{\"tag_name\":\"v1.10.0\",\"tag_name\":\"v1.9.0\"}")]
    public async Task MalformedResponsesCannotManufactureSuccess(string body){var h=new Handler{Respond=_=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(body)})};Assert.Equal("unavailable",(await Service(h,new()).GetAsync(false,TestContext.Current.CancellationToken)).State);}
    [Fact] public async Task OversizedResponseRefused(){var h=new Handler{Respond=_=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(new string('x',256*1024+1))})};Assert.Equal("unavailable",(await Service(h,new()).GetAsync(false,TestContext.Current.CancellationToken)).State);}
    [Fact] public async Task RateLimitDelayHonoredBeyondManualCooldown()
    {
        var h=new Handler{Respond=_=>{var r=new HttpResponseMessage(HttpStatusCode.TooManyRequests);r.Headers.RetryAfter=new RetryConditionHeaderValue(TimeSpan.FromHours(2));return Task.FromResult(r);}};
        var clock=new Clock();var s=Service(h,clock);var status=await s.GetAsync(false,TestContext.Current.CancellationToken);Assert.Equal(clock.Now.AddHours(2),status.NextCheckAt);
        clock.Now=clock.Now.AddHours(1);await s.GetAsync(true,TestContext.Current.CancellationToken);Assert.Equal(1,h.Calls);
    }
    [Fact] public async Task DevelopmentBuildNeverClaimsUpToDate(){var h=new Handler();Assert.Equal("development",(await Service(h,new(),official:false).GetAsync(false,TestContext.Current.CancellationToken)).State);}
    [Fact] public async Task FixedRequestDoesNotSendAccountOrInstanceData()
    {
        var h=new Handler{Respond=request=>{Assert.Equal("https://api.github.com/repos/RogelioRomo/ratefreak/releases/latest",request.RequestUri!.AbsoluteUri);Assert.Null(request.Headers.Authorization);Assert.NotEmpty(request.Headers.UserAgent);return Task.FromResult(Json("1.9.0"));}};
        Assert.Equal("current",(await Service(h,new()).GetAsync(false,TestContext.Current.CancellationToken)).State);
    }
}
