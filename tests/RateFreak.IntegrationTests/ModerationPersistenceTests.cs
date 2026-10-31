using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RateFreak.Application.Moderation;

namespace RateFreak.IntegrationTests;

public sealed partial class ModerationTests
{
    [Fact]
    public async Task Reports_and_audit_survive_account_and_content_deletion_without_copied_bodies()
    {
        await using var f = Factory(); using var author = Client(f); using var reporter = Client(f); using var mod = Client(f);
        var authorId = await Login(author); var reporterId = await Login(reporter); await Role(await Login(mod), "Moderator"); var (_, r) = await Review(author); var id = r.GetProperty("id").GetGuid();
        var report = await Json(await reporter.PostAsJsonAsync(Reports(id), new { reason = "other", details = "Investigate" }, Ct), HttpStatusCode.Created); var rid = report.GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.NoContent, (await mod.PostAsJsonAsync(Visibility(id), new { hidden = true, reason = "Under review", expectedTargetUpdatedAt = r.GetProperty("updatedAt").GetDateTimeOffset() }, Ct)).StatusCode);
        await using (var db = Db()) { await db.Users.Where(x => x.Id == authorId || x.Id == reporterId).ExecuteDeleteAsync(Ct); }
        var detail = await Json(await mod.GetAsync($"/api/v1/moderation/reports/{rid}", Ct)); Assert.Equal(System.Text.Json.JsonValueKind.Null, detail.GetProperty("target").ValueKind); Assert.Equal(System.Text.Json.JsonValueKind.Null, detail.GetProperty("report").GetProperty("reporter").ValueKind);
        Assert.Equal(HttpStatusCode.Conflict, (await mod.PostAsJsonAsync($"/api/v1/moderation/reports/{rid}/resolve", new { decision = "hide", reason = "Gone", expectedTargetUpdatedAt = (DateTimeOffset?)null }, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await mod.PostAsJsonAsync($"/api/v1/moderation/reports/{rid}/resolve", new { decision = "dismiss", reason = "Gone", expectedTargetUpdatedAt = (DateTimeOffset?)null }, Ct)).StatusCode);
        await using (var db = Db()) { var kept = await db.ContentReports.SingleAsync(x => x.Id == rid, Ct); Assert.Null(kept.ReviewId); Assert.Null(kept.ReporterId); Assert.Equal(id, kept.TargetId); Assert.Equal(2, await db.ModerationEvents.CountAsync(x => x.TargetId == id, Ct)); }
    }
    [Fact]
    public async Task Offline_admin_appointment_is_explicit_idempotent_and_audited()
    {
        await using var f = Factory(); using var user = Client(f); var id = await Login(user); await using var scope = f.Services.CreateAsyncScope(); var service = scope.ServiceProvider.GetRequiredService<IModerationService>();
        Assert.Equal(ModerationStatus.Invalid, (await service.AppointAdminAsync(id, " ", Ct)).Status);
        Assert.Equal(ModerationStatus.NotFound, (await service.AppointAdminAsync(Guid.NewGuid(), "Appoint", Ct)).Status);
        Assert.Equal(ModerationStatus.Success, (await service.AppointAdminAsync(id, "Appoint", Ct)).Status);
        Assert.Equal(ModerationStatus.Success, (await service.AppointAdminAsync(id, "Repeat", Ct)).Status);
        Assert.Equal(HttpStatusCode.OK, (await user.GetAsync("/api/v1/moderation/reports", Ct)).StatusCode);
        await using var db = Db(); var e = await db.ModerationEvents.SingleAsync(x => x.TargetId == id, Ct); Assert.Equal("operator", e.ActorKind); Assert.Null(e.ActorId); Assert.Equal("grant_admin", e.Action);
    }
    [Fact]
    public async Task Concurrent_duplicate_reports_and_decisions_have_one_winner()
    {
        await using var f = Factory(); using var owner = Client(f); using var reporter = Client(f); using var mod = Client(f); await Login(owner); await Login(reporter); await Role(await Login(mod), "Moderator"); var (_, r) = await Review(owner); var id = r.GetProperty("id").GetGuid();
        var responses = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => reporter.PostAsJsonAsync(Reports(id), new { reason = "spam" }, Ct)));
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Created); Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Conflict);
        var report = await Json(responses.Single(x => x.StatusCode == HttpStatusCode.Created), HttpStatusCode.Created); var rid = report.GetProperty("id").GetGuid();
        var decisions = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => mod.PostAsJsonAsync($"/api/v1/moderation/reports/{rid}/resolve", new { decision = "hide", reason = "Spam", expectedTargetUpdatedAt = r.GetProperty("updatedAt").GetDateTimeOffset() }, Ct)));
        Assert.Single(decisions, x => x.StatusCode == HttpStatusCode.NoContent); Assert.Single(decisions, x => x.StatusCode == HttpStatusCode.Conflict);
        await using var db = Db(); Assert.Equal(1, await db.ModerationEvents.CountAsync(x => x.ReportId == rid, Ct));
    }
    [Fact]
    public async Task Audit_failure_rolls_back_visibility_resolution_and_role_changes()
    {
        await using var f = Factory(); using var owner = Client(f); using var admin = Client(f); using var reporter = Client(f); using var candidate = Client(f);
        await Login(owner); var adminId = await Login(admin); await Role(adminId, "Admin"); await Login(reporter); var candidateId = await Login(candidate); var (_, r) = await Review(owner); var id = r.GetProperty("id").GetGuid();
        var report = await Json(await reporter.PostAsJsonAsync(Reports(id), new { reason = "spam" }, Ct), HttpStatusCode.Created); var rid = report.GetProperty("id").GetGuid();
        await using (var db = Db()) { await db.Database.ExecuteSqlRawAsync("ALTER TABLE moderation_events ADD CONSTRAINT test_reject_events CHECK (reason <> 'reject-test')", Ct); }
        try
        {
            Assert.Equal(HttpStatusCode.InternalServerError, (await admin.PostAsJsonAsync(Visibility(id), new { hidden = true, reason = "reject-test", expectedTargetUpdatedAt = r.GetProperty("updatedAt").GetDateTimeOffset() }, Ct)).StatusCode);
            Assert.Equal(HttpStatusCode.InternalServerError, (await admin.PostAsJsonAsync($"/api/v1/moderation/reports/{rid}/resolve", new { decision = "hide", reason = "reject-test", expectedTargetUpdatedAt = r.GetProperty("updatedAt").GetDateTimeOffset() }, Ct)).StatusCode);
            Assert.Equal(HttpStatusCode.InternalServerError, (await admin.PutAsJsonAsync($"/api/v1/admin/moderators/{candidateId}", new { reason = "reject-test" }, Ct)).StatusCode);
            await using var db = Db(); Assert.False((await db.Reviews.SingleAsync(x => x.Id == id, Ct)).IsHidden); Assert.Equal("open", (await db.ContentReports.SingleAsync(x => x.Id == rid, Ct)).Status); Assert.False(await db.UserRoles.AnyAsync(x => x.UserId == candidateId, Ct)); Assert.False(await db.ModerationEvents.AnyAsync(x => x.TargetId == id || x.TargetId == candidateId, Ct));
        }
        finally { await using var db = Db(); await db.Database.ExecuteSqlRawAsync("ALTER TABLE moderation_events DROP CONSTRAINT test_reject_events", Ct); }
    }
}
