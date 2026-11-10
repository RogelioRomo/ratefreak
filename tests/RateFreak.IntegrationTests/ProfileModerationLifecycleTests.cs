using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RateFreak.Application.Identity;
using RateFreak.Infrastructure.Authentication;

namespace RateFreak.IntegrationTests;

public sealed partial class ModerationTests
{
    [Fact]
    public async Task Profile_erasure_and_replay_remove_live_references_and_pseudonymize_targets_without_text_snapshots()
    {
        await using var f = Factory(); using var owner = Client(f); using var reporter = Client(f); using var staff = Client(f);
        var id = await Login(owner); await Login(reporter); await Role(await Login(staff), "Moderator");
        var report = await Json(await reporter.PostAsJsonAsync(ProfileReport(id), new { reason = "spam" }, Ct), HttpStatusCode.Created); var rid = report.GetProperty("id").GetGuid();
        await HideProfile(staff, id, true);
        await using var scope = f.Services.CreateAsyncScope();
        var journal = Path.GetTempFileName(); if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(journal, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        scope.ServiceProvider.GetRequiredService<IOptions<AccountDataOptions>>().Value.ErasureJournalPath = journal;
        try
        {
            await using var db = Db(); var user = await db.Users.AsNoTracking().SingleAsync(x => x.Id == id, Ct);
            var service = scope.ServiceProvider.GetRequiredService<IAccountDataService>();
            var grant = await service.ConfirmAsync(id, user.SecurityStamp!, new("export", "Local-password-72"), Ct);
            await using (var stream = await service.ExportAsync(id, user.SecurityStamp!, grant, Ct))
            using (var zip = new ZipArchive(stream))
            using (var doc = await JsonDocument.ParseAsync(zip.GetEntry("account.json")!.Open(), cancellationToken: Ct))
            { Assert.True(doc.RootElement.GetProperty("profileTextHidden").GetBoolean()); Assert.Equal("Reader", doc.RootElement.GetProperty("displayName").GetString()); }
            grant = await service.ConfirmAsync(id, user.SecurityStamp!, new("delete", "Local-password-72"), Ct);
            await service.DeleteAsync(id, user.SecurityStamp!, grant, user.UserName!, Ct);
            var retained = await db.ContentReports.AsNoTracking().SingleAsync(x => x.Id == rid, Ct);
            Assert.Null(retained.ProfileId); Assert.NotEqual(id, retained.TargetId); var targetId = retained.TargetId;
            Assert.False(await db.ModerationEvents.AnyAsync(x => x.TargetType == "profile" && x.TargetId == id, Ct));
            var detail = await Json(await staff.GetAsync($"/api/v1/moderation/reports/{rid}", Ct)); Assert.Equal(JsonValueKind.Null, detail.GetProperty("target").ValueKind);
            await scope.ServiceProvider.GetRequiredService<IAccountErasureMaintenance>().ReplayAsync(Ct);
            Assert.Equal(targetId, (await db.ContentReports.AsNoTracking().SingleAsync(x => x.Id == rid, Ct)).TargetId);
            Assert.Equal(HttpStatusCode.NoContent, (await staff.PostAsJsonAsync($"/api/v1/moderation/reports/{rid}/resolve", new { decision = "dismiss", reason = "Account deleted", expectedTargetUpdatedAt = (string?)null }, Ct)).StatusCode);
        }
        finally { File.Delete(journal); }
    }
}
