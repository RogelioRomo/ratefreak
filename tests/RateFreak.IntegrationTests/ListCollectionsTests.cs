using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RateFreak.Domain.Catalog;
using RateFreak.Infrastructure.Persistence;
using static RateFreak.IntegrationTests.CustomCatalogClient;

namespace RateFreak.IntegrationTests;

public sealed class ListCollectionsTests(AccountDatabase database) : IClassFixture<AccountDatabase>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private RateFreakDbContext Context() => new(new DbContextOptionsBuilder<RateFreakDbContext>().UseNpgsql(database.ConnectionString).Options);
    [Fact]
    public async Task Lists_require_catalog_references_reject_old_fields_and_preserve_item_on_removal()
    {
        await using var factory = new ApiFactory(connectionString: database.ConnectionString); using var owner = Client(factory); using var other = Client(factory); using var anon = Client(factory);
        await SignIn(owner, Ct); await SignIn(other, Ct);
        var collection = await Json(await owner.PostAsJsonAsync("/api/v1/collections", new { name = "My garden" }, Ct), Ct, HttpStatusCode.Created);
        Assert.False(collection.TryGetProperty("fields", out _)); Assert.False(collection.GetProperty("collection").TryGetProperty("schemaVersion", out _));
        var path = $"/api/v1/collections/{collection.GetProperty("collection").GetProperty("id").GetGuid()}";
        Assert.Equal(HttpStatusCode.NotFound, (await anon.GetAsync(path, Ct)).StatusCode); Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync(path, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync("/api/v1/collections", new { name = "Old", fields = new object[0] }, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.PostAsJsonAsync(path + "/fields", new { expectedRevision = 1 }, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync(path + "/items", new { expectedRevision = 1, title = "Free entry", data = new { } }, Ct)).StatusCode);
        await using var db = Context(); var item = new MediaItem("movie", "Shared subject"); db.Add(item); await db.SaveChangesAsync(Ct);
        var addedResponse = await owner.PostAsJsonAsync(path + "/items", new { expectedRevision = 1, mediaItemId = item.Id, position = 2 }, Ct); var added = await Json(addedResponse, Ct, HttpStatusCode.Created);
        Assert.Equal("2", addedResponse.Headers.GetValues("X-Collection-Revision").Single()); Assert.Equal(item.Id, added.GetProperty("mediaItemId").GetGuid()); Assert.Equal("Shared subject", added.GetProperty("media").GetProperty("title").GetString());
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PostAsJsonAsync(path + "/items", new { expectedRevision = 2, mediaItemId = item.Id }, Ct)).StatusCode);
        var membership = added.GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.NotFound, (await other.PatchAsJsonAsync(path + "/items/" + membership, new { expectedRevision = 2, position = 0 }, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PatchAsJsonAsync(path + "/items/" + membership, new { expectedRevision = 1, position = 0 }, Ct)).StatusCode);
        await Json(await owner.PatchAsJsonAsync(path + "/items/" + membership, new { expectedRevision = 2, position = 0 }, Ct), Ct);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.DeleteAsync(path + "/items/" + membership + "?expectedRevision=3", Ct)).StatusCode);
        Assert.True(await db.MediaItems.AnyAsync(m => m.Id == item.Id, Ct)); Assert.Equal(0, (await Json(await owner.GetAsync(path + "/items", Ct), Ct)).GetProperty("totalCount").GetInt32());
    }
    [Fact]
    public async Task Private_membership_eligibility_and_public_list_totals_follow_item_access()
    {
        await using var factory = new ApiFactory(connectionString: database.ConnectionString); using var owner = Client(factory); using var other = Client(factory); using var anon = Client(factory);
        await SignIn(owner, Ct); await SignIn(other, Ct);
        var category = await Json(await owner.PostAsJsonAsync("/api/v1/categories", new { name = "List flowers " + Guid.NewGuid(), scope = "public" }, Ct), Ct, HttpStatusCode.Created);
        var item = await Json(await owner.PostAsJsonAsync("/api/v1/media", new { title = "Shared flower", categoryId = category.GetProperty("id").GetGuid() }, Ct), Ct, HttpStatusCode.Created); var itemId = item.GetProperty("id").GetGuid();
        var list = await Json(await owner.PostAsJsonAsync("/api/v1/collections", new { name = "Garden", visibility = "private" }, Ct), Ct, HttpStatusCode.Created); var path = $"/api/v1/collections/{list.GetProperty("collection").GetProperty("id").GetGuid()}";
        await Json(await owner.PostAsJsonAsync(path + "/items", new { expectedRevision = 1, mediaItemId = itemId }, Ct), Ct, HttpStatusCode.Created);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PatchAsJsonAsync(path, new { expectedRevision = 2, name = "Garden", visibility = "public" }, Ct)).StatusCode);
        await Json(await owner.PostAsJsonAsync($"/api/v1/media/{itemId}/publish", new { expectedRevision = 1 }, Ct), Ct);
        await Json(await owner.PatchAsJsonAsync(path, new { expectedRevision = 2, name = "Garden", visibility = "public" }, Ct), Ct);
        Assert.Equal(1, (await Json(await anon.GetAsync(path + "/items", Ct), Ct)).GetProperty("totalCount").GetInt32());
        await Json(await owner.PostAsJsonAsync($"/api/v1/media/{itemId}/unpublish", new { expectedRevision = 2 }, Ct), Ct);
        Assert.Equal(0, (await Json(await anon.GetAsync(path + "/items", Ct), Ct)).GetProperty("totalCount").GetInt32()); Assert.Equal(1, (await Json(await owner.GetAsync(path + "/items", Ct), Ct)).GetProperty("totalCount").GetInt32());
    }
    [Fact]
    public async Task Unavailable_memberships_can_be_explicitly_cleared_without_disclosing_targets()
    {
        await using var factory = new ApiFactory(connectionString: database.ConnectionString);
        using var creator = Client(factory); using var owner = Client(factory); using var anon = Client(factory);
        await SignIn(creator, Ct); await SignIn(owner, Ct);
        var category = await Json(await creator.PostAsJsonAsync("/api/v1/categories", new { name = "Withdrawn " + Guid.NewGuid(), scope = "public" }, Ct), Ct, HttpStatusCode.Created);
        var item = await Json(await creator.PostAsJsonAsync("/api/v1/media", new { title = "Withdrawn subject", categoryId = category.GetProperty("id").GetGuid(), visibility = "public" }, Ct), Ct, HttpStatusCode.Created);
        var itemId = item.GetProperty("id").GetGuid();
        var list = await Json(await owner.PostAsJsonAsync("/api/v1/collections", new { name = "Shared list", visibility = "public" }, Ct), Ct, HttpStatusCode.Created);
        var path = $"/api/v1/collections/{list.GetProperty("collection").GetProperty("id").GetGuid()}";
        var added = await Json(await owner.PostAsJsonAsync(path + "/items", new { expectedRevision = 1, mediaItemId = itemId }, Ct), Ct, HttpStatusCode.Created);
        await Json(await creator.PostAsJsonAsync($"/api/v1/media/{itemId}/unpublish", new { expectedRevision = 1 }, Ct), Ct);
        Assert.Equal(0, (await Json(await owner.GetAsync(path + "/items", Ct), Ct)).GetProperty("totalCount").GetInt32());
        await Json(await owner.PatchAsJsonAsync(path, new { expectedRevision = 2, name = "Renamed public list", visibility = "public" }, Ct), Ct);
        await Json(await owner.PatchAsJsonAsync(path, new { expectedRevision = 3, name = "Renamed public list", visibility = "private" }, Ct), Ct);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PatchAsJsonAsync(path, new { expectedRevision = 4, name = "Renamed public list", visibility = "public" }, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.DeleteAsync(path + "/items/" + added.GetProperty("id").GetGuid() + "?expectedRevision=4", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.DeleteAsync(path + "/unavailable-items?expectedRevision=4", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await creator.DeleteAsync(path + "/unavailable-items?expectedRevision=4", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await owner.DeleteAsync(path + "/unavailable-items?expectedRevision=3", Ct)).StatusCode);
        var cleared = await owner.DeleteAsync(path + "/unavailable-items?expectedRevision=4", Ct);
        Assert.Equal(HttpStatusCode.NoContent, cleared.StatusCode);
        Assert.Equal("5", cleared.Headers.GetValues("X-Collection-Revision").Single());
        Assert.Equal("", await cleared.Content.ReadAsStringAsync(Ct));
        await Json(await owner.PatchAsJsonAsync(path, new { expectedRevision = 5, name = "Renamed public list", visibility = "public" }, Ct), Ct);
        await using var db = Context(); Assert.True(await db.MediaItems.AnyAsync(m => m.Id == itemId, Ct));
    }
    [Fact]
    public async Task Concurrent_same_revision_membership_writes_have_exactly_one_winner()
    {
        await using var factory = new ApiFactory(connectionString: database.ConnectionString); using var owner = Client(factory); await SignIn(owner, Ct);
        var list = await Json(await owner.PostAsJsonAsync("/api/v1/collections", new { name = "Concurrent list" }, Ct), Ct, HttpStatusCode.Created); var path = $"/api/v1/collections/{list.GetProperty("collection").GetProperty("id").GetGuid()}";
        await using var db = Context(); var a = new MediaItem("movie", "First"); var b = new MediaItem("album", "Second"); db.AddRange(a, b); await db.SaveChangesAsync(Ct);
        var results = await Task.WhenAll(owner.PostAsJsonAsync(path + "/items", new { expectedRevision = 1, mediaItemId = a.Id }, Ct), owner.PostAsJsonAsync(path + "/items", new { expectedRevision = 1, mediaItemId = b.Id }, Ct));
        Assert.Single(results, x => x.StatusCode == HttpStatusCode.Created); Assert.Single(results, x => x.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal(1, (await Json(await owner.GetAsync(path + "/items", Ct), Ct)).GetProperty("totalCount").GetInt32());
    }
}
