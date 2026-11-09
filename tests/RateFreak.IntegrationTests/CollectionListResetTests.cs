using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using RateFreak.Api.Maintenance;
using RateFreak.Domain.Catalog;
using RateFreak.Infrastructure.Authentication;
using RateFreak.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace RateFreak.IntegrationTests;

public sealed class CollectionListResetTests
{
    [Fact]
    public async Task Cutover_refuses_legacy_data_until_exactly_scoped_reset_preserving_catalog_and_collection_metadata()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var pg = new PostgreSqlBuilder("postgres:17").Build(); await pg.StartAsync(ct);
        await using var db = new RateFreakDbContext(new DbContextOptionsBuilder<RateFreakDbContext>().UseNpgsql(pg.GetConnectionString()).Options);
        var migration = typeof(RateFreakDbContext).Assembly.GetTypes().Single(t => t.Name == "AddArtworkMetadata").GetCustomAttributes(typeof(MigrationAttribute), false).Cast<MigrationAttribute>().Single().Id;
        await db.GetService<IMigrator>().MigrateAsync(migration, ct);
        var user = new AccountUser { Id = Guid.NewGuid(), UserName = "reset_owner", NormalizedUserName = "RESET_OWNER", Email = "reset@test.example", NormalizedEmail = "RESET@TEST.EXAMPLE", DisplayName = "Owner" }; var media = new MediaItem("movie", "Preserved subject"); db.AddRange(user, media); await db.SaveChangesAsync(ct);
        var rating = new RateFreak.Domain.PersonalMedia.Rating(user.Id, media.Id, 4.5m); var review = new RateFreak.Domain.PersonalMedia.Review(user.Id, media.Id, "Preserved review", false); db.AddRange(rating, review); await db.SaveChangesAsync(ct);
        var list = Guid.NewGuid(); var entry = Guid.NewGuid(); var field = Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO collections (id,owner_user_id,name,visibility,revision,schema_version,created_at,updated_at) VALUES ({list},{user.Id},'Preserved garden','private',7,1,now(),now())",ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO collection_fields (id,collection_id,key,label,field_type,required,configuration,position,created_at,updated_at) VALUES ({field},{list},'note','Note','text',false,'{{}}',0,now(),now())",ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO collection_items (id,collection_id,title,data,position,created_at,updated_at) VALUES ({entry},{list},'Old entry','{{}}',0,now(),now())",ct);
        await Assert.ThrowsAsync<PostgresException>(() => db.Database.MigrateAsync(ct));
        var databaseName = new NpgsqlConnectionStringBuilder(pg.GetConnectionString()).Database!;
        await Assert.ThrowsAsync<InvalidOperationException>(() => CollectionListReset.RunAsync(db,"wrong_database",true,ct));
        var dry = await CollectionListReset.RunAsync(db,databaseName,false,ct); Assert.Equal(1, dry.Memberships); Assert.Equal(1, dry.Fields); Assert.False(dry.Cleared);
        var reset = await CollectionListReset.RunAsync(db,databaseName,true,ct); Assert.True(reset.Cleared);
        await db.Database.MigrateAsync(ct); await db.Database.MigrateAsync(ct);
        var collection = await db.Collections.SingleAsync(x => x.Id == list,ct); Assert.Equal("Preserved garden",collection.Name); Assert.True(collection.Revision > 7); Assert.True(await db.Users.AnyAsync(x=>x.Id==user.Id,ct)); Assert.True(await db.MediaItems.AnyAsync(x=>x.Id==media.Id,ct)); Assert.Empty(await db.CollectionItems.ToListAsync(ct)); Assert.False(db.Database.HasPendingModelChanges()); Assert.Equal(4.5m, (await db.Ratings.SingleAsync(x => x.Id == rating.Id, ct)).Score); Assert.Equal("Preserved review", (await db.Reviews.SingleAsync(x => x.Id == review.Id, ct)).Body);
    }
}
