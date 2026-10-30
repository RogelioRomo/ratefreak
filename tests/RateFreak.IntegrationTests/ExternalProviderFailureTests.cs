using System.Collections.Concurrent;
using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace RateFreak.IntegrationTests;

public sealed partial class ExternalAccountTests
{
    private sealed class Logs : ILoggerProvider
    {
        public ConcurrentQueue<string> Messages { get; } = new();
        public ILogger CreateLogger(string categoryName) => new Sink(Messages);
        public void Dispose() { }
        private sealed class Sink(ConcurrentQueue<string> messages) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
                => messages.Enqueue(formatter(state, exception));
        }
    }
    [Fact]
    public async Task Callback_error_descriptions_are_not_written_to_logs_or_response()
    {
        using var logs = new Logs();
        await using var factory = Enabled(new Provider()).WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            { ["Logging:LogLevel:Microsoft.AspNetCore.Authentication"] = "Trace" }));
            builder.ConfigureServices(services => services.AddLogging(logging => logging.AddProvider(logs)));
        });
        using var client = Client(factory);
        var state = await Start(client, "google");
        const string sentinel = "sensitive-provider-error-value";
        using var response = await client.GetAsync($"/api/v1/auth/external/google/callback?error=server_error&error_description={sentinel}&state={Uri.EscapeDataString(state)}", Cancellation);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.DoesNotContain(sentinel, await response.Content.ReadAsStringAsync(Cancellation));
        Assert.DoesNotContain(logs.Messages, message => message.Contains(sentinel, StringComparison.Ordinal));
    }
    [Fact]
    public async Task GitHub_identity_login_remains_available_when_email_lookup_is_unavailable()
    {
        var upstream = new Provider();
        await using var factory = Enabled(upstream);
        using var client = Client(factory);
        using var onboarding = await Callback(client, "github", await Start(client, "github"));
        await CompleteRegistration(client);
        await Token(client);
        using var logout = await client.PostAsync("/api/v1/auth/logout", null, Cancellation);
        upstream.EmailThrows = true;
        using var login = await Callback(client, "github", await Start(client, "github"));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }
    [Theory]
    [InlineData("google", "{\"id\":\"\",\"email\":\"a@example.test\",\"verified_email\":true}")]
    [InlineData("google", "{\"id\":\"abc\",\"email\":\"invalid\",\"verified_email\":true}")]
    [InlineData("github", "{\"id\":0}")]
    [InlineData("github", "{\"id\":1,\"id\":2}")]
    [InlineData("github", "not-json")]
    public async Task Malformed_provider_profiles_fail_without_sessions(string provider, string profile)
    {
        await using var factory = Enabled(new Provider { ProfileJson = profile });
        using var client = Client(factory);
        var state = await Start(client, provider);
        using var callback = await client.GetAsync($"/api/v1/auth/external/{provider}/callback?code=test&state={Uri.EscapeDataString(state)}", Cancellation);
        var status = callback.StatusCode;
        if (status == HttpStatusCode.Redirect)
        {
            using var completed = await client.GetAsync(callback.Headers.Location, Cancellation);
            status = completed.StatusCode;
        }
        Assert.Equal(HttpStatusCode.BadRequest, status);
        using var me = await client.GetAsync("/api/v1/users/me", Cancellation);
        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
    }
}
