using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RateFreak.Infrastructure.Authentication;

namespace RateFreak.IntegrationTests;

public sealed partial class UserProfileTests
{
    [Fact]
    public async Task Stale_identity_write_cannot_overwrite_a_committed_profile_edit()
    {
        await using var factory = Factory(); using var owner = Client(factory);
        var account = await Login(owner); var id = account.GetProperty("id").GetGuid();
        await using var staleScope = factory.Services.CreateAsyncScope();
        var users = staleScope.ServiceProvider.GetRequiredService<UserManager<AccountUser>>();
        var staleAccount = await users.FindByIdAsync(id.ToString()); Assert.NotNull(staleAccount);

        await Patch(owner, "{\"displayName\":\"Committed name\",\"bio\":\"Committed bio\"}");
        // Identity writes the tracked account's complete row, even for a security-only change.
        var result = await users.UpdateSecurityStampAsync(staleAccount);
        await using var db = Db(); var stored = await db.Users.AsNoTracking().SingleAsync(user => user.Id == id, Ct);
        Assert.Equal("Committed name", stored.DisplayName); Assert.Equal("Committed bio", stored.Bio);
        Assert.True(result.Succeeded || result.Errors.Any(error => error.Code == "ConcurrencyFailure"));
    }
}
