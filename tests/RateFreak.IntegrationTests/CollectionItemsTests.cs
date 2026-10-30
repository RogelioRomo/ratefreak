using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace RateFreak.IntegrationTests;

public sealed partial class CollectionsTests
{
    [Fact]
    public async Task Items_preserve_independent_custom_entries_and_reject_duplicate_media()
    {
        await using var factory = Factory(); using var owner = Client(factory); using var other = Client(factory); using var anonymous = Client(factory);
        await SignIn(owner); await SignIn(other); var detail = await Create(owner, [Field()]); var id = Id(detail); var path = Root + "/" + id; var media = await Media();
        var created = await owner.PostAsJsonAsync(path + "/items", new { expectedRevision = 1, mediaItemId = media, data = new { note = "first" } }, Ct);
        var item = await Json(created, HttpStatusCode.Created); var itemId = item.GetProperty("id").GetGuid();
        Assert.Equal("Alien", item.GetProperty("title").GetString()); Assert.Equal("2", created.Headers.GetValues("X-Collection-Revision").Single());
        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync(created.Headers.Location, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync(created.Headers.Location, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync(path + "/items", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PostAsJsonAsync(path + "/items", new { expectedRevision = 2, mediaItemId = media, data = new { } }, Ct)).StatusCode);
        Assert.Equal(2, await Current(owner, id));
        var another = await Create(owner); await Json(await owner.PostAsJsonAsync(Root + "/" + Id(another) + "/items", new { expectedRevision = 1, mediaItemId = media, data = new { } }, Ct), HttpStatusCode.Created);
        for (var rev = 2; rev <= 3; rev++)
            await Json(await owner.PostAsJsonAsync(path + "/items", new { expectedRevision = rev, title = "Same title", data = new { }, position = 1 }, Ct), HttpStatusCode.Created);
        var edited = await Json(await owner.PatchAsJsonAsync(path + "/items/" + itemId, new { expectedRevision = 4, data = new { }, position = 2 }, Ct));
        Assert.Empty(edited.GetProperty("data").EnumerateObject()); Assert.Equal(media, edited.GetProperty("mediaItemId").GetGuid());
        Assert.Equal(HttpStatusCode.NotFound, (await other.PatchAsJsonAsync(path + "/items/" + itemId, new { expectedRevision = 5, data = new { }, position = 0 }, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await owner.DeleteAsync(path + "/items/" + itemId + "?expectedRevision=4", Ct)).StatusCode);
        var deletion = await owner.DeleteAsync(path + "/items/" + itemId + "?expectedRevision=5", Ct);
        Assert.Equal(HttpStatusCode.NoContent, deletion.StatusCode); Assert.Equal("6", deletion.Headers.GetValues("X-Collection-Revision").Single());
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync(path + "/items/" + itemId, Ct)).StatusCode);
        var page1 = await Json(await owner.GetAsync(path + "/items?pageSize=1", Ct)); var page2 = await Json(await owner.GetAsync(path + "/items?pageSize=1&page=2", Ct));
        Assert.Equal(2, page1.GetProperty("totalCount").GetInt32());
        Assert.NotEqual(page1.GetProperty("items")[0].GetProperty("id").GetGuid(), page2.GetProperty("items")[0].GetProperty("id").GetGuid());
    }

    [Theory]
    [InlineData("{\"expectedRevision\":1,\"data\":{}}", 400)]
    [InlineData("{\"title\":\"x\",\"data\":{}}", 400)]
    [InlineData("{\"expectedRevision\":null,\"title\":\"x\",\"data\":{}}", 400)]
    [InlineData("{\"expectedRevision\":0,\"title\":\"x\",\"data\":{}}", 400)]
    [InlineData("{\"expectedRevision\":2,\"title\":\"x\",\"data\":{}}", 409)]
    [InlineData("{\"expectedRevision\":1,\"title\":\"x\"}", 400)]
    [InlineData("{\"expectedRevision\":1,\"title\":\"x\",\"data\":[]}", 400)]
    [InlineData("{\"expectedRevision\":1,\"title\":\"x\",\"data\":{\"unknown\":1}}", 400)]
    [InlineData("{\"expectedRevision\":1,\"title\":\"x\",\"data\":{\"note\":\"a\",\"note\":\"b\"}}", 400)]
    [InlineData("{\"expectedRevision\":1,\"title\":\"x\",\"data\":{\"note\":\"bad\\u0000\"}}", 400)]
    [InlineData("{\"expectedRevision\":1,\"title\":\"x\",\"data\":{\"note\":\"\\uD800\"}}", 400)]
    [InlineData("{\"expectedRevision\":1,\"title\":\"x\",\"data\":{},\"position\":-1}", 400)]
    [InlineData("{\"expectedRevision\":1,\"mediaItemId\":\"00000000-0000-0000-0000-000000000001\",\"data\":{}}", 404)]
    [InlineData("{\"expectedRevision\":1,\"title\":\"x\",\"data\":{\"\\uD800\":\"x\"}}", 400)]
    [InlineData("{\"expectedRevision\":1,\"title\":\"x\",\"data\":{\"note\":\"a\",\"note\":\"b\"},\"data\":{}}", 400)]
    [InlineData("{\"expectedRevision\":9,\"expectedRevision\":1,\"title\":\"x\",\"data\":{}}", 400)]
    public async Task Invalid_item_writes_leave_collection_unchanged(string payload, int expectedStatus)
    {
        await using var factory = Factory(); using var client = Client(factory); await SignIn(client); var id = Id(await Create(client, [Field()]));
        var response = await client.PostAsync(Root + "/" + id + "/items", new StringContent(payload, Encoding.UTF8, "application/json"), Ct);
        Assert.Equal(expectedStatus, (int)response.StatusCode); Assert.Equal(1, await Current(client, id));
        Assert.Empty((await Json(await client.GetAsync(Root + "/" + id + "/items", Ct))).GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task Metadata_edits_require_current_revision_and_visibility_changes_take_effect()
    {
        await using var factory = Factory(); using var owner = Client(factory); using var other = Client(factory); using var anonymous = Client(factory);
        await SignIn(owner); await SignIn(other); var id = Id(await Create(owner, visibility: "public")); var path = Root + "/" + id;
        Assert.Equal(HttpStatusCode.NotFound, (await other.PatchAsJsonAsync(path, new { expectedRevision = 1, name = "Stolen", visibility = "public" }, Ct)).StatusCode);
        var updated = await Json(await owner.PatchAsJsonAsync(path, new { expectedRevision = 1, name = "New", visibility = "private" }, Ct));
        Assert.Equal(2, Revision(updated)); Assert.Equal(1, updated.GetProperty("collection").GetProperty("schemaVersion").GetInt64());
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync(path, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PatchAsJsonAsync(path, new { expectedRevision = 1, name = "Stale", visibility = "public" }, Ct)).StatusCode);
        Assert.Equal("New", (await Json(await owner.GetAsync(path, Ct))).GetProperty("collection").GetProperty("name").GetString());
    }
}
