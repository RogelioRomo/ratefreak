using Microsoft.EntityFrameworkCore;
using RateFreak.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace RateFreak.IntegrationTests;

public sealed class AccountDatabase : IAsyncLifetime
{
    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:17").Build();
    public string ConnectionString => postgres.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        await using var context = new RateFreakDbContext(new DbContextOptionsBuilder<RateFreakDbContext>()
            .UseNpgsql(ConnectionString).Options);
        await context.Database.MigrateAsync(TestContext.Current.CancellationToken);
        // Domain/account fixtures represent an initialized installation. Setup tests use
        // separate disposable databases and exercise the actual fresh-install guard.
        await context.Database.ExecuteSqlRawAsync("UPDATE installation_state SET completed_at = CURRENT_TIMESTAMP WHERE id = 1", TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync() => await postgres.DisposeAsync();
}
