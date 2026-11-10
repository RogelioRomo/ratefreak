namespace RateFreak.Application.Identity;

public sealed record RegisterRequest(string? Email, string? Password, string? Handle, string? DisplayName);
public sealed record LoginRequest(string? Email, string? Password);
public sealed record AccountProfile(Guid Id, string Handle, string DisplayName, string Email, DateTimeOffset CreatedAt, string? Bio = null, bool ProfileTextHidden = false);
public enum AccountStatus { Success, Invalid, Conflict, Unauthenticated }
public sealed record AccountResult(AccountStatus Status, AccountProfile? Profile = null,
    Dictionary<string, string[]>? Errors = null, string? RecoveryCode = null);

public interface IAccountService
{
    Task<AccountResult> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken);
    Task<AccountResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken);
    Task<AccountProfile?> FindAsync(Guid id, CancellationToken cancellationToken);
    Task LogoutAsync(Guid id, CancellationToken cancellationToken);
}
