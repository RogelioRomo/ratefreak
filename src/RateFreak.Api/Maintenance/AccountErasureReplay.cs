using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RateFreak.Application.Identity;
using RateFreak.Infrastructure.Persistence;

namespace RateFreak.Api.Maintenance;

public static class AccountErasureReplay
{
    public static async Task RunAsync(RateFreakDbContext db, IAccountErasureMaintenance maintenance,
        string expectedDatabase, CancellationToken ct)
    {
        await db.Database.OpenConnectionAsync(ct);
        if (string.IsNullOrWhiteSpace(expectedDatabase) ||
            !string.Equals(db.Database.GetDbConnection().Database, expectedDatabase, StringComparison.Ordinal))
            throw new InvalidOperationException("Configured database does not match the explicitly supplied database name.");
        await maintenance.ReplayAsync(ct);
    }

    public static async Task<int> ExecuteAsync(string[] args)
    {
        if (args.Length != 4 || args[0] != "--replay-account-erasures" || args[1] != "--database-name" ||
            string.IsNullOrWhiteSpace(args[2]) || args[3] != "--confirm-erasure-replay")
        {
            Console.Error.WriteLine("Usage: --replay-account-erasures --database-name <exact-name> --confirm-erasure-replay. Stop all writers and use the current independent erasure journal.");
            return 2;
        }

        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            EnvironmentName = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
                ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
        });
        builder.Logging.ClearProviders();
        RateFreak.Infrastructure.DependencyInjection.AddInfrastructure(builder.Services, builder.Configuration);
        builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies();
        using var host = builder.Build();
        await using var scope = host.Services.CreateAsyncScope();
        try
        {
            await RunAsync(scope.ServiceProvider.GetRequiredService<RateFreakDbContext>(),
                scope.ServiceProvider.GetRequiredService<IAccountErasureMaintenance>(), args[2], CancellationToken.None);
            await scope.ServiceProvider.GetRequiredService<RateFreak.Infrastructure.Artwork.ArtworkCleanupService>()
                .RunOnceAsync(CancellationToken.None);
            Console.WriteLine("Account erasure replay complete. Verify database/artwork cleanup before reopening traffic.");
            return 0;
        }
        catch
        {
            Console.Error.WriteLine("Account erasure replay failed. Keep traffic stopped; check the exact database name, current journal and database/storage access.");
            return 1;
        }
    }
}
