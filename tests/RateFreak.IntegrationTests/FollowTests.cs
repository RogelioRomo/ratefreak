using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using RateFreak.Infrastructure.Persistence;

namespace RateFreak.IntegrationTests;

public sealed class FollowTests(AccountDatabase database) : IClassFixture<AccountDatabase>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private RateFreakDbContext Context() => new(new DbContextOptionsBuilder<RateFreakDbContext>()
        .UseNpgsql(database.ConnectionString).Options);
    private ApiFactory Factory() => new(connectionString: database.ConnectionString);
    private static HttpClient Client(ApiFactory factory) => factory.CreateClient(new WebApplicationFactoryClientOptions
        { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
    private static async Task Token(HttpClient client)
    {
        var json = await client.GetFromJsonAsync<JsonElement>("/api/v1/auth/csrf", Ct);
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", json.GetProperty("requestToken").GetString());
    }
    private static async Task<Guid> SignIn(HttpClient client)
    {
        var tag = Guid.NewGuid().ToString("N")[..12];
        var email = tag + "@example.test";
        await Token(client);
        var registration = await client.PostAsJsonAsync("/api/v1/auth/register", new
            { email, password = "Local-password-72", handle = "user_" + tag, displayName = "Reader" }, Ct);
        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);
        var account = await registration.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/v1/auth/login",
            new { email, password = "Local-password-72" }, Ct)).StatusCode);
        await Token(client);
        return account.GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task Directed_lifecycle_is_public_private_data_free_and_repeat_safe()
    {
        await using var factory = Factory();
        using var actor = Client(factory); using var target = Client(factory); using var outsider = Client(factory);
        var actorId = await SignIn(actor); var targetId = await SignIn(target);
        var path = $"/api/v1/users/{targetId}/follow";
        Assert.Equal(HttpStatusCode.NoContent, (await actor.PutAsync(path, null, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await actor.PutAsync(path, null, Ct)).StatusCode);
        var response = await outsider.GetAsync($"/api/v1/users/{targetId}/followers", Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        var list = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal(1, list.GetProperty("totalCount").GetInt32());
        var row = list.GetProperty("items")[0];
        Assert.Equal(actorId, row.GetProperty("id").GetGuid());
        Assert.Equal(new[] { "displayName", "followedAt", "handle", "id" }, row.EnumerateObject().Select(p => p.Name).Order().ToArray());
        var time = row.GetProperty("followedAt").GetDateTimeOffset();
        Assert.Equal(0, (await target.GetFromJsonAsync<JsonElement>($"/api/v1/users/{targetId}/following", Ct)).GetProperty("totalCount").GetInt32());
        Assert.Equal(targetId, (await outsider.GetFromJsonAsync<JsonElement>($"/api/v1/users/{actorId}/following", Ct)).GetProperty("items")[0].GetProperty("id").GetGuid());
        await actor.PutAsync(path, null, Ct);
        Assert.Equal(time, (await outsider.GetFromJsonAsync<JsonElement>($"/api/v1/users/{targetId}/followers", Ct)).GetProperty("items")[0].GetProperty("followedAt").GetDateTimeOffset());
        // A different authenticated actor cannot remove the original relationship.
        await target.DeleteAsync($"/api/v1/users/{actorId}/follow", Ct);
        Assert.Equal(1, (await outsider.GetFromJsonAsync<JsonElement>($"/api/v1/users/{targetId}/followers", Ct)).GetProperty("totalCount").GetInt32());
        Assert.Equal(HttpStatusCode.NoContent, (await actor.DeleteAsync(path, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await actor.DeleteAsync(path, Ct)).StatusCode);
        Assert.Equal(0, (await outsider.GetFromJsonAsync<JsonElement>($"/api/v1/users/{targetId}/followers", Ct)).GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task Writes_require_session_and_csrf_and_validate_targets()
    {
        await using var factory = Factory(); using var client = Client(factory);
        var missing = $"/api/v1/users/{Guid.NewGuid()}/follow";
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PutAsync(missing, null, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.DeleteAsync(missing, Ct)).StatusCode);
        var id = await SignIn(client);
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsync(missing, null, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.DeleteAsync(missing, Ct)).StatusCode);
        await Token(client);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsync(missing, null, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync(missing, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsync($"/api/v1/users/{id}/follow", null, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.DeleteAsync($"/api/v1/users/{id}/follow", Ct)).StatusCode);
    }

    [Theory]
    [InlineData("followers")]
    [InlineData("following")]
    public async Task Lists_validate_pagination_and_distinguish_missing_users(string direction)
    {
        await using var factory = Factory(); using var client = Client(factory);
        var id = await SignIn(client); var path = $"/api/v1/users/{id}/{direction}";
        foreach (var query in new[] { "page=0", "page=10001", "pageSize=0", "pageSize=101", "page=invalid" })
            Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(path + "?" + query, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(path + "?page=10000&pageSize=100", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/users/{Guid.NewGuid()}/{direction}", Ct)).StatusCode);
    }
    [Fact]
    public async Task Concurrent_puts_preserve_one_edge_and_lists_paginate_tied_timestamps()
    {
        await using var factory = Factory(); using var actor = Client(factory); using var one = Client(factory); using var two = Client(factory);
        var actorId = await SignIn(actor); var oneId = await SignIn(one); var twoId = await SignIn(two);
        var path = $"/api/v1/users/{oneId}/follow";
        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => actor.PutAsync(path, null, Ct)));
        foreach (var response in responses) Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await actor.PutAsync($"/api/v1/users/{twoId}/follow", null, Ct)).StatusCode);
        await using var db = Context();
        Assert.Equal(2, await db.UserFollows.CountAsync(x => x.FollowerId == actorId, Ct));
        var tie = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        await db.UserFollows.Where(x => x.FollowerId == actorId).ExecuteUpdateAsync(set => set.SetProperty(x => x.CreatedAt, tie), Ct);
        var expected = new[] { oneId, twoId }.Order().ToArray();
        for (var page = 1; page <= 2; page++)
        {
            var list = await actor.GetFromJsonAsync<JsonElement>($"/api/v1/users/{actorId}/following?page={page}&pageSize=1", Ct);
            Assert.Equal(2, list.GetProperty("totalCount").GetInt32());
            Assert.Equal(page, list.GetProperty("page").GetInt32());
            Assert.Equal(1, list.GetProperty("pageSize").GetInt32());
            Assert.Equal(expected[page - 1], Assert.Single(list.GetProperty("items").EnumerateArray()).GetProperty("id").GetGuid());
        }
    }

    [Fact]
    public async Task Database_rejects_self_duplicate_and_orphan_edges_and_cascades_both_directions()
    {
        await using var factory = Factory(); using var one = Client(factory); using var two = Client(factory); using var three = Client(factory);
        var a = await SignIn(one); var b = await SignIn(two); var c = await SignIn(three);
        await one.PutAsync($"/api/v1/users/{b}/follow", null, Ct);
        await two.PutAsync($"/api/v1/users/{a}/follow", null, Ct);
        await three.PutAsync($"/api/v1/users/{b}/follow", null, Ct);
        await using var db = Context();
        async Task Reject(Guid follower, Guid followee, string code)
        {
            var error = await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO user_follows (follower_id, followee_id, created_at) VALUES ({follower}, {followee}, {DateTimeOffset.UtcNow})", Ct));
            Assert.Equal(code, error.SqlState);
        }
        await Reject(a, a, "23514"); await Reject(a, b, "23505");
        await Reject(a, Guid.NewGuid(), "23503"); await Reject(Guid.NewGuid(), b, "23503");
        await db.Users.Where(x => x.Id == a).ExecuteDeleteAsync(Ct);
        Assert.False(await db.UserFollows.AnyAsync(x => x.FollowerId == a || x.FolloweeId == a, Ct));
        Assert.True(await db.UserFollows.AnyAsync(x => x.FollowerId == c && x.FolloweeId == b, Ct));
        await db.Users.Where(x => x.Id == b).ExecuteDeleteAsync(Ct);
        Assert.False(await db.UserFollows.AnyAsync(x => x.FollowerId == c, Ct));
    }

    [Fact]
    public async Task Social_limits_are_separate_and_documented_routes_exist()
    {
        await using var factory = Factory(); using var client = Client(factory);
        var id = await SignIn(client); var path = $"/api/v1/users/{id}/followers";
        for (var i = 0; i < 60; i++) Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(path, Ct)).StatusCode);
        var limited = await client.GetAsync(path, Ct);
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.NotNull(limited.Headers.RetryAfter);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/media", Ct)).StatusCode);
        var missing = $"/api/v1/users/{Guid.NewGuid()}/follow";
        for (var i = 0; i < 30; i++) Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsync(missing, null, Ct)).StatusCode);
        limited = await client.DeleteAsync(missing, Ct);
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.NotNull(limited.Headers.RetryAfter);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync("/api/v1/users/me/library/" + Guid.NewGuid(), Ct)).StatusCode);
        await using var development = new ApiFactory(environment: "Development", connectionString: database.ConnectionString);
        using var docs = Client(development);
        using var other = Client(factory); await SignIn(other);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PutAsync(missing, null, Ct)).StatusCode);
        var openapi = await docs.GetFromJsonAsync<JsonElement>("/openapi/v1.json", Ct);
        var paths = openapi.GetProperty("paths");
        Assert.True(paths.GetProperty("/api/v1/users/{userId}/follow").TryGetProperty("put", out _));
        Assert.True(paths.GetProperty("/api/v1/users/{userId}/follow").TryGetProperty("delete", out _));
        Assert.True(paths.GetProperty("/api/v1/users/{userId}/followers").TryGetProperty("get", out _));
        Assert.True(paths.GetProperty("/api/v1/users/{userId}/following").TryGetProperty("get", out _));
    }

    [Fact]
    public async Task Following_does_not_expose_private_collections_or_library()
    {
        await using var factory = Factory(); using var owner = Client(factory); using var follower = Client(factory);
        var ownerId = await SignIn(owner); await SignIn(follower);
        var created = await owner.PostAsJsonAsync("/api/v1/collections", new { name = "Private", visibility = "private", fields = Array.Empty<object>() }, Ct);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var collection = await created.Content.ReadFromJsonAsync<JsonElement>(Ct);
        var id = collection.GetProperty("collection").GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.NoContent, (await follower.PutAsync($"/api/v1/users/{ownerId}/follow", null, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await follower.GetAsync($"/api/v1/collections/{id}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await follower.GetAsync($"/api/v1/collections/{id}/items", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await follower.GetAsync($"/api/v1/users/{ownerId}/library", Ct)).StatusCode);
        Assert.Empty((await follower.GetFromJsonAsync<JsonElement>("/api/v1/users/me/library", Ct)).GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task Followers_paginate_tied_timestamps_without_duplicates()
    {
        await using var factory = Factory(); using var target = Client(factory); using var one = Client(factory); using var two = Client(factory);
        var targetId = await SignIn(target); var oneId = await SignIn(one); var twoId = await SignIn(two);
        foreach (var client in new[] { one, two })
            Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsync($"/api/v1/users/{targetId}/follow", null, Ct)).StatusCode);
        await using var db = Context();
        var tie = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        await db.UserFollows.Where(x => x.FolloweeId == targetId).ExecuteUpdateAsync(set => set.SetProperty(x => x.CreatedAt, tie), Ct);
        var expected = new[] { oneId, twoId }.Order().ToArray();
        for (var page = 1; page <= 2; page++)
        {
            var list = await target.GetFromJsonAsync<JsonElement>($"/api/v1/users/{targetId}/followers?page={page}&pageSize=1", Ct);
            Assert.Equal(2, list.GetProperty("totalCount").GetInt32());
            Assert.Equal(expected[page - 1], Assert.Single(list.GetProperty("items").EnumerateArray()).GetProperty("id").GetGuid());
        }
    }

}
