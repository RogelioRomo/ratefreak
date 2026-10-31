using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Mvc.Testing;
using RateFreak.Api.Security;

namespace RateFreak.IntegrationTests;

public sealed class BrowserProxyTests
{
    private const string Secret = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;
    private static ApiFactory Factory() => new(configureServices: services =>
        services.Configure<BrowserProxyOptions>(options => options.SharedSecret = Secret));
    private static HttpClient Client(ApiFactory factory) => factory.CreateClient(
        new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });

    private static async Task<HttpResponseMessage> Token(HttpClient client, string? ip = null, string? key = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/csrf");
        if (ip is not null) request.Headers.Add("X-RateFreak-Client-IP", ip);
        if (key is not null) request.Headers.Add("X-RateFreak-Proxy-Key", key);
        return await client.SendAsync(request, Cancellation);
    }

    [Fact]
    public async Task Authenticated_client_addresses_have_independent_budgets()
    {
        await using var factory = Factory();
        using var client = Client(factory);
        for (var i = 0; i < 30; i++)
        {
            using var response = await Token(client, "192.0.2.1", Secret);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        using var limited = await Token(client, "192.0.2.1", Secret);
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.NotNull(limited.Headers.RetryAfter);
        using var independent = await Token(client, "192.0.2.2", Secret);
        Assert.Equal(HttpStatusCode.OK, independent.StatusCode);
        using var mapped = await Token(client, "::ffff:192.0.2.1", Secret);
        Assert.Equal(HttpStatusCode.TooManyRequests, mapped.StatusCode);
    }

    [Theory]
    [InlineData("192.0.2.1", "wrong")]
    [InlineData("192.0.2.1", null)]
    [InlineData(null, Secret)]
    [InlineData("192.0.2.1, 192.0.2.2", Secret)]
    [InlineData("not-an-ip", Secret)]
    public async Task Invalid_attribution_is_rejected_without_exposing_secrets(string? ip, string? key)
    {
        await using var factory = Factory();
        using var client = Client(factory);
        using var response = await Token(client, ip, key);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.DoesNotContain(Secret, await response.Content.ReadAsStringAsync(Cancellation));
        Assert.True(response.Headers.CacheControl?.NoStore);
    }

    [Fact]
    public async Task Disabled_attribution_ignores_spoofing_and_uses_connection_budget()
    {
        await using var factory = new ApiFactory();
        using var client = Client(factory);
        for (var i = 0; i < 30; i++)
        {
            using var response = await Token(client, $"192.0.2.{i + 1}", Secret);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        using var rejected = await Token(client, "198.51.100.1", Secret);
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
    }

    [Fact]
    public async Task Direct_callers_cannot_bypass_budgets_with_forwarding_headers_or_claims()
    {
        await using var factory = Factory();
        using var client = Client(factory);
        for (var i = 0; i < 31; i++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/csrf");
            request.Headers.Add("X-Forwarded-For", $"192.0.2.{i + 1}");
            using var response = await client.SendAsync(request, Cancellation);
            Assert.Equal(i < 30 ? HttpStatusCode.OK : HttpStatusCode.TooManyRequests, response.StatusCode);
        }
        using var me = new HttpRequestMessage(HttpMethod.Get, "/api/v1/users/me");
        me.Headers.Add("X-RateFreak-Proxy-Key", Secret);
        me.Headers.Add("X-RateFreak-Client-IP", "203.0.113.1");
        me.Headers.Add("X-User-Id", Guid.NewGuid().ToString());
        using var unauthorized = await client.SendAsync(me, Cancellation);
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
    }
}
