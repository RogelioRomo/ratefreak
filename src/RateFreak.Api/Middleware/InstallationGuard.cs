using System.Data.Common;
using RateFreak.Application.Installation;

namespace RateFreak.Api.Middleware;

internal static class InstallationGuard
{
    public static void UseInstallationGuard(this WebApplication app) => app.Use(async (context, next) =>
    {
        if (!context.Request.Path.StartsWithSegments("/api")) { await next(context); return; }
        var path = context.Request.Path.Value?.TrimEnd('/');
        var allowed = context.Request.Method == "GET" &&
            (string.Equals(path,"/api/v1/setup/status",StringComparison.OrdinalIgnoreCase) || string.Equals(path,"/api/v1/auth/csrf",StringComparison.OrdinalIgnoreCase)) ||
            context.Request.Method == "POST" && string.Equals(path,"/api/v1/setup",StringComparison.OrdinalIgnoreCase);
        if (allowed) { context.Response.Headers.CacheControl = "no-store"; await next(context); return; }
        InstallationStatus? status;
        try { status = await context.RequestServices.GetRequiredService<IInstallationService>().StatusAsync(context.RequestAborted); }
        catch (Exception exception) when (exception is DbException or InvalidOperationException { InnerException: DbException }) { status = null; }
        if (status is null || status.Required)
        {
            context.Response.Headers.CacheControl = "no-store";
            await Results.Problem(statusCode: 503, title: "Installation setup is required or unavailable.").ExecuteAsync(context);
            return;
        }
        await next(context);
    });
}
