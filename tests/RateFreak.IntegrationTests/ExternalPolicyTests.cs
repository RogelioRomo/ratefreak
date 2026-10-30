using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using RateFreak.Infrastructure.Persistence;

namespace RateFreak.IntegrationTests;

public sealed partial class ExternalAccountTests
{
    [Fact]
    public async Task Handle_collision_allows_retry_and_pending_registration_does_not_grant_login_on_replay()
    {
        var upstream = new Provider();
        await using var factory = Enabled(upstream);
        using var owner = Client(factory);
        await LocalAccount(owner);
        using var me = await owner.GetAsync("/api/v1/users/me", Cancellation);
        var handle = (await me.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("handle").GetString();
        using var newcomer = Client(factory);
        using var onboarding = await Callback(newcomer, "google", await Start(newcomer, "google"));
        newcomer.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        using var missingCsrf = await newcomer.PostAsJsonAsync("/api/v1/auth/external/register", new { handle, displayName = "Reader" }, Cancellation);
        Assert.Equal(HttpStatusCode.BadRequest, missingCsrf.StatusCode);
        await Token(newcomer);
        using var taken = await newcomer.PostAsJsonAsync("/api/v1/auth/external/register", new { handle, displayName = "Reader" }, Cancellation);
        Assert.Equal(HttpStatusCode.Conflict, taken.StatusCode);
        using var pending = await newcomer.GetAsync("/api/v1/auth/external/pending", Cancellation);
        Assert.Equal(HttpStatusCode.OK, pending.StatusCode);
        await CompleteRegistration(newcomer);
        await Token(newcomer);
        using var replay = await newcomer.PostAsJsonAsync("/api/v1/auth/external/register", new { handle, displayName = "Reader" }, Cancellation);
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
    }

    [Fact]
    public async Task Starts_enforce_origin_current_session_and_link_csrf()
    {
        await using var factory = Enabled(new Provider());
        using var client = Client(factory);
        using var anonymousLink = await client.PostAsync("/api/v1/users/me/logins/github/start", null, Cancellation);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousLink.StatusCode);
        await LocalAccount(client);
        // The anonymous token from before login must not authorize linking.
        using var oldToken = await client.PostAsync("/api/v1/users/me/logins/github/start", null, Cancellation);
        Assert.Equal(HttpStatusCode.BadRequest, oldToken.StatusCode);
        await Token(client);
        using var switchAccount = await client.PostAsync("/api/v1/auth/external/google/start", null, Cancellation);
        Assert.Equal(HttpStatusCode.Conflict, switchAccount.StatusCode);
        using var foreignOrigin = Client(factory);
        foreignOrigin.BaseAddress = new Uri("https://127.0.0.1");
        await Token(foreignOrigin);
        using var host = await foreignOrigin.PostAsync("/api/v1/auth/external/google/start", null, Cancellation);
        Assert.Equal(HttpStatusCode.BadRequest, host.StatusCode);
        using var callback = await foreignOrigin.GetAsync("/api/v1/auth/external/google/callback?code=test&state=forged", Cancellation);
        Assert.Equal(HttpStatusCode.BadRequest, callback.StatusCode);
    }

    [Theory]
    [InlineData("google")]
    [InlineData("github")]
    public async Task Callback_denial_missing_correlation_and_reused_state_are_rejected(string provider)
    {
        await using var factory = Enabled(new Provider());
        using var client = Client(factory);
        var state = await Start(client, provider);
        using var denied = await client.GetAsync($"/api/v1/auth/external/{provider}/callback?error=access_denied&state={Uri.EscapeDataString(state)}", Cancellation);
        Assert.Equal(HttpStatusCode.BadRequest, denied.StatusCode);
        Assert.True(denied.Headers.CacheControl?.NoStore);
        state = await Start(client, provider);
        using var other = Client(factory);
        using var noCookie = await other.GetAsync($"/api/v1/auth/external/{provider}/callback?code=test&state={Uri.EscapeDataString(state)}", Cancellation);
        Assert.Equal(HttpStatusCode.BadRequest, noCookie.StatusCode);
        using var completed = await Callback(client, provider, state);
        Assert.Equal(HttpStatusCode.OK, completed.StatusCode);
        using var reused = await client.GetAsync($"/api/v1/auth/external/{provider}/callback?code=test&state={Uri.EscapeDataString(state)}", Cancellation);
        Assert.Equal(HttpStatusCode.BadRequest, reused.StatusCode);
    }

