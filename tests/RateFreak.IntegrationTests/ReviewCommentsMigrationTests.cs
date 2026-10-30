using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RateFreak.Domain.Catalog;
using RateFreak.Domain.PersonalMedia;
using RateFreak.Domain.Social;
using RateFreak.Infrastructure.Authentication;
using RateFreak.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace RateFreak.IntegrationTests;

public sealed class ReviewCommentsMigrationTests
{
    [Fact]
    public async Task Comments_downgrade_and_reapply_preserve_prior_account_catalog_review_and_follows_schema()
    {
        var ct=TestContext.Current.CancellationToken;
        await using var postgres=new PostgreSqlBuilder("postgres:17").Build();await postgres.StartAsync(ct);
        await using var db=new RateFreakDbContext(new DbContextOptionsBuilder<RateFreakDbContext>().UseNpgsql(postgres.GetConnectionString()).Options);
        await db.Database.MigrateAsync(ct);
        var user=new AccountUser { Id=Guid.NewGuid(),UserName="writer",NormalizedUserName="WRITER",Email="writer@example.test",NormalizedEmail="WRITER@EXAMPLE.TEST",DisplayName="Writer" };
        var media=new MediaItem("video_game","Preserved game");db.AddRange(user,media);await db.SaveChangesAsync(ct);
        var review=new Review(user.Id,media.Id,"Preserved review",false);db.Add(review);await db.SaveChangesAsync(ct);
        db.Add(new ReviewComment(user.Id,review.Id,"Removed by explicit rollback",false));await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
        await db.GetService<IMigrator>().MigrateAsync("20261030212206_AddVideoGames",ct);
        Assert.True(await db.Users.AnyAsync(x=>x.Id==user.Id,ct));Assert.True(await db.MediaItems.AnyAsync(x=>x.Id==media.Id,ct));Assert.True(await db.Reviews.AnyAsync(x=>x.Id==review.Id,ct));
        await db.Database.MigrateAsync(ct);await db.Database.MigrateAsync(ct);
        Assert.Empty(await db.ReviewComments.ToListAsync(ct));Assert.Empty(await db.UserFollows.ToListAsync(ct));
        Assert.Equal("Preserved review",(await db.Reviews.SingleAsync(x=>x.Id==review.Id,ct)).Body);
        Assert.False(db.Database.HasPendingModelChanges());Assert.Single(await db.Database.GetAppliedMigrationsAsync(ct),x=>x.EndsWith("_AddReviewComments",StringComparison.Ordinal));
    }
}
