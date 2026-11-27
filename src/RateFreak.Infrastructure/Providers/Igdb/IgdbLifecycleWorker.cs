using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace RateFreak.Infrastructure.Providers.Igdb;

public interface IIgdbCatalogReadiness { bool Ready { get; } }

internal sealed class IgdbLifecycleWorker(IServiceScopeFactory scopes, IgdbContentPolicy policy, TimeProvider clock,
    ILogger<IgdbLifecycleWorker> logger) : BackgroundService, IIgdbCatalogReadiness, IHealthCheck
{
    private volatile string? reconciled;
    public bool Ready
    {
        get
        {
            var fingerprint = reconciled;
            if (fingerprint == null) return false;
            try { return fingerprint == policy.Read().Fingerprint; }
            catch (IOException) { return false; }
        }
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default) =>
        Task.FromResult(Ready ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy("IGDB reconciliation required."));

    public override async Task StartAsync(CancellationToken ct)
    {
        await SweepAsync(ct);
        await base.StartAsync(ct);
    }

    protected override Task ExecuteAsync(CancellationToken ct) => Task.WhenAll(
        PeriodicAsync(SweepAsync, false, ct), PeriodicAsync(RefreshAsync, true, ct));

    private async Task PeriodicAsync(Func<CancellationToken, Task> action, bool startImmediately, CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1), clock);
        if (startImmediately) await action(ct);
        while (await timer.WaitForNextTickAsync(ct)) await action(ct);
    }

    private async Task SweepAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            reconciled = await scope.ServiceProvider.GetRequiredService<IgdbLifecycle>().ReconcileAsync(ct);
        }
        catch (Exception e) when (!ct.IsCancellationRequested)
        {
            reconciled = null;
            logger.LogError("IGDB reconciliation failed ({ExceptionType}). Catalog traffic awaits successful cleanup.", e.GetType().Name);
        }
    }

    private async Task RefreshAsync(CancellationToken ct)
    {
        if (!Ready) return;
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<IgdbLifecycle>().RefreshAsync(ct);
        }
        catch (Exception e) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning("IGDB refresh batch failed ({ExceptionType}). Expiry sweeps continue.", e.GetType().Name);
        }
    }
}
