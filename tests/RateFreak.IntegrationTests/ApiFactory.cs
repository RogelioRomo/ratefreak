using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using RateFreak.Application.Installation;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace RateFreak.IntegrationTests;

public sealed class ApiFactory(string environment = "Production", string? connectionString = null,
    string? dataProtectionKeysPath = null, Action<IServiceCollection>? configureServices = null, string? setupSecret = null)
    : WebApplicationFactory<Program>
{
    private readonly string keyDirectory = dataProtectionKeysPath
        ?? Path.Combine(Path.GetTempPath(), "ratefreak-test-keys-" + Guid.NewGuid());

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        if (dataProtectionKeysPath is null && Directory.Exists(keyDirectory))
            Directory.Delete(keyDirectory, recursive: true);
    }

    private sealed class CompletedInstallation : IInstallationService
    {
        public Task<InstallationStatus?> StatusAsync(CancellationToken ct) => Task.FromResult<InstallationStatus?>(new(false,false));
        public Task<SetupResult> CompleteAsync(SetupRequest request, CancellationToken ct) => throw new NotSupportedException();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environment);
        // Database-free contract tests isolate their endpoint behavior. Real setup
        // tests always supply PostgreSQL and never replace the production guard.
        if (connectionString is null) builder.ConfigureTestServices(services => services.AddScoped<IInstallationService, CompletedInstallation>());
        if (configureServices is not null) builder.ConfigureServices(configureServices);
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Installation:SetupSecret"] = setupSecret,
                ["Authentication:DataProtectionKeysPath"] = keyDirectory,
                ["ConnectionStrings:Default"] = connectionString ??
                    "Host=127.0.0.1;Port=1;Database=unavailable;Username=test;Password=test;Timeout=1;Pooling=false"
            }));
    }
}
