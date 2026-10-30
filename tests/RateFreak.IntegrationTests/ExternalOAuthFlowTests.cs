using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RateFreak.Infrastructure.Persistence;

namespace RateFreak.IntegrationTests;

public sealed partial class ExternalAccountTests
{
    private sealed class Provider : HttpMessageHandler
    {
        public string Subject { get; set; } = Random.Shared.NextInt64(1, long.MaxValue).ToString();
        public string? Email { get; set; } = $"{Guid.NewGuid():N}@example.test";
        public bool Verified { get; set; } = true;
        public bool Failure { get; set; }
        public bool EmailThrows { get; set; }
        public string? ProfileJson { get; set; }
        public readonly List<string> TokenBodies = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            if (EmailThrows && request.RequestUri!.AbsolutePath.EndsWith("/emails")) throw new HttpRequestException("Email service unavailable.");
            if (Failure) return new(HttpStatusCode.BadGateway);
            string body;
            if (request.RequestUri!.AbsolutePath.Contains("token"))
            {
                TokenBodies.Add(await request.Content!.ReadAsStringAsync(token));
                body = "{\"access_token\":\"test-access-token\",\"token_type\":\"Bearer\"}";
            }
            else if (request.RequestUri.AbsolutePath.EndsWith("/emails"))
                body = JsonSerializer.Serialize(Email is null ? Array.Empty<object>() :
                    new object[] { new { email = Email, verified = Verified, primary = true } });
            else body = JsonSerializer.Serialize(new { id = Subject, name = "A Reader", login = "reader", email = Email, verified_email = Verified });
            // GitHub IDs are numeric, Google IDs are opaque strings.
            if (request.RequestUri.Host == "api.github.com" && request.RequestUri.AbsolutePath == "/user")
                body = JsonSerializer.Serialize(new { id = long.Parse(Subject), name = "A Reader", login = "reader", email = Email });
            if (ProfileJson is not null && !request.RequestUri.AbsolutePath.Contains("token") && !request.RequestUri.AbsolutePath.EndsWith("/emails")) body = ProfileJson;
            return new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }
    private WebApplicationFactory<Program> Enabled(Provider upstream) => Factory().WithWebHostBuilder(builder =>
    {
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:External:PublicOrigin"] = "https://localhost",
            ["Authentication:External:Google:Enabled"] = "true",
            ["Authentication:External:Google:ClientId"] = "test-client",
            ["Authentication:External:Google:ClientSecret"] = "test-secret",
            ["Authentication:External:GitHub:Enabled"] = "true",
            ["Authentication:External:GitHub:ClientId"] = "test-client",
            ["Authentication:External:GitHub:ClientSecret"] = "test-secret"
        }));
        builder.ConfigureServices(services =>
        {
            services.PostConfigure<GoogleOptions>("google", options => options.Backchannel = new HttpClient(upstream));
            services.PostConfigure<OAuthOptions>("github", options => options.Backchannel = new HttpClient(upstream));
        });
    });
    private static async Task<string> Start(HttpClient client, string provider, bool link = false)
    {
        await Token(client);
        using var response = await client.PostAsync(link ? $"/api/v1/users/me/logins/{provider}/start" :
            $"/api/v1/auth/external/{provider}/start", null, Cancellation);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        var query = QueryHelpers.ParseQuery(response.Headers.Location!.Query);
        Assert.Equal("S256", query["code_challenge_method"]);
        Assert.False(string.IsNullOrEmpty(query["code_challenge"]));
        Assert.Equal($"https://localhost/api/v1/auth/external/{provider}/callback", query["redirect_uri"]);
        return query["state"].ToString();
    }
    private static async Task<HttpResponseMessage> Callback(HttpClient client, string provider, string state)
    {
        using var callback = await client.GetAsync($"/api/v1/auth/external/{provider}/callback?code=test-code&state={Uri.EscapeDataString(state)}", Cancellation);
        Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);
        Assert.Equal("/api/v1/auth/external/complete", callback.Headers.Location!.OriginalString);
        return await client.GetAsync(callback.Headers.Location, Cancellation);
    }
    private static async Task<Guid> CompleteRegistration(HttpClient client)
    {
        await Token(client);
        using var response = await client.PostAsJsonAsync("/api/v1/auth/external/register", new
        { handle = "ext_" + Guid.NewGuid().ToString("N")[..14], displayName = "A Reader" }, Cancellation);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("id").GetGuid();
    }

    [Theory]
    [InlineData("google")]
    [InlineData("github")]
    public async Task New_external_account_onboards_then_reuses_existing_identity_without_email(string provider)
    {
        var upstream = new Provider();
        await using var factory = Enabled(upstream);
        using var client = Client(factory);
        using var completion = await Callback(client, provider, await Start(client, provider));
        Assert.Equal(HttpStatusCode.OK, completion.StatusCode);
        Assert.True((await completion.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("requiresRegistration").GetBoolean());
        using var noSession = await client.GetAsync("/api/v1/users/me", Cancellation);
        Assert.Equal(HttpStatusCode.Unauthorized, noSession.StatusCode);
        var id = await CompleteRegistration(client);
        using var logins = await client.GetAsync("/api/v1/users/me/logins", Cancellation);
        var loginJson = await logins.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        Assert.False(loginJson.GetProperty("hasPassword").GetBoolean());
        Assert.Contains(provider, loginJson.GetProperty("providers").EnumerateArray().Select(p => p.GetString()));
        Assert.DoesNotContain(upstream.Subject, await logins.Content.ReadAsStringAsync(Cancellation));
        await Token(client);
        using var logout = await client.PostAsync("/api/v1/auth/logout", null, Cancellation);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        upstream.Email = null;
        upstream.Verified = false;
        using var second = await Callback(client, provider, await Start(client, provider));
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(id, (await second.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("id").GetGuid());
        Assert.All(upstream.TokenBodies, body => Assert.Contains("code_verifier=", body));
    }

    [Theory]
    [InlineData("google", false)]
    [InlineData("github", false)]
    [InlineData("google", true)]
    [InlineData("github", true)]
    public async Task New_identity_requires_a_valid_verified_email(string provider, bool missing)
    {
        var upstream = new Provider { Verified = missing, Email = missing ? null : "reader@example.test" };
        await using var factory = Enabled(upstream);
        using var client = Client(factory);
        using var completion = await Callback(client, provider, await Start(client, provider));
        Assert.Equal(HttpStatusCode.BadRequest, completion.StatusCode);
        using var pending = await client.GetAsync("/api/v1/auth/external/pending", Cancellation);
        Assert.Equal(HttpStatusCode.Unauthorized, pending.StatusCode);
    }

    [Theory]
    [InlineData("google")]
    [InlineData("github")]
    public async Task Invalid_state_and_upstream_failure_never_issue_a_session(string provider)
    {
        var upstream = new Provider();
        await using var factory = Enabled(upstream);
        using var client = Client(factory);
        using var invalid = await client.GetAsync($"/api/v1/auth/external/{provider}/callback?code=secret-code&state=forged", Cancellation);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.DoesNotContain("secret-code", await invalid.Content.ReadAsStringAsync(Cancellation));
        var state = await Start(client, provider);
        upstream.Failure = true;
        using var failed = await client.GetAsync($"/api/v1/auth/external/{provider}/callback?code=test-code&state={Uri.EscapeDataString(state)}", Cancellation);
        Assert.Equal(HttpStatusCode.BadRequest, failed.StatusCode);
        using var me = await client.GetAsync("/api/v1/users/me", Cancellation);
        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
    }

    private static async Task<(Guid Id, string Email, string Password)> LocalAccount(HttpClient client)
    {
        var email = $"{Guid.NewGuid():N}@example.test";
        const string password = "A-long-local-password-72";
        await Token(client);
        using var register = await client.PostAsJsonAsync("/api/v1/auth/register", new
        { email, password, handle = "local_" + Guid.NewGuid().ToString("N")[..12], displayName = "A Reader" }, Cancellation);
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        var id = (await register.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("id").GetGuid();
        using var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password }, Cancellation);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        return (id, email, password);
    }

    [Theory]
    [InlineData("google")]
    [InlineData("github")]
    public async Task Linking_preserves_local_account_and_does_not_require_provider_email(string provider)
    {
        var upstream = new Provider { Email = null };
        await using var factory = Enabled(upstream);
        using var client = Client(factory);
        var account = await LocalAccount(client);
        using var completion = await Callback(client, provider, await Start(client, provider, link: true));
        Assert.Equal(HttpStatusCode.OK, completion.StatusCode);
        using var list = await client.GetAsync("/api/v1/users/me/logins", Cancellation);
        var json = await list.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        Assert.True(json.GetProperty("hasPassword").GetBoolean());
        Assert.Equal(provider, Assert.Single(json.GetProperty("providers").EnumerateArray()).GetString());
        using var repeat = await Callback(client, provider, await Start(client, provider, link: true));
        Assert.Equal(HttpStatusCode.OK, repeat.StatusCode);
        await Token(client);
        using var logout = await client.PostAsync("/api/v1/auth/logout", null, Cancellation);
        using var login = await Callback(client, provider, await Start(client, provider));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Equal(account.Id, (await login.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Matching_email_does_not_merge_and_revoked_session_cannot_finish_linking()
    {
        var upstream = new Provider();
        await using var factory = Enabled(upstream);
        using var owner = Client(factory);
        var account = await LocalAccount(owner);
        upstream.Email = account.Email;
        using var stranger = Client(factory);
        using var collision = await Callback(stranger, "google", await Start(stranger, "google"));
        Assert.Equal(HttpStatusCode.Conflict, collision.StatusCode);
        using var strangerMe = await stranger.GetAsync("/api/v1/users/me", Cancellation);
        Assert.Equal(HttpStatusCode.Unauthorized, strangerMe.StatusCode);
        var state = await Start(owner, "google", link: true);
        await Token(owner);
        using var logout = await owner.PostAsync("/api/v1/auth/logout", null, Cancellation);
        using var rejected = await Callback(owner, "google", state);
        Assert.Equal(HttpStatusCode.Unauthorized, rejected.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<RateFreakDbContext>().UserLogins.AnyAsync(l => l.UserId == account.Id, Cancellation));
    }
}
