using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RateFreak.Application.Identity;
using RateFreak.Infrastructure.Persistence;
using RateFreak.Infrastructure.Moderation;
namespace RateFreak.Infrastructure.Authentication;

internal sealed class AccountDataMaintenance(RateFreakDbContext db, AccountErasureJournal journal, AccountDataService service) : IAccountErasureMaintenance
{
    public async Task ReplayAsync(CancellationToken ct)
    {
        foreach (var id in await journal.AccountsAsync(ct))
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct); await ModerationLock.AcquireAsync(db, ct);
            _ = await db.Users.FromSqlInterpolated($"SELECT * FROM users WHERE id = {id} FOR UPDATE").AsNoTracking().ToListAsync(ct);
            var keys = await service.EraseAsync(id, ct); await tx.CommitAsync(ct);
            await journal.AppendAsync(id, true, ct); await service.CleanupAsync(keys);
        }
    }
    public async Task RetainAsync(DateTimeOffset now, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct); await ModerationLock.AcquireAsync(db, ct);
        var recoveryCutoff = now.AddDays(-90); var moderationCutoff = now.AddDays(-365);
        var recovery = await db.Set<AccountRecoveryEvent>().Where(e => e.CreatedAt <= recoveryCutoff).OrderBy(e => e.CreatedAt).Select(e => e.Id).Take(500).ToListAsync(ct);
        await db.Set<AccountRecoveryEvent>().Where(e => recovery.Contains(e.Id)).ExecuteDeleteAsync(ct);
        var reports = await db.ContentReports.Where(r => r.Status == "resolved" && r.ResolvedAt <= moderationCutoff).OrderBy(r => r.ResolvedAt).Select(r => r.Id).Take(500).ToListAsync(ct);
        await db.ModerationEvents.Where(e => e.ReportId != null && reports.Contains(e.ReportId.Value)).ExecuteDeleteAsync(ct);
        await db.ContentReports.Where(r => reports.Contains(r.Id)).ExecuteDeleteAsync(ct);
        var events = await db.ModerationEvents.Where(e => e.ReportId == null && e.CreatedAt <= moderationCutoff).OrderBy(e => e.CreatedAt).Select(e => e.Id).Take(500).ToListAsync(ct);
        await db.ModerationEvents.Where(e => events.Contains(e.Id)).ExecuteDeleteAsync(ct);
        var grants = await db.Set<AccountDataGrant>().Where(g => g.ExpiresAt <= now).OrderBy(g => g.ExpiresAt).Select(g => g.Hash).Take(500).ToListAsync(ct);
        await db.Set<AccountDataGrant>().Where(g => grants.Contains(g.Hash)).ExecuteDeleteAsync(ct); await tx.CommitAsync(ct);
    }
}
internal sealed class AccountDataRetentionWorker(IServiceScopeFactory scopes, ILogger<AccountDataRetentionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try { await using var scope = scopes.CreateAsyncScope(); await scope.ServiceProvider.GetRequiredService<IAccountErasureMaintenance>().RetainAsync(DateTimeOffset.UtcNow, stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception) { logger.LogError("Account retention maintenance failed; the next hourly run will retry."); }
        }
    }
}
