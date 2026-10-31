using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using RateFreak.Infrastructure.Persistence;

namespace RateFreak.IntegrationTests;

public sealed partial class UserProfileTests
{
    [Fact]
    public async Task Concurrent_disjoint_edits_preserve_both_fields_and_security_state()
    {
        await using var factory = Factory(); using var owner = Client(factory);
        var account = await Login(owner); var id = account.GetProperty("id").GetGuid(); var before = await Security(id);
        var responses = await Task.WhenAll(
            owner.PatchAsJsonAsync("/api/v1/users/me", new { displayName = "Parallel name" }, Ct),
            owner.PatchAsJsonAsync("/api/v1/users/me", new { bio = "Parallel bio" }, Ct));
        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        var current = await Json(await owner.GetAsync("/api/v1/users/me", Ct));
        Assert.Equal("Parallel name", current.GetProperty("displayName").GetString()); Assert.Equal("Parallel bio", current.GetProperty("bio").GetString());
        Assert.Equal(before, await Security(id));
    }

    private sealed class DeleteAroundUpdate(string connectionString, bool after) : DbCommandInterceptor
    {
        public Guid UserId { get; set; }
        public bool Attempted { get; private set; }
        public bool Blocked { get; private set; }
        private async Task Delete(CancellationToken ct)
        {
            Attempted = true;
            await using var connection = new NpgsqlConnection(connectionString); await connection.OpenAsync(ct);
            await using var timeout = new NpgsqlCommand("SET lock_timeout='250ms'", connection); await timeout.ExecuteNonQueryAsync(ct);
            await using var command = new NpgsqlCommand("DELETE FROM users WHERE id=@id", connection); command.Parameters.AddWithValue("id", UserId);
            try { await command.ExecuteNonQueryAsync(ct); }
            catch (PostgresException error) when (error.SqlState == PostgresErrorCodes.LockNotAvailable) { Blocked = true; }
        }
        private bool Match(DbCommand command) => UserId != Guid.Empty && !Attempted && command.CommandText.Contains("UPDATE users", StringComparison.Ordinal);
        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken ct = default)
        { if (!after && Match(command)) await Delete(ct); return result; }
        public override async ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData, int result, CancellationToken ct = default)
        { if (after && Match(command)) await Delete(ct); return result; }
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Account_deletion_before_update_is_401_and_after_update_waits_for_projection(bool after)
    {
        var gate = new DeleteAroundUpdate(database.ConnectionString, after);
        await using var factory = new ApiFactory(connectionString: database.ConnectionString,
            configureServices: services => services.AddDbContext<RateFreakDbContext>(options => options.AddInterceptors(gate)));
        using var owner = Client(factory); var account = await Login(owner); gate.UserId = account.GetProperty("id").GetGuid();
        var response = await owner.PatchAsJsonAsync("/api/v1/users/me", new { bio = "Racing edit" }, Ct);
        Assert.Equal(after ? HttpStatusCode.OK : HttpStatusCode.Unauthorized, response.StatusCode); Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.True(gate.Attempted); Assert.Equal(after, gate.Blocked);
        await using var db = Db(); Assert.Equal(after, await db.Users.AnyAsync(user => user.Id == gate.UserId, Ct));
        if (after) Assert.Equal("Racing edit", (await Json(response)).GetProperty("bio").GetString());
    }
}
