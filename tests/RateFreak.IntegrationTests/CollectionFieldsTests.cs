using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace RateFreak.IntegrationTests;

public sealed partial class CollectionsTests
{
    [Fact]
    public async Task Schema_edits_preserve_values_and_reject_incompatible_changes_atomically()
    {
        await using var factory = Factory(); using var client = Client(factory); await SignIn(client);
        var detail = await Create(client, [Field()]); var id = Id(detail); var fieldId = detail.GetProperty("fields")[0].GetProperty("id").GetGuid(); var path = Root + "/" + id;
        var item = await Json(await client.PostAsJsonAsync(path + "/items", new { expectedRevision = 1, title = "Custom", data = new { note = "Long value" } }, Ct), HttpStatusCode.Created);
        var added = await Json(await client.PostAsJsonAsync(path + "/fields", new { expectedRevision = 2, field = Field("flag", "boolean") }, Ct), HttpStatusCode.Created);
        Assert.Equal("flag", added.GetProperty("key").GetString());
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(path + "/fields", new { expectedRevision = 3, field = Field("required", required: true) }, Ct)).StatusCode);
        var before = await Json(await client.GetAsync(path, Ct));
        Assert.Equal(HttpStatusCode.Conflict, (await client.PatchAsJsonAsync(path + "/fields/" + fieldId, new { expectedRevision = 3, label = "Renamed", required = false, configuration = new { maxLength = 3 }, position = 1 }, Ct)).StatusCode);
        Assert.Equal(before.GetRawText(), (await Json(await client.GetAsync(path, Ct))).GetRawText());
        var edited = await Json(await client.PatchAsJsonAsync(path + "/fields/" + fieldId, new { expectedRevision = 3, label = "Renamed", required = true, configuration = new { maxLength = 100 }, position = 1 }, Ct));
        Assert.Equal("Renamed", edited.GetProperty("label").GetString()); Assert.Equal("note", edited.GetProperty("key").GetString());
        var stored = await Json(await client.GetAsync(path + "/items/" + item.GetProperty("id").GetGuid(), Ct));
        Assert.Equal("Long value", stored.GetProperty("data").GetProperty("note").GetString());
        var schema = await Json(await client.GetAsync(path, Ct)); Assert.Equal(4, Revision(schema)); Assert.Equal(3, schema.GetProperty("collection").GetProperty("schemaVersion").GetInt64());
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PatchAsJsonAsync(path + "/fields/" + fieldId, new { expectedRevision = 4, key = "changed", label = "Name", required = true, configuration = new { }, position = 1 }, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PatchAsJsonAsync(path + "/fields/" + fieldId, new { expectedRevision = 4, fieldType = "number", label = "Name", required = true, configuration = new { }, position = 1 }, Ct)).StatusCode);
    }

    [Fact]
    public async Task Field_deletion_requires_fresh_confirmed_impact_and_removes_only_its_values()
    {
        await using var factory = Factory(); using var owner = Client(factory); using var other = Client(factory); using var anonymous = Client(factory);
        await SignIn(owner); await SignIn(other); var detail = await Create(owner, [Field(), Field("keep")], "public"); var id = Id(detail);
        var fieldId = detail.GetProperty("fields").EnumerateArray().Single(x => x.GetProperty("key").GetString() == "note").GetProperty("id").GetGuid();
        var path = Root + "/" + id; var fieldPath = path + "/fields/" + fieldId;
        var one = await Json(await owner.PostAsJsonAsync(path + "/items", new { expectedRevision = 1, title = "One", data = new { note = "value", keep = "retained" } }, Ct), HttpStatusCode.Created);
        await Json(await owner.PostAsJsonAsync(path + "/items", new { expectedRevision = 2, title = "Two", data = new { note = (string?)null } }, Ct), HttpStatusCode.Created);
        await Json(await owner.PostAsJsonAsync(path + "/items", new { expectedRevision = 3, title = "Three", data = new { } }, Ct), HttpStatusCode.Created);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(fieldPath + "/deletion-impact", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync(fieldPath + "/deletion-impact", Ct)).StatusCode);
        var previewResponse = await owner.GetAsync(fieldPath + "/deletion-impact", Ct); Assert.True(previewResponse.Headers.CacheControl?.NoStore);
        var impact = await Json(previewResponse); Assert.Equal(1, impact.GetProperty("populatedItemCount").GetInt32()); Assert.Equal(4, impact.GetProperty("revision").GetInt64());
        foreach (var query in new[] { "", "?expectedRevision=4", "?expectedRevision=4&confirm=false", "?confirm=true" })
            Assert.Equal(HttpStatusCode.BadRequest, (await owner.DeleteAsync(fieldPath + query, Ct)).StatusCode);
        var before = await Json(await owner.GetAsync(path + "/items/" + one.GetProperty("id").GetGuid(), Ct)); Assert.Equal(4, await Current(owner, id));
        await Json(await owner.PatchAsJsonAsync(path + "/items/" + one.GetProperty("id").GetGuid(), new { expectedRevision = 4, title = "One", data = new { note = "new", keep = "retained" }, position = 0 }, Ct));
        Assert.Equal(HttpStatusCode.Conflict, (await owner.DeleteAsync(fieldPath + "?expectedRevision=4&confirm=true", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.DeleteAsync(fieldPath + "?expectedRevision=5&confirm=true", Ct)).StatusCode);
        var afterEdit = await Json(await owner.GetAsync(path + "/items/" + one.GetProperty("id").GetGuid(), Ct));
        var deletion = await owner.DeleteAsync(fieldPath + "?expectedRevision=5&confirm=true", Ct); Assert.Equal(HttpStatusCode.NoContent, deletion.StatusCode);
        Assert.Equal("6", deletion.Headers.GetValues("X-Collection-Revision").Single());
        var after = await Json(await owner.GetAsync(path, Ct)); Assert.Single(after.GetProperty("fields").EnumerateArray());
        var items = await Json(await owner.GetAsync(path + "/items", Ct));
        foreach (var x in items.GetProperty("items").EnumerateArray()) Assert.False(x.GetProperty("data").TryGetProperty("note", out _));
        var updated = items.GetProperty("items").EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == one.GetProperty("id").GetGuid());
        Assert.Equal("retained", updated.GetProperty("data").GetProperty("keep").GetString());
        Assert.True(updated.GetProperty("updatedAt").GetDateTimeOffset() >= afterEdit.GetProperty("updatedAt").GetDateTimeOffset());
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync(fieldPath + "/deletion-impact", Ct)).StatusCode);
    }

    [Theory]
    [InlineData("number", "{\"min\":1,\"max\":3}", "2.5", "4")]
    [InlineData("boolean", "{}", "true", "\"true\"")]
    [InlineData("date", "{}", "\"2024-02-29\"", "\"2026-02-29\"")]
    [InlineData("select", "{\"options\":[\"A\",\"B\"]}", "\"A\"", "\"a\"")]
    public async Task Stored_schema_validates_each_field_type_on_writes(string type, string config, string valid, string invalid)
    {
        await using var factory = Factory(); using var client = Client(factory); await SignIn(client);
        var id = Id(await Create(client, [Field("value", type, true, JsonDocument.Parse(config).RootElement.Clone())])); var path = Root + "/" + id + "/items";
        async Task<HttpResponseMessage> Send(string data, int revision) => await client.PostAsJsonAsync(path,
            new { expectedRevision = revision, title = "Entry", data = JsonDocument.Parse("{\"value\":" + data + "}").RootElement.Clone() }, Ct);
        await Json(await Send(valid, 1), HttpStatusCode.Created);
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(invalid, 2)).StatusCode); Assert.Equal(2, await Current(client, id));
    }
}
