using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace RateFreak.IntegrationTests;

public sealed partial class ModerationTests
{
    [Fact]
    public async Task Queue_has_stable_tied_pages_safe_summaries_and_validated_paging()
    {
        await using var f = Factory(); using var author = Client(f); using var reporter = Client(f); using var mod = Client(f); await Login(author); await Login(reporter); await Role(await Login(mod), "Moderator"); var ids = new List<Guid>();
        for (var i = 0; i < 3; i++) { var (_, r) = await Review(author); var id = r.GetProperty("id").GetGuid(); var report = await Json(await reporter.PostAsJsonAsync(Reports(id), new { reason = "other", details = "Private details" }, Ct), HttpStatusCode.Created); ids.Add(report.GetProperty("id").GetGuid()); }
        await using (var db = Db()) { await db.ContentReports.Where(x => ids.Contains(x.Id)).ExecuteUpdateAsync(s => s.SetProperty(x => x.CreatedAt, new DateTimeOffset(1900, 1, 1, 0, 0, 0, TimeSpan.Zero)), Ct); }
        var ordered = ids.Order().ToArray();
        for (var i = 0; i < 3; i++)
        { var response = await mod.GetAsync($"/api/v1/moderation/reports?page={i + 1}&pageSize=1", Ct); Assert.True(response.Headers.CacheControl?.NoStore); var page = await Json(response); Assert.True(page.GetProperty("totalCount").GetInt32() >= 3); var item = Assert.Single(page.GetProperty("items").EnumerateArray()); Assert.Equal(ordered[i], item.GetProperty("id").GetGuid()); Assert.False(item.TryGetProperty("details", out _)); Assert.False(item.TryGetProperty("body", out _)); }
        foreach (var query in new[] { "page=0", "page=10001", "pageSize=101", "status=all" }) Assert.Equal(HttpStatusCode.BadRequest, (await mod.GetAsync("/api/v1/moderation/reports?" + query, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await mod.GetAsync("/api/v1/moderation/audit?targetType=private", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await mod.GetAsync($"/api/v1/moderation/reports/{Guid.NewGuid()}", Ct)).StatusCode);
    }
    [Fact]
    public async Task Moderator_read_budget_is_separate_from_public_reads()
    {
        await using var f = Factory(); using var mod = Client(f); await Role(await Login(mod), "Moderator");
        for (var i = 0; i < 60; i++) Assert.Equal(HttpStatusCode.OK, (await mod.GetAsync("/api/v1/moderation/reports", Ct)).StatusCode);
        var limited = await mod.GetAsync("/api/v1/moderation/audit", Ct); Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode); Assert.True(limited.Headers.CacheControl?.NoStore); Assert.NotNull(limited.Headers.RetryAfter);
        Assert.Equal(HttpStatusCode.OK, (await mod.GetAsync("/api/v1/media", Ct)).StatusCode);
    }
    [Fact]
    public async Task Development_openapi_lists_reporting_moderation_and_admin_operations()
    {
        await using var f = new ApiFactory(environment: "Development", connectionString: database.ConnectionString); using var c = Client(f); var doc = await c.GetFromJsonAsync<JsonElement>("/openapi/v1.json", Ct); var paths = doc.GetProperty("paths");
        foreach (var (path, method) in new[] { ("/api/v1/reviews/{id}/reports", "post"), ("/api/v1/comments/{id}/reports", "post"), ("/api/v1/moderation/reports", "get"), ("/api/v1/moderation/reports/{id}", "get"), ("/api/v1/moderation/reports/{id}/resolve", "post"), ("/api/v1/moderation/reviews/{id}", "get"), ("/api/v1/moderation/comments/{id}", "get"), ("/api/v1/moderation/reviews/{id}/visibility", "post"), ("/api/v1/moderation/comments/{id}/visibility", "post"), ("/api/v1/moderation/audit", "get"), ("/api/v1/admin/moderators/{id}", "put"), ("/api/v1/admin/moderators/{id}", "delete") }) Assert.True(paths.GetProperty(path).TryGetProperty(method, out _));
    }
}
