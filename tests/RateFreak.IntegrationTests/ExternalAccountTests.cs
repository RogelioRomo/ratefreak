using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace RateFreak.IntegrationTests;

[Trait("Category", "Database")]
public sealed partial class ExternalAccountTests(AccountDatabase database) : IClassFixture<AccountDatabase>
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;
    private ApiFactory Factory() => new(connectionString: database.ConnectionString);
    private static HttpClient Client(WebApplicationFactory<Program> factory) => factory.CreateClient(new WebApplicationFactoryClientOptions
    { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
    private static async Task Token(HttpClient client)
    {
        using var response = await client.GetAsync("/api/v1/auth/csrf", Cancellation);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", json.GetProperty("requestToken").GetString());
    }

    [Fact]
    public async Task Discovery_lists_disabled_providers_without_credentials()
    {
        await using var factory = Factory();
        using var client = Client(factory);
        using var response = await client.GetAsync("/api/v1/auth/providers", Cancellation);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        var body = await response.Content.ReadAsStringAsync(Cancellation);
        using var json = JsonDocument.Parse(body);
        Assert.Equal(2, json.RootElement.GetArrayLength());
        Assert.All(json.RootElement.EnumerateArray(), p => Assert.False(p.GetProperty("enabled").GetBoolean()));
        Assert.DoesNotContain("secret", body, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("google", HttpStatusCode.ServiceUnavailable)]
    [InlineData("github", HttpStatusCode.ServiceUnavailable)]
    [InlineData("unknown", HttpStatusCode.NotFound)]
    public async Task Starts_validate_csrf_and_provider_availability(string provider, HttpStatusCode expected)
    {
        await using var factory = Factory();
        using var client = Client(factory);
        using var noToken = await client.PostAsync($"/api/v1/auth/external/{provider}/start", null, Cancellation);
        Assert.Equal(HttpStatusCode.BadRequest, noToken.StatusCode);
        await Token(client);
        using var response = await client.PostAsync($"/api/v1/auth/external/{provider}/start", null, Cancellation);
        Assert.Equal(expected, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
    }

    [Fact]
    public async Task Disabled_provider_callbacks_cannot_bypass_guard_using_mixed_case()
    {
        await using var factory = Factory();
        using var client = Client(factory);
        using var response = await client.GetAsync("/API/v1/auth/external/Google/CallBack?code=test&state=forged", Cancellation);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
    }

    [Theory]
    [InlineData("/api/v1/auth/external/pending")]
    [InlineData("/api/v1/auth/external/complete")]
    [InlineData("/api/v1/users/me/logins")]
    public async Task Pending_completion_and_logins_require_their_own_authentication(string path)
    {
        await using var factory = Factory();
        using var client = Client(factory);
        using var response = await client.GetAsync(path, Cancellation);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
    }
}
