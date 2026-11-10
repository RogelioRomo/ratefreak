using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace RateFreak.Api.Security;

internal static class BrowserProxyIdentity
{
    private static readonly object ClientAddress = new();

    public static bool IsAuthenticated(HttpContext context) => context.Items[ClientAddress] is IPAddress;

    public static string Partition(HttpContext context)
    {
        var address = context.Items[ClientAddress] as IPAddress ?? context.Connection.RemoteIpAddress;
        return address is null ? "unknown" :
            (address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address).ToString();
    }

    public static void UseBrowserProxyIdentity(this WebApplication app) => app.Use(async (context, next) =>
    {
        var options = context.RequestServices.GetRequiredService<IOptions<BrowserProxyOptions>>().Value;
        var secret = options.SharedSecret;
        var keys = context.Request.Headers["X-RateFreak-Proxy-Key"];
        var addresses = context.Request.Headers["X-RateFreak-Client-IP"];
        if (string.IsNullOrEmpty(secret) || (keys.Count == 0 && addresses.Count == 0))
        {
            await next(context);
            return;
        }

        if (keys.Count != 1 || addresses.Count != 1 || keys[0]?.Length != secret.Length ||
            !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(keys[0]!), Encoding.UTF8.GetBytes(secret)) ||
            !IPAddress.TryParse(addresses[0], out var address))
        {
            context.Response.Headers.CacheControl = "no-store";
            await Results.Problem(statusCode: 403, title: "Invalid proxy attribution.").ExecuteAsync(context);
            return;
        }

        // Attribution affects abuse budgets only, never the authenticated principal.
        context.Items[ClientAddress] = address;
        // The operator's fixed HTTPS origin describes the authenticated browser hop.
        // Never derive scheme/host from caller-supplied forwarding headers.
        var scheme = context.Request.Scheme;
        var host = context.Request.Host;
        if (options.AllowPrivateHttp && !context.Request.IsHttps)
        {
            var origin = new Uri(options.PublicOrigin!, UriKind.Absolute);
            context.Request.Scheme = origin.Scheme;
            context.Request.Host = new HostString(origin.Authority);
        }
        try { await next(context); }
        finally { context.Request.Scheme = scheme; context.Request.Host = host; }
    });
}
