using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RateFreak.Application.MetadataImport;
using RateFreak.Infrastructure.Persistence;
using RateFreak.Infrastructure.Providers.OpenLibrary;

namespace RateFreak.IntegrationTests;

public sealed class BookImportTests(AccountDatabase database) : IClassFixture<AccountDatabase>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private sealed class Transport : HttpMessageHandler
    {
        public int Calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Interlocked.Increment(ref Calls); var id = request.RequestUri!.AbsolutePath.Split('/').Last().Replace(".json", "", StringComparison.Ordinal);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent($$"""{"key":"/works/{{id}}","title":"Authoritative book","first_publish_date":"1970","covers":[12345]}""", Encoding.UTF8, "application/json") });
        }
    }
    private ApiFactory Factory(Transport transport) => new(connectionString: database.ConnectionString, configureServices: services =>
    {
        services.PostConfigure<OpenLibraryOptions>(o => { o.Enabled = true; o.Contact = "maintainer@example.test"; });
        services.AddHttpClient("openlibrary").ConfigurePrimaryHttpMessageHandler(() => transport);
    });
    private static HttpClient Client(ApiFactory factory) => factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new("https://localhost"), AllowAutoRedirect = false });
    private static async Task Token(HttpClient client)
    { var json = await client.GetFromJsonAsync<JsonElement>("/api/v1/auth/csrf", Ct); client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN"); client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", json.GetProperty("requestToken").GetString()); }
    private static async Task SignIn(HttpClient client)
    {
        var tag = Guid.NewGuid().ToString("N")[..12]; await Token(client);
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/v1/auth/register", new { email = tag + "@example.test", password = "Book-password-72", handle = "book_" + tag, displayName = "Reader" }, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/v1/auth/login", new { email = tag + "@example.test", password = "Book-password-72" }, Ct)).StatusCode); await Token(client);
    }
    private static Task<HttpResponseMessage> Import(HttpClient client, string id) => client.PostAsJsonAsync("/api/v1/media/import", new { provider = "openlibrary", externalId = id, mediaType = "book" }, Ct);
    private static async Task<JsonElement> Json(HttpResponseMessage response, HttpStatusCode expected = HttpStatusCode.OK)
    { Assert.Equal(expected, response.StatusCode); return await response.Content.ReadFromJsonAsync<JsonElement>(Ct); }

    [Fact, Trait("Category", "Database")]
    public async Task Book_import_is_local_idempotent_and_works_with_library_ratings_reviews_and_lists()
    {
        using var transport = new Transport(); await using var factory = Factory(transport); using var client = Client(factory); await SignIn(client);
        var response = await Import(client, "OL45804W"); var created = await Json(response, HttpStatusCode.Created); var id = created.GetProperty("mediaId").GetGuid();
        var detail = await Json(await client.GetAsync(response.Headers.Location, Ct)); Assert.Equal("book", detail.GetProperty("type").GetString()); Assert.Equal("Books", detail.GetProperty("category").GetProperty("name").GetString());
        Assert.Equal("Authoritative book", detail.GetProperty("title").GetString()); Assert.Equal("work", detail.GetProperty("externalIds")[0].GetProperty("namespace").GetString());
        Assert.Equal(JsonValueKind.Null, detail.GetProperty("releaseDate").ValueKind); Assert.Equal(1970, detail.GetProperty("metadata").GetProperty("firstPublicationYear").GetInt32());
        factory.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<OpenLibraryOptions>>().Value.Enabled = false;
        Assert.Equal(id, (await Json(await Import(client, "OL45804W"))).GetProperty("mediaId").GetGuid()); Assert.Equal(1, transport.Calls);
        Assert.Equal("Authoritative book", (await Json(await client.GetAsync(response.Headers.Location, Ct))).GetProperty("title").GetString());
        Assert.Contains((await Json(await client.GetAsync("/api/v1/media?type=book", Ct))).GetProperty("items").EnumerateArray(), x => x.GetProperty("id").GetGuid() == id);
        await Json(await client.PutAsJsonAsync($"/api/v1/users/me/library/{id}", new { status = "completed" }, Ct));
        Assert.Single((await Json(await client.GetAsync("/api/v1/users/me/library?type=book", Ct))).GetProperty("items").EnumerateArray());
        await Json(await client.PutAsJsonAsync($"/api/v1/media/{id}/rating", new { score = 4.5 }, Ct));
        await Json(await client.PostAsJsonAsync($"/api/v1/media/{id}/reviews", new { body = "A fine book" }, Ct), HttpStatusCode.Created);
        var collection = await Json(await client.PostAsJsonAsync("/api/v1/collections", new { name = "Reading list" }, Ct), HttpStatusCode.Created); var listId = collection.GetProperty("collection").GetProperty("id").GetGuid();
        await Json(await client.PostAsJsonAsync($"/api/v1/collections/{listId}/items", new { expectedRevision = 1, mediaItemId = id }, Ct), HttpStatusCode.Created);
    }

    [Fact, Trait("Category", "Database")]
    public async Task Concurrent_book_imports_share_provider_work_and_create_one_catalog_entry()
    {
        using var transport = new Transport(); await using var factory = Factory(transport); using var client = Client(factory); await SignIn(client);
        var results = await Task.WhenAll(Import(client, "OL987654W"), Import(client, "OL987654W"));
        Assert.Single(results, x => x.StatusCode == HttpStatusCode.Created); Assert.Single(results, x => x.StatusCode == HttpStatusCode.OK); Assert.Equal(1, transport.Calls);
        var a = await results[0].Content.ReadFromJsonAsync<ImportMediaResult>(Ct); var b = await results[1].Content.ReadFromJsonAsync<ImportMediaResult>(Ct); Assert.Equal(a!.MediaId, b!.MediaId);
        await using var db = new RateFreakDbContext(new DbContextOptionsBuilder<RateFreakDbContext>().UseNpgsql(database.ConnectionString).Options);
        Assert.Equal(1, await db.MediaExternalIds.CountAsync(x => x.Provider == "openlibrary" && x.ExternalId == "OL987654W", Ct));
    }

    [Fact, Trait("Category", "Database")]
    public async Task Book_import_requires_session_csrf_and_canonical_work_identity()
    {
        using var transport = new Transport(); await using var factory = Factory(transport); using var client = Client(factory);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Import(client, "OL1W")).StatusCode); await SignIn(client);
        Assert.Equal(HttpStatusCode.BadRequest, (await Import(client, "OL1M")).StatusCode);
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN"); Assert.Equal(HttpStatusCode.BadRequest, (await Import(client, "OL1W")).StatusCode); Assert.Equal(0, transport.Calls);
    }
}
