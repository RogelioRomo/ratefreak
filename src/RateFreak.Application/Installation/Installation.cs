using RateFreak.Application.Identity;

namespace RateFreak.Application.Installation;

public sealed record InstallationStatus(bool Required, bool Configured);
public sealed record SetupRequest(string? SetupSecret, string? Email, string? Password, string? Handle, string? DisplayName);
public enum SetupOutcome { Success, Invalid, Completed, Unavailable }
public sealed record SetupResult(SetupOutcome Outcome, LocalRegistrationResponse? Account = null, Dictionary<string, string[]>? Errors = null);
public interface IInstallationService
{
    Task<InstallationStatus?> StatusAsync(CancellationToken ct);
    Task<SetupResult> CompleteAsync(SetupRequest request, CancellationToken ct);
}
