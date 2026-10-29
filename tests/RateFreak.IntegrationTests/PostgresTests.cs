using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RateFreak.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace RateFreak.IntegrationTests;

public sealed class PostgresTests
{
    [Fact]
    [Trait("Category", "Database")]
    public async Task Readiness_requires_explicit_migration_and_migration_is_repeatable()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:17").Build();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        await using var factory = new ApiFactory(connectionString: postgres.GetConnectionString());
        using var client = factory.CreateClient();

        // Starting the API must not create a schema or mark it ready.
        Assert.Equal(HttpStatusCode.ServiceUnavailable,
            (await client.GetAsync("/health/ready", TestContext.Current.CancellationToken)).StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<RateFreakDbContext>();
        Assert.NotEmpty(await context.Database.GetPendingMigrationsAsync(TestContext.Current.CancellationToken));
        Assert.False(context.Database.HasPendingModelChanges());

        await context.Database.MigrateAsync(TestContext.Current.CancellationToken);
        await context.Database.MigrateAsync(TestContext.Current.CancellationToken);
        Assert.Empty(await context.Database.GetPendingMigrationsAsync(TestContext.Current.CancellationToken));
        var applied = await context.Database.GetAppliedMigrationsAsync(TestContext.Current.CancellationToken);
        Assert.Contains("20261028000100_InitialFoundation", applied);
        Assert.Contains("20261029163157_AddIdentityAccounts", applied);
        Assert.Empty(await context.Users.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/ready", TestContext.Current.CancellationToken)).StatusCode);
    }
}
