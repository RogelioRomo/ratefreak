using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using RateFreak.Domain.Catalog;
using RateFreak.Infrastructure.Persistence;

namespace RateFreak.IntegrationTests;

public sealed partial class ModerationTests(AccountDatabase database) : IClassFixture<AccountDatabase>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private RateFreakDbContext Db() => new(new DbContextOptionsBuilder<RateFreakDbContext>().UseNpgsql(database.ConnectionString).Options);
    private ApiFactory Factory() => new(connectionString: database.ConnectionString);
    private static HttpClient Client(ApiFactory f) => f.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
    private static async Task Token(HttpClient c) { var j = await c.GetFromJsonAsync<JsonElement>("/api/v1/auth/csrf", Ct); c.DefaultRequestHeaders.Remove("X-CSRF-TOKEN"); c.DefaultRequestHeaders.Add("X-CSRF-TOKEN", j.GetProperty("requestToken").GetString()); }
    private static async Task<Guid> Login(HttpClient c)
    {
        var tag = Guid.NewGuid().ToString("N")[..12]; var email = tag + "@example.test"; await Token(c);
        var r = await c.PostAsJsonAsync("/api/v1/auth/register", new { email, password = "Local-password-72", handle = "m_" + tag, displayName = "Reader" }, Ct);
        var j = await Json(r, HttpStatusCode.Created); Assert.Equal(HttpStatusCode.OK, (await c.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "Local-password-72" }, Ct)).StatusCode); await Token(c); return j.GetProperty("id").GetGuid();
    }
    private async Task Role(Guid user, string name)
    {
        await using var db = Db(); var role = await db.Roles.SingleOrDefaultAsync(x => x.Name == name, Ct);
        if (role is null) { role = new IdentityRole<Guid>(name) { Id = Guid.NewGuid(), NormalizedName = name.ToUpperInvariant() }; db.Roles.Add(role); await db.SaveChangesAsync(Ct); }
        db.UserRoles.Add(new IdentityUserRole<Guid> { UserId = user, RoleId = role.Id }); await db.SaveChangesAsync(Ct);
    }
    private async Task<(Guid Media, JsonElement Review)> Review(HttpClient c)
    {
        await using var db = Db(); var media = new MediaItem("movie", "Moderation test"); db.Add(media); await db.SaveChangesAsync(Ct);
        return (media.Id, await Json(await c.PostAsJsonAsync($"/api/v1/media/{media.Id}/reviews", new { body = "Review text" }, Ct), HttpStatusCode.Created));
    }
    private static async Task<JsonElement> Json(HttpResponseMessage r, HttpStatusCode expected = HttpStatusCode.OK) { Assert.Equal(expected, r.StatusCode); return await r.Content.ReadFromJsonAsync<JsonElement>(Ct); }
    private static string Reports(Guid id) => $"/api/v1/reviews/{id}/reports";
    private static string Visibility(Guid id) => $"/api/v1/moderation/reviews/{id}/visibility";

    [Fact]
    public async Task Reports_require_session_csrf_and_valid_public_nonowned_target_and_are_unique()
    {
        await using var f = Factory(); using var author = Client(f); using var reporter = Client(f); using var anon = Client(f);
        await Login(author); await Login(reporter); var (_, review) = await Review(author); var id = review.GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.PostAsJsonAsync(Reports(id), new { reason = "spam" }, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await author.PostAsJsonAsync(Reports(id), new { reason = "spam" }, Ct)).StatusCode);
        reporter.DefaultRequestHeaders.Remove("X-CSRF-TOKEN"); Assert.Equal(HttpStatusCode.BadRequest, (await reporter.PostAsJsonAsync(Reports(id), new { reason = "spam" }, Ct)).StatusCode); await Token(reporter);
        Assert.Equal(HttpStatusCode.BadRequest, (await reporter.PostAsJsonAsync(Reports(id), new { reason = "other" }, Ct)).StatusCode);
        var response = await reporter.PostAsJsonAsync(Reports(id), new { reason = "spam", details = "  unwanted  " }, Ct);
        var report = await Json(response, HttpStatusCode.Created); Assert.True(response.Headers.CacheControl?.NoStore); Assert.Equal("open", report.GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.Conflict, (await reporter.PostAsJsonAsync(Reports(id), new { reason = "harassment" }, Ct)).StatusCode);
    }

    [Fact]
    public async Task Staff_permissions_use_current_roles_and_admin_can_grant_and_revoke_moderators()
    {
        await using var f = Factory(); using var admin = Client(f); using var user = Client(f); using var anon = Client(f);
        var adminId = await Login(admin); var userId = await Login(user); await Role(adminId, "Admin");
        const string queue = "/api/v1/moderation/reports";
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync(queue, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await user.GetAsync(queue, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync(queue, Ct)).StatusCode);
        var path = $"/api/v1/admin/moderators/{userId}";
        Assert.Equal(HttpStatusCode.Forbidden, (await user.PutAsJsonAsync(path, new { reason = "elevate" }, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsJsonAsync(path, new { reason = "Appointed" }, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await user.GetAsync(queue, Ct)).StatusCode);
        using var req = new HttpRequestMessage(HttpMethod.Delete, path) { Content = JsonContent.Create(new { reason = "Revoked" }) };
        Assert.Equal(HttpStatusCode.NoContent, (await admin.SendAsync(req, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await user.GetAsync(queue, Ct)).StatusCode);
    }

    [Fact]
    public async Task Hide_resolve_and_restore_are_audited_and_stale_decisions_are_rejected()
    {
        await using var f = Factory(); using var author = Client(f); using var reporter = Client(f); using var mod = Client(f);
        await Login(author); await Login(reporter); await Role(await Login(mod), "Moderator"); var (_, review) = await Review(author); var id = review.GetProperty("id").GetGuid();
        var report = await Json(await reporter.PostAsJsonAsync(Reports(id), new { reason = "spam" }, Ct), HttpStatusCode.Created); var reportId = report.GetProperty("id").GetGuid();
        var path = $"/api/v1/moderation/reports/{reportId}/resolve";
        Assert.Equal(HttpStatusCode.Conflict, (await mod.PostAsJsonAsync(path, new { decision = "hide", reason = "Spam", expectedTargetUpdatedAt = DateTimeOffset.UnixEpoch }, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await mod.PostAsJsonAsync(path, new { decision = "hide", reason = "Spam", expectedTargetUpdatedAt = review.GetProperty("updatedAt").GetDateTimeOffset() }, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await reporter.GetAsync($"/api/v1/reviews/{id}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await mod.PostAsJsonAsync(path, new { decision = "dismiss", reason = "repeat", expectedTargetUpdatedAt = (DateTimeOffset?)null }, Ct)).StatusCode);
        var target = await Json(await mod.GetAsync($"/api/v1/moderation/reviews/{id}", Ct)); Assert.True(target.GetProperty("hidden").GetBoolean());
        Assert.Equal(HttpStatusCode.NoContent, (await mod.PostAsJsonAsync(Visibility(id), new { hidden = false, reason = "Corrected", expectedTargetUpdatedAt = target.GetProperty("updatedAt").GetDateTimeOffset() }, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await reporter.GetAsync($"/api/v1/reviews/{id}", Ct)).StatusCode);
        var audit = await Json(await mod.GetAsync($"/api/v1/moderation/audit?targetType=review&targetId={id}", Ct)); Assert.Equal(2, audit.GetProperty("totalCount").GetInt32());
    }
}
