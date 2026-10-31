using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace RateFreak.IntegrationTests;

public sealed partial class ModerationTests
{
    [Fact]
    public async Task Revocation_queued_before_decision_rechecks_permission_inside_transaction()
    {
        await using var f = Factory(); using var author = Client(f); using var mod = Client(f); using var admin = Client(f);
        await Login(author); var mid = await Login(mod); await Role(mid, "Moderator"); await Role(await Login(admin), "Admin"); var (_, r) = await Review(author); var id = r.GetProperty("id").GetGuid();
        await using var blocker = new NpgsqlConnection(database.ConnectionString); await blocker.OpenAsync(Ct); await using var tx = await blocker.BeginTransactionAsync(Ct);
        await using (var command = new NpgsqlCommand("SELECT pg_advisory_xact_lock(734188290134)", blocker, tx)) await command.ExecuteNonQueryAsync(Ct);
        async Task WaitForWaiters(int expected)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Ct); timeout.CancelAfter(TimeSpan.FromSeconds(10));
            await using var observer = new NpgsqlConnection(database.ConnectionString); await observer.OpenAsync(timeout.Token);
            while (true)
            {
                await using var query = new NpgsqlCommand("SELECT count(*) FROM pg_locks WHERE locktype='advisory' AND NOT granted AND database=(SELECT oid FROM pg_database WHERE datname=current_database())", observer);
                if (Convert.ToInt32(await query.ExecuteScalarAsync(timeout.Token)) >= expected) return;
                await Task.Delay(20, timeout.Token);
            }
        }
        using var revoke = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/admin/moderators/{mid}") { Content = JsonContent.Create(new { reason = "Revoked" }) };
        var revocation = admin.SendAsync(revoke, Ct); await WaitForWaiters(1);
        var decision = mod.PostAsJsonAsync(Visibility(id), new { hidden = true, reason = "Queued action", expectedTargetUpdatedAt = r.GetProperty("updatedAt").GetDateTimeOffset() }, Ct); await WaitForWaiters(2);
        await tx.CommitAsync(Ct);
        Assert.Equal(HttpStatusCode.NoContent, (await revocation).StatusCode); Assert.Equal(HttpStatusCode.Forbidden, (await decision).StatusCode);
        await using var db = Db(); Assert.False((await db.Reviews.SingleAsync(x => x.Id == id, Ct)).IsHidden); Assert.False(await db.ModerationEvents.AnyAsync(x => x.TargetId == id, Ct));
    }
    [Fact]
    public async Task Editing_after_inspection_rejects_stale_decision_without_audit_and_repeated_state_is_idempotent()
    {
        await using var f = Factory(); using var author = Client(f); using var mod = Client(f); await Login(author); await Role(await Login(mod), "Moderator"); var (_, r) = await Review(author); var id = r.GetProperty("id").GetGuid();
        var edited = await Json(await author.PatchAsJsonAsync($"/api/v1/reviews/{id}", new { body = "Different text" }, Ct));
        Assert.Equal(HttpStatusCode.Conflict, (await mod.PostAsJsonAsync(Visibility(id), new { hidden = true, reason = "Old version", expectedTargetUpdatedAt = r.GetProperty("updatedAt").GetDateTimeOffset() }, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await mod.PostAsJsonAsync(Visibility(id), new { hidden = false, reason = "Already public", expectedTargetUpdatedAt = edited.GetProperty("updatedAt").GetDateTimeOffset() }, Ct)).StatusCode);
        await using var db = Db(); Assert.False(await db.ModerationEvents.AnyAsync(x => x.TargetId == id, Ct)); Assert.Equal("Different text", (await db.Reviews.SingleAsync(x => x.Id == id, Ct)).Body);
    }
}
