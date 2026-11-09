using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using RateFreak.Api.Security;
using RateFreak.Application.Identity;

namespace RateFreak.Api.Endpoints;

internal static class ExternalAccountEndpoints
{
    private const string Purpose = "ratefreak:purpose";
    private const string Actor = "ratefreak:actor";
    private const string Stamp = "ratefreak:stamp";
    private const string Operation = "ratefreak:operation";
    private sealed record LifecycleStartRequest(string? Operation);
    public static void MapExternalAccountEndpoints(this WebApplication app)
    {
        var auth = app.MapGroup("/api/v1/auth").WithTags("Authentication");
        auth.MapGet("/providers", (IOptions<ExternalProviderOptions> options) => Results.Ok(new[]
        { new { name = "google", enabled = options.Value.Google.Enabled }, new { name = "github", enabled = options.Value.GitHub.Enabled } }))
            .RequireRateLimiting("external-auth-read");
        auth.MapPost("/external/{provider}/start", (string provider, HttpContext context,
            IOptions<ExternalProviderOptions> options, IExternalAccountService accounts, CancellationToken cancellation) =>
                Start(provider, false, context, options.Value, accounts, cancellation))
            .AddEndpointFilter<AntiforgeryFilter>().RequireRateLimiting("external-auth-start");
        app.MapPost("/api/v1/users/me/logins/{provider}/start", (string provider, HttpContext context,
            IOptions<ExternalProviderOptions> options, IExternalAccountService accounts, CancellationToken cancellation) =>
                Start(provider, true, context, options.Value, accounts, cancellation))
            .RequireAuthorization().AddEndpointFilter<AntiforgeryFilter>().RequireRateLimiting("external-auth-link").WithTags("Authentication");
        app.MapPost("/api/v1/users/me/data/external/{provider}/start", async (string provider,
            LifecycleStartRequest request, HttpContext context, IOptions<ExternalProviderOptions> options,
            IExternalAccountService accounts, CancellationToken cancellation) =>
        {
            if (request.Operation is not ("export" or "delete"))
                return Results.Problem(statusCode: 400, title: "Choose export or delete.");
            var logins = await accounts.ListAsync(Id(context.User), cancellation);
            if (logins is null) return Results.Unauthorized();
            if (logins.HasPassword || !logins.Providers.Contains(provider, StringComparer.Ordinal))
                return Results.Problem(statusCode: 400, title: "Confirm using your current account credentials.");
            return await Start(provider, true, context, options.Value, accounts, cancellation, request.Operation);
        }).RequireAuthorization().AddEndpointFilter<AntiforgeryFilter>()
            .RequireRateLimiting("lifecycle-confirm").WithTags("Authentication");
        auth.MapGet("/external/complete", Complete).RequireRateLimiting("external-auth-read");
        auth.MapGet("/external/pending", async (HttpContext context) =>
        {
            var ticket = await Ticket(context, "pending");
            if (ticket is null) return Results.Unauthorized();
            return Results.Ok(new { requiresRegistration = true, provider = ExternalIdentityReader.Read(ticket.Principal)!.Provider });
        }).RequireRateLimiting("external-auth-read");
        auth.MapPost("/external/register", Register).AddEndpointFilter<AntiforgeryFilter>()
            .RequireRateLimiting("external-auth-register");
        app.MapGet("/api/v1/users/me/logins", async (ClaimsPrincipal principal, IExternalAccountService accounts, CancellationToken cancellation) =>
        {
            var list = await accounts.ListAsync(Id(principal), cancellation);
            return list is null ? Results.Unauthorized() : Results.Ok(list);
        }).RequireAuthorization().RequireRateLimiting("external-auth-link").WithTags("Authentication");
    }

    private static async Task<IResult> Start(string provider, bool link, HttpContext context,
        ExternalProviderOptions options, IExternalAccountService accounts, CancellationToken cancellation, string? operation = null)
    {
        var configured = options.Find(provider);
        if (configured is null) return Results.NotFound();
        if (!configured.Enabled) return Results.Problem(statusCode: 503, title: "External provider is disabled.");
        if (!options.MatchesOrigin(context.Request)) return Results.Problem(statusCode: 400, title: "Invalid authentication origin.");
        if (!link && context.User.Identity?.IsAuthenticated == true)
            return Results.Problem(statusCode: 409, title: "Sign out before starting another sign-in.");
        var properties = new AuthenticationProperties { RedirectUri = ExternalAuthentication.CompletePath,
            ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(5), IsPersistent = false };
        properties.Items[Purpose] = operation is not null ? "reauthenticate" : link ? "link" : "login";
        if (operation is not null) properties.Items[Operation] = operation;
        properties.Items["provider"] = provider;
        if (link)
        {
            var id = Id(context.User);
            var stamp = await accounts.StampAsync(id, cancellation);
            if (stamp is null) return Results.Unauthorized();
            properties.Items[Actor] = id.ToString();
            properties.Items[Stamp] = stamp;
        }
        properties.Items["flow"] = (await accounts.BeginFlowAsync(properties.ExpiresUtc!.Value, cancellation)).ToString();
        await context.SignOutAsync(IdentityConstants.ExternalScheme);
        return Results.Challenge(properties, [provider]);
    }

