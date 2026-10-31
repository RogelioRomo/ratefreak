using System.Data.Common;
using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using RateFreak.Infrastructure.Persistence;

namespace RateFreak.IntegrationTests;

public sealed partial class UserProfileTests
{
    private sealed class EditAfterAccountRead : DbCommandInterceptor
    {
        public Func<Task>? Edit { get; set; }
        public bool Attempted { get; private set; }
        public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken ct = default)
        {
            if (!Attempted && Edit is not null && command.CommandText.Contains("FROM users", StringComparison.Ordinal))
            { Attempted = true; await Edit(); }
            return result;
        }
    }
    [Fact]
    public async Task Logout_refreshes_a_stale_account_after_a_concurrent_profile_edit_and_revokes_sessions()
    {
        var gate = new EditAfterAccountRead();
        await using var factory = new ApiFactory(connectionString: database.ConnectionString,
            configureServices: services => services.AddDbContext<RateFreakDbContext>(options => options.AddInterceptors(gate)));
        using var owner = Client(factory); var account = await Login(owner); var id = account.GetProperty("id").GetGuid();
        gate.Edit = async () => { await Patch(owner, "{\"displayName\":\"Concurrent name\",\"bio\":\"Concurrent bio\"}"); };
        var logout = await owner.PostAsync("/api/v1/auth/logout", null, Ct);
        Assert.True(gate.Attempted); Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        await using var db = Db(); var user = await db.Users.AsNoTracking().SingleAsync(x => x.Id == id, Ct);
        Assert.Equal("Concurrent name", user.DisplayName); Assert.Equal("Concurrent bio", user.Bio);
        Assert.Equal(HttpStatusCode.Unauthorized, (await owner.GetAsync("/api/v1/users/me", Ct)).StatusCode);
    }
}
