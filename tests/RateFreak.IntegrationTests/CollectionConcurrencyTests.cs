using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using RateFreak.Infrastructure.Persistence;

namespace RateFreak.IntegrationTests;

public sealed partial class CollectionsTests
{
    private sealed class LockBarrier : DbCommandInterceptor
    {
        public bool Enabled { get; set; }
        private int arrived;
        private readonly TaskCompletionSource both = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (Enabled && command.CommandText.Contains("FOR UPDATE", StringComparison.Ordinal))
            {
                if (Interlocked.Increment(ref arrived) == 2) both.TrySetResult();
                await both.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
            }
            return result;
        }
    }

    [Theory]
    [InlineData("items")]
    [InlineData("schema")]
    [InlineData("delete")]
    public async Task Concurrent_mutations_with_same_revision_allow_one_winner_and_keep_schema_valid(string kind)
    {
        var gate = new LockBarrier();
        await using var factory = new ApiFactory(connectionString: database.ConnectionString,
            configureServices: services => services.AddDbContext<RateFreakDbContext>(options => options.AddInterceptors(gate)));
        using var client = Client(factory); await SignIn(client);
        var detail = await Create(client, kind == "delete" ? [Field()] : []); var id = Id(detail); var path = Root + "/" + id;
        Guid itemId = default, fieldId = default;
        if (kind == "delete")
        {
            fieldId = detail.GetProperty("fields")[0].GetProperty("id").GetGuid();
            var item = await Json(await client.PostAsJsonAsync(path + "/items", new { expectedRevision = 1, title = "Entry", data = new { note = "old" } }, Ct), HttpStatusCode.Created);
            itemId = item.GetProperty("id").GetGuid();
        }
        gate.Enabled = true;
        var first = kind == "delete"
            ? client.DeleteAsync(path + "/fields/" + fieldId + "?expectedRevision=2&confirm=true", Ct)
            : kind == "schema" ? client.PostAsJsonAsync(path + "/fields", new { expectedRevision = 1, field = Field(required: true) }, Ct)
            : client.PostAsJsonAsync(path + "/items", new { expectedRevision = 1, title = "First", data = new { } }, Ct);
        var second = kind == "delete"
            ? client.PatchAsJsonAsync(path + "/items/" + itemId, new { expectedRevision = 2, title = "Entry", data = new { note = "new" }, position = 0 }, Ct)
            : client.PostAsJsonAsync(path + "/items", new { expectedRevision = 1, title = "Second", data = new { } }, Ct);
        var responses = await Task.WhenAll(first, second); gate.Enabled = false;
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Conflict);
        Assert.Single(responses, x => x.IsSuccessStatusCode);
        Assert.Equal(kind == "delete" ? 3 : 2, await Current(client, id));
        var state = await Json(await client.GetAsync(path, Ct));
        var items = (await Json(await client.GetAsync(path + "/items", Ct))).GetProperty("items");
        if (kind == "schema" && state.GetProperty("fields").GetArrayLength() == 1) Assert.Empty(items.EnumerateArray());
        if (kind == "delete")
        {
            var hasField = state.GetProperty("fields").GetArrayLength() == 1;
            var data = items[0].GetProperty("data");
            Assert.Equal(hasField, data.TryGetProperty("note", out var note));
            if (hasField) Assert.Equal("new", note.GetString());
        }
    }

    private sealed class PrivatizeAfterAuthorization(string connectionString) : DbCommandInterceptor
    {
        public Guid CollectionId { get; set; }
        public bool Triggered { get; private set; }
        public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
            DbDataReader result, CancellationToken cancellationToken = default)
        {
            if (!Triggered && CollectionId != Guid.Empty && command.CommandText.Contains("SELECT EXISTS", StringComparison.Ordinal)
                && command.CommandText.Contains("collections", StringComparison.Ordinal))
            {
                Triggered = true;
                await using var connection = new NpgsqlConnection(connectionString); await connection.OpenAsync(cancellationToken);
                await using var update = new NpgsqlCommand("""
                    UPDATE collections SET visibility = 'private', revision = revision + 1 WHERE id = @id;
                    INSERT INTO collection_items (id, collection_id, title, data, position, created_at, updated_at)
                    VALUES (gen_random_uuid(), @id, 'Private addition', '{}', 0, now(), now());
                    """, connection);
                update.Parameters.AddWithValue("id", CollectionId); await update.ExecuteNonQueryAsync(cancellationToken);
            }
            return result;
        }
    }

    [Fact]
    public async Task Collection_item_read_uses_one_snapshot_when_visibility_changes_after_authorization()
    {
        var gate = new PrivatizeAfterAuthorization(database.ConnectionString);
        await using var factory = new ApiFactory(connectionString: database.ConnectionString,
            configureServices: services => services.AddDbContext<RateFreakDbContext>(options => options.AddInterceptors(gate)));
        using var owner = Client(factory); using var anonymous = Client(factory); await SignIn(owner);
        var id = Id(await Create(owner, visibility: "public")); gate.CollectionId = id;
        var page = await Json(await anonymous.GetAsync(Root + "/" + id + "/items", Ct));
        Assert.True(gate.Triggered); Assert.Equal(0, page.GetProperty("totalCount").GetInt32()); Assert.Empty(page.GetProperty("items").EnumerateArray());
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync(Root + "/" + id + "/items", Ct)).StatusCode);
    }
}
