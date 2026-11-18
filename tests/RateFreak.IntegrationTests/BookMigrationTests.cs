using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RateFreak.Domain.Catalog;
using RateFreak.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace RateFreak.IntegrationTests;

public sealed class BookMigrationTests
{
    private const string Previous = "20261110014234_AddProfileModeration";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static RateFreakDbContext Db(string connection) => new(new DbContextOptionsBuilder<RateFreakDbContext>().UseNpgsql(connection).Options);

    [Fact, Trait("Category", "Database")]
    public async Task Books_upgrade_preserves_existing_catalog_and_empty_downgrade_is_repeatable()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:17").Build(); await postgres.StartAsync(Ct);
        await using var db = Db(postgres.GetConnectionString()); await db.GetService<IMigrator>().MigrateAsync(Previous, Ct);
        var movie = new MediaItem("movie", "Preserved movie"); db.Add(movie); await db.SaveChangesAsync(Ct);
        await db.Database.MigrateAsync(Ct); Assert.False(db.Database.HasPendingModelChanges());
        var books = await db.Categories.SingleAsync(x => x.Id == Guid.Parse("f11e0000-0000-0000-0000-000000000005"), Ct);
        Assert.Equal("Books", books.Name); Assert.True(books.IsSeeded); Assert.Equal("Preserved movie", (await db.MediaItems.SingleAsync(Ct)).Title);
        await db.GetService<IMigrator>().MigrateAsync(Previous, Ct); await db.Database.MigrateAsync(Ct); await db.Database.MigrateAsync(Ct);
        Assert.True(await db.MediaItems.AnyAsync(x => x.Id == movie.Id, Ct)); Assert.Single(await db.Categories.Where(x => x.NormalizedName == "BOOKS").ToListAsync(Ct));
    }

    [Theory, Trait("Category", "Database")]
    [InlineData("Books", "ca110000-0000-0000-0000-000000000005")]
    [InlineData("Existing category", "f11e0000-0000-0000-0000-000000000005")]
    public async Task Existing_public_name_or_seed_identity_refuses_upgrade_without_repurposing_data(string name, string id)
    {
        await using var postgres = new PostgreSqlBuilder("postgres:17").Build(); await postgres.StartAsync(Ct);
        await using var db = Db(postgres.GetConnectionString()); await db.GetService<IMigrator>().MigrateAsync(Previous, Ct);
        var categoryId = Guid.Parse(id);
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO catalog_categories (id,name,normalized_name,scope,is_hidden,is_seeded,revision,created_at,updated_at) VALUES ({categoryId},{name},{name.ToUpperInvariant()},'public',false,false,1,now(),now())", Ct);
        var before = (await db.Database.GetAppliedMigrationsAsync(Ct)).ToArray();
        var error = await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.Database.MigrateAsync(Ct)); Assert.Contains("Books category", error.Message);
        Assert.Equal(before, (await db.Database.GetAppliedMigrationsAsync(Ct)).ToArray());
        Assert.Equal(name, (await db.Categories.SingleAsync(x => x.Id == categoryId, Ct)).Name);
    }

    [Fact, Trait("Category", "Database")]
    public async Task Downgrade_with_books_is_refused_without_removing_history_or_content()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:17").Build(); await postgres.StartAsync(Ct);
        await using var db = Db(postgres.GetConnectionString()); await db.Database.MigrateAsync(Ct);
        var book = new MediaItem("book", "Retained book"); db.Add(book); await db.SaveChangesAsync(Ct);
        var before = (await db.Database.GetAppliedMigrationsAsync(Ct)).ToArray();
        var error = await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.GetService<IMigrator>().MigrateAsync(Previous, Ct)); Assert.Contains("books", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(before, (await db.Database.GetAppliedMigrationsAsync(Ct)).ToArray()); Assert.True(await db.MediaItems.AnyAsync(x => x.Id == book.Id, Ct));
    }
}
