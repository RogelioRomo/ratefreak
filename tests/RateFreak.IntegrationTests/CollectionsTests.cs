using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using RateFreak.Domain.Catalog;
using RateFreak.Infrastructure.Persistence;

namespace RateFreak.IntegrationTests;

public sealed partial class CollectionsTests(AccountDatabase database) : IClassFixture<AccountDatabase>
{
    private const string Root = "/api/v1/collections";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private RateFreakDbContext Context() => new(new DbContextOptionsBuilder<RateFreakDbContext>().UseNpgsql(database.ConnectionString).Options);
    private ApiFactory Factory() => new(connectionString: database.ConnectionString);
    private static HttpClient Client(ApiFactory factory) => factory.CreateClient(new WebApplicationFactoryClientOptions
        { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
    private static async Task Token(HttpClient client)
    {
        var token = await client.GetFromJsonAsync<JsonElement>("/api/v1/auth/csrf", Ct);
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token.GetProperty("requestToken").GetString());
    }
    private static async Task<Guid> SignIn(HttpClient client)
    {
        var tag = Guid.NewGuid().ToString("N")[..12]; var email = tag + "@example.test";
        await Token(client);
        var result = await Json(await client.PostAsJsonAsync("/api/v1/auth/register", new
            { email, password = "Local-password-72", handle = "user_" + tag, displayName = "Collector" }, Ct), HttpStatusCode.Created);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "Local-password-72" }, Ct)).StatusCode);
        await Token(client); return result.GetProperty("id").GetGuid();
    }
    private static async Task<JsonElement> Json(HttpResponseMessage response, HttpStatusCode status = HttpStatusCode.OK)
    {
        Assert.Equal(status, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }
    private static object Field(string key = "note", string type = "text", bool required = false, object? configuration = null) =>
        new { key, label = key, fieldType = type, required, configuration = configuration ?? new { }, position = 0 };
    private static async Task<JsonElement> Create(HttpClient client, object[]? fields = null, string? visibility = null) =>
        await Json(await client.PostAsJsonAsync(Root, new { name = " My collection ", description = "Notes", visibility, fields }, Ct), HttpStatusCode.Created);
    private static Guid Id(JsonElement detail) => detail.GetProperty("collection").GetProperty("id").GetGuid();
    private static long Revision(JsonElement detail) => detail.GetProperty("collection").GetProperty("revision").GetInt64();
    private static async Task<long> Current(HttpClient client, Guid id) => Revision(await Json(await client.GetAsync(Root + "/" + id, Ct)));
    private async Task<Guid> Media()
    {
        await using var db = Context(); var media = new MediaItem("movie", "Alien"); db.Add(media); await db.SaveChangesAsync(Ct); return media.Id;
    }

    [Fact]
    public async Task Collections_default_private_and_only_public_or_owned_records_are_accessible()
    {
        await using var factory = Factory(); using var owner = Client(factory); using var other = Client(factory); using var anonymous = Client(factory);
        var userId = await SignIn(owner); await SignIn(other);
        var response = await owner.PostAsJsonAsync(Root, new { name = " Private ", fields = new[] { Field() } }, Ct);
        var detail = await Json(response, HttpStatusCode.Created); var id = Id(detail); var path = Root + "/" + id;
        Assert.Equal(path, response.Headers.Location?.OriginalString);
        Assert.Equal("Private", detail.GetProperty("collection").GetProperty("name").GetString());
        Assert.Equal("private", detail.GetProperty("collection").GetProperty("visibility").GetString());
        Assert.Equal(userId, detail.GetProperty("collection").GetProperty("owner").GetProperty("id").GetGuid());
        Assert.Equal(1, Revision(detail));
        Assert.DoesNotContain("email", detail.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.Single(detail.GetProperty("fields").EnumerateArray());
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync(path, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync(path, Ct)).StatusCode);
        foreach (var suffix in new[] { "", "/" })
        {
            var read = await owner.GetAsync(path.ToUpperInvariant() + suffix, Ct);
            Assert.Equal(HttpStatusCode.OK, read.StatusCode); Assert.True(read.Headers.CacheControl?.NoStore);
        }
        var own = await Json(await owner.GetAsync(Root + "?ownOnly=true", Ct)); Assert.Single(own.GetProperty("items").EnumerateArray());
        Assert.Empty((await Json(await other.GetAsync(Root + "?ownOnly=true", Ct))).GetProperty("items").EnumerateArray());
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(Root + "?ownOnly=true", Ct)).StatusCode);
        var pub = await Create(owner, visibility: "public");
        Assert.Equal(HttpStatusCode.OK, (await anonymous.GetAsync(Root + "/" + Id(pub), Ct)).StatusCode);
        var publicList = await Json(await anonymous.GetAsync(Root, Ct));
        Assert.Contains(publicList.GetProperty("items").EnumerateArray(), x => x.GetProperty("id").GetGuid() == Id(pub));
        Assert.DoesNotContain(publicList.GetProperty("items").EnumerateArray(), x => x.GetProperty("id").GetGuid() == id);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"name\":\" \"}")]
    [InlineData("{\"name\":\"bad\\u0000\"}")]
    [InlineData("{\"name\":\"x\",\"ownerUserId\":\"x\"}")]
    [InlineData("{\"name\":\"x\",\"visibility\":\"unlisted\"}")]
    [InlineData("{\"name\":\"x\",\"fields\":[null]}")]
    [InlineData("{\"name\":\"x\",\"fields\":[{\"key\":\"id\",\"label\":\"Id\",\"fieldType\":\"text\"}]}")]
    [InlineData("{\"name\":\"first\",\"name\":\"second\"}")]
    [InlineData("{\"name\":\"x\",\"fields\":[{\"key\":\"note\",\"label\":\"First\",\"label\":\"Second\",\"fieldType\":\"text\"}]}")]
    public async Task Invalid_collection_creation_is_atomic(string payload)
    {
        await using var factory = Factory(); using var client = Client(factory); await SignIn(client);
        var response = await client.PostAsync(Root, new StringContent(payload, Encoding.UTF8, "application/json"), Ct);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Empty((await Json(await client.GetAsync(Root + "?ownOnly=true", Ct))).GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task Collection_creation_requires_session_and_identity_bound_csrf()
    {
        await using var factory = Factory(); using var client = Client(factory);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync(Root, new { name = "x" }, Ct)).StatusCode);
        await SignIn(client); client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(Root, new { name = "x" }, Ct)).StatusCode);
        await Token(client); await Create(client);
    }
}
