using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using RateFreak.Application.Identity;
using RateFreak.Infrastructure.Persistence;

namespace RateFreak.Infrastructure.Authentication;

// The caller owns the transaction so setup can include privileges and completion atomically.
internal sealed class LocalAccountRegistration(UserManager<AccountUser> users, RateFreakDbContext db)
{
    public async Task<AccountResult> CreateAsync(RegisterRequest request, CancellationToken cancellationToken)
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
        var code = RecoveryCode.Generate();
        db.Add(new AccountRecoveryState { UserId = user.Id, Verifier = RecoveryCode.Hash(user.Id, code) });
        db.Add(new AccountRecoveryEvent { UserId = user.Id, Action = "code-issued" });
        IdentityResult result;
        try { result = await users.CreateAsync(user, request.Password!); }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: "ux_users_normalized_email" or "ux_users_normalized_handle" })
        {
            return new(AccountStatus.Conflict);
        }
        if (result.Succeeded)
        {
            return new(AccountStatus.Success, Profile(user), RecoveryCode: code);
        }
        if (result.Errors.Any(e => e.Code is "DuplicateEmail" or "DuplicateUserName"))
            return new(AccountStatus.Conflict);
        return new(AccountStatus.Invalid, Errors: new() { ["password"] = result.Errors.Select(e => e.Description).ToArray() });
    }

    private static AccountProfile Profile(AccountUser user) =>
        new(user.Id, user.UserName!, user.DisplayName, user.Email!, user.CreatedAt, user.Bio, user.IsProfileHidden);
}
