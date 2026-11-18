using System.Net;
using System.Net.Http.Headers;
using RateFreak.Application.MetadataImport;
using RateFreak.Infrastructure.MetadataImport;
using RateFreak.Infrastructure.Providers.OpenLibrary;

namespace RateFreak.ProviderContractTests;

public sealed partial class OpenLibraryTests
{
    [Theory]
    [InlineData(429, ProviderFailure.RateLimited)] [InlineData(503, ProviderFailure.RateLimited)]
    [InlineData(403, ProviderFailure.Unavailable)] [InlineData(302, ProviderFailure.Unavailable)]
    public async Task Upstream_statuses_preserve_safe_failures_and_retry_after(int status, ProviderFailure failure)
    {
        using var handler = new Handler((_, _) => { var response = Json("{}", (HttpStatusCode)status); response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(60)); return Task.FromResult(response); });
        using var client = new HttpClient(handler); using var gate = new OpenLibraryRequestGate(TimeProvider.System); using var cache = new ProviderMetadataCache(TimeProvider.System);
        var error = await Assert.ThrowsAsync<MediaProviderException>(() => Provider(client, gate, cache).GetDetailsAsync("OL1W", "book", Ct)); Assert.Equal(failure, error.Failure);
        if (failure == ProviderFailure.RateLimited) Assert.Equal(60, error.RetryAfterSeconds);
    }

    [Fact]
    public async Task Caller_cancellation_is_preserved_and_operation_timeout_is_sanitized()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var handler = new Handler(async (_, ct) => { started.TrySetResult(); await Task.Delay(Timeout.Infinite, ct); return Json("{}"); });
        using var client = new HttpClient(handler); using var gate = new OpenLibraryRequestGate(TimeProvider.System); using var cache = new ProviderMetadataCache(TimeProvider.System);
        using var cancel = new CancellationTokenSource(); var call = Provider(client, gate, cache).GetDetailsAsync("OL1W", "book", cancel.Token); await started.Task.WaitAsync(Ct); cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => call);
        var clock = new ManualClock(); using var timedGate = new OpenLibraryRequestGate(clock); using var timedCache = new ProviderMetadataCache(clock);
        call = Provider(client, timedGate, timedCache, clock).GetDetailsAsync("OL1W", "book", Ct); clock.Advance(TimeSpan.FromSeconds(10));
        Assert.Equal(ProviderFailure.Timeout, (await Assert.ThrowsAsync<MediaProviderException>(() => call)).Failure);
    }

    [Fact]
    public async Task Every_outbound_request_is_paced_and_a_busy_gate_is_bounded()
    {
        var clock = new ManualClock(); using var gate = new OpenLibraryRequestGate(clock);
        using var cache = new ProviderMetadataCache(clock); var starts = new List<long>();
        using var handler = new Handler((request, _) => { starts.Add(clock.GetTimestamp()); return Task.FromResult(Json("""{"key":"/works/OL1W","title":"Book"}""")); });
        using var client = new HttpClient(handler); var provider = Provider(client, gate, cache, clock);
        Assert.NotNull(await provider.GetDetailsAsync("OL1W", "book", Ct)); var next = provider.GetDetailsAsync("OL1W", "book", Ct);
        Assert.False(next.IsCompleted); Assert.Equal(ProviderFailure.Unavailable, (await Assert.ThrowsAsync<MediaProviderException>(() => provider.GetDetailsAsync("OL2W", "book", Ct))).Failure);
        clock.Advance(TimeSpan.FromSeconds(1)); Assert.NotNull(await next); Assert.Equal(TimeSpan.TicksPerSecond, starts[1] - starts[0]);
    }

    [Fact]
    public async Task Oversized_bodies_and_excess_search_rows_are_refused()
    {
        foreach (var body in new[] { new string('x', 1024 * 1024 + 1), "{\"docs\":[" + string.Join(',', Enumerable.Repeat("{}", 21)) + "]}" })
        {
            using var handler = new Handler((_, _) => Task.FromResult(Json(body))); using var client = new HttpClient(handler); using var gate = new OpenLibraryRequestGate(TimeProvider.System); using var cache = new ProviderMetadataCache(TimeProvider.System);
            Assert.Equal(ProviderFailure.InvalidResponse, (await Assert.ThrowsAsync<MediaProviderException>(() => Provider(client, gate, cache).SearchAsync("Book", "book", 1, Ct))).Failure);
        }
    }
}
