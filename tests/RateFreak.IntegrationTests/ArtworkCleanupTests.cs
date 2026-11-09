using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RateFreak.Infrastructure.Artwork;
using RateFreak.Infrastructure.Persistence;

namespace RateFreak.IntegrationTests;

public sealed class ArtworkCleanupTests(AccountDatabase database) : IClassFixture<AccountDatabase>
{
    [Fact]
    public async Task Cleanup_removes_only_old_unreferenced_files_and_skips_active_staging()
    {
        var ct = TestContext.Current.CancellationToken; var root = Path.Combine(Path.GetTempPath(), "ratefreak-cleanup-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            await using var factory = new ApiFactory(connectionString: database.ConnectionString, configureServices: services => services.Configure<ArtworkOptions>(o => { o.Enabled = true; o.RootPath = root; }));
            var old = Guid.NewGuid().ToString("N"); var fresh = Guid.NewGuid().ToString("N");
            await File.WriteAllBytesAsync(Path.Combine(root, old + ".webp"), [1], ct); File.SetLastWriteTimeUtc(Path.Combine(root, old + ".webp"), DateTime.UtcNow.AddDays(-2));
            await File.WriteAllBytesAsync(Path.Combine(root, fresh + ".webp"), [2], ct);
            using var scope = factory.Services.CreateScope(); var storage = scope.ServiceProvider.GetRequiredService<RateFreak.Application.Catalog.IArtworkStorage>();
            using var active = await storage.StageAsync(new MemoryStream([3]), ct); File.SetLastWriteTimeUtc(Path.Combine(root, active.Key), DateTime.UtcNow.AddDays(-2));
            var cleanup = scope.ServiceProvider.GetRequiredService<ArtworkCleanupService>(); await cleanup.RunOnceAsync(ct);
            Assert.False(File.Exists(Path.Combine(root, old + ".webp"))); Assert.True(File.Exists(Path.Combine(root, fresh + ".webp"))); Assert.True(File.Exists(Path.Combine(root, active.Key)));
        }
        finally { Directory.Delete(root, true); }
    }
    [Fact]
    public async Task Bounded_sweeps_progress_past_fresh_files_instead_of_starving_old_orphans()
    {
        var ct = TestContext.Current.CancellationToken; var root = Path.Combine(Path.GetTempPath(), "ratefreak-sweep-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            for (var i = 0; i < 1001; i++) await File.WriteAllBytesAsync(Path.Combine(root, Guid.NewGuid().ToString("N") + ".webp"), [1], ct);
            var last = Directory.GetFiles(root).Last(); File.SetLastWriteTimeUtc(last, DateTime.UtcNow.AddDays(-2));
            await using var factory = new ApiFactory(connectionString: database.ConnectionString, configureServices: services => services.Configure<ArtworkOptions>(o => { o.Enabled = true; o.RootPath = root; }));
            using var scope = factory.Services.CreateScope(); var cleanup = scope.ServiceProvider.GetRequiredService<ArtworkCleanupService>();
            await cleanup.RunOnceAsync(ct); await cleanup.RunOnceAsync(ct);
            Assert.False(File.Exists(last)); Assert.Equal(1000, Directory.GetFiles(root).Length);
        }
        finally { Directory.Delete(root, true); }
    }

}
