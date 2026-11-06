using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RateFreak.Application.Identity;
using RateFreak.Infrastructure.Persistence;

namespace RateFreak.Infrastructure.Authentication;

internal sealed class AccountService(UserManager<AccountUser> users, SignInManager<AccountUser> signIn, RateFreakDbContext db, LocalAccountRegistration registration) : IAccountService
{
    public async Task<AccountResult> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var result = await registration.CreateAsync(request, cancellationToken);
        if (result.Status == AccountStatus.Success) await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public async Task<AccountResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var errors = AccountInput.Validate(request);
        if (errors.Count != 0) return new(AccountStatus.Invalid, Errors: errors);
        var user = await users.FindByEmailAsync(request.Email!.Trim());
        if (user is null) return new(AccountStatus.Unauthenticated);
        var result = await signIn.PasswordSignInAsync(user, request.Password!, isPersistent: false, lockoutOnFailure: true);
        return result.Succeeded ? new(AccountStatus.Success, Profile(user)) : new(AccountStatus.Unauthenticated);
    }

    public async Task<AccountProfile?> FindAsync(Guid id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await users.FindByIdAsync(id.ToString());
        return user is null ? null : Profile(user);
    }

    public async Task LogoutAsync(Guid id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            var user = await db.Users.FromSqlInterpolated($"SELECT * FROM users WHERE id = {id} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken);
            if (user is not null)
            {
                // Authentication can have tracked an older account before a concurrent profile edit.
                await db.Entry(user).ReloadAsync(cancellationToken);
                var result = await users.UpdateSecurityStampAsync(user);
                if (!result.Succeeded) throw new InvalidOperationException("Session revocation failed.");
            }
            await transaction.CommitAsync(cancellationToken);
        }
        await signIn.SignOutAsync();
    }

    private static AccountProfile Profile(AccountUser user) =>
        new(user.Id, user.UserName!, user.DisplayName, user.Email!, user.CreatedAt, user.Bio);
}
