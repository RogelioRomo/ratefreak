using Microsoft.AspNetCore.Identity;
using RateFreak.Infrastructure;
using RateFreak.Infrastructure.Providers.Igdb;

namespace RateFreak.Api.Maintenance;

public static class IgdbMaintenance
{
    public static async Task<int> ExecuteAsync(string[] args)
    {
        string? identity = null;
        var posters = new List<string>();
        var valid = args.Length >= 4 && args[0] == "--igdb-maintenance" && args[1] == "--database-name" && !string.IsNullOrWhiteSpace(args[2]);
        if (valid && args[3] == "--block" && args.Length >= 5)
        {
            identity = args[4];
            valid = IgdbContentPolicy.ValidIdentity(identity) && (args.Length - 5) % 2 == 0;
            for (var i = 5; valid && i < args.Length; i += 2)
            {
                valid = args[i] == "--poster" && IgdbContentPolicy.ValidPoster(args[i + 1]);
                if (valid) posters.Add(args[i + 1]);
            }
        }
        else valid = valid && args.Length == 4 && args[3] is "--terminate" or "--reconcile";
        if (!valid)
        {
            Console.Error.WriteLine("Usage: --igdb-maintenance --database-name <exact-name> (--reconcile | --terminate | --block <game:N> [--poster <filename> ...])");
            return 2;
        }

        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.Services.AddInfrastructure(builder.Configuration);
        builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies();
        using var host = builder.Build();
        await using var scope = host.Services.CreateAsyncScope();
        try
        {
            await scope.ServiceProvider.GetRequiredService<IgdbLifecycle>()
                .MaintainAsync(args[2], identity, args[3] == "--terminate", posters, CancellationToken.None);
            Console.WriteLine("IGDB database and independent policy reconciled. Complete poster-cache cleanup before reopening traffic.");
            if (identity != null) Console.WriteLine("Search-only and other unknown historical poster filenames require explicit --poster arguments.");
            return 0;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"IGDB maintenance failed ({e.GetType().Name}). Keep writers stopped and verify the database, schema and private policy.");
            return 1;
        }
    }
}
