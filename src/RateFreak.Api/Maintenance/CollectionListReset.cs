using Microsoft.EntityFrameworkCore;
using RateFreak.Infrastructure.Persistence;

namespace RateFreak.Api.Maintenance;

public sealed record CollectionResetResult(string Database, string SchemaVersion, long Collections, long Memberships, long Fields, bool Cleared);
public static class CollectionListReset
{
    public static async Task<CollectionResetResult> RunAsync(RateFreakDbContext db, string expectedDatabase, bool confirm, CancellationToken ct)
    {
        await db.Database.OpenConnectionAsync(ct);
        if (string.IsNullOrWhiteSpace(expectedDatabase) || !string.Equals(db.Database.GetDbConnection().Database, expectedDatabase, StringComparison.Ordinal)) throw new InvalidOperationException("Configured database does not match the explicitly supplied database name.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(734188290134)", ct);
        var hasFields = await db.Database.SqlQuery<int>($"SELECT count(*)::integer AS \"Value\" FROM information_schema.tables WHERE table_schema = current_schema() AND table_name = 'collection_fields'").SingleAsync(ct);
        if (hasFields != 1) throw new InvalidOperationException("Legacy collection schema is not present. No reset was performed.");
        // Count and clear under one stable table lock, including old writers that
        // do not know the advisory protocol. Operators must still stop writers first.
        await db.Database.ExecuteSqlRawAsync(confirm
            ? "LOCK TABLE collections, collection_items, collection_fields IN ACCESS EXCLUSIVE MODE"
            : "LOCK TABLE collections, collection_items, collection_fields IN SHARE MODE", ct);
        var collections = await db.Database.SqlQuery<long>($"SELECT count(*) AS \"Value\" FROM collections").SingleAsync(ct);
        var memberships = await db.Database.SqlQuery<long>($"SELECT count(*) AS \"Value\" FROM collection_items").SingleAsync(ct);
        var fields = await db.Database.SqlQuery<long>($"SELECT count(*) AS \"Value\" FROM collection_fields").SingleAsync(ct);
        var version = (await db.Database.GetAppliedMigrationsAsync(ct)).LastOrDefault() ?? "none";
        if (confirm)
        {
            await db.Database.ExecuteSqlRawAsync("DELETE FROM collection_items", ct);
            await db.Database.ExecuteSqlRawAsync("DELETE FROM collection_fields", ct);
            await db.Database.ExecuteSqlRawAsync("UPDATE collections SET revision = revision + 1, updated_at = CURRENT_TIMESTAMP", ct);
        }
        await tx.CommitAsync(ct); return new(expectedDatabase, version, collections, memberships, fields, confirm);
    }
    public static async Task<int> ExecuteAsync(string[] args)
    {
        if (args.Length is not (3 or 4) || args[0] != "--reset-test-collection-data" || args[1] != "--database-name" || string.IsNullOrWhiteSpace(args[2]) || args.Length == 4 && args[3] != "--confirm-test-data-loss")
        { Console.Error.WriteLine("Usage: --reset-test-collection-data --database-name <exact-name> [--confirm-test-data-loss]. Stop incompatible writers first. Without confirmation, only counts are reported."); return 2; }
        var builder = Host.CreateApplicationBuilder(); builder.Logging.ClearProviders();
        RateFreak.Infrastructure.DependencyInjection.AddInfrastructure(builder.Services, builder.Configuration);
        using var host = builder.Build(); await using var scope = host.Services.CreateAsyncScope();
        try
        {
            var result = await RunAsync(scope.ServiceProvider.GetRequiredService<RateFreakDbContext>(), args[2], args.Length == 4, CancellationToken.None);
            Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(result)); return 0;
        }
        catch { Console.Error.WriteLine("Collection reset failed. Check the exact database name, legacy schema and stopped writers. No credentials are included in this message."); return 1; }
    }
}
