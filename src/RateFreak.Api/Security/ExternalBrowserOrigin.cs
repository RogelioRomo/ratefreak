using Microsoft.Extensions.Options;

namespace RateFreak.Api.Security;

// Only the existing authenticated Next proxy may describe the browser origin.
// Normalize from server configuration, never from an untrusted forwarding header.
internal static class ExternalBrowserOrigin
{
    public static void UseExternalBrowserOrigin(this WebApplication app) => app.Use(async (context, next) =>
    {
        var path = context.Request.Path;
        var external = new[] { "google", "github" }.Any(provider =>
            path.Equals(new PathString($"/api/v1/auth/external/{provider}/start")) ||
            path.Equals(new PathString($"/api/v1/auth/external/{provider}/callback")) ||
            path.Equals(new PathString($"/api/v1/users/me/logins/{provider}/start"))) ||
            path.Equals(new PathString(ExternalAuthentication.CompletePath));
        if (!external || !context.Request.Headers.ContainsKey("X-RateFreak-Browser-Origin"))
        {
            await next(context);
            return;
        }
        var declared = context.Request.Headers["X-RateFreak-Browser-Origin"];
        var configured = context.RequestServices.GetRequiredService<IOptions<ExternalProviderOptions>>().Value.PublicOrigin;
        if (!BrowserProxyIdentity.IsAuthenticated(context) || declared.Count != 1 ||
            !string.Equals(declared[0], configured, StringComparison.Ordinal) ||
            !Uri.TryCreate(configured, UriKind.Absolute, out var origin))
        {
            context.Response.Headers.CacheControl = "no-store";
            await Results.Problem(statusCode: 403, title: "Invalid external authentication origin.").ExecuteAsync(context);
            return;
        }
        var scheme = context.Request.Scheme;
        var host = context.Request.Host;
        context.Request.Scheme = origin.Scheme;
        context.Request.Host = new HostString(origin.Authority);
        try { await next(context); }
        finally { context.Request.Scheme = scheme; context.Request.Host = host; }
    });
}
