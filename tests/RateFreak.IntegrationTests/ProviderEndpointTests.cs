using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RateFreak.Infrastructure.Providers.Tmdb;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace RateFreak.IntegrationTests;

public sealed class ProviderEndpointTests
{
    [Fact]
    public async Task Provider_capabilities_are_public_without_revealing_credentials()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/api/v1/media/providers", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal(2, json.GetArrayLength());
        var provider = json.EnumerateArray().Single(x => x.GetProperty("name").GetString() == "tmdb");
        var music = json.EnumerateArray().Single(x => x.GetProperty("name").GetString() == "musicbrainz");
        Assert.False(music.GetProperty("enabled").GetBoolean());
        Assert.False(music.TryGetProperty("contact", out _));
        Assert.Equal("tmdb", provider.GetProperty("name").GetString());
        Assert.False(provider.GetProperty("enabled").GetBoolean());
        Assert.False(provider.TryGetProperty("accessToken", out _));
    }

    [Theory]
    [InlineData("/api/v1/media/external/search?q=Alien&type=movie")]
    [InlineData("/api/v1/media/external/tmdb/movie/11")]
    [InlineData("/api/v1/media/external/search?provider=musicbrainz&q=Album&type=album")]
    [InlineData("/api/v1/media/external/musicbrainz/album/3bd76d40-7f0e-36b7-9348-91a33afee20e")]
    public async Task Disabled_provider_returns_unavailable_instead_of_empty_results(string route)
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync(route, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Import_requires_authentication()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/api/v1/media/import",
            new { provider = "tmdb", mediaType = "movie", externalId = "11" }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
    [Theory]
    [InlineData("q=&type=movie")]
    [InlineData("q=Movie&type=episode")]
    [InlineData("q=Movie&type=movie&page=501")]
    [InlineData("q=Movie&type=movie&page=0")]
    [InlineData("q=%00&type=movie")]
    public async Task Invalid_search_input_is_rejected_before_provider_access(string query)
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/api/v1/media/external/search?" + query, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("tmdb/movie/0")]
    [InlineData("tmdb/movie/2147483648")]
    [InlineData("tmdb/movie/-1")]
    [InlineData("tmdb/movie/not-a-number")]
    [InlineData("tmdb/episode/11")]
    public async Task Invalid_external_identity_is_rejected(string path)
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/api/v1/media/external/" + path, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Enabled_provider_without_credentials_fails_startup_with_safe_validation()
    {
        await using var factory = new ApiFactory(configureServices: services =>
            services.PostConfigure<TmdbOptions>(options => { options.Enabled = true; options.AccessToken = null; }));
        var error = Assert.Throws<OptionsValidationException>(() => factory.CreateClient());
        Assert.Contains("TMDB configuration is invalid", error.Message);
    }

    [Fact]
    public async Task External_search_budget_is_separate_from_local_catalog()
    {
        var cancellation = TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        for (var i = 0; i < 20; i++)
        {
            using var response = await client.GetAsync("/api/v1/media/external/search?q=Alien&type=movie", cancellation);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        }
        using var limited = await client.GetAsync("/api/v1/media/external/search?q=Alien&type=movie", cancellation);
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.NotNull(limited.Headers.RetryAfter);
        using var capabilities = await client.GetAsync("/api/v1/media/providers", cancellation);
        Assert.Equal(HttpStatusCode.OK, capabilities.StatusCode);
    }

}
