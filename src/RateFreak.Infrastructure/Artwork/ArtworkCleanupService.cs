using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RateFreak.Domain.Catalog;
using RateFreak.Infrastructure.Moderation;
using RateFreak.Infrastructure.Persistence;

namespace RateFreak.Infrastructure.Artwork;

public sealed class ArtworkCleanupService(IServiceScopeFactory scopes, IOptions<ArtworkOptions> options, ILogger<ArtworkCleanupService> logger) : BackgroundService
{
    private IEnumerator<string>? scan;
    private string? scanRoot;
    private readonly SemaphoreSlim sweepGate = new(1, 1);
    private IEnumerable<string> NextBatch(string root)
    {
        if (scan is null || scanRoot != root)
        { scan?.Dispose(); scan = Directory.EnumerateFiles(root).GetEnumerator(); scanRoot = root; }
        for (var count = 0; count < 1000; count++)
        {
            if (!scan.MoveNext()) { scan.Dispose(); scan = null; yield break; }
            yield return scan.Current;
        }
    }
    public override void Dispose() { scan?.Dispose(); base.Dispose(); }
    public async Task RunOnceAsync(CancellationToken ct)
    {
        if (!options.Value.Enabled) return;
        await sweepGate.WaitAsync(ct);
        try
        {
            var storage = new FileArtworkStorage(options); var root = storage.Root();
            using var scope = scopes.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<RateFreakDbContext>();
            await using var tx = await db.Database.BeginTransactionAsync(ct); await ModerationLock.AcquireAsync(db, ct);
            // If the database is unavailable, no files are removed.
            var referenced = (await db.Set<ArtworkAsset>().Select(a => a.StorageKey).ToListAsync(ct)).ToHashSet(StringComparer.Ordinal);
            foreach (var path in NextBatch(root))
            {
                ct.ThrowIfCancellationRequested(); var key = Path.GetFileName(path);
                if (File.GetLastWriteTimeUtc(path) > DateTime.UtcNow.AddHours(-24) || referenced.Contains(key) || new FileInfo(path).LinkTarget is not null) continue;
                if (Path.GetExtension(key) is not (".stage" or ".webp" or ".lease") || !Guid.TryParseExact(Path.GetFileNameWithoutExtension(key), "N", out _)) continue;
                var leasePath = Path.ChangeExtension(path, ".lease"); FileStream? lease = null;
                try
                {
                    if (File.Exists(leasePath)) lease = new FileStream(leasePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                    await storage.DeleteAsync(key, ct);
                }
                catch (IOException) { /* An upload owns the lease, or deletion can be retried later. */ }
                finally { lease?.Dispose(); }
            }
            await tx.CommitAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception) { scan?.Dispose(); scan = null; logger.LogWarning("Artwork cleanup deferred; database or storage unavailable"); }
        finally { sweepGate.Release(); }
    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        try { while (await timer.WaitForNextTickAsync(stoppingToken)) await RunOnceAsync(stoppingToken); }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}
