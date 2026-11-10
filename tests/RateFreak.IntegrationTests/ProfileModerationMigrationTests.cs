using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using RateFreak.Domain.Moderation;
using RateFreak.Infrastructure.Authentication;
using RateFreak.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace RateFreak.IntegrationTests;

public sealed class ProfileModerationMigrationTests
{
    [Fact]
    public async Task Profile_migration_preserves_existing_text_defaults_visible_and_refuses_loss_of_hidden_state_or_reports()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var pg = new PostgreSqlBuilder("postgres:17").Build(); await pg.StartAsync(ct);
        await using var db = new RateFreakDbContext(new DbContextOptionsBuilder<RateFreakDbContext>().UseNpgsql(pg.GetConnectionString()).Options);
        await db.Database.MigrateAsync(ct);
        var user = new AccountUser { Id = Guid.NewGuid(), UserName = "kept", NormalizedUserName = "KEPT", Email = "kept@example.test", NormalizedEmail = "KEPT@EXAMPLE.TEST", DisplayName = "Kept name", Bio = "Kept bio" };
        db.Add(user); await db.SaveChangesAsync(ct);
        var migrator = db.GetService<IMigrator>(); const string prior = "20261109171007_AddAccountDataLifecycle";
        await migrator.MigrateAsync(prior, ct); await migrator.MigrateAsync(null, ct); db.ChangeTracker.Clear();
        var stored = await db.Users.AsNoTracking().SingleAsync(ct); Assert.Equal("Kept name", stored.DisplayName); Assert.Equal("Kept bio", stored.Bio); Assert.False(stored.IsProfileHidden); Assert.True(stored.ProfileUpdatedAt > DateTimeOffset.UnixEpoch);
        await db.Users.ExecuteUpdateAsync(s => s.SetProperty(u => u.IsProfileHidden, true), ct);
        await Assert.ThrowsAsync<PostgresException>(() => migrator.MigrateAsync(prior, ct));
        Assert.True((await db.Users.AsNoTracking().SingleAsync(ct)).IsProfileHidden);
        await db.Users.ExecuteUpdateAsync(s => s.SetProperty(u => u.IsProfileHidden, false), ct);
        var report = new ContentReport { TargetType = "profile", TargetId = user.Id, ProfileId = user.Id, OriginalReporterId = Guid.NewGuid(), OriginalAuthorId = user.Id, Reason = "spam" };
        db.Add(report); await db.SaveChangesAsync(ct);
        await Assert.ThrowsAsync<PostgresException>(() => migrator.MigrateAsync(prior, ct));
        Assert.True(await db.ContentReports.AnyAsync(r => r.Id == report.Id, ct));
        var invalid = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE content_reports SET profile_id = NULL, review_id = {Guid.NewGuid()} WHERE id = {report.Id}", ct));
        Assert.Equal(PostgresErrorCodes.CheckViolation, invalid.SqlState);
        await db.ContentReports.ExecuteDeleteAsync(ct);
        db.Add(new ModerationEvent { Action = "restore", TargetType = "profile", TargetId = user.Id, ActorKind = "operator", Reason = "History preserved" }); await db.SaveChangesAsync(ct);
        await Assert.ThrowsAsync<PostgresException>(() => migrator.MigrateAsync(prior, ct));
        Assert.True(await db.ModerationEvents.AnyAsync(e => e.TargetType == "profile", ct));
    }
}
