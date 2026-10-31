using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using RateFreak.Domain.Catalog;
using RateFreak.Domain.Moderation;
using RateFreak.Domain.PersonalMedia;
using RateFreak.Infrastructure.Authentication;
using RateFreak.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace RateFreak.IntegrationTests;

public sealed class ModerationMigrationTests
{
    [Fact]
    public async Task Rollback_refuses_hidden_content_reports_and_history_and_safe_reapply_preserves_data()
    {
        var ct = TestContext.Current.CancellationToken; await using var pg = new PostgreSqlBuilder("postgres:17").Build(); await pg.StartAsync(ct);
        await using var db = new RateFreakDbContext(new DbContextOptionsBuilder<RateFreakDbContext>().UseNpgsql(pg.GetConnectionString()).Options); await db.Database.MigrateAsync(ct);
        var user = new AccountUser { Id = Guid.NewGuid(), UserName = "staff", NormalizedUserName = "STAFF", Email = "staff@example.test", NormalizedEmail = "STAFF@EXAMPLE.TEST", DisplayName = "Staff" }; var media = new MediaItem("movie", "Kept"); db.AddRange(user, media); await db.SaveChangesAsync(ct); var review = new Review(user.Id, media.Id, "Kept body", false); db.Add(review); await db.SaveChangesAsync(ct);
        var migrator = db.GetService<IMigrator>(); const string prior = "20261030215540_AddExternalSignIn";
        async Task Refused()
        {
            await Assert.ThrowsAsync<PostgresException>(() => migrator.MigrateAsync(prior, ct)); Assert.Single(await db.Database.GetAppliedMigrationsAsync(ct), m => m.EndsWith("_AddModeration", StringComparison.Ordinal)); Assert.True(await db.Reviews.AnyAsync(x => x.Id == review.Id, ct));
        }
        await db.Reviews.Where(x => x.Id == review.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsHidden, true), ct); await Refused();
        await db.Reviews.Where(x => x.Id == review.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsHidden, false), ct);
        var report = new ContentReport { TargetType = "review", TargetId = review.Id, ReviewId = review.Id, OriginalAuthorId = user.Id, OriginalReporterId = Guid.NewGuid(), Reason = "spam" }; db.Add(report); await db.SaveChangesAsync(ct); await Refused(); await db.ContentReports.ExecuteDeleteAsync(ct);
        db.Add(new ModerationEvent { Action = "hide", TargetType = "review", TargetId = review.Id, Reason = "Preserve history", ActorId = user.Id }); await db.SaveChangesAsync(ct); await Refused(); await db.ModerationEvents.ExecuteDeleteAsync(ct);
        db.ChangeTracker.Clear(); await migrator.MigrateAsync(prior, ct); await db.Database.MigrateAsync(ct); await db.Database.MigrateAsync(ct);
        Assert.Equal("Kept body", (await db.Reviews.SingleAsync(x => x.Id == review.Id, ct)).Body); Assert.False(db.Database.HasPendingModelChanges()); Assert.True(await db.Users.AnyAsync(x => x.Id == user.Id, ct)); Assert.True(await db.MediaItems.AnyAsync(x => x.Id == media.Id, ct));
    }
}
