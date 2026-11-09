using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace RateFreak.IntegrationTests;

internal static class CustomCatalogClient
{
    public static HttpClient Client(ApiFactory factory) => factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
    public static async Task<Guid> SignIn(HttpClient client, CancellationToken ct)
    {
        async Task Token()
        {
            var token = await client.GetFromJsonAsync<JsonElement>("/api/v1/auth/csrf", ct);
            client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN"); client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token.GetProperty("requestToken").GetString());
        }
        await Token(); var tag = Guid.NewGuid().ToString("N")[..12]; var email = tag + "@example.test";
        var response = await client.PostAsJsonAsync("/api/v1/auth/register", new { email, password = "Local-password-72", handle = "user_" + tag, displayName = "Reader" }, ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode); var account = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "Local-password-72" }, ct)).StatusCode);
        await Token(); return account.GetProperty("id").GetGuid();
    }
    public static async Task<JsonElement> Json(HttpResponseMessage response, CancellationToken ct, HttpStatusCode expected = HttpStatusCode.OK)
    {
        Assert.Equal(expected, response.StatusCode); return await response.Content.ReadFromJsonAsync<JsonElement>(ct);
    }
}
