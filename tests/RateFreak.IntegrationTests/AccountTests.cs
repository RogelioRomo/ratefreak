using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using RateFreak.Infrastructure.Authentication;
using RateFreak.Infrastructure.Persistence;

namespace RateFreak.IntegrationTests;

[Trait("Category", "Database")]
public sealed class AccountTests(AccountDatabase database) : IClassFixture<AccountDatabase>
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;
    private ApiFactory Factory() => new(connectionString: database.ConnectionString);
    private static HttpClient Client(ApiFactory factory, bool cookies = true) => factory.CreateClient(
        new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false, HandleCookies = cookies });

    private sealed record Registration(string Email, string Password, string Handle, string DisplayName);
    private static Registration NewAccount() => new($"{Guid.NewGuid():N}@example.test",
        "A-long-local-password-72", "user_" + Guid.NewGuid().ToString("N")[..12], "A Reader");

    private static async Task Token(HttpClient client)
    {
        using var response = await client.GetAsync("/api/v1/auth/csrf", Cancellation);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Cancellation));
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", json.RootElement.GetProperty("requestToken").GetString());
    }

    private static Task<HttpResponseMessage> Register(HttpClient client, Registration account) =>
        client.PostAsJsonAsync("/api/v1/auth/register", account, Cancellation);
    private static Task<HttpResponseMessage> Login(HttpClient client, Registration account, string? password = null) =>
        client.PostAsJsonAsync("/api/v1/auth/login", new { email = account.Email.ToUpperInvariant(),
            password = password ?? account.Password }, Cancellation);

    [Fact]
    public async Task Account_flow_uses_secure_cookies_and_logout_revokes_replayed_sessions()
    {
        await using var factory = Factory();
        using var client = Client(factory);
        var account = NewAccount();
        await Token(client);
        using var registration = await Register(client, account);
        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);
        var body = await registration.Content.ReadAsStringAsync(Cancellation);
        Assert.DoesNotContain(account.Password, body);
        Assert.DoesNotContain("passwordHash", body);
        using var profile = JsonDocument.Parse(body);
        var id = profile.RootElement.GetProperty("id").GetGuid();
        Assert.NotEqual(Guid.Empty, id);
        Assert.Equal(account.Handle, profile.RootElement.GetProperty("handle").GetString());
        using var anonymous = await client.GetAsync("/api/v1/users/me", Cancellation);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        using var login = await Login(client, account);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var cookie = Assert.Single(login.Headers.GetValues("Set-Cookie"),
            value => value.StartsWith("__Host-ratefreak.session="));
        Assert.Contains("httponly", cookie.ToLowerInvariant());
        Assert.Contains("secure", cookie.ToLowerInvariant());
        Assert.Contains("samesite=lax", cookie.ToLowerInvariant());
        Assert.DoesNotContain("domain=", cookie.ToLowerInvariant());
        using var me = await client.GetAsync("/api/v1/users/me", Cancellation);
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        Assert.True(me.Headers.CacheControl?.NoStore);
        using var current = JsonDocument.Parse(await me.Content.ReadAsStringAsync(Cancellation));
        Assert.Equal(id, current.RootElement.GetProperty("id").GetGuid());
        Assert.Equal(account.Email, current.RootElement.GetProperty("email").GetString());

        // Authentication changes the identity bound to the antiforgery token.
        await Token(client);
        using var logout = await client.PostAsync("/api/v1/auth/logout", null, Cancellation);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        using var afterLogout = await client.GetAsync("/api/v1/users/me", Cancellation);
        Assert.Equal(HttpStatusCode.Unauthorized, afterLogout.StatusCode);
        using var replay = Client(factory, cookies: false);
        replay.DefaultRequestHeaders.Add("Cookie", cookie.Split(';')[0]);
        using var rejected = await replay.GetAsync("/api/v1/users/me", Cancellation);
        Assert.Equal(HttpStatusCode.Unauthorized, rejected.StatusCode);
    }

    [Theory]
    [InlineData("/api/v1/auth/register")]
    [InlineData("/api/v1/auth/login")]
    public async Task Anonymous_auth_mutations_reject_missing_or_forged_csrf_tokens(string route)
    {
        await using var factory = Factory();
        using var client = Client(factory);
        using var missing = await client.PostAsJsonAsync(route, NewAccount(), Cancellation);
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        await Token(client);
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", "forged");
        using var forged = await client.PostAsJsonAsync(route, NewAccount(), Cancellation);
        Assert.Equal(HttpStatusCode.BadRequest, forged.StatusCode);
    }

    [Fact]
    public async Task Logout_requires_authentication_and_csrf_and_rejects_another_clients_token()
    {
        await using var factory = Factory();
        using var client = Client(factory);
        using var unauthorized = await client.PostAsync("/api/v1/auth/logout", null, Cancellation);
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
        Assert.Null(unauthorized.Headers.Location);
        var account = NewAccount();
        await Token(client);
        using var registration = await Register(client, account);
        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);
        using var login = await Login(client, account);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        // An anonymous token from before login must not authorize this mutation.
        using var oldToken = await client.PostAsync("/api/v1/auth/logout", null, Cancellation);
        Assert.Equal(HttpStatusCode.BadRequest, oldToken.StatusCode);
        using var other = Client(factory);
        await Token(other);
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", other.DefaultRequestHeaders.GetValues("X-CSRF-TOKEN"));
        using var foreignToken = await client.PostAsync("/api/v1/auth/logout", null, Cancellation);
        Assert.Equal(HttpStatusCode.BadRequest, foreignToken.StatusCode);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Registration_enforces_case_insensitive_email_and_handle_uniqueness(bool duplicateEmail)
    {
        await using var factory = Factory();
        using var client = Client(factory);
        var first = NewAccount();
        var second = NewAccount();
        second = duplicateEmail ? second with { Email = first.Email.ToUpperInvariant() }
            : second with { Handle = first.Handle.ToUpperInvariant() };
        await Token(client);
        using var created = await Register(client, first);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var duplicate = await Register(client, second);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    }

    [Theory]
    [InlineData("email")]
    [InlineData("handle")]
    [InlineData("displayName")]
    [InlineData("password")]
    [InlineData("passwordLength")]
    [InlineData("emailNormalizationLength")]
    public async Task Registration_rejects_invalid_or_unbounded_input(string field)
    {
        await using var factory = Factory();
        using var client = Client(factory);
        var account = NewAccount();
        account = field switch
        {
            "email" => account with { Email = "not-an-email" },
            "handle" => account with { Handle = "bad handle!" },
            "displayName" => account with { DisplayName = new string('x', 81) },
            "password" => account with { Password = "weak" },
            "emailNormalizationLength" => account with { Email = new string('\u0344', 240) + "@a.co" },
            _ => account with { Password = new string('x', 129) }
        };
        await Token(client);
        using var response = await Register(client, account);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Failed_logins_are_generic_and_five_failures_lock_out_the_account()
    {
        await using var factory = Factory();
        using var client = Client(factory);
        var account = NewAccount();
        await Token(client);
        using var created = await Register(client, account);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var unknown = await Login(client, NewAccount());
        Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);
        using var unknownJson = JsonDocument.Parse(await unknown.Content.ReadAsStringAsync(Cancellation));
        for (var attempt = 0; attempt < 5; attempt++)
        {
            using var failure = await Login(client, account, "wrong-password-123");
            Assert.Equal(HttpStatusCode.Unauthorized, failure.StatusCode);
            Assert.False(failure.Headers.Contains("Set-Cookie"));
            using var failureJson = JsonDocument.Parse(await failure.Content.ReadAsStringAsync(Cancellation));
            Assert.Equal(unknownJson.RootElement.GetProperty("title").GetString(),
                failureJson.RootElement.GetProperty("title").GetString());
        }
        using var locked = await Login(client, account);
        Assert.Equal(HttpStatusCode.Unauthorized, locked.StatusCode);
    }

    [Fact]
    public async Task Authentication_rate_limits_return_problem_details_and_retry_after()
    {
        await using var factory = Factory();
        using var client = Client(factory);
        for (var request = 0; request < 30; request++)
        {
            using var token = await client.GetAsync("/api/v1/auth/csrf", Cancellation);
            Assert.Equal(HttpStatusCode.OK, token.StatusCode);
        }
        using var rejected = await client.GetAsync("/api/v1/auth/csrf", Cancellation);
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.Equal("application/problem+json", rejected.Content.Headers.ContentType?.MediaType);
        Assert.NotNull(rejected.Headers.RetryAfter);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Concurrent_registrations_cannot_create_duplicate_accounts(bool duplicateEmail)
    {
        await using var factory = Factory();
        using var first = Client(factory);
        using var second = Client(factory);
        var account = NewAccount();
        var other = NewAccount();
        other = duplicateEmail ? other with { Email = account.Email.ToUpperInvariant() }
            : other with { Handle = account.Handle.ToUpperInvariant() };
        await Token(first);
        await Token(second);
        var responses = await Task.WhenAll(Register(first, account), Register(second, other));
        try
        {
            Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
            Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict);
        }
        finally { foreach (var response in responses) response.Dispose(); }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Database_rejects_duplicate_normalized_identifiers_without_application_validation(bool duplicateEmail)
    {
        await using var factory = Factory();
        using var client = Client(factory);
        var account = NewAccount();
        await Token(client);
        using var created = await Register(client, account);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<RateFreakDbContext>();
        var other = NewAccount();
        context.Users.Add(new AccountUser
        {
            Id = Guid.NewGuid(), DisplayName = "Another Reader",
            Email = duplicateEmail ? account.Email.ToUpperInvariant() : other.Email,
            NormalizedEmail = (duplicateEmail ? account.Email : other.Email).ToUpperInvariant(),
            UserName = duplicateEmail ? other.Handle : account.Handle.ToUpperInvariant(),
            NormalizedUserName = (duplicateEmail ? other.Handle : account.Handle).ToUpperInvariant()
        });
        var error = await Assert.ThrowsAsync<DbUpdateException>(
            () => context.SaveChangesAsync(Cancellation));
        var postgres = Assert.IsType<PostgresException>(error.InnerException);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, postgres.SqlState);
        Assert.Equal(duplicateEmail ? "ux_users_normalized_email" : "ux_users_normalized_handle", postgres.ConstraintName);
    }

    [Theory]
    [InlineData("register", 5)]
    [InlineData("login", 10)]
    public async Task Login_and_registration_have_separate_bounded_request_budgets(string route, int permits)
    {
        await using var factory = Factory();
        using var client = Client(factory);
        await Token(client);
        for (var request = 0; request < permits; request++)
        {
            using var response = await client.PostAsJsonAsync("/api/v1/auth/" + route,
                new { email = "invalid", password = "invalid" }, Cancellation);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        using var rejected = await client.PostAsJsonAsync("/api/v1/auth/" + route,
            new { email = "invalid", password = "invalid" }, Cancellation);
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.NotNull(rejected.Headers.RetryAfter);
    }

    [Fact]
    public async Task Session_remains_valid_after_server_restart_with_the_same_durable_keys()
    {
        var keys = Path.Combine(Path.GetTempPath(), "ratefreak-persistence-test-" + Guid.NewGuid());
        try
        {
            string cookie;
            await using (var first = new ApiFactory(connectionString: database.ConnectionString, dataProtectionKeysPath: keys))
            {
                using var client = Client(first);
                var account = NewAccount();
                await Token(client);
                using var created = await Register(client, account);
                Assert.Equal(HttpStatusCode.Created, created.StatusCode);
                using var login = await Login(client, account);
                Assert.Equal(HttpStatusCode.OK, login.StatusCode);
                cookie = Assert.Single(login.Headers.GetValues("Set-Cookie"),
                    value => value.StartsWith("__Host-ratefreak.session=")).Split(';')[0];
            }
            await using var restarted = new ApiFactory(connectionString: database.ConnectionString, dataProtectionKeysPath: keys);
            using var resumed = Client(restarted, cookies: false);
            resumed.DefaultRequestHeaders.Add("Cookie", cookie);
            using var profile = await resumed.GetAsync("/api/v1/users/me", Cancellation);
            Assert.Equal(HttpStatusCode.OK, profile.StatusCode);
        }
        finally { if (Directory.Exists(keys)) Directory.Delete(keys, recursive: true); }
    }
}