    private static async Task<IResult> Complete(HttpContext context, IExternalAccountService accounts, CancellationToken cancellation)
    {
        var ticket = await Ticket(context);
        await context.SignOutAsync(IdentityConstants.ExternalScheme);
        if (ticket is null) return Results.Unauthorized();
        if (!Guid.TryParse(ticket.Properties!.Items.Get("flow"), out var flow) ||
            !await accounts.ConsumeFlowAsync(flow, cancellation)) return Results.Unauthorized();
        var identity = ExternalIdentityReader.Read(ticket.Principal)!;
        var purpose = ticket.Properties!.Items.Get(Purpose);
        if (purpose is "link" or "reauthenticate")
        {
            if (!Guid.TryParse(ticket.Properties.Items.Get(Actor), out var actor) ||
                context.User.Identity?.IsAuthenticated != true || Id(context.User) != actor ||
                context.User.FindFirstValue("AspNet.Identity.SecurityStamp") != ticket.Properties.Items.Get(Stamp))
                return Results.Unauthorized();
            if (purpose == "reauthenticate")
            {
                var operation = ticket.Properties.Items.Get(Operation);
                if (operation is not ("export" or "delete")) return Results.Unauthorized();
                var lifecycle = context.RequestServices.GetRequiredService<IAccountDataService>();
                // The lifecycle service rechecks stamp, account and existing provider subject
                // under the account lock in the same transaction that issues the grant.
                var grant = await lifecycle.IssueExternalGrantAsync(actor, ticket.Properties.Items.Get(Stamp)!,
                    operation, identity, cancellation);
                if (grant is null) return Results.Unauthorized();
                AccountDataCookies.Set(context, grant);
                return Results.Ok(new { reauthenticated = true, operation, provider = identity.Provider });
            }
            var status = await accounts.LinkAsync(actor, ticket.Properties.Items.Get(Stamp)!, identity, cancellation);
            return status switch
            {
                AccountStatus.Success => Results.Ok(new { linked = true, provider = identity.Provider }),
                AccountStatus.Unauthenticated => Results.Unauthorized(),
                _ => Conflict()
            };
        }
        if (purpose != "login" || context.User.Identity?.IsAuthenticated == true) return Results.Unauthorized();
        var login = await accounts.LoginAsync(identity, cancellation);
        if (login is not null) return login.Status == AccountStatus.Success ? Results.Ok(login.Profile) : Results.Unauthorized();
        if (!identity.EmailVerified || !AccountInput.ValidEmail(identity.Email))
            return Results.Problem(statusCode: 400, title: "A verified email from the provider is required to create an account.");
        if (!await accounts.EmailAvailableAsync(identity.Email!, cancellation)) return Conflict();
        ticket.Properties.Items[Purpose] = "pending";
        // Keep the original expiry: inspecting/completing a ticket never renews it.
        await context.SignInAsync(IdentityConstants.ExternalScheme, ticket.Principal!, ticket.Properties);
        return Results.Ok(new { requiresRegistration = true, provider = identity.Provider });
    }

    private static async Task<IResult> Register(ExternalRegistrationRequest request, HttpContext context,
        IExternalAccountService accounts, CancellationToken cancellation)
    {
        var ticket = await Ticket(context, "pending");
        if (ticket is null || context.User.Identity?.IsAuthenticated == true) return Results.Unauthorized();
        var result = await accounts.RegisterAsync(ExternalIdentityReader.Read(ticket.Principal)!, request, cancellation);
        if (result.Status == AccountStatus.Success)
        {
            await context.SignOutAsync(IdentityConstants.ExternalScheme);
            return Results.Json(result.Profile, statusCode: 201);
        }
        if (result.Status == AccountStatus.Invalid) return Results.ValidationProblem(result.Errors!);
        // Conflicts allow handle retries. Replays remain harmless because uniqueness
        // and the transactional service reject the already claimed provider identity.
        return Conflict();
    }

    private static async Task<AuthenticateResult?> Ticket(HttpContext context, string? purpose = null)
    {
        var result = await context.AuthenticateAsync(IdentityConstants.ExternalScheme);
        if (!result.Succeeded || result.Properties?.ExpiresUtc is not { } expiry || expiry <= DateTimeOffset.UtcNow ||
            ExternalIdentityReader.Read(result.Principal) is not { } identity ||
            result.Properties.Items.Get("provider") != identity.Provider ||
            purpose is not null && result.Properties.Items.Get(Purpose) != purpose) return null;
        return result;
    }
    private static string? Get(this IDictionary<string, string?> items, string key) => items.TryGetValue(key, out var value) ? value : null;
    private static Guid Id(ClaimsPrincipal principal) => Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;
    private static IResult Conflict() => Results.Problem(statusCode: 409,
        title: "Account or provider login is unavailable. Sign in to your existing account to link a provider, or choose another handle.");
}
