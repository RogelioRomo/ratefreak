using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace RateFreak.Api.Security;

internal static class ExternalAuthentication
{
    public const string CompletePath = "/api/v1/auth/external/complete";
    public static void AddExternalAuthentication(this WebApplicationBuilder builder)
    {
        var development = builder.Environment.IsDevelopment();
        // Framework remote-error logs include untrusted error descriptions, and
        // trace logs include full authorization URLs. Keep these out of all sinks.
        builder.Logging.AddFilter("Microsoft.AspNetCore.Authentication.Google", LogLevel.None);
        builder.Logging.AddFilter("Microsoft.AspNetCore.Authentication.OAuth", LogLevel.None);
        builder.Services.AddOptions<ExternalProviderOptions>().Bind(builder.Configuration.GetSection(ExternalProviderOptions.Section))
            .Validate(options => options.IsValid(development), "External sign-in configuration is invalid: check enabled credentials and public origin.")
            .ValidateOnStart();
        builder.Services.AddAuthentication().AddGoogle("google", _ => { }).AddOAuth("github", _ => { });
        builder.Services.AddOptions<GoogleOptions>("google").Configure<IOptions<ExternalProviderOptions>>((options, configured) =>
        {
            Configure(options, configured.Value.Google, "google", development);
            options.Events.OnCreatingTicket = ExternalIdentityReader.Google;
        });
        builder.Services.AddOptions<OAuthOptions>("github").Configure<IOptions<ExternalProviderOptions>>((options, configured) =>
        {
            Configure(options, configured.Value.GitHub, "github", development);
            options.AuthorizationEndpoint = "https://github.com/login/oauth/authorize";
            options.TokenEndpoint = "https://github.com/login/oauth/access_token";
            options.UserInformationEndpoint = "https://api.github.com/user";
            options.Scope.Add("read:user");
            options.Scope.Add("user:email");
            options.Events.OnCreatingTicket = ExternalIdentityReader.GitHub;
        });
        builder.Services.ConfigureExternalCookie(options =>
        {
            options.Cookie.Name = development ? "ratefreak.external" : "__Host-ratefreak.external";
            options.Cookie.HttpOnly = true;
            options.Cookie.Path = "/";
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = development ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
            options.ExpireTimeSpan = TimeSpan.FromMinutes(5);
            options.SlidingExpiration = false;
        });
        builder.Services.AddSingleton(PartitionedRateLimiter.Create<HttpContext, string>(context =>
            RateLimitPartition.GetFixedWindowLimiter(BrowserProxyIdentity.Partition(context), _ =>
                new FixedWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 })));
    }
    private static void Configure(OAuthOptions options, ExternalProvider configured, string provider, bool development)
    {
        options.ClientId = configured.Enabled ? configured.ClientId! : "disabled";
        options.ClientSecret = configured.Enabled ? configured.ClientSecret! : "disabled";
        options.SignInScheme = IdentityConstants.ExternalScheme;
        options.CallbackPath = $"/api/v1/auth/external/{provider}/callback";
        options.UsePkce = true;
        options.SaveTokens = false;
        options.RemoteAuthenticationTimeout = TimeSpan.FromMinutes(5);
        options.BackchannelTimeout = TimeSpan.FromSeconds(10);
        options.BackchannelHttpHandler = new BoundedOAuthHandler();
        options.CorrelationCookie.Name = $".AspNetCore.Correlation.{provider}.";
        options.CorrelationCookie.Path = options.CallbackPath;
        options.CorrelationCookie.HttpOnly = true;
        options.CorrelationCookie.SameSite = SameSiteMode.Lax;
        options.CorrelationCookie.SecurePolicy = development ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
        options.Events.OnRemoteFailure = async context =>
        {
            context.HandleResponse();
            await context.HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
            await Results.Problem(statusCode: 400, title: "External sign-in could not be completed. Please start again.")
                .ExecuteAsync(context.HttpContext);
        };
    }
    public static void UseExternalAuthenticationGuard(this WebApplication app) => app.Use(async (context, next) =>
    {
        // PathString matches case-insensitively in the OAuth middleware. The guard
        // must use identical matching, including before disabled handlers run.
        var path = context.Request.Path;
        var provider = path.Equals(new PathString("/api/v1/auth/external/google/callback")) ? "google" :
            path.Equals(new PathString("/api/v1/auth/external/github/callback")) ? "github" : null;
        if (provider is not null)
        {
            var configured = context.RequestServices.GetRequiredService<IOptions<ExternalProviderOptions>>().Value;
            if (configured.Find(provider)?.Enabled != true)
            { await Results.Problem(statusCode: 503, title: "External provider is disabled.").ExecuteAsync(context); return; }
            if (!configured.MatchesOrigin(context.Request))
            { await Results.Problem(statusCode: 400, title: "Invalid authentication origin.").ExecuteAsync(context); return; }
            using var lease = context.RequestServices.GetRequiredService<PartitionedRateLimiter<HttpContext>>().AttemptAcquire(context);
            if (!lease.IsAcquired)
            {
                context.Response.Headers.RetryAfter = "60";
                await Results.Problem(statusCode: 429, title: "Too many authentication requests.").ExecuteAsync(context); return;
            }
        }
        await next(context);
    });
}
