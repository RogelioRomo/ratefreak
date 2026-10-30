using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.OAuth;
using RateFreak.Application.Identity;

namespace RateFreak.Api.Security;

internal static class ExternalIdentityReader
{
    public const string ProviderClaim = "ratefreak:provider";
    public const string VerifiedClaim = "ratefreak:email-verified";
    public static ExternalIdentity? Read(ClaimsPrincipal? principal)
    {
        var provider = principal?.FindFirstValue(ProviderClaim);
        var subject = principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        return provider is "google" or "github" && Safe(subject, 200) ?
            new(provider, subject!, principal?.FindFirstValue(ClaimTypes.Email), principal?.FindFirstValue(VerifiedClaim) == "true") : null;
    }
    public static Task Google(OAuthCreatingTicketContext context)
    {
        var user = context.User;
        var subject = String(user, "id", 200);
        var email = String(user, "email", 254);
        var verified = user.TryGetProperty("verified_email", out var flag) && flag.ValueKind == JsonValueKind.True;
        Set(context, "google", subject, email, verified);
        return Task.CompletedTask;
    }
    public static async Task GitHub(OAuthCreatingTicketContext context)
    {
        using var profile = await Fetch(context, "https://api.github.com/user");
        var user = profile.RootElement;
        var subject = user.TryGetProperty("id", out var id) && id.TryGetInt64(out var value) && value > 0
            ? value.ToString(System.Globalization.CultureInfo.InvariantCulture) : null;
        string? email = null;
        // A linked identity need not have any email. Failed email permission/lookup must
        // not prevent identity login; a new account will be rejected at onboarding.
        try
        {
            using var request = Request("https://api.github.com/user/emails", context.AccessToken!);
            using var response = await context.Backchannel.SendAsync(request, context.HttpContext.RequestAborted);
            if (response.IsSuccessStatusCode)
            {
                using var emails = await Parse(response, context.HttpContext.RequestAborted);
                if (emails.RootElement.ValueKind != JsonValueKind.Array) throw new JsonException("Invalid email response.");
                foreach (var candidate in emails.RootElement.EnumerateArray())
                    if (candidate.TryGetProperty("verified", out var verified) && verified.ValueKind == JsonValueKind.True &&
                        candidate.TryGetProperty("primary", out var primary) && primary.ValueKind == JsonValueKind.True)
                    { email = String(candidate, "email", 254); break; }
            }
        }
        catch (HttpRequestException) { }
        catch (JsonException) { }
        catch (InvalidOperationException) { }
        catch (OperationCanceledException) when (!context.HttpContext.RequestAborted.IsCancellationRequested) { }
        Set(context, "github", subject, email, email is not null);
    }
    private static void Set(OAuthCreatingTicketContext context, string provider, string? subject, string? email, bool verified)
    {
        if (!Safe(subject, 200)) throw new JsonException("Invalid provider identity.");
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, subject!), new(ProviderClaim, provider),
            new(VerifiedClaim, verified ? "true" : "false") };
        if (email is not null) claims.Add(new(ClaimTypes.Email, email));
        context.Principal = new ClaimsPrincipal(new ClaimsIdentity(claims, provider));
    }
    private static async Task<JsonDocument> Fetch(OAuthCreatingTicketContext context, string url)
    {
        using var request = Request(url, context.AccessToken!);
        using var response = await context.Backchannel.SendAsync(request, context.HttpContext.RequestAborted);
        response.EnsureSuccessStatusCode();
        return await Parse(response, context.HttpContext.RequestAborted);
    }
    private static HttpRequestMessage Request(string url, string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.UserAgent.ParseAdd("RateFreak/1.0");
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        return request;
    }
    private static async Task<JsonDocument> Parse(HttpResponseMessage response, CancellationToken token) =>
        JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync(token), new JsonDocumentOptions
        { MaxDepth = 16, AllowDuplicateProperties = false });
    private static string? String(JsonElement element, string key, int limit) =>
        element.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String && Safe(value.GetString(), limit)
            ? value.GetString()!.Trim() : null;
    private static bool Safe(string? value, int limit) => !string.IsNullOrWhiteSpace(value) && value.Length <= limit &&
        !value.Any(char.IsControl) && ValidUnicode(value);
    private static bool ValidUnicode(string value)
    {
        for (var i = 0; i < value.Length; i++)
            if (char.IsSurrogate(value[i]) && (!char.IsHighSurrogate(value[i]) || ++i == value.Length || !char.IsLowSurrogate(value[i]))) return false;
        return true;
    }
}

// Bounds even framework-owned token/userinfo responses before they are parsed.
internal sealed class BoundedOAuthHandler() : DelegatingHandler(new SocketsHttpHandler { AllowAutoRedirect = false })
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);
        try
        {
            await response.Content.LoadIntoBufferAsync(64 * 1024, cancellationToken);
            return response;
        }
        catch { response.Dispose(); throw; }
    }
}
