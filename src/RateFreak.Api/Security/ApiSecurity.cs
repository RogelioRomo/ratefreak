using System.Security.Claims;
using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace RateFreak.Api.Security;

internal static class ApiSecurity
{
    public static void AddApiSecurity(this WebApplicationBuilder builder)
    {
        var development = builder.Environment.IsDevelopment();
        builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 64 * 1024);
        builder.Services.AddOptions<SessionOptions>().Bind(builder.Configuration.GetSection("Authentication"))
            .Validate(options => development
                ? string.IsNullOrWhiteSpace(options.DataProtectionKeysPath) || Path.IsPathFullyQualified(options.DataProtectionKeysPath)
                : !string.IsNullOrWhiteSpace(options.DataProtectionKeysPath) && Path.IsPathFullyQualified(options.DataProtectionKeysPath),
                "Authentication:DataProtectionKeysPath must be an absolute durable key directory outside Development.")
            .ValidateOnStart();
        builder.Services.AddDataProtection().SetApplicationName("RateFreak:" + builder.Environment.EnvironmentName);
        // Resolve configuration after the host has applied environment/test overrides.
        builder.Services.AddOptions<KeyManagementOptions>()
            .Configure<IOptions<SessionOptions>, IHostEnvironment, ILoggerFactory>((keys, session, environment, logging) =>
            {
                var path = string.IsNullOrWhiteSpace(session.Value.DataProtectionKeysPath)
                    ? Path.Combine(environment.ContentRootPath, ".local", "keys") : session.Value.DataProtectionKeysPath;
                var directory = new DirectoryInfo(path);
                if (!directory.Exists)
                {
                    if (OperatingSystem.IsWindows()) directory.Create();
                    else Directory.CreateDirectory(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
                }
                keys.XmlRepository = new FileSystemXmlRepository(directory, logging);
            });

        builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies();
        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy("moderation", p => p.RequireAuthenticatedUser().AddRequirements(new ModerationRequirement(false)));
            options.AddPolicy("admin", p => p.RequireAuthenticatedUser().AddRequirements(new ModerationRequirement(true)));
        });
        builder.Services.AddScoped<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, ModerationAuthorization>();
        builder.Services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = development ? "ratefreak.session" : "__Host-ratefreak.session";
            options.Cookie.HttpOnly = true;
            options.Cookie.Path = "/";
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = development ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
            options.ExpireTimeSpan = TimeSpan.FromHours(8);
            options.SlidingExpiration = false;
            options.Events.OnRedirectToLogin = context => Status(context, StatusCodes.Status401Unauthorized);
            options.Events.OnRedirectToAccessDenied = context => Status(context, StatusCodes.Status403Forbidden);
        });
        builder.Services.Configure<SecurityStampValidatorOptions>(options => options.ValidationInterval = TimeSpan.Zero);
        builder.Services.AddAntiforgery(options =>
        {
            options.HeaderName = "X-CSRF-TOKEN";
            options.Cookie.Name = development ? "ratefreak.csrf" : "__Host-ratefreak.csrf";
            options.Cookie.HttpOnly = true;
            options.Cookie.Path = "/";
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = development ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
        });
        builder.Services.AddRateLimiter(options =>
        {
            options.OnRejected = async (context, cancellationToken) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retry))
                    context.HttpContext.Response.Headers.RetryAfter = Math.Ceiling(retry.TotalSeconds).ToString(CultureInfo.InvariantCulture);
                await Results.Problem(statusCode: StatusCodes.Status429TooManyRequests,
                    title: "Too many requests. Please try again later.").ExecuteAsync(context.HttpContext);
            };
            AddLimit(options, "external-auth-start", 10, TimeSpan.FromMinutes(1));
            AddLimit(options, "external-auth-read", 60, TimeSpan.FromMinutes(1));
            AddLimit(options, "external-auth-register", 5, TimeSpan.FromHours(1));
            AddAccountLimit(options, "external-auth-link", 10);
            AddLimit(options, "registration", 5, TimeSpan.FromHours(1));
            AddLimit(options, "login", 10, TimeSpan.FromMinutes(1));
            AddLimit(options, "csrf", 30, TimeSpan.FromMinutes(1));
            AddLimit(options, "catalog", 60, TimeSpan.FromMinutes(1));
            AddLimit(options, "external-search", 20, TimeSpan.FromMinutes(1));
            AddLimit(options, "social-read", 60, TimeSpan.FromMinutes(1));
            AddLimit(options, "profile-read", 60, TimeSpan.FromMinutes(1));
            AddAccountLimit(options, "profile-write", 10);
            AddAccountLimit(options, "social-write", 30);
            AddAccountLimit(options, "moderation-read", 60);
            AddAccountLimit(options, "moderation-write", 30);
            AddAccountLimit(options, "role-write", 10);
            options.AddPolicy("reporting", context => RateLimitPartition.GetFixedWindowLimiter(
                context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous", _ => new FixedWindowRateLimiterOptions
                { PermitLimit = 5, Window = TimeSpan.FromHours(1), QueueLimit = 0, AutoReplenishment = true }));
            AddAccountLimit(options, "personal-read", 60);
            AddAccountLimit(options, "personal-write", 30);
            options.AddPolicy("media-import", context => RateLimitPartition.GetFixedWindowLimiter(
                context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous", _ => new FixedWindowRateLimiterOptions
                { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true }));
        });
    }

    private static Task Status(Microsoft.AspNetCore.Authentication.RedirectContext<Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationOptions> context, int status)
    {
        context.Response.StatusCode = status;
        return Task.CompletedTask;
    }

    private static void AddAccountLimit(RateLimiterOptions options, string name, int permits) =>
        options.AddPolicy(name, context => RateLimitPartition.GetFixedWindowLimiter(
            context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous", _ => new FixedWindowRateLimiterOptions
            { PermitLimit = permits, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true }));

    private static void AddLimit(RateLimiterOptions options, string name, int permits, TimeSpan window) =>
        options.AddPolicy(name, context => RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
            { PermitLimit = permits, Window = window, QueueLimit = 0, AutoReplenishment = true }));
}
