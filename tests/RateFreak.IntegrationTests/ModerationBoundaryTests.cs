using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace RateFreak.IntegrationTests;

public sealed partial class ModerationTests
{
    [Theory]
    [InlineData("{\"reason\":\"spam\",\"reason\":\"hate\"}")]
    [InlineData("{\"reason\":\"spam\",\"actorId\":\"spoof\"}")]
    [InlineData("{\"reason\":\"other\",\"details\":\"\\uD800\"}")]
    [InlineData("{\"reason\":\"other\",\"details\":\"bad\\u0000\"}")]
    [InlineData("{\"reason\":\"unknown\"}")]
    [InlineData("{\"reason\":null}")]
    public async Task Strict_report_payloads_reject_ambiguous_or_invalid_content(string payload)
    {
        await using var f = Factory(); using var owner = Client(f); using var reporter = Client(f); await Login(owner); await Login(reporter); var (_, r) = await Review(owner); var id = r.GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.BadRequest, (await reporter.PostAsync(Reports(id), new StringContent(payload, Encoding.UTF8, "application/json"), Ct)).StatusCode);
        await using var db = Db(); Assert.False(await db.ContentReports.AnyAsync(x => x.TargetId == id, Ct));
    }
    [Fact]
    public async Task Report_budget_is_separate_per_account_and_no_store_includes_errors()
    {
        await using var f = Factory(); using var owner = Client(f); using var reporter = Client(f); using var other = Client(f); await Login(owner); await Login(reporter); await Login(other); var (_, r) = await Review(owner); var id = r.GetProperty("id").GetGuid();
        for (var i = 0; i < 5; i++) { var response = await reporter.PostAsJsonAsync(Reports(Guid.NewGuid()), new { reason = "spam" }, Ct); Assert.Equal(HttpStatusCode.NotFound, response.StatusCode); Assert.True(response.Headers.CacheControl?.NoStore); }
        var limited = await reporter.PostAsJsonAsync(Reports(id), new { reason = "spam" }, Ct); Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode); Assert.NotNull(limited.Headers.RetryAfter); Assert.True(limited.Headers.CacheControl?.NoStore);
        await Json(await other.PostAsJsonAsync(Reports(id), new { reason = "spam" }, Ct), HttpStatusCode.Created);
        Assert.Equal(HttpStatusCode.Created, (await reporter.PostAsJsonAsync($"/api/v1/reviews/{id}/comments", new { body = "Separate write budget" }, Ct)).StatusCode);
    }
    [Fact]
    public async Task Moderator_cannot_change_roles_or_moderate_own_content_and_all_writes_require_csrf()
    {
        await using var f = Factory(); using var owner = Client(f); using var reporter = Client(f); using var staff = Client(f); var ownerId = await Login(owner); await Role(ownerId, "Moderator"); await Login(reporter); var staffId = await Login(staff); await Role(staffId, "Admin"); var (_, r) = await Review(owner); var id = r.GetProperty("id").GetGuid();
        var report = await Json(await reporter.PostAsJsonAsync(Reports(id), new { reason = "spam" }, Ct), HttpStatusCode.Created); var rid = report.GetProperty("id").GetGuid();
        var body = new { hidden = true, reason = "Own", expectedTargetUpdatedAt = r.GetProperty("updatedAt").GetDateTimeOffset() };
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.PostAsJsonAsync(Visibility(id), body, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.PostAsJsonAsync($"/api/v1/moderation/reports/{rid}/resolve", new { decision = "dismiss", reason = "Own", expectedTargetUpdatedAt = r.GetProperty("updatedAt").GetDateTimeOffset() }, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.PutAsJsonAsync($"/api/v1/admin/moderators/{ownerId}", new { reason = "Own" }, Ct)).StatusCode);
        staff.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        Assert.Equal(HttpStatusCode.BadRequest, (await staff.PostAsJsonAsync(Visibility(id), body, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await staff.PostAsJsonAsync($"/api/v1/moderation/reports/{rid}/resolve", new { decision = "dismiss", reason = "Check", expectedTargetUpdatedAt = r.GetProperty("updatedAt").GetDateTimeOffset() }, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await staff.PutAsJsonAsync($"/api/v1/admin/moderators/{ownerId}", new { reason = "Check" }, Ct)).StatusCode);
        using var delete = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/admin/moderators/{ownerId}") { Content = JsonContent.Create(new { reason = "Check" }) }; Assert.Equal(HttpStatusCode.BadRequest, (await staff.SendAsync(delete, Ct)).StatusCode);
        await using var db = Db(); Assert.False(await db.ModerationEvents.AnyAsync(x => x.TargetId == id, Ct));
    }
    [Fact]
    public async Task Comment_reports_and_child_history_survive_parent_deletion_and_safe_dto_projection()
    {
        await using var f = Factory(); using var author = Client(f); using var reporter = Client(f); using var mod = Client(f); await Login(author); await Login(reporter); await Role(await Login(mod), "Moderator"); var (_, r) = await Review(author); var id = r.GetProperty("id").GetGuid();
        var c = await Json(await author.PostAsJsonAsync($"/api/v1/reviews/{id}/comments", new { body = "Reported child" }, Ct), HttpStatusCode.Created); var cid = c.GetProperty("id").GetGuid();
        var rr = await Json(await reporter.PostAsJsonAsync($"/api/v1/comments/{cid}/reports", new { reason = "other", details = "x" }, Ct), HttpStatusCode.Created); Assert.Equal(new[] { "createdAt", "id", "status" }, rr.EnumerateObject().Select(x => x.Name).Order().ToArray()); var rid = rr.GetProperty("id").GetGuid();
        var detail = await Json(await mod.GetAsync($"/api/v1/moderation/reports/{rid}", Ct)); Assert.Equal(new[] { "displayName", "handle", "id" }, detail.GetProperty("report").GetProperty("reporter").EnumerateObject().Select(x => x.Name).Order().ToArray());
        Assert.Equal(HttpStatusCode.NoContent, (await author.DeleteAsync($"/api/v1/reviews/{id}", Ct)).StatusCode);
        await using var db = Db(); Assert.Null((await db.ContentReports.SingleAsync(x => x.Id == rid, Ct)).CommentId);
        var gone = await Json(await mod.GetAsync($"/api/v1/moderation/reports/{rid}", Ct)); Assert.Equal(System.Text.Json.JsonValueKind.Null, gone.GetProperty("target").ValueKind);
    }
}
