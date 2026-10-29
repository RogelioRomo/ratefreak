namespace RateFreak.Api.Documentation;

internal static class DevelopmentDocumentation
{
    public static void MapDevelopmentDocumentation(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment()) return;

        app.MapOpenApi();
        app.UseSwaggerUI(options =>
        {
            options.RoutePrefix = "swagger";
            options.DocumentTitle = "RateFreak API — Development";
            options.SwaggerEndpoint("/openapi/v1.json", "RateFreak v1");
            options.UseRequestInterceptor("""
                (request) => {
                    const target = new URL(request.url, window.location.origin);
                    const method = (request.method || "GET").toUpperCase();
                    if (target.origin === window.location.origin &&
                        target.pathname.startsWith("/api/v1/") &&
                        !["GET", "HEAD", "OPTIONS"].includes(method)) {
                        return fetch("/api/v1/auth/csrf", {
                            credentials: "same-origin", cache: "no-store"
                        }).then(response => {
                            if (!response.ok) throw new Error("Could not obtain a CSRF token. Please retry later.");
                            return response.json();
                        }).then(({ requestToken }) => {
                            if (!requestToken) throw new Error("The CSRF token response was invalid.");
                            request.headers = request.headers || {};
                            request.headers["X-CSRF-TOKEN"] = requestToken;
                            request.credentials = "same-origin";
                            return request;
                        });
                    }
                    return request;
                }
                """);
        });
    }
}
