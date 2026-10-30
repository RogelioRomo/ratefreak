using System.Text.Json.Serialization;

namespace RateFreak.Application.Identity;

public sealed record ExternalIdentity(string Provider, string Subject, string? Email, bool EmailVerified);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ExternalRegistrationRequest(string? Handle, string? DisplayName);
public sealed record AccountLogins(bool HasPassword, IReadOnlyList<string> Providers);

public interface IExternalAccountService
{
    Task<Guid> BeginFlowAsync(DateTimeOffset expiresAt, CancellationToken cancellationToken);
    Task<bool> ConsumeFlowAsync(Guid id, CancellationToken cancellationToken);
    Task<AccountResult?> LoginAsync(ExternalIdentity identity, CancellationToken cancellationToken);
    Task<bool> EmailAvailableAsync(string email, CancellationToken cancellationToken);
    Task<AccountResult> RegisterAsync(ExternalIdentity identity, ExternalRegistrationRequest request, CancellationToken cancellationToken);
    Task<AccountStatus> LinkAsync(Guid userId, string stamp, ExternalIdentity identity, CancellationToken cancellationToken);
    Task<AccountLogins?> ListAsync(Guid userId, CancellationToken cancellationToken);
    Task<string?> StampAsync(Guid userId, CancellationToken cancellationToken);
}
