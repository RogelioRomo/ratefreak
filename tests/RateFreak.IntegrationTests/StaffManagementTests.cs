using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace RateFreak.IntegrationTests;

public sealed partial class ModerationTests
{
    [Fact]
    public async Task Admin_directory_requires_current_admin_role_and_returns_only_public_identity_and_roles()
    {
        await using var f = Factory(); using var admin = Client(f); using var mod = Client(f); using var anon = Client(f);
        var adminId = await Login(admin); var modId = await Login(mod);
        const string path = "/api/v1/admin/accounts";
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync(path, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await mod.GetAsync(path, Ct)).StatusCode);
        await Role(modId, "Moderator");
        Assert.Equal(HttpStatusCode.Forbidden, (await mod.GetAsync(path, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await mod.GetAsync("/api/v1/moderation/reports", Ct)).StatusCode);
        await Role(adminId, "Admin");
        var response = await admin.GetAsync(path, Ct); var page = await Json(response);
        Assert.True(response.Headers.CacheControl?.NoStore);
        var row = page.GetProperty("items").EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == adminId);
        Assert.Equal(new[] { "displayName", "handle", "id", "isAdmin", "isModerator" }, row.EnumerateObject().Select(p => p.Name).Order().ToArray());
        Assert.True(row.GetProperty("isAdmin").GetBoolean()); Assert.False(row.GetProperty("isModerator").GetBoolean());
        await using var db = Db(); await db.UserRoles.Where(x => x.UserId == adminId).ExecuteDeleteAsync(Ct);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.GetAsync(path, Ct)).StatusCode);
    }

    [Fact]
    public async Task Admin_directory_search_is_literal_paginated_and_reflects_role_changes()
    {
        await using var f = Factory(); using var admin = Client(f); using var user = Client(f);
        var adminId = await Login(admin); var userId = await Login(user); await Role(adminId, "Admin");
        var tag = Guid.NewGuid().ToString("N");
        await using (var db = Db())
        {
            var a = await db.Users.SingleAsync(x => x.Id == adminId, Ct); a.DisplayName = tag + " Alpha";
            var u = await db.Users.SingleAsync(x => x.Id == userId, Ct); u.DisplayName = tag + " beta_100%";
            await db.SaveChangesAsync(Ct);
        }
        var basePath = $"/api/v1/admin/accounts?q={tag}&staffOnly=false&pageSize=1";
        var first = await Json(await admin.GetAsync(basePath, Ct)); var second = await Json(await admin.GetAsync(basePath + "&page=2", Ct));
        Assert.Equal(2, first.GetProperty("totalCount").GetInt32());
        Assert.NotEqual(first.GetProperty("items")[0].GetProperty("id").GetGuid(), second.GetProperty("items")[0].GetProperty("id").GetGuid());
        var literal = await Json(await admin.GetAsync("/api/v1/admin/accounts?staffOnly=false&q=" + Uri.EscapeDataString(tag + " beta_100%"), Ct));
        Assert.Equal(userId, literal.GetProperty("items")[0].GetProperty("id").GetGuid());
        var absent = await Json(await admin.GetAsync($"/api/v1/admin/accounts?staffOnly=false&q={tag}%25_", Ct));
        Assert.Equal(0, absent.GetProperty("totalCount").GetInt32());
        var staff = await Json(await admin.GetAsync($"/api/v1/admin/accounts?q={tag}", Ct)); Assert.Equal(1, staff.GetProperty("totalCount").GetInt32());
        var rolePath = $"/api/v1/admin/moderators/{userId}";
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsJsonAsync(rolePath, new { reason = "Directory appointment" }, Ct)).StatusCode);
        staff = await Json(await admin.GetAsync($"/api/v1/admin/accounts?q={tag}", Ct)); Assert.Equal(2, staff.GetProperty("totalCount").GetInt32());
        using var delete = new HttpRequestMessage(HttpMethod.Delete, rolePath) { Content = JsonContent.Create(new { reason = "Directory revocation" }) };
        Assert.Equal(HttpStatusCode.NoContent, (await admin.SendAsync(delete, Ct)).StatusCode);
        staff = await Json(await admin.GetAsync($"/api/v1/admin/accounts?q={tag}", Ct)); Assert.Equal(1, staff.GetProperty("totalCount").GetInt32());
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("pageSize=101")]
    [InlineData("staffOnly=all")]
    [InlineData("q=a&q=b")]
    [InlineData("unknown=value")]
    public async Task Admin_directory_rejects_invalid_filters(string query)
    {
        await using var f = Factory(); using var admin = Client(f); await Role(await Login(admin), "Admin");
        var response = await admin.GetAsync("/api/v1/admin/accounts?" + query, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode); Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/v1/admin/accounts?q=" + new string('a', 101), Ct)).StatusCode);
    }
}
