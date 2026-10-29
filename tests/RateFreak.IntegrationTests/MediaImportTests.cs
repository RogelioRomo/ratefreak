using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RateFreak.Application.MetadataImport;
using RateFreak.Infrastructure.Persistence;
using RateFreak.Infrastructure.Providers.Tmdb;

namespace RateFreak.IntegrationTests;

public sealed class MediaImportTests(AccountDatabase database) : IClassFixture<AccountDatabase>
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;
    private sealed class Transport : HttpMessageHandler
    {
        public int Calls;
        public HttpStatusCode Status = HttpStatusCode.OK;
        public bool SynchronizePair;
        public string? OverrideBody;
        private readonly TaskCompletionSource pairReady = new(TaskCreationOptions.RunContinuationsAsynchronously);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var call = Interlocked.Increment(ref Calls);
            if (SynchronizePair)
            {
                if (call == 2) pairReady.SetResult();
                await pairReady.Task.WaitAsync(cancellationToken);
            }
            var path = request.RequestUri!.AbsolutePath;
            var id = int.Parse(path.Split('/').Last(), System.Globalization.CultureInfo.InvariantCulture);
            var titleKey = path.Contains("/tv/") ? "name" : "title";
            var body = $$"""{"id":{{id}},"{{titleKey}}":"Authoritative title {{id}}","overview":"TMDB synopsis","release_date":"2020-01-01","first_air_date":"2021-02-03","runtime":100}""";
            var response = new HttpResponseMessage(Status) { Content = new StringContent(OverrideBody ?? body, Encoding.UTF8, "application/json") };
            if (Status == HttpStatusCode.TooManyRequests) response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(45));
            return response;
        }
    }

    private ApiFactory Factory(Transport transport) => new(connectionString: database.ConnectionString, configureServices: services =>
    {
        services.PostConfigure<TmdbOptions>(options => { options.Enabled = true; options.AccessToken = "fake-transport-secret"; });
        services.AddHttpClient<IMediaProvider, TmdbMediaProvider>().ConfigurePrimaryHttpMessageHandler(() => transport);
    });
    private static HttpClient Client(ApiFactory factory) => factory.CreateClient(new WebApplicationFactoryClientOptions
        { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });

    private static async Task Token(HttpClient client)
    {
        using var response = await client.GetAsync("/api/v1/auth/csrf", Cancellation);
        response.EnsureSuccessStatusCode();
        var token = await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token.GetProperty("requestToken").GetString());
    }

    private static async Task SignIn(HttpClient client)
    {
        var email = $"import-{Guid.NewGuid():N}@example.test";
        const string password = "A-valid-local-password-71";
        await Token(client);
        using var registration = await client.PostAsJsonAsync("/api/v1/auth/register",
            new { email, password, handle = "import_" + Guid.NewGuid().ToString("N")[..12], displayName = "Reader" }, Cancellation);
        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);
        using var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password }, Cancellation);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        await Token(client);
    }

    private static Task<HttpResponseMessage> Import(HttpClient client, string id, string type = "movie") =>
        client.PostAsJsonAsync("/api/v1/media/import", new { provider = "tmdb", externalId = id, mediaType = type }, Cancellation);

    [Fact]
    public async Task Imports_fetch_authoritative_data_and_replays_preserve_existing_corrections()
    {
        using var transport = new Transport();
        await using var factory = Factory(transport);
        using var client = Client(factory);
        await SignIn(client);
        using var imported = await Import(client, "00011");
        Assert.Equal(HttpStatusCode.Created, imported.StatusCode);
        var created = await imported.Content.ReadFromJsonAsync<ImportMediaResult>(Cancellation);
        Assert.NotNull(created);
        Assert.True(created.Created);
        Assert.Equal($"/api/v1/media/{created.MediaId}", imported.Headers.Location?.ToString());
        var detail = await client.GetFromJsonAsync<JsonElement>(imported.Headers.Location, Cancellation);
        Assert.Equal("Authoritative title 11", detail.GetProperty("title").GetString());
        Assert.Equal("movie", detail.GetProperty("externalIds")[0].GetProperty("namespace").GetString());
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<RateFreakDbContext>();
        await context.Database.ExecuteSqlInterpolatedAsync($"UPDATE media_items SET title = 'Moderator correction' WHERE id = {created.MediaId}", Cancellation);
        using var replay = await Import(client, "11");
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        var repeated = await replay.Content.ReadFromJsonAsync<ImportMediaResult>(Cancellation);
        Assert.Equal(new ImportMediaResult(created.MediaId, false), repeated);
        Assert.Equal(1, transport.Calls);
        detail = await client.GetFromJsonAsync<JsonElement>(imported.Headers.Location, Cancellation);
        Assert.Equal("Moderator correction", detail.GetProperty("title").GetString());
        using var television = await Import(client, "11", "tv_show");
        Assert.Equal(HttpStatusCode.Created, television.StatusCode);
        var tv = await television.Content.ReadFromJsonAsync<ImportMediaResult>(Cancellation);
        Assert.NotEqual(created.MediaId, tv!.MediaId);
    }

    [Fact]
    public async Task Concurrent_imports_return_one_canonical_record_without_orphan_items()
    {
        using var transport = new Transport { SynchronizePair = true };
        await using var factory = Factory(transport);
        using var client = Client(factory);
        await SignIn(client);
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<RateFreakDbContext>();
        var before = await context.MediaItems.CountAsync(Cancellation);
        var responses = await Task.WhenAll(Import(client, "987"), Import(client, "987"));
        try
        {
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Created);
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
            var first = await responses[0].Content.ReadFromJsonAsync<ImportMediaResult>(Cancellation);
            var second = await responses[1].Content.ReadFromJsonAsync<ImportMediaResult>(Cancellation);
            Assert.Equal(first!.MediaId, second!.MediaId);
            Assert.Equal(before + 1, await context.MediaItems.CountAsync(Cancellation));
            Assert.Equal(1, await context.MediaExternalIds.CountAsync(x => x.Provider == "tmdb" && x.Namespace == "movie" && x.ExternalId == "987", Cancellation));
        }
        finally { foreach (var response in responses) response.Dispose(); }
    }

    [Fact]
    public async Task Authenticated_import_requires_fresh_csrf_and_rejects_client_metadata()
    {
        using var transport = new Transport();
        await using var factory = Factory(transport);
        using var client = Client(factory);
        await SignIn(client);
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        using var missingCsrf = await Import(client, "22");
        Assert.Equal(HttpStatusCode.BadRequest, missingCsrf.StatusCode);
        await Token(client);
        using var injection = await client.PostAsJsonAsync("/api/v1/media/import",
            new { provider = "tmdb", externalId = "22", mediaType = "movie", title = "Forged title" }, Cancellation);
        Assert.Equal(HttpStatusCode.BadRequest, injection.StatusCode);
        Assert.Equal(0, transport.Calls);
    }

    [Fact]
    public async Task Not_found_upstream_does_not_create_catalog_rows()
    {
        using var transport = new Transport { Status = HttpStatusCode.NotFound };
        await using var factory = Factory(transport);
        using var client = Client(factory);
        await SignIn(client);
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<RateFreakDbContext>();
        var before = await context.MediaItems.CountAsync(Cancellation);
        using var result = await Import(client, "404");
        Assert.Equal(HttpStatusCode.NotFound, result.StatusCode);
        Assert.Equal(before, await context.MediaItems.CountAsync(Cancellation));
    }

    [Fact]
    public async Task Import_limits_are_per_account_and_independent_of_search()
    {
        using var transport = new Transport { Status = HttpStatusCode.NotFound };
        await using var factory = Factory(transport);
        using var first = Client(factory);
        using var second = Client(factory);
        await SignIn(first);
        await SignIn(second);
        for (var i = 0; i < 10; i++)
        {
            using var response = await Import(first, "404");
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
        using var limited = await Import(first, "404");
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.NotNull(limited.Headers.RetryAfter);
        using var other = await Import(second, "404");
        Assert.Equal(HttpStatusCode.NotFound, other.StatusCode);
    }

    [Fact]
    public async Task Provider_rate_limit_has_safe_problem_details_and_retry_after()
    {
        using var transport = new Transport { Status = HttpStatusCode.TooManyRequests };
        await using var factory = Factory(transport);
        using var client = Client(factory);
        using var response = await client.GetAsync("/api/v1/media/external/tmdb/movie/11", Cancellation);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(TimeSpan.FromSeconds(45), response.Headers.RetryAfter?.Delta);
        var body = await response.Content.ReadAsStringAsync(Cancellation);
        Assert.DoesNotContain("fake-transport-secret", body);
        Assert.DoesNotContain("Authoritative title", body);
    }
    [Fact]
    public async Task Malformed_upstream_unicode_returns_502_without_creating_rows()
    {
        using var transport = new Transport { OverrideBody = "{\"id\":71,\"title\":\"\\uD800\"}" };
        await using var factory = Factory(transport);
        using var client = Client(factory);
        await SignIn(client);
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<RateFreakDbContext>();
        var before = await context.MediaItems.CountAsync(Cancellation);
        using var result = await Import(client, "71");
        Assert.Equal(HttpStatusCode.BadGateway, result.StatusCode);
        Assert.Equal(before, await context.MediaItems.CountAsync(Cancellation));
        Assert.DoesNotContain("InvalidOperationException", await result.Content.ReadAsStringAsync(Cancellation));
    }

}
