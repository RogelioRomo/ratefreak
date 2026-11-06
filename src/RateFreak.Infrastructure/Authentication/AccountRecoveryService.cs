using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RateFreak.Application.Identity;
using RateFreak.Infrastructure.Persistence;

namespace RateFreak.Infrastructure.Authentication;

internal sealed class AccountRecoveryService(RateFreakDbContext db, UserManager<AccountUser> users,
    SignInManager<AccountUser> signIn) : IAccountRecoveryService
{
    public async Task<RecoveryStatus?> StatusAsync(Guid userId, CancellationToken ct) =>
        await db.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => new RecoveryStatus(
            u.PasswordHash != null, db.Set<AccountRecoveryState>().Any(s => s.UserId == userId && s.Verifier != null)))
            .SingleOrDefaultAsync(ct);

    public async Task<RecoveryResult> GenerateAsync(Guid userId, string stamp, GenerateRecoveryCodeRequest request, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(request.Password) || request.Password.Length > 128) return Invalid();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var user = await LockAsync(userId, ct);
        if (user is null || string.IsNullOrEmpty(stamp) || user.SecurityStamp != stamp) return new(RecoveryOutcome.Unauthenticated);
        if (user.PasswordHash is null) return Invalid();
        var state = await StateAsync(userId, ct);
        if (Blocked(state)) return Invalid();
        if (!await users.CheckPasswordAsync(user, request.Password))
        {
            Fail(state);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return Invalid();
        }
        var code = Issue(state, "code-replaced");
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new(RecoveryOutcome.Success, code);
    }

    public async Task<RecoveryResult> ResetAsync(ResetPasswordRequest request, CancellationToken ct)
    {
        var handle = request.Handle?.Trim();
        if (handle is null || handle.Length is < 3 or > 30 || handle.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '_') ||
            RecoveryCode.Normalize(request.RecoveryCode) is null || string.IsNullOrEmpty(request.Password) || request.Password.Length > 128)
            return Invalid();
        // Find only the stable identifier first; all mutable account state is read under the lock.
        var normalized = users.NormalizeName(handle);
        var id = await db.Users.AsNoTracking().Where(u => u.NormalizedUserName == normalized)
            .Select(u => (Guid?)u.Id).SingleOrDefaultAsync(ct);
        if (id is null) return Invalid();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var user = await LockAsync(id.Value, ct);
        if (user is null || user.PasswordHash is null) return Invalid();
        var state = await StateAsync(id.Value, ct);
        if (Blocked(state)) return Invalid();
        if (!RecoveryCode.Matches(user.Id, request.RecoveryCode!, state.Verifier))
        {
            Fail(state);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return Invalid();
        }
        var passwordErrors = new List<string>();
        foreach (var validator in users.PasswordValidators)
        {
            var result = await validator.ValidateAsync(users, user, request.Password);
            passwordErrors.AddRange(result.Errors.Select(e => e.Description));
        }
        if (passwordErrors.Count > 0) return new(RecoveryOutcome.Invalid, Errors: new() { ["password"] = passwordErrors.ToArray() });
        user.PasswordHash = users.PasswordHasher.HashPassword(user, request.Password);
        user.AccessFailedCount = 0;
        user.LockoutEnd = null;
        var code = Issue(state, "password-reset");
        // Identity updates the security/concurrency stamps and persists the staged verifier/event
        // within this transaction; rollback preserves the old password and code on any failure.
        var updated = await users.UpdateSecurityStampAsync(user);
        if (!updated.Succeeded) throw new InvalidOperationException("Account recovery update failed.");
        await transaction.CommitAsync(ct);
        // Clear a requesting browser's cookie as well; other sessions fail stamp validation.
        await signIn.SignOutAsync();
        return new(RecoveryOutcome.Success, code);
    }

    private async Task<AccountUser?> LockAsync(Guid id, CancellationToken ct)
    {
        var user = await db.Users.FromSqlInterpolated($"SELECT * FROM users WHERE id = {id} FOR UPDATE").SingleOrDefaultAsync(ct);
        if (user is not null) await db.Entry(user).ReloadAsync(ct);
        return user;
    }

    private async Task<AccountRecoveryState> StateAsync(Guid id, CancellationToken ct)
    {
        var state = await db.Set<AccountRecoveryState>().SingleOrDefaultAsync(s => s.UserId == id, ct);
        if (state is not null) return state;
        state = new() { UserId = id };
        db.Add(state);
        return state;
    }

    private static bool Blocked(AccountRecoveryState state) =>
        state.AttemptWindowEndsAt > DateTimeOffset.UtcNow && state.FailedAttempts >= 5;

    private static void Fail(AccountRecoveryState state)
    {
        var now = DateTimeOffset.UtcNow;
        if (state.AttemptWindowEndsAt is null || state.AttemptWindowEndsAt <= now)
        {
            state.FailedAttempts = 0;
            state.AttemptWindowEndsAt = now.AddMinutes(15);
        }
        state.FailedAttempts++;
    }

    private string Issue(AccountRecoveryState state, string action)
    {
        var code = RecoveryCode.Generate();
        state.Verifier = RecoveryCode.Hash(state.UserId, code);
        state.FailedAttempts = 0;
        state.AttemptWindowEndsAt = null;
        db.Add(new AccountRecoveryEvent { UserId = state.UserId, Action = action });
        return code;
    }

    private static RecoveryResult Invalid() => new(RecoveryOutcome.Invalid);
}
