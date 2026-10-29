using System.Net;
using System.Text.Json;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace RateFreak.IntegrationTests;

public sealed class ApiTests
{
    [Theory]
    [InlineData("Development", "/swagger", HttpStatusCode.OK)]
    [InlineData("Development", "/swagger/index.html", HttpStatusCode.OK)]
    [InlineData("Development", "/swagger/swagger-ui-bundle.js", HttpStatusCode.OK)]
    [InlineData("Production", "/swagger", HttpStatusCode.NotFound)]
    [InlineData("Production", "/swagger/index.html", HttpStatusCode.NotFound)]
    [InlineData("Production", "/swagger/swagger-ui-bundle.js", HttpStatusCode.NotFound)]
    public async Task Swagger_UI_and_assets_are_available_only_in_development(
        string environment, string path, HttpStatusCode expected)
    {
        await using var factory = new ApiFactory(environment);
        using var client = factory.CreateClient();
        using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);
        Assert.Equal(expected, response.StatusCode);
        if (expected == HttpStatusCode.OK && !path.EndsWith(".js"))
            Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public async Task Malformed_account_json_returns_a_client_error_without_parser_details(string environment)
    {
        await using var factory = new ApiFactory(environment);
        using var client = factory.CreateClient();
        using var body = new StringContent("{", Encoding.UTF8, "application/json");
        using var response = await client.PostAsync("/api/v1/auth/register", body, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var text = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.DoesNotContain("JsonException", text);
    }

    [Fact]
    public async Task Production_requires_an_explicit_durable_key_directory()
    {
        await using var factory = new ApiFactory();
        await using var missingKeys = factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?> { ["Authentication:DataProtectionKeysPath"] = null })));
        var error = Assert.Throws<OptionsValidationException>(() => missingKeys.CreateClient());
        Assert.Contains("Authentication:DataProtectionKeysPath", error.Message);
    }

    [Fact]
    public async Task Liveness_does_not_require_a_database()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live", TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync("/health/ready", TestContext.Current.CancellationToken)).StatusCode);
    }

    [Theory]
    [InlineData("Development", HttpStatusCode.OK)]
    [InlineData("Production", HttpStatusCode.NotFound)]
    public async Task OpenApi_is_development_only(string environment, HttpStatusCode expected)
    {
        await using var factory = new ApiFactory(environment);
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);
        Assert.Equal(expected, response.StatusCode);
        if (expected == HttpStatusCode.OK)
        {
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            Assert.True(json.RootElement.TryGetProperty("openapi", out _));
        }
    }

    [Fact]
    public async Task Missing_routes_return_problem_details_with_trace_id()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/api/v1/does-not-exist", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(404, json.RootElement.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(json.RootElement.GetProperty("traceId").GetString()));
    }

    [Fact]
    public async Task Unexpected_failures_are_sanitized_even_in_development()
    {
        await using var factory = new ApiFactory("Development");
        await using var failing = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services => services.AddSingleton<IStartupFilter, ThrowingStartupFilter>()));
        using var client = failing.CreateClient();
        var response = await client.GetAsync("/test-only-failure", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.DoesNotContain("secret-database-error", body);
        using var json = JsonDocument.Parse(body);
        Assert.Equal(500, json.RootElement.GetProperty("status").GetInt32());
        Assert.True(json.RootElement.TryGetProperty("traceId", out _));
    }

    public sealed class ThrowingStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            next(app);
            app.Use((context, nextMiddleware) => context.Request.Path == "/test-only-failure"
                ? throw new InvalidOperationException("secret-database-error")
                : nextMiddleware(context));
        };
    }
}
