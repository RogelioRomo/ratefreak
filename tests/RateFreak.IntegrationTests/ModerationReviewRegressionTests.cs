using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;

namespace RateFreak.IntegrationTests;

public sealed partial class ModerationTests
{
    [Fact]
    public async Task Trailing_slash_reporting_routes_keep_no_store_on_success_and_errors()
    {
        await using var f = Factory(); using var author = Client(f); using var reporter = Client(f); using var anon = Client(f); await Login(author); await Login(reporter); var (_, r) = await Review(author); var id = r.GetProperty("id").GetGuid();
        var c = await Json(await author.PostAsJsonAsync($"/api/v1/reviews/{id}/comments", new { body = "Child" }, Ct), HttpStatusCode.Created); var cid = c.GetProperty("id").GetGuid();
        foreach (var path in new[] { Reports(id) + "/", $"/api/v1/comments/{cid}/reports/" })
        {
            var denied = await anon.PostAsJsonAsync(path, new { reason = "spam" }, Ct); Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode); Assert.True(denied.Headers.CacheControl?.NoStore);
            var success = await reporter.PostAsJsonAsync(path, new { reason = "spam" }, Ct); Assert.Equal(HttpStatusCode.Created, success.StatusCode); Assert.True(success.Headers.CacheControl?.NoStore);
        }
    }
    [Fact]
    public async Task Deleted_report_target_does_not_reconnect_to_a_reused_historical_uuid()
    {
        await using var f = Factory(); using var author = Client(f); using var reporter = Client(f); using var mod = Client(f); await Login(author); await Login(reporter); await Role(await Login(mod), "Moderator"); var (_, r) = await Review(author); var id = r.GetProperty("id").GetGuid();
        var report = await Json(await reporter.PostAsJsonAsync(Reports(id), new { reason = "spam" }, Ct), HttpStatusCode.Created); var rid = report.GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.NoContent, (await author.DeleteAsync($"/api/v1/reviews/{id}", Ct)).StatusCode);
        var (_, replacement) = await Review(author); var replacementId = replacement.GetProperty("id").GetGuid();
        await using (var db = Db()) { await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE reviews SET id={id} WHERE id={replacementId}", Ct); }
        var detail = await Json(await mod.GetAsync($"/api/v1/moderation/reports/{rid}", Ct)); Assert.Equal(System.Text.Json.JsonValueKind.Null, detail.GetProperty("target").ValueKind);
        Assert.Equal(HttpStatusCode.NoContent, (await mod.PostAsJsonAsync($"/api/v1/moderation/reports/{rid}/resolve", new { decision = "dismiss", reason = "Original deleted", expectedTargetUpdatedAt = (DateTimeOffset?)null }, Ct)).StatusCode);
    }
}
