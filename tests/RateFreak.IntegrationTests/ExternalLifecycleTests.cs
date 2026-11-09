using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;

namespace RateFreak.IntegrationTests;

public sealed partial class ExternalAccountTests
{
    private static async Task<string> StartLifecycle(HttpClient client, string provider, string operation = "export")
    {
        await Token(client);
        using var response = await client.PostAsJsonAsync($"/api/v1/users/me/data/external/{provider}/start", new { operation }, Cancellation);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return QueryHelpers.ParseQuery(response.Headers.Location!.Query)["state"].ToString();
    }

    [Theory]
    [InlineData("google")]
    [InlineData("github")]
    public async Task Linked_provider_confirms_only_the_requested_lifecycle_operation(string provider)
    {
        var upstream = new Provider();
        await using var factory = Enabled(upstream);
        using var client = Client(factory);
        using var initial = await Callback(client, provider, await Start(client, provider));
        var accountId = await CompleteRegistration(client);
        using var completion = await Callback(client, provider, await StartLifecycle(client, provider));
        Assert.Equal(HttpStatusCode.OK, completion.StatusCode);
        var json = await completion.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        Assert.True(json.GetProperty("reauthenticated").GetBoolean());
        Assert.Equal("export", json.GetProperty("operation").GetString());
        Assert.Equal(provider, json.GetProperty("provider").GetString());
        Assert.Contains(completion.Headers.GetValues("Set-Cookie"), value => value.StartsWith("__Secure-ratefreak.lifecycle=") &&
            value.Contains("path=/api/v1/users/me/data", StringComparison.OrdinalIgnoreCase) &&
            value.Contains("httponly", StringComparison.OrdinalIgnoreCase) && value.Contains("secure", StringComparison.OrdinalIgnoreCase) &&
            value.Contains("samesite=strict", StringComparison.OrdinalIgnoreCase));
        using var me = await client.GetAsync("/api/v1/users/me", Cancellation);
        Assert.Equal(accountId, (await me.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("id").GetGuid());
        using var replay = await client.GetAsync("/api/v1/auth/external/complete", Cancellation);
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
    }

    [Fact]
    public async Task Lifecycle_confirmation_rejects_a_different_provider_subject_without_registration_or_switching()
    {
        var upstream = new Provider();
        await using var factory = Enabled(upstream);
        using var client = Client(factory);
        using var initial = await Callback(client, "google", await Start(client, "google"));
        var accountId = await CompleteRegistration(client);
        var state = await StartLifecycle(client, "google", "delete");
        upstream.Subject = Guid.NewGuid().ToString("N");
        using var completion = await Callback(client, "google", state);
        Assert.Equal(HttpStatusCode.Unauthorized, completion.StatusCode);
        Assert.DoesNotContain(completion.Headers.TryGetValues("Set-Cookie", out var cookies) ? cookies : [], value => value.StartsWith("__Secure-ratefreak.lifecycle="));
        using var pending = await client.GetAsync("/api/v1/auth/external/pending", Cancellation);
        Assert.Equal(HttpStatusCode.Unauthorized, pending.StatusCode);
        using var me = await client.GetAsync("/api/v1/users/me", Cancellation);
        Assert.Equal(accountId, (await me.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Lifecycle_start_rejects_local_password_accounts_invalid_operations_and_missing_csrf()
    {
        await using var factory = Enabled(new Provider());
        using var client = Client(factory);
        await LocalAccount(client);
        await Token(client);
        using var local = await client.PostAsJsonAsync("/api/v1/users/me/data/external/google/start", new { operation = "export" }, Cancellation);
        Assert.Equal(HttpStatusCode.BadRequest, local.StatusCode);
        using var invalid = await client.PostAsJsonAsync("/api/v1/users/me/data/external/google/start", new { operation = "other" }, Cancellation);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        using var csrf = await client.PostAsJsonAsync("/api/v1/users/me/data/external/google/start", new { operation = "delete" }, Cancellation);
        Assert.Equal(HttpStatusCode.BadRequest, csrf.StatusCode);
    }

    [Fact]
    public async Task Revoked_session_cannot_complete_lifecycle_confirmation()
    {
        var upstream = new Provider();
        await using var factory = Enabled(upstream);
        using var client = Client(factory);
        using var initial = await Callback(client, "google", await Start(client, "google"));
        await CompleteRegistration(client);
        var state = await StartLifecycle(client, "google");
        await Token(client);
        using var logout = await client.PostAsync("/api/v1/auth/logout", null, Cancellation);
        using var completion = await Callback(client, "google", state);
        Assert.Equal(HttpStatusCode.Unauthorized, completion.StatusCode);
    }
}
