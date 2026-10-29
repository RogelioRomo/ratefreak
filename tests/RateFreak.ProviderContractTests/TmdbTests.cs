using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RateFreak.Application.MetadataImport;
using RateFreak.Infrastructure.Providers.Tmdb;

namespace RateFreak.ProviderContractTests;

public sealed class TmdbTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;
    private const string Movie = """{"id":11,"title":"  A Movie  ","overview":"Synopsis","release_date":"2020-02-29","poster_path":"/abc123.jpg","runtime":120,"vote_average":9.9}""";
    private static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request, cancellationToken);
    }
    private static TmdbMediaProvider Provider(HttpClient client, TmdbRequestGate gate, bool enabled = true, int timeout = 4000) =>
        new(client, Options.Create(new TmdbOptions { Enabled = enabled, AccessToken = "test-secret", TimeoutMilliseconds = timeout }), gate,
            NullLogger<TmdbMediaProvider>.Instance);

    [Fact]
    public async Task Movie_search_uses_bearer_auth_encodes_query_and_maps_canonical_fields()
    {
        using var handler = new Handler((request, _) =>
        {
            Assert.Equal("https", request.RequestUri!.Scheme);
            Assert.Equal("api.themoviedb.org", request.RequestUri.Host);
            Assert.Equal("/3/search/movie", request.RequestUri.AbsolutePath);
            Assert.Contains("query=Alien%20%26%20Life", request.RequestUri.Query);
            Assert.Contains("include_adult=false", request.RequestUri.Query);
            Assert.Contains("page=2", request.RequestUri.Query);
            Assert.Contains("language=en-US", request.RequestUri.Query);
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.Equal("test-secret", request.Headers.Authorization?.Parameter);
            Assert.DoesNotContain("test-secret", request.RequestUri.ToString());
            return Task.FromResult(Json("{\"results\":[" + Movie + "]}"));
        });
        using var client = new HttpClient(handler);
        using var gate = new TmdbRequestGate();
        var page = await Provider(client, gate).SearchAsync("Alien & Life", "movie", 2, Cancellation);
        var item = Assert.Single(page.Items);
        Assert.Equal("11", item.ExternalId);
        Assert.Equal("A Movie", item.Title);
        Assert.Equal("movie", item.Type);
        Assert.Equal(new DateOnly(2020, 2, 29), item.ReleaseDate);
        Assert.Equal("https://image.tmdb.org/t/p/w500/abc123.jpg", item.ImageReference);
        Assert.Equal(120, item.Metadata.GetProperty("runtime").GetInt32());
        Assert.False(item.Metadata.TryGetProperty("vote_average", out _));
    }

    [Fact]
    public async Task Tv_details_use_tv_namespace_and_allow_absent_dates_and_artwork()
    {
        using var handler = new Handler((request, _) =>
        {
            Assert.Equal("/3/tv/11", request.RequestUri!.AbsolutePath);
            return Task.FromResult(Json("""{"id":11,"name":"A Series","first_air_date":"","poster_path":null,"number_of_seasons":2,"number_of_episodes":16}"""));
        });
        using var client = new HttpClient(handler);
        using var gate = new TmdbRequestGate();
        var item = await Provider(client, gate).GetDetailsAsync("00011", "tv_show", Cancellation);
        Assert.NotNull(item);
        Assert.Equal("tv_show", item.Type);
        Assert.Equal("A Series", item.Title);
        Assert.Null(item.ReleaseDate);
        Assert.Null(item.ImageReference);
        Assert.Equal(2, item.Metadata.GetProperty("number_of_seasons").GetInt32());
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("not-json")]
    [InlineData("{\"id\":\"11\",\"title\":\"Title\"}")]
    [InlineData("{\"id\":12,\"title\":\"Wrong identity\"}")]
    [InlineData("{\"id\":11,\"title\":\" \"}")]
    [InlineData("{\"id\":11,\"title\":\"Title\",\"overview\":\"bad\\u0000text\"}")]
    [InlineData("{\"id\":11,\"title\":\"Title\",\"poster_path\":\"//evil.test/x.jpg\"}")]
    [InlineData("{\"id\":11,\"title\":\"Title\",\"runtime\":\"long\"}")]
    [InlineData("{\"id\":11,\"title\":\"\\uD800\"}")]
    [InlineData("{\"id\":11,\"title\":\"Title\",\"overview\":\"\\uDC00\"}")]
    public async Task Invalid_details_are_distinguished_from_no_match(string body)
    {
        using var handler = new Handler((_, _) => Task.FromResult(Json(body)));
        using var client = new HttpClient(handler);
        using var gate = new TmdbRequestGate();
        var error = await Assert.ThrowsAsync<MediaProviderException>(() => Provider(client, gate).GetDetailsAsync("11", "movie", Cancellation));
        Assert.Equal(ProviderFailure.InvalidResponse, error.Failure);
    }

    [Fact]
    public async Task Not_found_and_adult_details_are_not_importable()
    {
        using var handler = new Handler((request, _) => Task.FromResult(request.RequestUri!.AbsolutePath.EndsWith("/11")
            ? Json("{}", HttpStatusCode.NotFound) : Json("""{"adult":true,"id":12,"title":"Excluded"}""")));
        using var client = new HttpClient(handler);
        using var gate = new TmdbRequestGate();
        var provider = Provider(client, gate);
        Assert.Null(await provider.GetDetailsAsync("11", "movie", Cancellation));
        Assert.Null(await provider.GetDetailsAsync("12", "movie", Cancellation));
    }

    [Fact]
    public async Task Transient_failure_is_retried_once()
    {
        var calls = 0;
        using var handler = new Handler((_, _) => Task.FromResult(++calls == 1 ? Json("secret upstream text", HttpStatusCode.BadGateway) : Json(Movie)));
        using var client = new HttpClient(handler);
        using var gate = new TmdbRequestGate();
        var item = await Provider(client, gate).GetDetailsAsync("11", "movie", Cancellation);
        Assert.NotNull(item);
        Assert.Equal(2, calls);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, 1)]
    [InlineData(HttpStatusCode.Forbidden, 1)]
    [InlineData(HttpStatusCode.Redirect, 1)]
    [InlineData(HttpStatusCode.ServiceUnavailable, 2)]
    public async Task Failures_are_bounded_and_do_not_expose_upstream_body(HttpStatusCode status, int expectedCalls)
    {
        var calls = 0;
        using var handler = new Handler((_, _) => { calls++; return Task.FromResult(Json("private-provider-detail", status)); });
        using var client = new HttpClient(handler);
        using var gate = new TmdbRequestGate();
        var error = await Assert.ThrowsAsync<MediaProviderException>(() => Provider(client, gate).GetDetailsAsync("11", "movie", Cancellation));
        Assert.Equal(ProviderFailure.Unavailable, error.Failure);
        Assert.Equal(expectedCalls, calls);
        Assert.DoesNotContain("private-provider-detail", error.ToString());
    }

    [Fact]
    public async Task Long_retry_after_is_respected_without_immediate_retry()
    {
        var calls = 0;
        using var handler = new Handler((_, _) =>
        {
            calls++;
            var response = Json("{}", HttpStatusCode.TooManyRequests);
            response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(60));
            return Task.FromResult(response);
        });
        using var client = new HttpClient(handler);
        using var gate = new TmdbRequestGate();
        var error = await Assert.ThrowsAsync<MediaProviderException>(() => Provider(client, gate).GetDetailsAsync("11", "movie", Cancellation));
        Assert.Equal(ProviderFailure.RateLimited, error.Failure);
        Assert.Equal(60, error.RetryAfterSeconds);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task Timeout_is_bounded_but_caller_cancellation_propagates()
    {
        using var handler = new Handler(async (_, cancellation) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellation);
            return Json(Movie);
        });
        using var client = new HttpClient(handler);
        using var gate = new TmdbRequestGate();
        var provider = Provider(client, gate, timeout: 500);
        var error = await Assert.ThrowsAsync<MediaProviderException>(() => provider.GetDetailsAsync("11", "movie", Cancellation));
        Assert.Equal(ProviderFailure.Timeout, error.Failure);
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(Cancellation);
        await caller.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => provider.GetDetailsAsync("11", "movie", caller.Token));
    }

    [Fact]
    public async Task Disabled_provider_makes_no_outbound_request()
    {
        using var handler = new Handler((_, _) => throw new InvalidOperationException("HTTP must not run"));
        using var client = new HttpClient(handler);
        using var gate = new TmdbRequestGate();
        var error = await Assert.ThrowsAsync<MediaProviderException>(() => Provider(client, gate, enabled: false).GetDetailsAsync("11", "movie", Cancellation));
        Assert.Equal(ProviderFailure.Disabled, error.Failure);
    }

    [Fact]
    public async Task Oversized_response_is_rejected()
    {
        using var handler = new Handler((_, _) => Task.FromResult(Json(new string('x', 1024 * 1024 + 1))));
        using var client = new HttpClient(handler);
        using var gate = new TmdbRequestGate();
        var error = await Assert.ThrowsAsync<MediaProviderException>(() => Provider(client, gate).GetDetailsAsync("11", "movie", Cancellation));
        Assert.Equal(ProviderFailure.InvalidResponse, error.Failure);
    }

    [Fact]
    public async Task Concurrency_gate_rejects_excess_work_without_waiting()
    {
        using var handler = new Handler((_, _) => throw new InvalidOperationException("HTTP must not run"));
        using var client = new HttpClient(handler);
        using var gate = new TmdbRequestGate();
        for (var i = 0; i < 4; i++) Assert.True(gate.TryEnter());
        try
        {
            var error = await Assert.ThrowsAsync<MediaProviderException>(() => Provider(client, gate).GetDetailsAsync("11", "movie", Cancellation));
            Assert.Equal(ProviderFailure.Unavailable, error.Failure);
        }
        finally { for (var i = 0; i < 4; i++) gate.Exit(); }
        Assert.True(gate.TryEnter());
        gate.Exit();
    }

    [Theory]
    [InlineData(false, null, true)]
    [InlineData(true, null, false)]
    [InlineData(true, "bad\r\ntoken", false)]
    [InlineData(true, "valid.token-1", true)]
    public void Configuration_requires_a_safe_token_only_when_enabled(bool enabled, string? token, bool valid) =>
        Assert.Equal(valid, new TmdbOptions { Enabled = enabled, AccessToken = token }.IsValid());
}
