using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RateFreak.Domain.Catalog;
using RateFreak.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace RateFreak.IntegrationTests;

public sealed class SongMigrationTests
{
    private const string Previous = "20261127171719_AddIgdbContentLifecycle";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static RateFreakDbContext Db(string connection) => new(new DbContextOptionsBuilder<RateFreakDbContext>().UseNpgsql(connection).Options);

    [Fact, Trait("Category", "Database")]
    public async Task Songs_upgrade_preserves_existing_catalog_and_empty_downgrade_is_repeatable()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:17").Build(); await postgres.StartAsync(Ct);
        await using var db = Db(postgres.GetConnectionString()); await db.GetService<IMigrator>().MigrateAsync(Previous, Ct);
        var movie = new MediaItem("movie", "Preserved movie"); db.Add(movie); await db.SaveChangesAsync(Ct);
        await db.Database.MigrateAsync(Ct); Assert.False(db.Database.HasPendingModelChanges());
        var songs = await db.Categories.SingleAsync(x => x.Id == Guid.Parse("f11e0000-0000-0000-0000-000000000006"), Ct);
        Assert.Equal("Songs", songs.Name); Assert.True(songs.IsSeeded); Assert.Equal("Preserved movie", (await db.MediaItems.SingleAsync(Ct)).Title);
        await db.GetService<IMigrator>().MigrateAsync(Previous, Ct); await db.Database.MigrateAsync(Ct); await db.Database.MigrateAsync(Ct);
        Assert.True(await db.MediaItems.AnyAsync(x => x.Id == movie.Id, Ct)); Assert.Single(await db.Categories.Where(x => x.NormalizedName == "SONGS").ToListAsync(Ct));
    }

    [Theory, Trait("Category", "Database")]
    [InlineData("Songs", "ca110000-0000-0000-0000-000000000006")]
    [InlineData("Existing category", "f11e0000-0000-0000-0000-000000000006")]
    public async Task Existing_public_name_or_seed_identity_refuses_upgrade_without_repurposing_data(string name, string id)
    {
        await using var postgres = new PostgreSqlBuilder("postgres:17").Build(); await postgres.StartAsync(Ct);
        await using var db = Db(postgres.GetConnectionString()); await db.GetService<IMigrator>().MigrateAsync(Previous, Ct);
        var categoryId = Guid.Parse(id);
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO catalog_categories (id,name,normalized_name,scope,is_hidden,is_seeded,revision,created_at,updated_at) VALUES ({categoryId},{name},{name.ToUpperInvariant()},'public',false,false,1,now(),now())", Ct);
        var before = (await db.Database.GetAppliedMigrationsAsync(Ct)).ToArray();
        var error = await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.Database.MigrateAsync(Ct)); Assert.Contains("Songs category", error.Message);
        Assert.Equal(before, (await db.Database.GetAppliedMigrationsAsync(Ct)).ToArray());
        Assert.Equal(name, (await db.Categories.SingleAsync(x => x.Id == categoryId, Ct)).Name);
    }

    [Fact, Trait("Category", "Database")]
    public async Task Downgrade_with_songs_is_refused_without_removing_history_or_content()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:17").Build(); await postgres.StartAsync(Ct);
        await using var db = Db(postgres.GetConnectionString()); await db.GetService<IMigrator>().MigrateAsync("20261127222703_AddSongs", Ct);
        var song = new MediaItem("song", "Retained song"); db.Add(song); await db.SaveChangesAsync(Ct);
        var before = (await db.Database.GetAppliedMigrationsAsync(Ct)).ToArray();
        var error = await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.GetService<IMigrator>().MigrateAsync(Previous, Ct)); Assert.Contains("songs", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(before, (await db.Database.GetAppliedMigrationsAsync(Ct)).ToArray()); Assert.True(await db.MediaItems.AnyAsync(x => x.Id == song.Id, Ct));
    }
}
