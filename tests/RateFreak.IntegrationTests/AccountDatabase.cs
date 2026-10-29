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
    }

    public async ValueTask DisposeAsync() => await postgres.DisposeAsync();
}
