using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using RateFreak.Domain.Catalog;
using RateFreak.Domain.Moderation;
using RateFreak.Infrastructure.Authentication;
using RateFreak.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace RateFreak.IntegrationTests;

public sealed class UserProfilesMigrationTests
{
    [Fact]
    public async Task Bio_rollback_guard_refuses_data_loss_and_safe_reapply_preserves_existing_identity_and_history()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var postgres = new PostgreSqlBuilder("postgres:17").Build(); await postgres.StartAsync(ct);
        await using var db = new RateFreakDbContext(new DbContextOptionsBuilder<RateFreakDbContext>().UseNpgsql(postgres.GetConnectionString()).Options);
        await db.Database.MigrateAsync(ct);
        var user = new AccountUser { Id = Guid.NewGuid(), UserName = "preserved", NormalizedUserName = "PRESERVED", Email = "kept@example.test", NormalizedEmail = "KEPT@EXAMPLE.TEST", DisplayName = "Reader", PasswordHash = "preserved-hash", SecurityStamp = "kept-stamp", Bio = "Do not discard" };
        var media = new MediaItem("movie", "Preserved media"); var history = new ModerationEvent { ActorId = user.Id, Action = "grant_moderator", TargetType = "account", TargetId = user.Id, Reason = "Preserved history" };
        db.AddRange(user, media, history); await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
        var migrator = db.GetService<IMigrator>(); const string prior = "20261031003720_AddModeration";
        await Assert.ThrowsAsync<PostgresException>(() => migrator.MigrateAsync(prior, ct));
        Assert.Equal("Do not discard", (await db.Users.AsNoTracking().SingleAsync(ct)).Bio);
        Assert.Single(await db.Database.GetAppliedMigrationsAsync(ct), name => name.EndsWith("_AddUserBios", StringComparison.Ordinal));
        var invalid = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE users SET bio={'\t'.ToString()} WHERE id={user.Id}", ct)); Assert.Equal(PostgresErrorCodes.CheckViolation, invalid.SqlState);
        await db.Users.Where(x => x.Id == user.Id).ExecuteUpdateAsync(set => set.SetProperty(x => x.Bio, (string?)null), ct);
        await migrator.MigrateAsync(prior, ct); await db.Database.MigrateAsync(ct); await db.Database.MigrateAsync(ct);
        var kept = await db.Users.AsNoTracking().SingleAsync(ct); Assert.Equal("preserved-hash", kept.PasswordHash); Assert.Equal("kept-stamp", kept.SecurityStamp); Assert.Null(kept.Bio);
        Assert.True(await db.MediaItems.AnyAsync(x => x.Id == media.Id, ct)); Assert.True(await db.ModerationEvents.AnyAsync(x => x.Id == history.Id, ct)); Assert.False(db.Database.HasPendingModelChanges());
    }
}
