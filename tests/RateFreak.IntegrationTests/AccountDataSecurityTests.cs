using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace RateFreak.IntegrationTests;

public sealed partial class ExternalAccountTests
{
    [Fact]
    public async Task Password_confirmation_requires_CSRF_valid_operation_and_current_stamp()
    {
        await using var factory = Factory();
        using var client = Client(factory);
        var account = await LocalAccount(client);
        await Token(client);
        using var invalidOperation = await client.PostAsJsonAsync("/api/v1/users/me/data/confirm", new { operation = "other", password = account.Password }, Cancellation);
        Assert.Equal(HttpStatusCode.BadRequest, invalidOperation.StatusCode);
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        using var missingCsrf = await client.PostAsJsonAsync("/api/v1/users/me/data/confirm", new { operation = "export", password = account.Password }, Cancellation);
        Assert.Equal(HttpStatusCode.BadRequest, missingCsrf.StatusCode);
        await Token(client);
        using var confirmed = await client.PostAsJsonAsync("/api/v1/users/me/data/confirm", new { operation = "export", password = account.Password }, Cancellation);
        Assert.Equal(HttpStatusCode.NoContent, confirmed.StatusCode);
        using var status = await client.GetAsync("/api/v1/users/me/data", Cancellation);
        Assert.Equal("export", (await status.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("confirmedOperation").GetString());
        using var logout = await client.PostAsync("/api/v1/auth/logout", null, Cancellation);
        await Token(client);
        using var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = account.Email, password = account.Password }, Cancellation);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        await Token(client);
        using var afterLogout = await client.PostAsJsonAsync("/api/v1/users/me/data/export", new { }, Cancellation);
        Assert.Equal(HttpStatusCode.BadRequest, afterLogout.StatusCode);
    }

    [Fact]
    public async Task Lifecycle_confirmation_has_an_IP_budget_shared_across_accounts()
    {
        await using var factory = BrowserEnabled();
        var clients = new[] { BrowserClient(factory), BrowserClient(factory), BrowserClient(factory) };
        try
        {
            foreach (var client in clients) { await LocalAccount(client); await Token(client); }
            // Invalid ownership requests consume the budget without issuing grants.
            for (var i = 0; i < 20; i++)
            {
                using var response = await clients[i % 3].PostAsJsonAsync("/api/v1/users/me/data/external/google/start", new { operation = "invalid" }, Cancellation);
                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            }
            using var rejected = await clients[2].PostAsJsonAsync("/api/v1/users/me/data/external/google/start", new { operation = "invalid" }, Cancellation);
            Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
            Assert.NotNull(rejected.Headers.RetryAfter);
            Assert.True(rejected.Headers.CacheControl?.NoStore);
            clients[2].DefaultRequestHeaders.Remove("X-RateFreak-Client-IP");
            clients[2].DefaultRequestHeaders.Add("X-RateFreak-Client-IP", "192.0.2.2");
            using var otherIp = await clients[2].PostAsJsonAsync("/api/v1/users/me/data/external/google/start", new { operation = "invalid" }, Cancellation);
            Assert.Equal(HttpStatusCode.BadRequest, otherIp.StatusCode);
        }
        finally { foreach (var client in clients) client.Dispose(); }
    }

    [Fact]
    public async Task Lifecycle_confirmation_account_budget_cannot_be_reset_by_changing_IP()
    {
        await using var factory = BrowserEnabled();
        using var client = BrowserClient(factory);
        await LocalAccount(client); await Token(client);
        for (var i = 0; i < 10; i++)
        {
            client.DefaultRequestHeaders.Remove("X-RateFreak-Client-IP");
            client.DefaultRequestHeaders.Add("X-RateFreak-Client-IP", $"192.0.2.{i + 1}");
            using var response = await client.PostAsJsonAsync("/api/v1/users/me/data/external/google/start", new { operation = "invalid" }, Cancellation);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        client.DefaultRequestHeaders.Remove("X-RateFreak-Client-IP");
        client.DefaultRequestHeaders.Add("X-RateFreak-Client-IP", "192.0.2.100");
        using var rejected = await client.PostAsJsonAsync("/api/v1/users/me/data/external/google/start", new { operation = "invalid" }, Cancellation);
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.NotNull(rejected.Headers.RetryAfter);
    }
}
