using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RateFreak.Domain.Catalog;
using RateFreak.Infrastructure.Authentication;
using RateFreak.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace RateFreak.IntegrationTests;
public sealed class RatingSharingMigrationTests
{
    [Fact, Trait("Category", "Database")]
    public async Task Upgrade_preserves_old_rating_without_publishing_it_and_rejects_inconsistent_sharing()
    {
        var ct=TestContext.Current.CancellationToken;
        await using var postgres=new PostgreSqlBuilder("postgres:17").Build(); await postgres.StartAsync(ct);
        await using var db=new RateFreakDbContext(new DbContextOptionsBuilder<RateFreakDbContext>().UseNpgsql(postgres.GetConnectionString()).Options);
        await db.GetService<IMigrator>().MigrateAsync("20261118195946_AddNotifications",ct);
        var user=new AccountUser{Id=Guid.NewGuid(),UserName="old_reader",NormalizedUserName="OLD_READER",Email="old@example.test",NormalizedEmail="OLD@EXAMPLE.TEST",DisplayName="Old Reader"};
        var media=new MediaItem("book","Preserved opinion");db.Add(media);await db.SaveChangesAsync(ct);
        // Insert the historical shape explicitly: current EF user columns do not exist yet.
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO users (id,user_name,normalized_user_name,email,normalized_email,display_name,created_at,updated_at,email_confirmed,phone_number_confirmed,two_factor_enabled,lockout_enabled,access_failed_count) VALUES ({user.Id},{user.UserName},{user.NormalizedUserName},{user.Email},{user.NormalizedEmail},{user.DisplayName},{user.CreatedAt},{user.UpdatedAt},FALSE,FALSE,FALSE,FALSE,0)",ct);
        var id=Guid.NewGuid();var time=new DateTimeOffset(2026,1,1,0,0,0,TimeSpan.Zero);
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO ratings (id,user_id,media_item_id,score,created_at,updated_at) VALUES ({id},{user.Id},{media.Id},4.5,{time},{time})",ct);
        await db.Database.MigrateAsync(ct);Assert.False(db.Database.HasPendingModelChanges());
        var rating=await db.Ratings.SingleAsync(ct);Assert.Equal(id,rating.Id);Assert.Equal(4.5m,rating.Score);Assert.Equal(time,rating.CreatedAt);Assert.Equal(time,rating.UpdatedAt);Assert.False(rating.IsShared);Assert.Null(rating.SharedAt);
        var error=await Assert.ThrowsAsync<Npgsql.PostgresException>(()=>db.Database.ExecuteSqlInterpolatedAsync($"UPDATE ratings SET is_shared=TRUE WHERE id={id}",ct));Assert.Equal("23514",error.SqlState);
    }
}
