using System.Text.Json;
using RateFreak.Application.MetadataImport;
using RateFreak.Infrastructure.MetadataImport;

namespace RateFreak.ProviderContractTests;

public sealed class MetadataCacheTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private sealed class Provider(string name, string type) : IMediaProvider
    {
        public bool Enabled = true;
        public int Searches, Details;
        public Func<CancellationToken, Task>? Before;
        public bool Missing;
        public ProviderCapability Capability => new(name, Enabled, [type], "Test", "https://example.test");
        public async Task<ExternalSearchPage> SearchAsync(string query, string mediaType, int page, CancellationToken ct)
        {
            Interlocked.Increment(ref Searches);
            if (Before is not null) await Before(ct);
            return new(name, page, [Item(query, mediaType)]);
        }
        public async Task<ExternalMediaResult?> GetDetailsAsync(string id, string mediaType, CancellationToken ct)
        {
            Interlocked.Increment(ref Details);
            if (Before is not null) await Before(ct);
            return Missing ? null : Item(id, mediaType);
        }
        private ExternalMediaResult Item(string title, string mediaType) => new(name, title, mediaType, title, null, null, null, JsonSerializer.SerializeToElement(new { }));
    }

    [Theory]
    [InlineData("tmdb", "movie", "11")]
    [InlineData("igdb", "video_game", "1942")]
    [InlineData("musicbrainz", "album", "3bd76d40-7f0e-36b7-9348-91a33afee20e")]
    public async Task Every_provider_reuses_searches_and_details_until_their_own_expiry(string name, string type, string id)
    {
        var clock = new ManualClock(); using var cache = new ProviderMetadataCache(clock);
        var source = new Provider(name, type); var provider = new CachedMediaProvider(source, cache);
        await provider.SearchAsync(" A book ", type, 1, Ct);
        await provider.SearchAsync("A book", type, 1, Ct);
        await provider.GetDetailsAsync(id, type, Ct); await provider.GetDetailsAsync(id, type, Ct);
        Assert.Equal(1, source.Searches); Assert.Equal(1, source.Details);
        clock.Advance(TimeSpan.FromMinutes(5));
        await provider.SearchAsync("A book", type, 1, Ct); await provider.GetDetailsAsync(id, type, Ct);
        Assert.Equal(2, source.Searches); Assert.Equal(1, source.Details);
        clock.Advance(TimeSpan.FromMinutes(55)); await provider.GetDetailsAsync(id, type, Ct);
        Assert.Equal(2, source.Details);
    }

    [Fact]
    public async Task Keys_separate_provider_type_page_and_query_and_canonicalize_ids()
    {
        using var cache = new ProviderMetadataCache(TimeProvider.System);
        var movie = new Provider("tmdb", "movie"); var tv = new Provider("tmdb", "tv_show"); var game = new Provider("igdb", "video_game");
        var a = new CachedMediaProvider(movie, cache); var b = new CachedMediaProvider(tv, cache); var c = new CachedMediaProvider(game, cache);
        Assert.Equal("One", (await a.SearchAsync("One", "movie", 1, Ct)).Items[0].Title);
        Assert.Equal(2, (await a.SearchAsync("One", "movie", 2, Ct)).Page);
        Assert.Equal("Two", (await a.SearchAsync("Two", "movie", 1, Ct)).Items[0].Title);
        Assert.Equal("tv_show", (await b.SearchAsync("One", "tv_show", 1, Ct)).Items[0].Type);
        Assert.Equal("igdb", (await c.SearchAsync("One", "video_game", 1, Ct)).Provider);
        await a.GetDetailsAsync("0011", "movie", Ct); await a.GetDetailsAsync("11", "movie", Ct);
        Assert.Equal(3, movie.Searches); Assert.Equal(1, movie.Details);
    }

    [Fact]
    public async Task Disabled_and_invalid_requests_never_bypass_validation_through_cached_success()
    {
        using var cache = new ProviderMetadataCache(TimeProvider.System); var source = new Provider("tmdb", "movie"); var provider = new CachedMediaProvider(source, cache);
        await provider.GetDetailsAsync("11", "movie", Ct); source.Enabled = false;
        Assert.Equal(ProviderFailure.Disabled, (await Assert.ThrowsAsync<MediaProviderException>(() => provider.GetDetailsAsync("11", "movie", Ct))).Failure);
        source.Enabled = true;
        await Assert.ThrowsAsync<ArgumentException>(() => provider.GetDetailsAsync("11", "album", Ct));
        await Assert.ThrowsAsync<ArgumentException>(() => provider.SearchAsync("\0", "movie", 1, Ct));
        Assert.Equal(1, source.Details); Assert.Equal(0, source.Searches);
    }

    [Fact]
    public async Task Shared_request_survives_one_cancellation_and_stops_when_the_last_waiter_cancels()
    {
        using var cache = new ProviderMetadataCache(TimeProvider.System);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var source = new Provider("tmdb", "movie") { Before = async ct => { started.TrySetResult(); using var registration = ct.Register(() => cancelled.TrySetResult()); await release.Task.WaitAsync(ct); } };
        var provider = new CachedMediaProvider(source, cache);
        using var first = new CancellationTokenSource(); using var second = new CancellationTokenSource();
        var a = provider.GetDetailsAsync("11", "movie", first.Token); await started.Task.WaitAsync(Ct);
        var b = provider.GetDetailsAsync("11", "movie", second.Token);
        first.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => a);
        Assert.False(cancelled.Task.IsCompleted); release.SetResult(); Assert.NotNull(await b);
        Assert.Equal(1, source.Details);
        source.Before = async ct => { using var registration = ct.Register(() => cancelled.TrySetResult()); await Task.Delay(Timeout.Infinite, ct); };
        using var only = new CancellationTokenSource(); var pending = provider.GetDetailsAsync("12", "movie", only.Token);
        only.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        await cancelled.Task.WaitAsync(Ct);
    }

    [Fact]
    public async Task Missing_results_and_provider_failures_do_not_poison_cache()
    {
        using var cache = new ProviderMetadataCache(TimeProvider.System); var source = new Provider("tmdb", "movie") { Missing = true }; var provider = new CachedMediaProvider(source, cache);
        Assert.Null(await provider.GetDetailsAsync("11", "movie", Ct)); source.Missing = false;
        Assert.NotNull(await provider.GetDetailsAsync("11", "movie", Ct)); Assert.Equal(2, source.Details);
        source.Before = _ => throw new MediaProviderException(ProviderFailure.RateLimited, 10);
        var error = await Assert.ThrowsAsync<MediaProviderException>(() => provider.GetDetailsAsync("12", "movie", Ct)); Assert.Equal(10, error.RetryAfterSeconds);
        source.Before = null; Assert.NotNull(await provider.GetDetailsAsync("12", "movie", Ct)); Assert.Equal(4, source.Details);
    }

    [Fact]
    public async Task Inflight_requests_are_bounded_and_timeout_without_retaining_failures()
    {
        var clock = new ManualClock(); using var cache = new ProviderMetadataCache(clock);
        var source = new Provider("tmdb", "movie") { Before = ct => Task.Delay(Timeout.Infinite, ct) }; var provider = new CachedMediaProvider(source, cache);
        var calls = Enumerable.Range(1, 64).Select(id => provider.GetDetailsAsync(id.ToString(), "movie", Ct)).ToArray();
        Assert.Equal(ProviderFailure.Unavailable, (await Assert.ThrowsAsync<MediaProviderException>(() => provider.GetDetailsAsync("65", "movie", Ct))).Failure);
        clock.Advance(TimeSpan.FromSeconds(20));
        foreach (var call in calls) Assert.Equal(ProviderFailure.Timeout, (await Assert.ThrowsAsync<MediaProviderException>(() => call)).Failure);
        source.Before = null; Assert.NotNull(await provider.GetDetailsAsync("65", "movie", Ct));
    }

    [Fact]
    public async Task Entry_count_is_bounded_even_for_small_results()
    {
        using var cache = new ProviderMetadataCache(TimeProvider.System); var source = new Provider("tmdb", "movie"); var provider = new CachedMediaProvider(source, cache);
        // Each successful search entry carries its query in the title. Bounded keys
        // plus repeated pages exercise count eviction without generating huge payloads.
        for (var i = 0; i < 2100; i++) await provider.SearchAsync("Book " + i, "movie", 1, Ct);
        await provider.SearchAsync("Book 0", "movie", 1, Ct); Assert.Equal(2101, source.Searches);
    }

    [Fact]
    public async Task Cancelled_flights_keep_their_capacity_until_upstream_cleanup_finishes()
    {
        using var cache = new ProviderMetadataCache(TimeProvider.System);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cleaning = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cleaningCount = 0; var finishedCount = 0;
        var source = new Provider("tmdb", "movie") { Before = async ct =>
        {
            try { await Task.Delay(Timeout.Infinite, ct); }
            catch (OperationCanceledException)
            {
                if (Interlocked.Increment(ref cleaningCount) == 64) cleaning.TrySetResult();
                await release.Task.WaitAsync(Ct);
                if (Interlocked.Increment(ref finishedCount) == 64) finished.TrySetResult();
                throw;
            }
        } };
        var provider = new CachedMediaProvider(source, cache);
        using var excessCaller = new CancellationTokenSource();
        Task<ExternalMediaResult?>? excess = null;
        try
        {
            for (var i = 1; i <= 64; i++)
            {
                using var caller = new CancellationTokenSource();
                var pending = provider.GetDetailsAsync(i.ToString(), "movie", caller.Token);
                caller.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
            }
            await cleaning.Task.WaitAsync(Ct);
            excess = provider.GetDetailsAsync("65", "movie", excessCaller.Token);
            Assert.True(excess.IsCompleted, "An upstream operation must retain its admission slot until its cancellation cleanup finishes.");
            Assert.Equal(ProviderFailure.Unavailable, (await Assert.ThrowsAsync<MediaProviderException>(() => excess)).Failure);
            Assert.Equal(64, source.Details);
        }
        finally
        {
            excessCaller.Cancel(); release.TrySetResult();
            if (excess is not null) try { await excess; } catch (Exception) { }
        }
        await finished.Task.WaitAsync(Ct);
        source.Before = null;
        Assert.NotNull(await provider.GetDetailsAsync("65", "movie", Ct));
    }
}
