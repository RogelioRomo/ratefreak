using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RateFreak.Application.Moderation;
using RateFreak.Infrastructure.Persistence;

namespace RateFreak.IntegrationTests;

public sealed class AdminRevocationTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static HttpClient Client(ApiFactory f) => f.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new("https://localhost"), AllowAutoRedirect = false });
    private static async Task<Guid> Login(HttpClient client)
    {
        var handle = "revoke_" + Guid.NewGuid().ToString("N")[..12];
        var token = await client.GetFromJsonAsync<JsonElement>("/api/v1/auth/csrf", Ct); client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token.GetProperty("requestToken").GetString());
        var registered = await client.PostAsJsonAsync("/api/v1/auth/register", new { handle, email = handle + "@example.test", displayName = "Admin candidate", password = "Local-password-72" }, Ct); Assert.Equal(HttpStatusCode.Created, registered.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/v1/auth/login", new { email = handle + "@example.test", password = "Local-password-72" }, Ct)).StatusCode);
        token = await client.GetFromJsonAsync<JsonElement>("/api/v1/auth/csrf", Ct); client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN"); client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token.GetProperty("requestToken").GetString());
        return (await registered.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();
    }
    private static async Task<(int ExitCode, string Output)> Command(string connection, params string[] args)
    {
        var start = new ProcessStartInfo("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        start.ArgumentList.Add(typeof(Program).Assembly.Location); foreach (var arg in args) start.ArgumentList.Add(arg);
        start.Environment["ConnectionStrings__Default"] = connection; start.Environment["ASPNETCORE_ENVIRONMENT"] = "Production"; start.Environment["DOTNET_ENVIRONMENT"] = "Production";
        using var process = Process.Start(start)!; var output = process.StandardOutput.ReadToEndAsync(Ct); var error = process.StandardError.ReadToEndAsync(Ct);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Ct); timeout.CancelAfter(TimeSpan.FromSeconds(10));
        try { await process.WaitForExitAsync(timeout.Token); } catch (OperationCanceledException) { process.Kill(entireProcessTree: true); Assert.Fail("Admin command did not exit without starting HTTP."); }
        var text = (await output) + (await error); Assert.DoesNotContain("Now listening", text); Assert.DoesNotContain(connection, text);
        return (process.ExitCode, text);
    }

    [Fact]
    public async Task Revoke_command_is_audited_idempotent_preserves_moderator_and_denies_existing_admin_session()
    {
        await using var database = new AccountDatabase(); await database.InitializeAsync();
        await using var f = new ApiFactory(connectionString: database.ConnectionString); using var user = Client(f); using var other = Client(f);
        var id = await Login(user); var otherId = await Login(other);
        await using var scope = f.Services.CreateAsyncScope(); var service = scope.ServiceProvider.GetRequiredService<IModerationService>();
        Assert.Equal(ModerationStatus.Success, (await service.AppointAdminAsync(id, "Initial", Ct)).Status); Assert.Equal(ModerationStatus.Success, (await service.AppointAdminAsync(otherId, "Recovery administrator", Ct)).Status);
        Assert.Equal(ModerationStatus.Success, (await service.ModeratorAsync(otherId, id, true, "Retain moderator", Ct)).Status);
        var db = scope.ServiceProvider.GetRequiredService<RateFreakDbContext>(); var stamp = (await db.Users.AsNoTracking().SingleAsync(u => u.Id == id, Ct)).SecurityStamp;
        Assert.Equal(HttpStatusCode.OK, (await user.GetAsync("/api/v1/admin/accounts", Ct)).StatusCode);
        var result = await Command(database.ConnectionString, "--revoke-admin", id.ToString(), "--reason", "Operator removal"); Assert.Equal(0, result.ExitCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await user.GetAsync("/api/v1/admin/accounts", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await user.GetAsync("/api/v1/moderation/reports", Ct)).StatusCode);
        Assert.Equal(stamp, (await db.Users.AsNoTracking().SingleAsync(u => u.Id == id, Ct)).SecurityStamp);
        var entry = await db.ModerationEvents.AsNoTracking().SingleAsync(e => e.TargetId == id && e.Action == "revoke_admin", Ct);
        Assert.Equal("operator", entry.ActorKind); Assert.Null(entry.ActorId); Assert.True(entry.Before); Assert.False(entry.After); Assert.Equal("Operator removal", entry.Reason);
        Assert.Equal(0, (await Command(database.ConnectionString, "--revoke-admin", id.ToString(), "--reason", "Repeat")).ExitCode);
        Assert.Equal(1, await db.ModerationEvents.CountAsync(e => e.TargetId == id && e.Action == "revoke_admin", Ct));
        Assert.Equal(2, (await Command(database.ConnectionString, "--revoke-admin", otherId.ToString(), "--reason", "Last administrator")).ExitCode);
        Assert.True(await service.HasAccessAsync(otherId, true, Ct));
        Assert.False(await db.ModerationEvents.AnyAsync(e => e.TargetId == otherId && e.Action == "revoke_admin", Ct));
    }

    [Fact]
    public async Task Concurrent_revocations_leave_one_admin_and_reject_invalid_reasons_or_missing_accounts()
    {
        await using var database = new AccountDatabase(); await database.InitializeAsync();
        await using var f = new ApiFactory(connectionString: database.ConnectionString); using var a = Client(f); using var b = Client(f);
        var first = await Login(a); var second = await Login(b);
        await using var scope = f.Services.CreateAsyncScope(); var service = scope.ServiceProvider.GetRequiredService<IModerationService>();
        await service.AppointAdminAsync(first, "Initial", Ct); await service.AppointAdminAsync(second, "Second", Ct);
        Assert.Equal(ModerationStatus.Invalid, (await service.RevokeAdminAsync(first, " ", Ct)).Status);
        Assert.Equal(ModerationStatus.Invalid, (await service.RevokeAdminAsync(first, new string('a', 1001), Ct)).Status);
        Assert.Equal(ModerationStatus.NotFound, (await service.RevokeAdminAsync(Guid.NewGuid(), "Missing", Ct)).Status);
        async Task<ModerationStatus> Revoke(Guid id) { await using var s = f.Services.CreateAsyncScope(); return (await s.ServiceProvider.GetRequiredService<IModerationService>().RevokeAdminAsync(id, "Concurrent removal", Ct)).Status; }
        var results = await Task.WhenAll(Revoke(first), Revoke(second)); Assert.Single(results, s => s == ModerationStatus.Success); Assert.Single(results, s => s == ModerationStatus.Conflict);
        var removed = results[0] == ModerationStatus.Success ? a : b;
        Assert.Equal(HttpStatusCode.Forbidden, (await removed.GetAsync("/api/v1/moderation/reports", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await removed.GetAsync("/api/v1/users/me", Ct)).StatusCode);
        var db = scope.ServiceProvider.GetRequiredService<RateFreakDbContext>();
        Assert.Equal(1, await (from m in db.UserRoles join r in db.Roles on m.RoleId equals r.Id where r.NormalizedName == "ADMIN" select m).CountAsync(Ct));
        Assert.Equal(1, await db.ModerationEvents.CountAsync(e => e.Action == "revoke_admin", Ct));
    }

    [Fact]
    public async Task Admin_http_removal_requires_current_admin_csrf_reason_and_protects_last_admin_including_self_removal()
    {
        await using var database = new AccountDatabase(); await database.InitializeAsync();
        await using var f = new ApiFactory(connectionString: database.ConnectionString); using var a = Client(f); using var b = Client(f); using var mod = Client(f); using var anon = Client(f);
        var first = await Login(a); var second = await Login(b); var moderator = await Login(mod);
        await using var scope = f.Services.CreateAsyncScope(); var service = scope.ServiceProvider.GetRequiredService<IModerationService>();
        await service.AppointAdminAsync(first, "Initial", Ct); await service.AppointAdminAsync(second, "Second", Ct); await service.ModeratorAsync(first, moderator, true, "Moderator", Ct);
        async Task<HttpResponseMessage> Remove(HttpClient c, Guid id, string reason = "Role removal")
        {
            using var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/admin/administrators/{id}") { Content = JsonContent.Create(new { reason }) };
            return await c.SendAsync(request, Ct);
        }
        Assert.Equal(HttpStatusCode.Unauthorized, (await Remove(anon, second)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Remove(mod, second)).StatusCode);
        a.DefaultRequestHeaders.Remove("X-CSRF-TOKEN"); Assert.Equal(HttpStatusCode.BadRequest, (await Remove(a, second)).StatusCode);
        var csrf = await a.GetFromJsonAsync<JsonElement>("/api/v1/auth/csrf", Ct); a.DefaultRequestHeaders.Add("X-CSRF-TOKEN", csrf.GetProperty("requestToken").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, (await Remove(a, second, " ")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Remove(a, Guid.NewGuid())).StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await a.PutAsJsonAsync($"/api/v1/admin/administrators/{second}", new { reason = "No HTTP grants" }, Ct)).StatusCode);
        var response = await Remove(a, second); Assert.Equal(HttpStatusCode.NoContent, response.StatusCode); Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.Equal(HttpStatusCode.Forbidden, (await b.GetAsync("/api/v1/admin/accounts", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await b.GetAsync("/api/v1/users/me", Ct)).StatusCode);
        var db = scope.ServiceProvider.GetRequiredService<RateFreakDbContext>(); var entry = await db.ModerationEvents.AsNoTracking().SingleAsync(e => e.TargetId == second && e.Action == "revoke_admin", Ct);
        Assert.Equal(first, entry.ActorId); Assert.Equal("account", entry.ActorKind);
        Assert.Equal(HttpStatusCode.Conflict, (await Remove(a, first)).StatusCode);
        // Each real operator invocation/request has a fresh scope. The original
        // setup scope still tracks memberships removed by the intervening HTTP call.
        await using var recoveryScope = f.Services.CreateAsyncScope(); var recovery = recoveryScope.ServiceProvider.GetRequiredService<IModerationService>();
        await recovery.AppointAdminAsync(second, "Recovery", Ct); await recovery.ModeratorAsync(second, first, true, "Keep moderator", Ct);
        Assert.Equal(HttpStatusCode.NoContent, (await Remove(a, first)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await a.GetAsync("/api/v1/admin/accounts", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await a.GetAsync("/api/v1/moderation/reports", Ct)).StatusCode);
        Assert.Equal(ModerationStatus.Forbidden, (await service.RevokeAdminAsync(first, second, "Stale actor", Ct)).Status);
        Assert.True(await service.HasAccessAsync(second, true, Ct));
    }

    [Theory]
    [InlineData("bad", "--reason", "Reason")]
    [InlineData("00000000-0000-0000-0000-000000000000", "--reason", "Reason")]
    [InlineData("11111111-1111-1111-1111-111111111111", "--reason", " ")]
    [InlineData("11111111-1111-1111-1111-111111111111", "--reason", "Reason", "--appoint-admin")]
    [InlineData("11111111-1111-1111-1111-111111111111")]
    public async Task Invalid_revocation_arguments_exit_without_http_or_database_access(params string[] args)
    {
        Assert.Equal(2, (await Command("Host=127.0.0.1;Port=1;Database=unused;Username=unused", ["--revoke-admin", ..args])).ExitCode);
    }
}
