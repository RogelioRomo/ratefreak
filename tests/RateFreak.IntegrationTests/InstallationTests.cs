using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RateFreak.Infrastructure.Authentication;
using RateFreak.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace RateFreak.IntegrationTests;

[Trait("Category", "Database")]
public sealed class InstallationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer pg = new PostgreSqlBuilder("postgres:17").Build();
    private const string Secret = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private const string Password = "Initial-admin-password-72";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    public async ValueTask InitializeAsync() { await pg.StartAsync(Ct); await using var db = Db(); await db.Database.MigrateAsync(Ct); }
    public async ValueTask DisposeAsync() => await pg.DisposeAsync();
    private RateFreakDbContext Db() => new(new DbContextOptionsBuilder<RateFreakDbContext>().UseNpgsql(pg.GetConnectionString()).Options);
    private ApiFactory Factory(string? secret = Secret) => new(connectionString: pg.GetConnectionString(), setupSecret: secret);
    private static HttpClient Client(ApiFactory f) => f.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
    private static async Task Token(HttpClient c)
    {
        var json = await c.GetFromJsonAsync<JsonElement>("/api/v1/auth/csrf", Ct);
        c.DefaultRequestHeaders.Remove("X-CSRF-TOKEN"); c.DefaultRequestHeaders.Add("X-CSRF-TOKEN", json.GetProperty("requestToken").GetString());
    }
    private static object Input(string secret = Secret, string handle = "initial_admin") => new { setupSecret = secret, handle, email = handle + "@example.test", displayName = "First Admin", password = Password };
    private static Task<HttpResponseMessage> Setup(HttpClient c, string secret = Secret, string handle = "initial_admin") => c.PostAsJsonAsync("/api/v1/setup", Input(secret, handle), Ct);
    private static async Task<JsonElement> Status(HttpClient c)
    {
        using var response = await c.GetAsync("/api/v1/setup/status", Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); Assert.True(response.Headers.CacheControl?.NoStore);
        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }

    [Fact]
    public async Task Fresh_install_blocks_business_apis_but_allows_csrf_and_health()
    {
        await using var f = Factory(); using var c = Client(f);
        var status = await Status(c); Assert.True(status.GetProperty("required").GetBoolean()); Assert.True(status.GetProperty("configured").GetBoolean());
        foreach (var path in new[] { "/api/v1/media", "/api/v1/users/me", "/api/v1/auth/providers", "/api/v1/auth/external/google/callback", "/API/v1/AUTH/EXTERNAL/github/CALLBACK" })
        {
            using var response = await c.GetAsync(path, Ct); Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode); Assert.True(response.Headers.CacheControl?.NoStore);
        }
        await Token(c);
        using var registration = await c.PostAsJsonAsync("/api/v1/auth/register", Input(), Ct); Assert.Equal(HttpStatusCode.ServiceUnavailable, registration.StatusCode);
        using var live = await c.GetAsync("/health/live", Ct); Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        using var ready = await c.GetAsync("/health/ready", Ct); Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
    }

    [Fact]
    public async Task Setup_atomically_creates_admin_recovery_and_audit_then_normal_registration_is_unprivileged()
    {
        await using var f = Factory(); using var c = Client(f); await Token(c);
        using var setup = await Setup(c); Assert.Equal(HttpStatusCode.Created, setup.StatusCode); Assert.True(setup.Headers.CacheControl?.NoStore);
        var account = await setup.Content.ReadFromJsonAsync<JsonElement>(Ct); var id = account.GetProperty("id").GetGuid();
        Assert.DoesNotContain(Secret,account.GetRawText()); Assert.DoesNotContain(Password,account.GetRawText());
        Assert.Matches("^[A-F0-9]{8}(-[A-F0-9]{8}){3}$", account.GetProperty("recoveryCode").GetString()!);
        using var anonymous = await c.GetAsync("/api/v1/users/me", Ct); Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.False((await Status(c)).GetProperty("required").GetBoolean());
        await using var db = Db();
        Assert.True(await (from ur in db.UserRoles join r in db.Roles on ur.RoleId equals r.Id where ur.UserId == id && r.NormalizedName == "ADMIN" select ur).AnyAsync(Ct));
        Assert.Equal(1, await db.Database.SqlQueryRaw<int>("SELECT count(*)::int AS \"Value\" FROM account_recovery_states").SingleAsync(Ct));
        Assert.True(await db.ModerationEvents.AnyAsync(e => e.TargetId == id && e.Action == "grant_admin", Ct));
        using var login = await c.PostAsJsonAsync("/api/v1/auth/login", new { email = "initial_admin@example.test", password = Password }, Ct); Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        using var staff = await c.GetAsync("/api/v1/moderation/reports", Ct); Assert.Equal(HttpStatusCode.OK, staff.StatusCode);
        using var normal = Client(f); await Token(normal);
        using var registration = await normal.PostAsJsonAsync("/api/v1/auth/register", Input(handle: "ordinary_user"), Ct); Assert.Equal(HttpStatusCode.Created, registration.StatusCode);
        var normalBody = await registration.Content.ReadFromJsonAsync<JsonElement>(Ct); var normalId = normalBody.GetProperty("id").GetGuid();
        Assert.False(await db.UserRoles.AnyAsync(r => r.UserId == normalId, Ct));
        using var repeat = await Setup(normal, handle: "second_admin"); Assert.Equal(HttpStatusCode.Conflict, repeat.StatusCode);
        Assert.Equal(2, await db.Users.CountAsync(Ct));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("weak")]
    public async Task Missing_or_malformed_secret_keeps_installation_closed(string? secret)
    {
        await using var f = Factory(secret); using var c = Client(f);
        var status = await Status(c); Assert.True(status.GetProperty("required").GetBoolean()); Assert.False(status.GetProperty("configured").GetBoolean());
        await Token(c); using var setup = await Setup(c); Assert.Equal(HttpStatusCode.ServiceUnavailable, setup.StatusCode);
        await using var db = Db(); Assert.Empty(await db.Users.ToListAsync(Ct));
    }

    [Fact]
    public async Task Wrong_secret_and_invalid_input_create_no_accounts_or_completion()
    {
        await using var f = Factory(); using var c = Client(f); await Token(c);
        using var invalid = await Setup(c, new string('f',64)); Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        using var malformed = await c.PostAsJsonAsync("/api/v1/setup", new { setupSecret = Secret, handle = "bad handle", password = "weak" }, Ct); Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);
        Assert.DoesNotContain(Secret, await malformed.Content.ReadAsStringAsync(Ct));
        Assert.True((await Status(c)).GetProperty("required").GetBoolean()); await using var db = Db(); Assert.Empty(await db.Users.ToListAsync(Ct));
    }

    [Fact]
    public async Task Setup_requires_csrf_and_has_an_independent_ip_budget()
    {
        await using var f = Factory(); using var c = Client(f);
        using var missing = await Setup(c); Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        await Token(c);
        for (var i = 0; i < 4; i++) { using var wrong = await Setup(c, new string('f',64)); Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode); }
        using var blocked = await Setup(c); Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode); Assert.True(blocked.Headers.CacheControl?.NoStore); Assert.NotNull(blocked.Headers.RetryAfter);
    }

    [Fact]
    public async Task Concurrent_setup_has_exactly_one_admin_and_one_account()
    {
        await using var f = Factory(); using var a = Client(f); using var b = Client(f); await Token(a); await Token(b);
        var responses = await Task.WhenAll(Setup(a, handle: "first_candidate"), Setup(b, handle: "second_candidate"));
        try { Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created); Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict); }
        finally { foreach (var r in responses) r.Dispose(); }
        await using var db = Db(); Assert.Equal(1, await db.Users.CountAsync(Ct)); Assert.Equal(1, await db.UserRoles.CountAsync(Ct));
    }

    [Fact]
    public async Task Completion_survives_admin_deletion_restart_and_secret_removal()
    {
        await using (var f = Factory()) { using var c = Client(f); await Token(c); using var response = await Setup(c); Assert.Equal(HttpStatusCode.Created,response.StatusCode); }
        await using (var db = Db()) { await db.Users.ExecuteDeleteAsync(Ct); }
        await using var restarted = Factory(null); using var fresh = Client(restarted);
        Assert.False((await Status(fresh)).GetProperty("required").GetBoolean()); await Token(fresh);
        using var denied = await Setup(fresh); Assert.Equal(HttpStatusCode.Conflict, denied.StatusCode);
    }

    [Fact]
    public async Task Missing_singleton_fails_closed_without_recreating_setup()
    {
        await using var db = Db(); await db.Database.ExecuteSqlRawAsync("DELETE FROM installation_state", Ct);
        await using var f = Factory(); using var c = Client(f);
        using var status = await c.GetAsync("/api/v1/setup/status",Ct); Assert.Equal(HttpStatusCode.ServiceUnavailable,status.StatusCode);
        using var catalog = await c.GetAsync("/api/v1/media",Ct); Assert.Equal(HttpStatusCode.ServiceUnavailable,catalog.StatusCode);
        await Token(c); using var setup = await Setup(c); Assert.Equal(HttpStatusCode.ServiceUnavailable,setup.StatusCode);
    }

    [Fact]
    public async Task Existing_accounts_migrate_to_completed_without_promotion()
    {
        await using var db = Db(); var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20261106222423_AddAccountRecovery",Ct);
        var id=Guid.NewGuid(); db.Users.Add(new AccountUser { Id=id, UserName="legacy_user", NormalizedUserName="LEGACY_USER", Email="legacy@example.test", NormalizedEmail="LEGACY@EXAMPLE.TEST", DisplayName="Legacy" }); await db.SaveChangesAsync(Ct);
        await db.Database.MigrateAsync(Ct);
        await using var f = Factory(); using var c = Client(f); Assert.False((await Status(c)).GetProperty("required").GetBoolean());
        Assert.Empty(await db.UserRoles.ToListAsync(Ct)); Assert.Equal(id, (await db.Users.SingleAsync(Ct)).Id);
    }

    [Fact]
    public async Task Failure_during_admin_audit_rolls_back_account_recovery_role_and_completion()
    {
        await using var f = Factory(); using var c = Client(f); await Token(c); await using var db=Db();
        await db.Database.ExecuteSqlRawAsync("CREATE FUNCTION reject_setup_audit() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'fixture audit rejection'; END $$; CREATE TRIGGER reject_setup_audit BEFORE INSERT ON moderation_events FOR EACH ROW EXECUTE FUNCTION reject_setup_audit();",Ct);
        using var failed = await Setup(c); Assert.Equal(HttpStatusCode.InternalServerError,failed.StatusCode);
        Assert.Empty(await db.Users.ToListAsync(Ct)); Assert.Empty(await db.UserRoles.ToListAsync(Ct)); Assert.Empty(await db.Roles.ToListAsync(Ct));
        Assert.Equal(0,await db.Database.SqlQueryRaw<int>("SELECT count(*)::int AS \"Value\" FROM account_recovery_states").SingleAsync(Ct));
        Assert.True((await Status(c)).GetProperty("required").GetBoolean());
    }
}
