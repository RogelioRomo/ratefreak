using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace RateFreak.IntegrationTests;

public sealed partial class ModerationTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Report_constraints_reject_missing_other_details_and_incomplete_resolution(bool missingDetails)
    {
        await using var f = Factory(); using var owner = Client(f); using var reporter = Client(f); await Login(owner); await Login(reporter); var (_, r) = await Review(owner); var id = r.GetProperty("id").GetGuid(); var report = await Json(await reporter.PostAsJsonAsync(Reports(id), new { reason = "spam" }, Ct), HttpStatusCode.Created); var rid = report.GetProperty("id").GetGuid();
        await using var db = Db();
        if (missingDetails)
        {
            var details = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE content_reports SET reason='other', details=NULL WHERE id={rid}", Ct)); Assert.Equal(PostgresErrorCodes.CheckViolation, details.SqlState);
        }
        else
        {
            var resolution = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE content_reports SET status='resolved', resolved_at=now(), resolved_by={Guid.NewGuid()}, resolution_reason='done', decision=NULL WHERE id={rid}", Ct)); Assert.Equal(PostgresErrorCodes.CheckViolation, resolution.SqlState);
        }
        Assert.Equal("open", (await db.ContentReports.SingleAsync(x => x.Id == rid, Ct)).Status);
    }
}
