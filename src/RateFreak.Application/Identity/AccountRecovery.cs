namespace RateFreak.Application.Identity;

public sealed record LocalRegistrationResponse(Guid Id, string Handle, string DisplayName, string Email,
    DateTimeOffset CreatedAt, string? Bio, string RecoveryCode);

public sealed record GenerateRecoveryCodeRequest(string? Password);
public sealed record ResetPasswordRequest(string? Handle, string? RecoveryCode, string? Password);
public sealed record RecoveryStatus(bool HasPassword, bool HasRecoveryCode);
public enum RecoveryOutcome { Success, Invalid, Unauthenticated }
public sealed record RecoveryResult(RecoveryOutcome Outcome, string? RecoveryCode = null,
    Dictionary<string, string[]>? Errors = null);

public interface IAccountRecoveryService
{
    Task<RecoveryResult> GenerateAsync(Guid userId, string stamp, GenerateRecoveryCodeRequest request, CancellationToken ct);
    Task<RecoveryResult> ResetAsync(ResetPasswordRequest request, CancellationToken ct);
    Task<RecoveryStatus?> StatusAsync(Guid userId, CancellationToken ct);
}
