using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace RateFreak.IntegrationTests;

public sealed partial class CollectionsTests
{
    [Theory]
    [InlineData("PATCH", "")]
    [InlineData("POST", "/items")]
    [InlineData("PATCH", "/items/{item}")]
    [InlineData("DELETE", "/items/{item}?expectedRevision=2")]
    [InlineData("POST", "/fields")]
    [InlineData("PATCH", "/fields/{field}")]
    [InlineData("DELETE", "/fields/{field}?expectedRevision=2&confirm=true")]
    public async Task Every_collection_mutation_enforces_session_csrf_and_owner(string method, string suffix)
    {
        await using var factory = Factory(); using var owner = Client(factory); using var other = Client(factory); using var anonymous = Client(factory);
        await SignIn(owner); await SignIn(other); var detail = await Create(owner, [Field()], "public"); var id = Id(detail);
        var item = await Json(await owner.PostAsJsonAsync(Root + "/" + id + "/items", new { expectedRevision = 1, title = "Entry", data = new { } }, Ct), HttpStatusCode.Created);
        var path = Root + "/" + id + suffix.Replace("{item}", item.GetProperty("id").GetGuid().ToString()).Replace("{field}", detail.GetProperty("fields")[0].GetProperty("id").GetGuid().ToString());
        object payload = suffix == "" ? new { expectedRevision = 2, name = "Changed", visibility = "public" }
            : suffix == "/items" || suffix.Contains("/items/", StringComparison.Ordinal) ? new { expectedRevision = 2, title = "Entry", data = new { }, position = 0 }
            : suffix == "/fields" ? new { expectedRevision = 2, field = Field("added") }
            : new { expectedRevision = 2, label = "New", required = false, configuration = new { }, position = 0 };
        Task<HttpResponseMessage> Send(HttpClient client) => client.SendAsync(new HttpRequestMessage(new HttpMethod(method), path) { Content = JsonContent.Create(payload) }, Ct);
        var unauth = await Send(anonymous); Assert.Equal(HttpStatusCode.Unauthorized, unauth.StatusCode); Assert.True(unauth.Headers.CacheControl?.NoStore);
        Assert.Equal(HttpStatusCode.NotFound, (await Send(other)).StatusCode);
        owner.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(owner)).StatusCode); Assert.Equal(2, await Current(owner, id));
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("page=10001")]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=101")]
    public async Task Collection_pages_reject_out_of_range_inputs(string query)
    {
        await using var factory = Factory(); using var client = Client(factory); await SignIn(client); var id = Id(await Create(client));
        foreach (var path in new[] { Root, Root + "/" + id + "/items" })
            Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(path + "?" + query, Ct)).StatusCode);
    }

    [Fact]
    public async Task Collection_limits_share_catalog_and_personal_budgets_and_preserve_no_store_on_errors()
    {
        await using var factory = Factory(); using var client = Client(factory); await SignIn(client); var id = Id(await Create(client));
        for (var i = 0; i < 59; i++) await Json(await client.GetAsync("/api/v1/media", Ct));
        await Json(await client.GetAsync(Root, Ct));
        var limitedRead = await client.GetAsync(Root.ToUpperInvariant() + "/", Ct);
        Assert.Equal(HttpStatusCode.TooManyRequests, limitedRead.StatusCode); Assert.NotNull(limitedRead.Headers.RetryAfter); Assert.True(limitedRead.Headers.CacheControl?.NoStore);
        // Creation consumed one of the account's shared thirty writes.
        for (var i = 0; i < 29; i++) Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync("/api/v1/media/" + Guid.NewGuid() + "/rating", new { score = 7 }, Ct)).StatusCode);
        var limitedWrite = await client.PatchAsJsonAsync(Root + "/" + id, new { expectedRevision = 1, name = "Changed", visibility = "private" }, Ct);
        Assert.Equal(HttpStatusCode.TooManyRequests, limitedWrite.StatusCode); Assert.NotNull(limitedWrite.Headers.RetryAfter); Assert.True(limitedWrite.Headers.CacheControl?.NoStore);
    }

    [Fact]
    public async Task Collection_database_constraints_and_cascades_preserve_relational_integrity()
    {
        await using var factory = Factory(); using var client = Client(factory); var userId = await SignIn(client);
        var id = Id(await Create(client, [Field()])); var media = await Media();
        await Json(await client.PostAsJsonAsync(Root + "/" + id + "/items", new { expectedRevision = 1, mediaItemId = media, data = new { } }, Ct), HttpStatusCode.Created);
        await using var db = Context();
        var statements = new (string Sql, string Code)[]
        {
            ("UPDATE collections SET visibility = 'unlisted' WHERE id = {0}", PostgresErrorCodes.CheckViolation),
            ("UPDATE collections SET revision = 0 WHERE id = {0}", PostgresErrorCodes.CheckViolation),
            ("UPDATE collection_fields SET position = -1 WHERE collection_id = {0}", PostgresErrorCodes.CheckViolation),
            ("UPDATE collection_fields SET configuration = '[]' WHERE collection_id = {0}", PostgresErrorCodes.CheckViolation),
            ("UPDATE collection_items SET data = '[]' WHERE collection_id = {0}", PostgresErrorCodes.CheckViolation),
            ("UPDATE collection_items SET position = -1 WHERE collection_id = {0}", PostgresErrorCodes.CheckViolation),
            ("UPDATE collection_items SET title = 'Not allowed' WHERE collection_id = {0}", PostgresErrorCodes.CheckViolation),
            ("UPDATE collection_items SET media_item_id = gen_random_uuid() WHERE collection_id = {0}", PostgresErrorCodes.ForeignKeyViolation),
            ("INSERT INTO collection_items (id,collection_id,media_item_id,title,data,position,created_at,updated_at) SELECT gen_random_uuid(),collection_id,media_item_id,title,data,position,created_at,updated_at FROM collection_items WHERE collection_id = {0}", PostgresErrorCodes.UniqueViolation),
            ("INSERT INTO collection_fields (id,collection_id,key,label,field_type,required,configuration,position,created_at,updated_at) SELECT gen_random_uuid(),collection_id,key,label,field_type,required,configuration,position,created_at,updated_at FROM collection_fields WHERE collection_id = {0}", PostgresErrorCodes.UniqueViolation)
        };
        foreach (var (sql, code) in statements)
        {
            var error = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync(sql, new object[] { id }, Ct)); Assert.Equal(code, error.SqlState);
        }
        var restricted = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM media_items WHERE id = {media}", Ct));
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, restricted.SqlState);
        Assert.Equal(1, await db.Users.Where(x => x.Id == userId).ExecuteDeleteAsync(Ct));
        Assert.False(await db.Collections.AnyAsync(x => x.Id == id, Ct)); Assert.False(await db.CollectionFields.AnyAsync(x => x.CollectionId == id, Ct)); Assert.False(await db.CollectionItems.AnyAsync(x => x.CollectionId == id, Ct));
        Assert.True(await db.MediaItems.AnyAsync(x => x.Id == media, Ct));
    }

    [Fact]
    public async Task Fields_cannot_exceed_thirty_and_invalid_replacements_do_not_mutate_data()
    {
        await using var factory = Factory(); using var client = Client(factory); await SignIn(client);
        var id = Id(await Create(client, Enumerable.Range(0, 30).Select(i => Field("key" + i)).ToArray())); var path = Root + "/" + id;
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(path + "/fields", new { expectedRevision = 1, field = Field("extra") }, Ct)).StatusCode);
        Assert.Equal(1, await Current(client, id));
        var item = await Json(await client.PostAsJsonAsync(path + "/items", new { expectedRevision = 1, title = "Custom", data = new { key0 = "Original" } }, Ct), HttpStatusCode.Created);
        var itemPath = path + "/items/" + item.GetProperty("id").GetGuid();
        var persisted = await Json(await client.GetAsync(itemPath, Ct));
        foreach (var payload in new[]
        {
            "{\"expectedRevision\":2,\"title\":\"Custom\",\"data\":{}}", // position required on replace
            "{\"expectedRevision\":2,\"title\":\"Custom\",\"position\":0,\"data\":{},\"mediaItemId\":null}",
            "{\"expectedRevision\":2,\"title\":\"Custom\",\"position\":0,\"data\":{\"key0\":42}}"
        }) Assert.Equal(HttpStatusCode.BadRequest, (await client.SendAsync(new HttpRequestMessage(HttpMethod.Patch, itemPath) { Content = new StringContent(payload, Encoding.UTF8, "application/json") }, Ct)).StatusCode);
        Assert.Equal(persisted.GetRawText(), (await Json(await client.GetAsync(itemPath, Ct))).GetRawText()); Assert.Equal(2, await Current(client, id));
    }
}
