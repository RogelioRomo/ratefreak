using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using RateFreak.Api.Security;
using RateFreak.Application.Identity;
using RateFreak.Infrastructure.Authentication;
namespace RateFreak.Api.Endpoints;

internal static class AccountDataCookies
{
    internal static string Name(HttpContext context) => context.RequestServices.GetRequiredService<IHostEnvironment>().IsDevelopment() ? "ratefreak.lifecycle" : "__Secure-ratefreak.lifecycle";
    private static CookieOptions Options(HttpContext context) => new() { Path = "/api/v1/users/me/data", HttpOnly = true, Secure = !context.RequestServices.GetRequiredService<IHostEnvironment>().IsDevelopment(), SameSite = SameSiteMode.Strict, MaxAge = TimeSpan.FromMinutes(5), IsEssential = true };
    internal static void Set(HttpContext context, string grant) => context.Response.Cookies.Append(Name(context), grant, Options(context));
    internal static void Clear(HttpContext context) => context.Response.Cookies.Delete(Name(context), Options(context));
}
internal static class AccountDataEndpoints
{
    public static void MapAccountDataEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/users/me/data").RequireAuthorization().WithTags("Account data");
        static Guid Id(ClaimsPrincipal user) => Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
        static string Stamp(ClaimsPrincipal user) => user.FindFirstValue("AspNet.Identity.SecurityStamp") ?? "";
        group.MapGet("/", async (HttpContext context, IAccountDataService service, CancellationToken ct) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            var status = await service.StatusAsync(Id(context.User), Stamp(context.User), context.Request.Cookies[AccountDataCookies.Name(context)], ct);
            return status == null ? Results.Unauthorized() : Results.Ok(status);
        }).RequireRateLimiting("personal-read");
        group.MapPost("/confirm", async (AccountDataConfirmation request, HttpContext context, IAccountDataService service, CancellationToken ct) =>
        {
            var grant = await service.ConfirmAsync(Id(context.User), Stamp(context.User), request, ct);
            if (grant == null) return Results.Problem(statusCode: 400, title: "Unable to confirm account ownership. Check your details or try again later.");
            AccountDataCookies.Set(context, grant); return Results.NoContent();
        }).AddEndpointFilter<AntiforgeryFilter>().RequireRateLimiting("lifecycle-confirm");
        group.MapPost("/export", async (HttpContext context, IAccountDataService service, CancellationToken ct) =>
        {
            try
            {
                var stream = await service.ExportAsync(Id(context.User), Stamp(context.User), context.Request.Cookies[AccountDataCookies.Name(context)], ct);
                AccountDataCookies.Clear(context); context.Response.Headers.CacheControl = "no-store";
                return Results.Stream(stream, "application/zip", "ratefreak-account-export.zip", enableRangeProcessing: false);
            }
            catch (AccountDataException ex) { return Results.Problem(statusCode: ex.StatusCode, title: ex.Message); }
        }).AddEndpointFilter<AntiforgeryFilter>().RequireRateLimiting("personal-write");
        group.MapPost("/delete", async (AccountDataDeletion request, HttpContext context, IAccountDataService service, SignInManager<AccountUser> signIn, CancellationToken ct) =>
        {
            try
            {
                await service.DeleteAsync(Id(context.User), Stamp(context.User), context.Request.Cookies[AccountDataCookies.Name(context)], request.Handle, ct);
                await signIn.SignOutAsync(); AccountDataCookies.Clear(context); return Results.NoContent();
            }
            catch (AccountDataException ex) { return Results.Problem(statusCode: ex.StatusCode, title: ex.Message); }
            catch (IOException) { return Results.Problem(statusCode: 503, title: "Account deletion is unavailable because its erasure journal could not be recorded. Try again later."); }
            catch (UnauthorizedAccessException) { return Results.Problem(statusCode: 503, title: "Account deletion is unavailable because its erasure journal could not be recorded. Try again later."); }
        }).AddEndpointFilter<AntiforgeryFilter>().RequireRateLimiting("personal-write");
    }
}
