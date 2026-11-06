using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RateFreak.Infrastructure.Authentication;
using RateFreak.Infrastructure.Persistence;

namespace RateFreak.IntegrationTests;

[Trait("Category", "Database")]
public sealed class AccountRecoveryTests(AccountDatabase database) : IClassFixture<AccountDatabase>
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;
    private const string Password = "Original-local-password-72";
    private const string NewPassword = "Replacement-local-password-83";
    private ApiFactory Factory() => new(connectionString: database.ConnectionString);
    private static HttpClient Client(ApiFactory factory) => factory.CreateClient(new WebApplicationFactoryClientOptions
        { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
    private static async Task Token(HttpClient client)
    {
        using var response = await client.GetAsync("/api/v1/auth/csrf", Cancellation);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", body.GetProperty("requestToken").GetString());
    }
    private static async Task<(Guid Id, string Handle, string Email, string Code)> Register(HttpClient client)
    {
        await Token(client);
        var handle = "recovery_" + Guid.NewGuid().ToString("N")[..12];
        var email = handle + "@example.test";
        using var response = await client.PostAsJsonAsync("/api/v1/auth/register",
            new { handle, email, password = Password, displayName = "Recovery Reader" }, Cancellation);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        return (body.GetProperty("id").GetGuid(), handle, email, body.GetProperty("recoveryCode").GetString()!);
    }
    private static async Task Login(HttpClient client, string email, string password = Password)
    {
        await Token(client);
        using var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password }, Cancellation);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await Token(client);
    }
    private static Task<HttpResponseMessage> Reset(HttpClient client, string handle, string code, string password = NewPassword) =>
        client.PostAsJsonAsync("/api/v1/auth/reset-password", new { handle, recoveryCode = code, password }, Cancellation);
    private static async Task<string> Code(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        return body.GetProperty("recoveryCode").GetString()!;
    }

    [Fact]
    public async Task Registration_issues_code_without_signing_in_or_exposing_it_in_profile_reads()
    {
        await using var factory = Factory();
        using var client = Client(factory);
        var account = await Register(client);
        Assert.Matches("^[A-F0-9]{8}(-[A-F0-9]{8}){3}$", account.Code);
        using var anonymous = await client.GetAsync("/api/v1/users/me", Cancellation);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        await Login(client, account.Email);
        using var profile = await client.GetAsync("/api/v1/users/me", Cancellation);
        var text = await profile.Content.ReadAsStringAsync(Cancellation);
        Assert.DoesNotContain(account.Code, text);
        Assert.False(JsonDocument.Parse(text).RootElement.TryGetProperty("recoveryCode", out _));
        using var status = await client.GetAsync("/api/v1/auth/recovery-status", Cancellation);
        var body = await status.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        Assert.True(body.GetProperty("hasRecoveryCode").GetBoolean());
        Assert.True(body.GetProperty("hasPassword").GetBoolean());
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RateFreakDbContext>();
        var user = await db.Users.AsNoTracking().SingleAsync(u => u.Id == account.Id, Cancellation);
        var verifier = await db.Database.SqlQueryRaw<string>("SELECT verifier AS \"Value\" FROM account_recovery_states WHERE user_id = {0}", account.Id).SingleAsync(Cancellation);
        Assert.Matches("^[A-F0-9]{64}$", verifier);
        Assert.DoesNotContain(account.Code.Replace("-", ""), verifier);
        var stored = JsonSerializer.Serialize(user);
        Assert.DoesNotContain(account.Code, stored);
        Assert.DoesNotContain(account.Code.Replace("-", ""), stored);
    }

    [Fact]
    public async Task Reset_consumes_code_replaces_password_and_revokes_all_sessions()
    {
        await using var factory = Factory();
        using var first = Client(factory);
        var account = await Register(first);
        await Login(first, account.Email);
        using var second = Client(factory);
        await Login(second, account.Email);
        using var recovery = Client(factory);
        await Token(recovery);
        using var reset = await Reset(recovery, account.Handle.ToUpperInvariant(), account.Code.ToLowerInvariant().Replace("-", ""));
        var replacement = await Code(reset);
        Assert.NotEqual(account.Code, replacement);
        foreach (var client in new[] { first, second, recovery })
        {
            using var me = await client.GetAsync("/api/v1/users/me", Cancellation);
            Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
        }
        using var replay = await Reset(recovery, account.Handle, account.Code);
        Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode);
        using var oldLogin = await recovery.PostAsJsonAsync("/api/v1/auth/login", new { email = account.Email, password = Password }, Cancellation);
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);
        await Login(recovery, account.Email, NewPassword);
        await using var scope = factory.Services.CreateAsyncScope();
        var user = await scope.ServiceProvider.GetRequiredService<RateFreakDbContext>().Users.AsNoTracking().SingleAsync(u => u.Id == account.Id, Cancellation);
        Assert.False(user.EmailConfirmed);
        Assert.Equal(account.Handle, user.UserName);
    }

    [Fact]
    public async Task Replacement_requires_password_and_invalidates_previous_code()
    {
        await using var factory = Factory();
        using var client = Client(factory);
        var account = await Register(client);
        await Login(client, account.Email);
        using var wrong = await client.PostAsJsonAsync("/api/v1/auth/recovery-code", new { password = "Wrong-password-72" }, Cancellation);
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
        using var issued = await client.PostAsJsonAsync("/api/v1/auth/recovery-code", new { password = Password }, Cancellation);
        var replacement = await Code(issued);
        using var recovery = Client(factory);
        await Token(recovery);
        using var obsolete = await Reset(recovery, account.Handle, account.Code);
        Assert.Equal(HttpStatusCode.BadRequest, obsolete.StatusCode);
        using var reset = await Reset(recovery, account.Handle, replacement);
        await Code(reset);
    }

    [Fact]
    public async Task Invalid_and_unknown_codes_have_same_error_and_invalid_password_does_not_consume_code()
    {
        await using var factory = Factory();
        using var client = Client(factory);
        var account = await Register(client);
        using var bad = await Reset(client, account.Handle, "AAAAAAAA-AAAAAAAA-AAAAAAAA-AAAAAAAA");
        using var unknown = await Reset(client, "unknown_" + Guid.NewGuid().ToString("N")[..12], account.Code);
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        Assert.Equal(bad.StatusCode, unknown.StatusCode);
        var badJson = await bad.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        var unknownJson = await unknown.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        Assert.Equal(badJson.GetProperty("title").GetString(), unknownJson.GetProperty("title").GetString());
        using var weak = await Reset(client, account.Handle, account.Code, "weak");
        Assert.Equal(HttpStatusCode.BadRequest, weak.StatusCode);
        using var valid = await Reset(client, account.Handle, account.Code);
        await Code(valid);
    }

    [Fact]
    public async Task Concurrent_code_replays_have_exactly_one_success()
    {
        await using var factory = Factory();
        using var first = Client(factory);
        var account = await Register(first);
        using var second = Client(factory);
        await Token(second);
        var responses = await Task.WhenAll(Reset(first, account.Handle, account.Code), Reset(second, account.Handle, account.Code));
        try
        {
            Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
            Assert.Single(responses, r => r.StatusCode == HttpStatusCode.BadRequest);
        }
        finally { foreach (var response in responses) response.Dispose(); }
    }

    [Fact]
    public async Task Recovery_routes_require_csrf_and_authenticated_enrollment()
    {
        await using var factory = Factory();
        using var client = Client(factory);
        using var status = await client.GetAsync("/api/v1/auth/recovery-status", Cancellation);
        Assert.Equal(HttpStatusCode.Unauthorized, status.StatusCode);
        using var issue = await client.PostAsJsonAsync("/api/v1/auth/recovery-code", new { password = Password }, Cancellation);
        Assert.Equal(HttpStatusCode.Unauthorized, issue.StatusCode);
        using var missing = await Reset(client, "some_handle", "AAAAAAAA-AAAAAAAA-AAAAAAAA-AAAAAAAA");
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        var account = await Register(client);
        await Login(client, account.Email);
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        using var noCsrf = await client.PostAsJsonAsync("/api/v1/auth/recovery-code", new { password = Password }, Cancellation);
        Assert.Equal(HttpStatusCode.BadRequest, noCsrf.StatusCode);
    }

    [Fact]
    public async Task Successful_recovery_clears_login_lockout_but_failed_recovery_does_not_change_it()
    {
        await using var factory = Factory();
        using var client = Client(factory);
        var account = await Register(client);
        for (var i = 0; i < 5; i++)
        {
            using var failed = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = account.Email, password = "Wrong-password-72" }, Cancellation);
            Assert.Equal(HttpStatusCode.Unauthorized, failed.StatusCode);
        }
        using var bad = await Reset(client, account.Handle, "AAAAAAAA-AAAAAAAA-AAAAAAAA-AAAAAAAA");
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        using var locked = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = account.Email, password = Password }, Cancellation);
        Assert.Equal(HttpStatusCode.Unauthorized, locked.StatusCode);
        using var reset = await Reset(client, account.Handle, account.Code);
        await Code(reset);
        await Login(client, account.Email, NewPassword);
    }
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Legacy_accounts_enroll_with_password_but_external_only_accounts_cannot(bool hasPassword)
    {
        await using var factory = Factory();
        using var client = Client(factory);
        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AccountUser>>();
        var handle = "legacy_" + Guid.NewGuid().ToString("N")[..12];
        var user = new AccountUser { Id = Guid.NewGuid(), UserName = handle, Email = handle + "@example.test", DisplayName = "Legacy" };
        var created = hasPassword ? await users.CreateAsync(user, Password) : await users.CreateAsync(user);
        Assert.True(created.Succeeded);
        var recovery = scope.ServiceProvider.GetRequiredService<RateFreak.Application.Identity.IAccountRecoveryService>();
        var before = await recovery.StatusAsync(user.Id, Cancellation);
        Assert.NotNull(before);
        Assert.False(before.HasRecoveryCode);
        Assert.Equal(hasPassword, before.HasPassword);
        var result = await recovery.GenerateAsync(user.Id, user.SecurityStamp!, new(Password), Cancellation);
        Assert.Equal(hasPassword ? RateFreak.Application.Identity.RecoveryOutcome.Success : RateFreak.Application.Identity.RecoveryOutcome.Invalid, result.Outcome);
        if (hasPassword)
        {
            await Token(client);
            using var reset = await Reset(client, handle, result.RecoveryCode!);
            await Code(reset);
        }
    }

    [Fact]
    public async Task Revoked_session_cannot_replace_code_even_when_user_was_already_tracked()
    {
        await using var factory = Factory();
        using var client = Client(factory);
        var account = await Register(client);
        await using var staleScope = factory.Services.CreateAsyncScope();
        var users = staleScope.ServiceProvider.GetRequiredService<UserManager<AccountUser>>();
        var tracked = await users.FindByIdAsync(account.Id.ToString());
        var staleStamp = tracked!.SecurityStamp!;
        using var reset = await Reset(client, account.Handle, account.Code);
        var currentCode = await Code(reset);
        var recovery = staleScope.ServiceProvider.GetRequiredService<RateFreak.Application.Identity.IAccountRecoveryService>();
        var denied = await recovery.GenerateAsync(account.Id, staleStamp, new(NewPassword), Cancellation);
        Assert.Equal(RateFreak.Application.Identity.RecoveryOutcome.Unauthenticated, denied.Outcome);
        await Token(client);
        using var valid = await Reset(client, account.Handle, currentCode, Password);
        await Code(valid);
    }

    [Fact]
    public async Task Account_attempt_budget_survives_api_restart_and_does_not_block_password_login()
    {
        (Guid Id, string Handle, string Email, string Code) account;
        await using (var factory = Factory())
        {
            using var client = Client(factory);
            account = await Register(client);
            for (var i = 0; i < 5; i++)
            {
                using var bad = await Reset(client, account.Handle, "AAAAAAAA-AAAAAAAA-AAAAAAAA-AAAAAAAA");
                Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
            }
        }
        await using var restarted = Factory();
        using var fresh = Client(restarted);
        await Token(fresh);
        using var blocked = await Reset(fresh, account.Handle, account.Code);
        Assert.Equal(HttpStatusCode.BadRequest, blocked.StatusCode);
        await Login(fresh, account.Email);
    }

    [Fact]
    public async Task Recovery_has_an_independent_ip_budget_and_rejects_oversized_input()
    {
        await using var factory = Factory();
        using var client = Client(factory);
        await Token(client);
        for (var i = 0; i < 10; i++)
        {
            using var invalid = await Reset(client, new string('x', 31), new string('A', 65), new string('a', 129));
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
            Assert.True(invalid.Headers.CacheControl?.NoStore);
        }
        using var blocked = await Reset(client, "valid_handle", "AAAAAAAA-AAAAAAAA-AAAAAAAA-AAAAAAAA");
        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);
        Assert.NotNull(blocked.Headers.RetryAfter);
    }

    [Fact]
    public async Task Openapi_registration_documents_the_recovery_code()
    {
        await using var factory = new ApiFactory(environment: "Development", connectionString: database.ConnectionString);
        using var client = Client(factory);
        var doc = await client.GetFromJsonAsync<JsonElement>("/openapi/v1.json", Cancellation);
        var schema = doc.GetProperty("paths").GetProperty("/api/v1/auth/register").GetProperty("post")
            .GetProperty("responses").GetProperty("201").GetProperty("content").GetProperty("application/json").GetProperty("schema");
        var name = schema.GetProperty("$ref").GetString()!.Split('/')[^1];
        var properties = doc.GetProperty("components").GetProperty("schemas").GetProperty(name).GetProperty("properties");
        Assert.True(properties.TryGetProperty("recoveryCode", out _));
        Assert.True(properties.TryGetProperty("id", out _));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Recovery_event_persistence_failure_rolls_back_registration_or_reset(bool registration)
    {
        await using var factory = Factory();
        using var client = Client(factory);
        var account = await Register(client);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RateFreakDbContext>();
        // This fixture owns a disposable database; force a real database failure at the audit boundary.
        await db.Database.ExecuteSqlRawAsync("CREATE FUNCTION reject_recovery_event() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'fixture recovery event rejection'; END $$; CREATE TRIGGER reject_recovery_event BEFORE INSERT ON account_recovery_events FOR EACH ROW EXECUTE FUNCTION reject_recovery_event();", Cancellation);
        var handle = "rollback_" + Guid.NewGuid().ToString("N")[..12];
        try
        {
            using var failed = registration
                ? await client.PostAsJsonAsync("/api/v1/auth/register", new { handle, email = handle + "@example.test", password = Password, displayName = "Rollback" }, Cancellation)
                : await Reset(client, account.Handle, account.Code);
            Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
            Assert.DoesNotContain(account.Code, await failed.Content.ReadAsStringAsync(Cancellation));
            if (registration) Assert.False(await db.Users.AnyAsync(u => u.UserName == handle, Cancellation));
        }
        finally
        {
            await db.Database.ExecuteSqlRawAsync("DROP TRIGGER reject_recovery_event ON account_recovery_events; DROP FUNCTION reject_recovery_event();", Cancellation);
        }
        // A rolled-back reset must preserve both the old password and the original usable code.
        await Login(client, account.Email);
        using var recovery = Client(factory); await Token(recovery);
        using var reset = await Reset(recovery, account.Handle, account.Code);
        await Code(reset);
    }

}
