using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using RateFreak.Application.Identity;
using RateFreak.Infrastructure.Persistence;

namespace RateFreak.Infrastructure.Authentication;

internal sealed class ExternalAccountService(RateFreakDbContext db, UserManager<AccountUser> users,
    SignInManager<AccountUser> signIn) : IExternalAccountService
{
    public async Task<Guid> BeginFlowAsync(DateTimeOffset expiresAt, CancellationToken cancellationToken)
    {
        await db.Set<ExternalAuthenticationChallenge>().Where(c => c.ExpiresAt <= DateTimeOffset.UtcNow)
            .ExecuteDeleteAsync(cancellationToken);
        var challenge = new ExternalAuthenticationChallenge { Id = Guid.NewGuid(), ExpiresAt = expiresAt };
        db.Add(challenge);
        await db.SaveChangesAsync(cancellationToken);
        return challenge.Id;
    }

    public async Task<bool> ConsumeFlowAsync(Guid id, CancellationToken cancellationToken) =>
        await db.Set<ExternalAuthenticationChallenge>().Where(c => c.Id == id && c.ExpiresAt > DateTimeOffset.UtcNow)
            .ExecuteDeleteAsync(cancellationToken) == 1;

    public async Task<AccountResult?> LoginAsync(ExternalIdentity identity, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await users.FindByLoginAsync(identity.Provider, identity.Subject);
        if (user is null) return null;
        if (!await signIn.CanSignInAsync(user) || await users.IsLockedOutAsync(user))
            return new(AccountStatus.Unauthenticated);
        await signIn.SignInAsync(user, isPersistent: false, authenticationMethod: identity.Provider);
        return new(AccountStatus.Success, Profile(user));
    }

    public async Task<bool> EmailAvailableAsync(string email, CancellationToken cancellationToken) =>
        !await db.Users.AnyAsync(u => u.NormalizedEmail == users.NormalizeEmail(email.Trim()), cancellationToken);

    public async Task<AccountResult> RegisterAsync(ExternalIdentity identity, ExternalRegistrationRequest request,
        CancellationToken cancellationToken)
    {
        var errors = AccountInput.ValidateProfile(request.Handle, request.DisplayName);
        if (!identity.EmailVerified || !AccountInput.ValidEmail(identity.Email))
            errors["email"] = ["A verified provider email is required."];
        if (errors.Count != 0) return new(AccountStatus.Invalid, Errors: errors);
        var now = DateTimeOffset.UtcNow;
        var user = new AccountUser { Id = Guid.NewGuid(), UserName = request.Handle!.Trim(),
            DisplayName = request.DisplayName!.Trim(), Email = identity.Email!.Trim(), EmailConfirmed = true,
            CreatedAt = now, UpdatedAt = now };
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            if (await users.FindByLoginAsync(identity.Provider, identity.Subject) is not null)
                return new(AccountStatus.Conflict);
            var created = await users.CreateAsync(user);
            if (!created.Succeeded) return new(AccountStatus.Conflict);
            var linked = await users.AddLoginAsync(user, new(identity.Provider, identity.Subject, identity.Provider));
            if (!linked.Succeeded) return new(AccountStatus.Conflict);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (KnownConflict(ex))
        {
            await transaction.RollbackAsync(cancellationToken);
            db.ChangeTracker.Clear();
            return new(AccountStatus.Conflict);
        }
        await signIn.SignInAsync(user, isPersistent: false, authenticationMethod: identity.Provider);
        return new(AccountStatus.Success, Profile(user));
    }

    public async Task<AccountStatus> LinkAsync(Guid userId, string stamp, ExternalIdentity identity,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            // Serialize links and security-stamp changes on this account, including concurrent logout.
            var user = await db.Users.FromSqlInterpolated($"SELECT * FROM users WHERE id = {userId} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken);
            // Session validation may already track this user. A tracking SQL query
            // resolves that instance without refreshing values; reload under the lock.
            if (user is not null) await db.Entry(user).ReloadAsync(cancellationToken);
            if (user is null || user.SecurityStamp != stamp || !await signIn.CanSignInAsync(user) || await users.IsLockedOutAsync(user))
                return AccountStatus.Unauthenticated;
            var existing = await db.UserLogins.AsNoTracking().SingleOrDefaultAsync(l =>
                l.LoginProvider == identity.Provider && l.ProviderKey == identity.Subject, cancellationToken);
            if (existing is not null)
                return existing.UserId == userId ? AccountStatus.Success : AccountStatus.Conflict;
            if (await db.UserLogins.AnyAsync(l => l.UserId == userId && l.LoginProvider == identity.Provider, cancellationToken))
                return AccountStatus.Conflict;
            var result = await users.AddLoginAsync(user, new(identity.Provider, identity.Subject, identity.Provider));
            if (!result.Succeeded) return AccountStatus.Conflict;
            await transaction.CommitAsync(cancellationToken);
            return AccountStatus.Success;
        }
        catch (DbUpdateException ex) when (KnownConflict(ex))
        {
            await transaction.RollbackAsync(cancellationToken);
            db.ChangeTracker.Clear();
            return AccountStatus.Conflict;
        }
    }

    public async Task<AccountLogins?> ListAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null) return null;
        var providers = await db.UserLogins.AsNoTracking().Where(l => l.UserId == userId)
            .OrderBy(l => l.LoginProvider).Select(l => l.LoginProvider).ToListAsync(cancellationToken);
        return new(user.PasswordHash is not null, providers);
    }

    public Task<string?> StampAsync(Guid userId, CancellationToken cancellationToken) => db.Users.AsNoTracking()
        .Where(u => u.Id == userId).Select(u => u.SecurityStamp).SingleOrDefaultAsync(cancellationToken);

    private static bool KnownConflict(DbUpdateException ex) => ex.InnerException is PostgresException
    { SqlState: PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.ForeignKeyViolation };
    private static AccountProfile Profile(AccountUser user) =>
        new(user.Id, user.UserName!, user.DisplayName, user.Email!, user.CreatedAt);
}
