using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using RateFreak.Domain.Catalog;
using RateFreak.Infrastructure.Authentication;
using RateFreak.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace RateFreak.IntegrationTests;

public sealed class CustomCatalogMigrationTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Existing_catalog_backfill_preserves_identity_and_mappings()
    {
        await using var pg = new PostgreSqlBuilder("postgres:17").Build();
        await pg.StartAsync(Ct);
        await using var db = new RateFreakDbContext(new DbContextOptionsBuilder<RateFreakDbContext>().UseNpgsql(pg.GetConnectionString()).Options);
        await db.GetService<IMigrator>().MigrateAsync("20261106225209_AddInstallationSetup", Ct);
        var id = Guid.NewGuid(); var mappingId = Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO media_items (id, media_type, title, metadata, created_at, updated_at) VALUES ({id}, 'movie', 'Preserved movie', '{{}}', now(), now())", Ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO media_external_ids (id, media_item_id, provider, namespace, external_id) VALUES ({mappingId}, {id}, 'tmdb', 'movie', '11')", Ct);
        await db.Database.MigrateAsync(Ct); await db.Database.MigrateAsync(Ct);
        var item = await db.MediaItems.SingleAsync(x => x.Id == id, Ct);
        Assert.Equal("public", item.Visibility); Assert.Equal("system", item.Provenance); Assert.Null(item.CreatorUserId);
        Assert.Equal("Movies", (await db.Categories.SingleAsync(x => x.Id == item.CategoryId, Ct)).Name);
        Assert.Equal(mappingId, (await db.MediaExternalIds.SingleAsync(Ct)).Id);
        Assert.False(db.Database.HasPendingModelChanges());
    }

    [Fact]
    public async Task Category_uniqueness_is_scoped_and_owner_deletion_is_restricted()
    {
        await using var pg = new PostgreSqlBuilder("postgres:17").Build(); await pg.StartAsync(Ct);
        await using var db = new RateFreakDbContext(new DbContextOptionsBuilder<RateFreakDbContext>().UseNpgsql(pg.GetConnectionString()).Options);
        await db.Database.MigrateAsync(Ct);
        var a = new AccountUser { Id = Guid.NewGuid(), UserName = "owner_a", NormalizedUserName = "OWNER_A", Email = "a@test.example", NormalizedEmail = "A@TEST.EXAMPLE", DisplayName = "A" };
        var b = new AccountUser { Id = Guid.NewGuid(), UserName = "owner_b", NormalizedUserName = "OWNER_B", Email = "b@test.example", NormalizedEmail = "B@TEST.EXAMPLE", DisplayName = "B" };
        db.AddRange(a, b); await db.SaveChangesAsync(Ct);
        db.AddRange(new Category("Flowers", "public", a.Id), new Category("Flowers", "private", a.Id), new Category("Flowers", "private", b.Id));
        await db.SaveChangesAsync(Ct);
        var duplicate = new Category(" flowers ", "public", b.Id); db.Add(duplicate);
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(Ct)); Assert.Equal(PostgresErrorCodes.UniqueViolation, ((PostgresException)error.InnerException!).SqlState);
        db.ChangeTracker.Clear();
        var category = await db.Categories.FirstAsync(x => x.Scope == "private" && x.OwnerUserId == a.Id, Ct);
        db.Add(MediaItem.CreateCustom(a.Id, "Hydrangea", null, category.Id)); await db.SaveChangesAsync(Ct);
        await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM users WHERE id = {a.Id}", Ct));
        Assert.True(await db.MediaItems.AnyAsync(x => x.CreatorUserId == a.Id, Ct));
    }
}
