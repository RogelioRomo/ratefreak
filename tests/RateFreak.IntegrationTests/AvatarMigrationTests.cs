using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using RateFreak.Infrastructure.Authentication;
using RateFreak.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace RateFreak.IntegrationTests;

public sealed class AvatarMigrationTests
{
    [Fact]
    public async Task Upgrade_preserves_profiles_and_downgrade_refuses_live_avatar_data()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var pg = new PostgreSqlBuilder("postgres:17").Build(); await pg.StartAsync(ct);
        await using var db = new RateFreakDbContext(new DbContextOptionsBuilder<RateFreakDbContext>().UseNpgsql(pg.GetConnectionString()).Options);
        await db.Database.MigrateAsync(ct);
        var user = new AccountUser { Id = Guid.NewGuid(), UserName = "kept_avatar", NormalizedUserName = "KEPT_AVATAR", Email = "kept@example.test", NormalizedEmail = "KEPT@EXAMPLE.TEST", DisplayName = "Kept profile", Bio = "Existing bio" };
        db.Add(user); await db.SaveChangesAsync(ct);
        var migrator = db.GetService<IMigrator>(); const string previous = "20261119013555_AddRatingSharingAndFeedIndexes";
        await migrator.MigrateAsync(previous, ct); await migrator.MigrateAsync(null, ct); db.ChangeTracker.Clear();
        var stored = await db.Users.AsNoTracking().SingleAsync(ct);
        Assert.Equal(user.Bio, stored.Bio); Assert.Equal(user.DisplayName, stored.DisplayName); Assert.Null(stored.AvatarId); Assert.Equal(0, stored.AvatarRevision);
        var assetId = Guid.NewGuid(); db.Add(new AvatarAsset { UserId = user.Id, AssetId = assetId, StorageKey = assetId.ToString("N") + ".webp", Width = 2, Height = 2, ByteLength = 10 });
        await db.SaveChangesAsync(ct);
        await db.Users.ExecuteUpdateAsync(s => s.SetProperty(u => u.AvatarId, assetId).SetProperty(u => u.AvatarRevision, 1L), ct);
        await Assert.ThrowsAsync<PostgresException>(() => migrator.MigrateAsync(previous, ct));
        Assert.True(await db.Set<AvatarAsset>().AnyAsync(a => a.AssetId == assetId, ct));
    }
}
