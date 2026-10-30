using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RateFreak.Infrastructure.Persistence;

namespace RateFreak.IntegrationTests;

public sealed partial class ExternalAccountTests
{
    [Fact]
    public async Task Completed_external_cookie_cannot_be_replayed_after_logout()
    {
        var upstream = new Provider();
        await using var factory = Enabled(upstream);
        using var client = Client(factory);
        using var onboarding = await Callback(client, "google", await Start(client, "google"));
        await CompleteRegistration(client);
        await Token(client);
        using var logout = await client.PostAsync("/api/v1/auth/logout", null, Cancellation);
        var state = await Start(client, "google");
        using var callback = await client.GetAsync($"/api/v1/auth/external/google/callback?code=test&state={Uri.EscapeDataString(state)}", Cancellation);
        Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);
        var cookie = Assert.Single(callback.Headers.GetValues("Set-Cookie"), c => c.StartsWith("__Host-ratefreak.external=")).Split(';')[0];
        using var completed = await client.GetAsync("/api/v1/auth/external/complete", Cancellation);
        Assert.Equal(HttpStatusCode.OK, completed.StatusCode);
        await Token(client);
        using var logoutAgain = await client.PostAsync("/api/v1/auth/logout", null, Cancellation);
        using var replay = factory.CreateClient(new WebApplicationFactoryClientOptions
        { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = false });
        replay.DefaultRequestHeaders.Add("Cookie", cookie);
        using var rejected = await replay.GetAsync("/api/v1/auth/external/complete", Cancellation);
        Assert.Equal(HttpStatusCode.Unauthorized, rejected.StatusCode);
        Assert.DoesNotContain(rejected.Headers.GetValues("Set-Cookie"), c => c.StartsWith("__Host-ratefreak.session="));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task External_ticket_expiry_and_tampering_are_rejected(bool tamper)
    {
        var upstream = new Provider();
        await using var factory = Enabled(upstream);
        using var client = Client(factory);
        var state = await Start(client, "github");
        using var callback = await client.GetAsync($"/api/v1/auth/external/github/callback?code=test&state={Uri.EscapeDataString(state)}", Cancellation);
        var cookie = Assert.Single(callback.Headers.GetValues("Set-Cookie"), c => c.StartsWith("__Host-ratefreak.external=")).Split(';')[0];
        var format = factory.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(IdentityConstants.ExternalScheme).TicketDataFormat;
        var value = cookie[(cookie.IndexOf('=') + 1)..];
        if (tamper) value = "forged" + value[6..];
        else
        {
            var ticket = format.Unprotect(value)!;
            ticket.Properties.ExpiresUtc = DateTimeOffset.UtcNow.AddSeconds(-1);
            value = format.Protect(ticket);
        }
        using var replay = factory.CreateClient(new WebApplicationFactoryClientOptions
        { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = false });
        replay.DefaultRequestHeaders.Add("Cookie", "__Host-ratefreak.external=" + value);
        using var rejected = await replay.GetAsync("/api/v1/auth/external/complete", Cancellation);
        Assert.Equal(HttpStatusCode.Unauthorized, rejected.StatusCode);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"handle\":\"bad handle\",\"displayName\":\"Reader\"}")]
    [InlineData("{\"handle\":\"valid_handle\",\"displayName\":\"Reader\",\"email\":\"supplied@example.test\"}")]
    [InlineData("{\"handle\":\"valid_handle\",\"handle\":\"another\",\"displayName\":\"Reader\"}")]
    [InlineData("{\"handle\":\"valid_handle\",\"displayName\":\"\\ud800\"}")]
    public async Task Onboarding_rejects_invalid_unknown_duplicate_or_malformed_fields(string payload)
    {
        await using var factory = Enabled(new Provider());
        using var client = Client(factory);
        using var onboarding = await Callback(client, "google", await Start(client, "google"));
        await Token(client);
        using var response = await client.PostAsync("/api/v1/auth/external/register", new StringContent(payload, System.Text.Encoding.UTF8, "application/json"), Cancellation);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
    }

    [Fact]
    public async Task Two_accounts_cannot_claim_the_same_external_identity_or_replace_a_provider()
    {
        var upstream = new Provider { Email = null };
        await using var factory = Enabled(upstream);
        using var first = Client(factory);
        var a = await LocalAccount(first);
        using var linked = await Callback(first, "github", await Start(first, "github", true));
        Assert.Equal(HttpStatusCode.OK, linked.StatusCode);
        using var other = Client(factory);
        var b = await LocalAccount(other);
        using var taken = await Callback(other, "github", await Start(other, "github", true));
        Assert.Equal(HttpStatusCode.Conflict, taken.StatusCode);
        upstream.Subject = Random.Shared.NextInt64(1, long.MaxValue).ToString();
        using var replace = await Callback(first, "github", await Start(first, "github", true));
        Assert.Equal(HttpStatusCode.Conflict, replace.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RateFreakDbContext>();
        Assert.Single(await db.UserLogins.Where(l => l.UserId == a.Id).ToListAsync(Cancellation));
        Assert.False(await db.UserLogins.AnyAsync(l => l.UserId == b.Id, Cancellation));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Concurrent_onboarding_creates_one_account_without_orphans(bool changeEmail)
    {
        var upstream = new Provider();
        await using var factory = Enabled(upstream);
        using var first = Client(factory);
        using var second = Client(factory);
        using var one = await Callback(first, "github", await Start(first, "github"));
        if (changeEmail) upstream.Email = $"{Guid.NewGuid():N}@example.test";
        using var two = await Callback(second, "github", await Start(second, "github"));
        await Token(first); await Token(second);
        var handles = new[] { "race_" + Guid.NewGuid().ToString("N")[..12], "race_" + Guid.NewGuid().ToString("N")[..12] };
        var responses = await Task.WhenAll(first.PostAsJsonAsync("/api/v1/auth/external/register", new { handle = handles[0], displayName = "One" }, Cancellation),
            second.PostAsJsonAsync("/api/v1/auth/external/register", new { handle = handles[1], displayName = "Two" }, Cancellation));
        try
        {
            Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
            Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict);
        }
        finally { foreach (var response in responses) response.Dispose(); }
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RateFreakDbContext>();
        Assert.Single(await db.Users.Where(u => handles.Contains(u.UserName!)).ToListAsync(Cancellation));
        Assert.Single(await db.UserLogins.Where(l => l.ProviderKey == upstream.Subject && l.LoginProvider == "github").ToListAsync(Cancellation));
    }
}
