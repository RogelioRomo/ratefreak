using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using RateFreak.Domain.Catalog;
using RateFreak.Infrastructure.Persistence;

namespace RateFreak.IntegrationTests;

internal static class FeedTestSupport
{
    internal static CancellationToken Ct => TestContext.Current.CancellationToken;
    internal static HttpClient Client(ApiFactory factory) => factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
    internal static async Task Token(HttpClient client)
    {
        var token = await client.GetFromJsonAsync<JsonElement>("/api/v1/auth/csrf", Ct);
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token.GetProperty("requestToken").GetString());
    }
    internal static async Task<Guid> SignIn(HttpClient client)
    {
        var tag = Guid.NewGuid().ToString("N")[..12];
        await Token(client);
        var response = await client.PostAsJsonAsync("/api/v1/auth/register", new { email = tag + "@example.test", password = "Local-password-72", handle = "feed_" + tag, displayName = "Reader" }, Ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var account = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/v1/auth/login", new { email = tag + "@example.test", password = "Local-password-72" }, Ct)).StatusCode);
        await Token(client); return account.GetProperty("id").GetGuid();
    }
    internal static RateFreakDbContext Context(AccountDatabase database) => new(new DbContextOptionsBuilder<RateFreakDbContext>().UseNpgsql(database.ConnectionString).Options);
    internal static async Task<Guid> Media(AccountDatabase database, string type = "movie")
    {
        await using var db = Context(database); var item = new MediaItem(type, "Feed fixture"); db.Add(item); await db.SaveChangesAsync(Ct); return item.Id;
    }
    internal static async Task<JsonElement> Json(HttpResponseMessage response, HttpStatusCode expected = HttpStatusCode.OK)
    {
        Assert.Equal(expected, response.StatusCode); return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }
}

public sealed class RatingSharingTests(AccountDatabase database) : IClassFixture<AccountDatabase>
{
    [Fact]
    public async Task Explicit_sharing_preserves_private_defaults_zero_and_legacy_publication_time()
    {
        await using var factory = new ApiFactory(connectionString: database.ConnectionString); using var client = FeedTestSupport.Client(factory);
        await FeedTestSupport.SignIn(client); var id = await FeedTestSupport.Media(database); var path = $"/api/v1/media/{id}/rating";
        var original = await FeedTestSupport.Json(await client.PutAsJsonAsync(path, new { score = 0 }, FeedTestSupport.Ct));
        Assert.False(original.GetProperty("isShared").GetBoolean()); Assert.Equal(JsonValueKind.Null, original.GetProperty("sharedAt").ValueKind);
        var shared = await FeedTestSupport.Json(await client.PutAsJsonAsync(path, new { score = 0, isShared = true }, FeedTestSupport.Ct));
        var published = shared.GetProperty("sharedAt").GetDateTimeOffset(); Assert.True(shared.GetProperty("isShared").GetBoolean()); Assert.Equal(0, shared.GetProperty("score").GetDecimal());
        var legacy = await FeedTestSupport.Json(await client.PutAsJsonAsync(path, new { score = 4.5 }, FeedTestSupport.Ct));
        Assert.Equal(published, legacy.GetProperty("sharedAt").GetDateTimeOffset()); Assert.True(legacy.GetProperty("isShared").GetBoolean());
        var repeated = await FeedTestSupport.Json(await client.PutAsJsonAsync(path, new { score = 3, isShared = true }, FeedTestSupport.Ct));
        Assert.Equal(published, repeated.GetProperty("sharedAt").GetDateTimeOffset());
        var unshared = await FeedTestSupport.Json(await client.PutAsJsonAsync(path, new { score = 3, isShared = false }, FeedTestSupport.Ct));
        Assert.False(unshared.GetProperty("isShared").GetBoolean()); Assert.Equal(JsonValueKind.Null, unshared.GetProperty("sharedAt").ValueKind);
        var again = await FeedTestSupport.Json(await client.PutAsJsonAsync(path, new { score = 3, isShared = true }, FeedTestSupport.Ct));
        Assert.True(again.GetProperty("sharedAt").GetDateTimeOffset() > published);
        Assert.Equal(original.GetProperty("createdAt").GetDateTimeOffset(), again.GetProperty("createdAt").GetDateTimeOffset());
        var aggregate = await client.GetFromJsonAsync<JsonElement>($"/api/v1/media/{id}/ratings", FeedTestSupport.Ct);
        Assert.Equal(1, aggregate.GetProperty("count").GetInt32()); Assert.Equal(3, aggregate.GetProperty("average").GetDecimal());
    }

    [Fact]
    public async Task Private_item_share_is_rejected_atomically_and_writes_require_session_and_csrf()
    {
        await using var factory = new ApiFactory(connectionString: database.ConnectionString); using var client = FeedTestSupport.Client(factory);
        var id = await FeedTestSupport.Media(database); var path = $"/api/v1/media/{id}/rating";
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PutAsJsonAsync(path, new { score = 4, isShared = true }, FeedTestSupport.Ct)).StatusCode);
        var owner = await FeedTestSupport.SignIn(client);
        await FeedTestSupport.Json(await client.PutAsJsonAsync(path, new { score = 2 }, FeedTestSupport.Ct));
        await using var db = FeedTestSupport.Context(database);
        await db.MediaItems.Where(x => x.Id == id).ExecuteUpdateAsync(s => s.SetProperty(x => x.CreatorUserId, owner).SetProperty(x => x.Provenance, "user").SetProperty(x => x.MediaType, "custom").SetProperty(x => x.Visibility, "private"), FeedTestSupport.Ct);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync(path, new { score = 5, isShared = true }, FeedTestSupport.Ct)).StatusCode);
        var unchanged = await client.GetFromJsonAsync<JsonElement>(path, FeedTestSupport.Ct); Assert.Equal(2, unchanged.GetProperty("score").GetDecimal());
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync(path, new { score = 4, isShared = false }, FeedTestSupport.Ct)).StatusCode);
    }
}
