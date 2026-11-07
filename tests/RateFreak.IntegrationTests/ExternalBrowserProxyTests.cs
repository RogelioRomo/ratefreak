using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;

namespace RateFreak.IntegrationTests;

public sealed partial class ExternalAccountTests
{
    private const string ProxyKey = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private WebApplicationFactory<Program> BrowserEnabled() => Enabled(new Provider()).WithWebHostBuilder(builder =>
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AllowedHosts"] = "localhost;api.internal;web.test",
            ["Authentication:External:PublicOrigin"] = "https://web.test",
            ["BrowserProxy:SharedSecret"] = ProxyKey
        })));
    private static HttpClient BrowserClient(WebApplicationFactory<Program> factory)
    {
        var client = Client(factory);
        client.BaseAddress = new Uri("https://api.internal");
        client.DefaultRequestHeaders.Add("X-RateFreak-Proxy-Key", ProxyKey);
        client.DefaultRequestHeaders.Add("X-RateFreak-Client-IP", "192.0.2.1");
        client.DefaultRequestHeaders.Add("X-RateFreak-Browser-Origin", "https://web.test");
        return client;
    }
    private static async Task<string> BrowserStart(HttpClient client, string provider, bool link = false)
    {
        await Token(client);
        using var start = await client.PostAsync(link ? $"/api/v1/users/me/logins/{provider}/start" : $"/api/v1/auth/external/{provider}/start", null, Cancellation);
        Assert.Equal(HttpStatusCode.Redirect, start.StatusCode);
        var query = QueryHelpers.ParseQuery(start.Headers.Location!.Query);
        Assert.Equal($"https://web.test/api/v1/auth/external/{provider}/callback", query["redirect_uri"]);
        return query["state"].ToString();
    }

    [Theory]
    [InlineData("google")]
    [InlineData("github")]
    public async Task Trusted_browser_proxy_uses_public_origin_and_preserves_onboarding_and_replay_rules(string provider)
    {
        await using var factory = BrowserEnabled();
        using var client = BrowserClient(factory);
        var state = await BrowserStart(client, provider);
        using var completion = await Callback(client, provider, state);
        Assert.Equal(HttpStatusCode.OK, completion.StatusCode);
        var id = await CompleteRegistration(client);
        using var staff = await client.GetAsync("/api/v1/moderation/reports", Cancellation);
        Assert.Equal(HttpStatusCode.Forbidden, staff.StatusCode);
        using var replay = await client.GetAsync($"/api/v1/auth/external/{provider}/callback?code=test&state={Uri.EscapeDataString(state)}", Cancellation);
        Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode);
        using var me = await client.GetAsync("/api/v1/users/me", Cancellation);
        Assert.Equal(id, (await me.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("id").GetGuid());
    }

    [Theory]
    [InlineData(false, "https://web.test")]
    [InlineData(true, "https://evil.test")]
    [InlineData(true, "https://web.test/")]
    public async Task Canonical_origin_header_requires_authenticated_exact_match(bool proof, string origin)
    {
        await using var factory = BrowserEnabled();
        using var client = BrowserClient(factory);
        await Token(client);
        client.DefaultRequestHeaders.Remove("X-RateFreak-Browser-Origin");
        client.DefaultRequestHeaders.Add("X-RateFreak-Browser-Origin", origin);
        if (!proof)
        {
            client.DefaultRequestHeaders.Remove("X-RateFreak-Proxy-Key");
            client.DefaultRequestHeaders.Remove("X-RateFreak-Client-IP");
        }
        using var response = await client.PostAsync("/api/v1/auth/external/google/start", null, Cancellation);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.Null(response.Headers.Location);
    }

    [Fact]
    public async Task Trusted_browser_linking_still_rejects_revoked_sessions()
    {
        await using var factory = BrowserEnabled();
        using var client = BrowserClient(factory);
        await LocalAccount(client);
        var state = await BrowserStart(client, "github", true);
        await Token(client);
        using var logout = await client.PostAsync("/api/v1/auth/logout", null, Cancellation);
        using var completion = await Callback(client, "github", state);
        Assert.Equal(HttpStatusCode.Unauthorized, completion.StatusCode);
    }

    [Fact]
    public async Task Callback_budgets_partition_by_authenticated_client_address()
    {
        await using var factory = BrowserEnabled();
        using var client = BrowserClient(factory);
        for (var i = 0; i < 31; i++)
        {
            using var response = await client.GetAsync("/api/v1/auth/external/google/callback?state=forged&code=test", Cancellation);
            Assert.Equal(i < 30 ? HttpStatusCode.BadRequest : HttpStatusCode.TooManyRequests, response.StatusCode);
        }
        client.DefaultRequestHeaders.Remove("X-RateFreak-Client-IP");
        client.DefaultRequestHeaders.Add("X-RateFreak-Client-IP", "192.0.2.2");
        using var independent = await client.GetAsync("/api/v1/auth/external/google/callback?state=forged&code=test", Cancellation);
        Assert.Equal(HttpStatusCode.BadRequest, independent.StatusCode);
    }
}
