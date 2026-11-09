using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RateFreak.Application.Identity;
using RateFreak.Infrastructure.Persistence;

namespace RateFreak.Infrastructure.Authentication;

internal sealed partial class AccountDataService(RateFreakDbContext db, UserManager<AccountUser> users,
    AccountErasureJournal journal, RateFreak.Application.Catalog.IArtworkStorage artwork,
    Microsoft.Extensions.Logging.ILogger<AccountDataService> logger) : IAccountDataService
{
    private static bool Operation(string operation) => operation is "export" or "delete";
    private static string Hash(string grant) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("ratefreak.lifecycle.v1:" + grant)));
    private async Task<AccountUser?> LockAsync(Guid id, string stamp, CancellationToken ct)
    {
        var user = await db.Users.FromSqlInterpolated($"SELECT * FROM users WHERE id = {id} FOR UPDATE").SingleOrDefaultAsync(ct);
        if (user != null) await db.Entry(user).ReloadAsync(ct);
        return user != null && !string.IsNullOrEmpty(stamp) && user.SecurityStamp == stamp ? user : null;
    }
    private async Task<string> IssueAsync(AccountUser user, string operation, CancellationToken ct)
    {
        await db.Set<AccountDataGrant>().Where(g => g.UserId == user.Id).ExecuteDeleteAsync(ct);
        var grant = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        db.Add(new AccountDataGrant { Hash = Hash(grant), UserId = user.Id, SecurityStamp = user.SecurityStamp!, Operation = operation, ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5) });
        await db.SaveChangesAsync(ct); return grant;
    }
    public async Task<AccountDataStatus?> StatusAsync(Guid userId, string stamp, string? grant, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == userId && u.SecurityStamp == stamp, ct);
        if (user == null) return null;
        var providers = await db.UserLogins.Where(l => l.UserId == userId).Select(l => l.LoginProvider.ToLower()).ToArrayAsync(ct);
        var hash = grant is { Length: 64 } ? Hash(grant) : "";
        var confirmed = await db.Set<AccountDataGrant>().Where(g => g.UserId == userId && g.Hash == hash && g.SecurityStamp == stamp && g.ExpiresAt > DateTimeOffset.UtcNow).Select(g => g.Operation).SingleOrDefaultAsync(ct);
        return new(user.PasswordHash != null, providers, confirmed);
    }
    public async Task<string?> ConfirmAsync(Guid userId, string stamp, AccountDataConfirmation request, CancellationToken ct)
    {
        if (!Operation(request.Operation) || string.IsNullOrEmpty(request.Password) || request.Password.Length > 128) return null;
        await using var tx = await db.Database.BeginTransactionAsync(ct); var user = await LockAsync(userId, stamp, ct);
        if (user?.PasswordHash == null) return null;
        var state = await db.Set<AccountRecoveryState>().SingleOrDefaultAsync(s => s.UserId == userId, ct);
        if (state == null) { state = new() { UserId = userId }; db.Add(state); }
        var now = DateTimeOffset.UtcNow;
        if (state.AttemptWindowEndsAt > now && state.FailedAttempts >= 5) return null;
        if (!await users.CheckPasswordAsync(user, request.Password))
        {
            if (state.AttemptWindowEndsAt == null || state.AttemptWindowEndsAt <= now) { state.FailedAttempts = 0; state.AttemptWindowEndsAt = now.AddMinutes(15); }
            state.FailedAttempts++; await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return null;
        }
        state.FailedAttempts = 0; state.AttemptWindowEndsAt = null;
        var grant = await IssueAsync(user, request.Operation, ct); await tx.CommitAsync(ct); return grant;
    }
    public async Task<string?> IssueExternalGrantAsync(Guid userId, string stamp, string operation, ExternalIdentity identity, CancellationToken ct)
    {
        if (!Operation(operation)) return null;
        await using var tx = await db.Database.BeginTransactionAsync(ct); var user = await LockAsync(userId, stamp, ct);
        if (user == null || user.PasswordHash != null || !await db.UserLogins.AnyAsync(l => l.UserId == userId && l.LoginProvider == identity.Provider && l.ProviderKey == identity.Subject, ct)) return null;
        var grant = await IssueAsync(user, operation, ct); await tx.CommitAsync(ct); return grant;
    }
    private async Task ConsumeAsync(Guid userId, string stamp, string? grant, string operation, CancellationToken ct)
    {
        if (grant is not { Length: 64 }) throw new AccountDataException(400, "Confirm account ownership again before continuing.");
        var hash = Hash(grant);
        if (await db.Set<AccountDataGrant>().Where(g => g.UserId == userId && g.Hash == hash && g.SecurityStamp == stamp && g.Operation == operation && g.ExpiresAt > DateTimeOffset.UtcNow).ExecuteDeleteAsync(ct) != 1)
            throw new AccountDataException(400, "Confirm account ownership again before continuing.");
    }
}
