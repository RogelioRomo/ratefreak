using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using static RateFreak.IntegrationTests.FeedTestSupport;

namespace RateFreak.IntegrationTests;

public sealed class FeedTests(AccountDatabase database) : IClassFixture<AccountDatabase>
{
    [Fact]
    public async Task Following_uses_current_edges_and_merges_only_explicitly_shared_scores()
    {
        await using var factory = new ApiFactory(connectionString: database.ConnectionString);
        using var author = Client(factory); using var reader = Client(factory);
        var authorId = await SignIn(author); await SignIn(reader); var id = await Media(database);
        await Json(await author.PutAsJsonAsync($"/api/v1/media/{id}/rating", new { score = 0 }, Ct));
        var review = await Json(await author.PostAsJsonAsync($"/api/v1/media/{id}/reviews", new { body = new string('x', 400), isSpoiler = true }, Ct), HttpStatusCode.Created);
        var before = await Json(await reader.GetAsync("/api/v1/feed", Ct)); Assert.Empty(before.GetProperty("items").EnumerateArray()); Assert.False(before.GetProperty("hasFollowees").GetBoolean());
        await reader.PutAsync($"/api/v1/users/{authorId}/follow", null, Ct);
        var first = await Json(await reader.GetAsync("/api/v1/feed", Ct)); var row = Assert.Single(first.GetProperty("items").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, row.GetProperty("score").ValueKind); Assert.Equal(280, row.GetProperty("review").GetProperty("excerpt").GetString()!.Length);
        Assert.True(row.GetProperty("review").GetProperty("isSpoiler").GetBoolean()); Assert.DoesNotContain(new string('x', 281), row.GetRawText());
        var reviewTime = row.GetProperty("publishedAt").GetDateTimeOffset();
        await Json(await author.PutAsJsonAsync($"/api/v1/media/{id}/rating", new { score = 0, isShared = true }, Ct));
        var combined = Assert.Single((await Json(await reader.GetAsync("/api/v1/feed", Ct))).GetProperty("items").EnumerateArray());
        Assert.Equal(0, combined.GetProperty("score").GetDecimal()); Assert.Equal(review.GetProperty("id").GetGuid(), combined.GetProperty("review").GetProperty("id").GetGuid());
        var published = combined.GetProperty("publishedAt").GetDateTimeOffset(); Assert.True(published >= reviewTime);
        await author.PutAsJsonAsync($"/api/v1/media/{id}/rating", new { score = 4.5 }, Ct);
        Assert.Equal(published, Assert.Single((await Json(await reader.GetAsync("/api/v1/feed", Ct))).GetProperty("items").EnumerateArray()).GetProperty("publishedAt").GetDateTimeOffset());
        await author.PutAsJsonAsync($"/api/v1/media/{id}/rating", new { score = 4.5, isShared = false }, Ct);
        Assert.Equal(JsonValueKind.Null, Assert.Single((await Json(await reader.GetAsync("/api/v1/feed", Ct))).GetProperty("items").EnumerateArray()).GetProperty("score").ValueKind);
        await reader.DeleteAsync($"/api/v1/users/{authorId}/follow", Ct);
        Assert.Empty((await Json(await reader.GetAsync("/api/v1/feed", Ct))).GetProperty("items").EnumerateArray());
        Assert.Contains((await Json(await reader.GetAsync("/api/v1/feed?tab=community", Ct))).GetProperty("items").EnumerateArray(), x => x.GetProperty("media").GetProperty("id").GetGuid() == id);
    }