    [Fact]
    public async Task Locked_account_cannot_use_a_linked_identity_to_bypass_lockout()
    {
        var upstream = new Provider();
        await using var factory = Enabled(upstream);
        using var client = Client(factory);
        var account = await LocalAccount(client);
        using var linked = await Callback(client, "google", await Start(client, "google", true));
        Assert.Equal(HttpStatusCode.OK, linked.StatusCode);
        await Token(client);
        using var logout = await client.PostAsync("/api/v1/auth/logout", null, Cancellation);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RateFreakDbContext>();
            await db.Users.Where(u => u.Id == account.Id).ExecuteUpdateAsync(u => u
                .SetProperty(x => x.LockoutEnabled, true).SetProperty(x => x.LockoutEnd, DateTimeOffset.UtcNow.AddMinutes(15)), Cancellation);
        }
        using var completion = await Callback(client, "google", await Start(client, "google"));
        Assert.Equal(HttpStatusCode.Unauthorized, completion.StatusCode);
    }

    [Fact]
    public async Task External_auth_budgets_return_no_store_and_retry_after()
    {
        await using var factory = Enabled(new Provider());
        using var client = Client(factory);
        await Token(client);
        for (var i = 0; i < 10; i++)
        {
            using var start = await client.PostAsync("/api/v1/auth/external/google/start", null, Cancellation);
            Assert.Equal(HttpStatusCode.Redirect, start.StatusCode);
        }
        using var limited = await client.PostAsync("/api/v1/auth/external/google/start", null, Cancellation);
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.True(limited.Headers.CacheControl?.NoStore);
        Assert.NotNull(limited.Headers.RetryAfter);
        for (var i = 0; i < 30; i++)
        {
            using var callback = await client.GetAsync("/api/v1/auth/external/github/callback?code=test&state=forged", Cancellation);
            Assert.Equal(HttpStatusCode.BadRequest, callback.StatusCode);
        }
        using var callbackLimited = await client.GetAsync("/api/v1/auth/external/github/callback?code=test&state=forged", Cancellation);
        Assert.Equal(HttpStatusCode.TooManyRequests, callbackLimited.StatusCode);
        Assert.True(callbackLimited.Headers.CacheControl?.NoStore);
        Assert.NotNull(callbackLimited.Headers.RetryAfter);
        using var mixedCase = await client.GetAsync("/API/v1/auth/external/Google/CallBack?code=test&state=forged", Cancellation);
        Assert.Equal(HttpStatusCode.TooManyRequests, mixedCase.StatusCode);
    }

    private sealed class StampBeforeLock(string connectionString) : DbCommandInterceptor
    {
        public Guid? Actor { get; set; }
        public override async ValueTask<InterceptionResult<System.Data.Common.DbDataReader>> ReaderExecutingAsync(
            System.Data.Common.DbCommand command, CommandEventData eventData,
            InterceptionResult<System.Data.Common.DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (Actor is { } actor && command.CommandText.Contains("FOR UPDATE", StringComparison.Ordinal))
            {
                Actor = null;
                await using var connection = new Npgsql.NpgsqlConnection(connectionString);
                await connection.OpenAsync(cancellationToken);
                await using var update = new Npgsql.NpgsqlCommand("UPDATE users SET security_stamp = @stamp WHERE id = @id", connection);
                update.Parameters.AddWithValue("stamp", Guid.NewGuid().ToString());
                update.Parameters.AddWithValue("id", actor);
                await update.ExecuteNonQueryAsync(cancellationToken);
            }
            return result;
        }
    }
    [Fact]
    public async Task Security_stamp_changed_between_session_validation_and_link_write_is_rejected()
    {
        var hook = new StampBeforeLock(database.ConnectionString);
        await using var factory = Enabled(new Provider()).WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddDbContext<RateFreakDbContext>(options => options.AddInterceptors(hook))));
        using var client = Client(factory);
        var account = await LocalAccount(client);
        var state = await Start(client, "google", true);
        hook.Actor = account.Id;
        using var completion = await Callback(client, "google", state);
        Assert.Equal(HttpStatusCode.Unauthorized, completion.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<RateFreakDbContext>().UserLogins.AnyAsync(l => l.UserId == account.Id, Cancellation));
    }
}
