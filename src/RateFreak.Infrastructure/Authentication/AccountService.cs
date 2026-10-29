using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using RateFreak.Application.Identity;

namespace RateFreak.Infrastructure.Authentication;

internal sealed class AccountService(UserManager<AccountUser> users, SignInManager<AccountUser> signIn) : IAccountService
{
    public async Task<AccountResult> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var errors = AccountInput.Validate(request);
        if (errors.Count != 0) return new(AccountStatus.Invalid, Errors: errors);
        var now = DateTimeOffset.UtcNow;
        var user = new AccountUser
        {
            Id = Guid.NewGuid(), UserName = request.Handle!.Trim(), Email = request.Email!.Trim(),
            DisplayName = request.DisplayName!.Trim(), CreatedAt = now, UpdatedAt = now
        };
        IdentityResult result;
        try { result = await users.CreateAsync(user, request.Password!); }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: "ux_users_normalized_email" or "ux_users_normalized_handle" })
        {
            return new(AccountStatus.Conflict);
        }
        if (result.Succeeded) return new(AccountStatus.Success, Profile(user));
        if (result.Errors.Any(e => e.Code is "DuplicateEmail" or "DuplicateUserName"))
            return new(AccountStatus.Conflict);
        return new(AccountStatus.Invalid, Errors: new() { ["password"] = result.Errors.Select(e => e.Description).ToArray() });
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
        var user = await users.FindByIdAsync(id.ToString());
        if (user is not null)
        {
            var result = await users.UpdateSecurityStampAsync(user);
            if (!result.Succeeded) throw new InvalidOperationException("Session revocation failed.");
        }
        await signIn.SignOutAsync();
    }

    private static AccountProfile Profile(AccountUser user) =>
        new(user.Id, user.UserName!, user.DisplayName, user.Email!, user.CreatedAt);
}