    [Fact]
    public async Task Feed_rechecks_public_visibility_review_moderation_and_profile_fallback_even_for_owner()
    {
        await using var factory = new ApiFactory(connectionString: database.ConnectionString); using var author = Client(factory);
        var authorId = await SignIn(author); var id = await Media(database);
        await author.PutAsJsonAsync($"/api/v1/media/{id}/rating", new { score = 4, isShared = true }, Ct);
        var review = await Json(await author.PostAsJsonAsync($"/api/v1/media/{id}/reviews", new { body = "Public opinion" }, Ct), HttpStatusCode.Created);
        await using var db = Context(database);
        await db.Users.Where(x => x.Id == authorId).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsProfileHidden, true), Ct);
        await db.Reviews.Where(x => x.Id == review.GetProperty("id").GetGuid()).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsHidden, true), Ct);
        var row = Assert.Single((await Json(await author.GetAsync("/api/v1/feed?tab=community", Ct))).GetProperty("items").EnumerateArray(), x => x.GetProperty("media").GetProperty("id").GetGuid() == id);
        Assert.Equal(JsonValueKind.Null, row.GetProperty("review").ValueKind); Assert.Equal(row.GetProperty("author").GetProperty("handle").GetString(), row.GetProperty("author").GetProperty("displayName").GetString());
        await db.MediaItems.Where(x => x.Id == id).ExecuteUpdateAsync(s => s.SetProperty(x => x.CreatorUserId, authorId).SetProperty(x => x.Provenance, "user").SetProperty(x => x.MediaType, "custom").SetProperty(x => x.Visibility, "private"), Ct);
        Assert.DoesNotContain((await Json(await author.GetAsync("/api/v1/feed?tab=community", Ct))).GetProperty("items").EnumerateArray(), x => x.GetProperty("media").GetProperty("id").GetGuid() == id);
        await db.MediaItems.Where(x => x.Id == id).ExecuteUpdateAsync(s => s.SetProperty(x => x.Visibility, "public").SetProperty(x => x.IsHidden, true), Ct);
        Assert.DoesNotContain((await Json(await author.GetAsync("/api/v1/feed?tab=community", Ct))).GetProperty("items").EnumerateArray(), x => x.GetProperty("media").GetProperty("id").GetGuid() == id);
    }

    [Fact]
    public async Task Cursor_paginates_ties_and_is_bound_to_viewer_and_filters()
    {
        await using var factory = new ApiFactory(connectionString: database.ConnectionString); using var author = Client(factory); using var other = Client(factory);
        var authorId = await SignIn(author); await SignIn(other);
        // Following isolates this test's fixture from other tests sharing the database.
        await other.PutAsync($"/api/v1/users/{authorId}/follow", null, Ct);
        await using var db = Context(database); var ids = new List<Guid>();
        for (var i = 0; i < 23; i++) { var id = await Media(database, "book"); ids.Add(id); db.Add(new RateFreak.Domain.PersonalMedia.Review(authorId, id, "Tied review", false)); }
        await db.SaveChangesAsync(Ct);
        var tie = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        await db.Reviews.Where(x => x.UserId == authorId).ExecuteUpdateAsync(s => s.SetProperty(x => x.CreatedAt, tie), Ct);
        var response = await other.GetAsync("/api/v1/feed?type=book", Ct); Assert.True(response.Headers.CacheControl?.NoStore);
        var first = await Json(response); Assert.Equal(20, first.GetProperty("items").GetArrayLength()); var cursor = first.GetProperty("nextCursor").GetString()!;
        var second = await Json(await other.GetAsync("/api/v1/feed?type=book&cursor=" + Uri.EscapeDataString(cursor), Ct)); Assert.Equal(3, second.GetProperty("items").GetArrayLength());
        var actual = first.GetProperty("items").EnumerateArray().Concat(second.GetProperty("items").EnumerateArray()).Select(x => x.GetProperty("media").GetProperty("id").GetGuid()).ToArray();
        Assert.Equal(ids.Order().ToArray(), actual); Assert.Equal(JsonValueKind.Null, second.GetProperty("nextCursor").ValueKind);
        Assert.Equal(HttpStatusCode.BadRequest, (await other.GetAsync("/api/v1/feed?tab=community&type=book&cursor=" + Uri.EscapeDataString(cursor), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await author.GetAsync("/api/v1/feed?type=book&cursor=" + Uri.EscapeDataString(cursor), Ct)).StatusCode);
    }

    [Fact]
    public async Task Feed_is_authenticated_no_store_and_rejects_invalid_queries()
    {
        await using var factory = new ApiFactory(connectionString: database.ConnectionString); using var client = Client(factory);
        var anonymous = await client.GetAsync("/api/v1/feed", Ct); Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode); Assert.True(anonymous.Headers.CacheControl?.NoStore);
        await SignIn(client);
        foreach (var query in new[] { "tab=popular", "type=invalid", "tab=following&tab=community", "unknown=1", "cursor=broken", "cursor=" + new string('x', 1025) })
            Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/v1/feed?" + query, Ct)).StatusCode);
    }
    [Fact]
    public async Task Deleting_an_author_removes_derived_feed_entries_and_category_hiding_suppresses_them()
    {
        await using var factory = new ApiFactory(connectionString: database.ConnectionString); using var author = Client(factory); using var reader = Client(factory);
        var authorId=await SignIn(author); await SignIn(reader); var id=await Media(database);
        await reader.PutAsync($"/api/v1/users/{authorId}/follow",null,Ct);
        await author.PutAsJsonAsync($"/api/v1/media/{id}/rating",new {score=4,isShared=true},Ct);
        await using var db=Context(database);
        var category=new RateFreak.Domain.Catalog.Category("Feed category "+Guid.NewGuid().ToString("N")[..8],"public",null);
        db.Add(category);await db.SaveChangesAsync(Ct);
        await db.MediaItems.Where(x=>x.Id==id).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.CategoryId,category.Id),Ct);
        Assert.Single((await Json(await reader.GetAsync("/api/v1/feed",Ct))).GetProperty("items").EnumerateArray());
        await db.Categories.Where(x=>x.Id==category.Id).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.IsHidden,true),Ct);
        Assert.Empty((await Json(await reader.GetAsync("/api/v1/feed",Ct))).GetProperty("items").EnumerateArray());
        await db.Categories.Where(x=>x.Id==category.Id).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.IsHidden,false),Ct);
        await db.Users.Where(x=>x.Id==authorId).ExecuteDeleteAsync(Ct);
        Assert.Empty((await Json(await reader.GetAsync("/api/v1/feed",Ct))).GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task Feed_uses_social_read_budget_and_is_documented_in_OpenAPI()
    {
        await using var factory=new ApiFactory(connectionString:database.ConnectionString);using var client=Client(factory);await SignIn(client);
        for(var i=0;i<60;i++)Assert.Equal(HttpStatusCode.OK,(await client.GetAsync("/api/v1/feed",Ct)).StatusCode);
        var limited=await client.GetAsync("/api/v1/feed",Ct);Assert.Equal(HttpStatusCode.TooManyRequests,limited.StatusCode);Assert.NotNull(limited.Headers.RetryAfter);Assert.True(limited.Headers.CacheControl?.NoStore);
        Assert.Equal(HttpStatusCode.OK,(await client.GetAsync("/api/v1/media",Ct)).StatusCode);
        await using var development=new ApiFactory(environment:"Development",connectionString:database.ConnectionString);using var docs=Client(development);
        var openapi=await docs.GetFromJsonAsync<JsonElement>("/openapi/v1.json",Ct);Assert.True(openapi.GetProperty("paths").GetProperty("/api/v1/feed").TryGetProperty("get",out _));
    }

}
