using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http.Json;
using System.Text.Json;

namespace RateFreak.IntegrationTests;

public sealed class InstallationUnavailableTests
{
    [Fact]
    public async Task Unreachable_database_fails_closed_but_csrf_remains_available()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(connectionString: "Host=127.0.0.1;Port=1;Database=unavailable;Username=test;Password=test;Timeout=1;Pooling=false");
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        foreach (var path in new[] { "/api/v1/setup/status", "/api/v1/media", "/api/v1/users/me" })
        {
            using var response = await client.GetAsync(path, ct);
            Assert.Equal(HttpStatusCode.ServiceUnavailable,response.StatusCode);
            Assert.True(response.Headers.CacheControl?.NoStore);
        }
        var csrf = await client.GetFromJsonAsync<JsonElement>("/api/v1/auth/csrf", ct);
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", csrf.GetProperty("requestToken").GetString());
        using var setup = await client.PostAsJsonAsync("/api/v1/setup", new { setupSecret = new string('a',64) }, ct);
        Assert.Equal(HttpStatusCode.ServiceUnavailable,setup.StatusCode);
    }
}
