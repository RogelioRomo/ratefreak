using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RateFreak.Domain.Catalog;
using RateFreak.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace RateFreak.IntegrationTests;

public sealed class VideoGamesMigrationTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    [Fact]
    public async Task Downgrade_refuses_games_without_changing_records_history_or_constraint()
    {
        await using var postgres=new PostgreSqlBuilder("postgres:17").Build();await postgres.StartAsync(Ct);
        await using var db=new RateFreakDbContext(new DbContextOptionsBuilder<RateFreakDbContext>().UseNpgsql(postgres.GetConnectionString()).Options);
        await db.Database.MigrateAsync(Ct);
        var game=new MediaItem("video_game","Preserved game");db.Add(game);await db.SaveChangesAsync(Ct);
        var history=(await db.Database.GetAppliedMigrationsAsync(Ct)).ToArray();
        var error=await Assert.ThrowsAsync<Npgsql.PostgresException>(()=>db.GetService<IMigrator>().MigrateAsync("20261030211018_AddUserFollows",Ct));
        Assert.Contains("video games exist",error.Message,StringComparison.OrdinalIgnoreCase);
        Assert.Equal(history,(await db.Database.GetAppliedMigrationsAsync(Ct)).ToArray());Assert.True(await db.MediaItems.AnyAsync(x=>x.Id==game.Id,Ct));
        // The widened constraint remains usable after a rejected downgrade.
        db.Add(new MediaItem("video_game","Another game"));await db.SaveChangesAsync(Ct);
    }
    [Fact]
    public async Task Safe_downgrade_and_reapply_preserve_existing_catalog_and_follows_schema()
    {
        await using var postgres=new PostgreSqlBuilder("postgres:17").Build();await postgres.StartAsync(Ct);
        await using var db=new RateFreakDbContext(new DbContextOptionsBuilder<RateFreakDbContext>().UseNpgsql(postgres.GetConnectionString()).Options);
        await db.Database.MigrateAsync(Ct);
        var movie=new MediaItem("movie","Preserved movie");var album=new MediaItem("album","Preserved album");db.AddRange(movie,album);await db.SaveChangesAsync(Ct);
        await db.GetService<IMigrator>().MigrateAsync("20261030211018_AddUserFollows",Ct);
        Assert.Equal(2,await db.MediaItems.CountAsync(Ct));
        await db.Database.MigrateAsync(Ct);await db.Database.MigrateAsync(Ct);
        Assert.True(await db.MediaItems.AnyAsync(x=>x.Id==movie.Id,Ct));Assert.True(await db.MediaItems.AnyAsync(x=>x.Id==album.Id,Ct));
        Assert.Empty(await db.UserFollows.ToListAsync(Ct));Assert.False(db.Database.HasPendingModelChanges());
        Assert.Single(await db.Database.GetAppliedMigrationsAsync(Ct),x=>x.EndsWith("_AddVideoGames",StringComparison.Ordinal));
    }
}
