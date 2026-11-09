namespace RateFreak.Application.Identity;

public sealed record AccountDataStatus(bool HasPassword, string[] Providers, string? ConfirmedOperation,
    int RecoveryEventDays = 90, int ResolvedModerationDays = 365, int BackupDays = 30);
public sealed record AccountDataConfirmation(string Operation, string Password);
public sealed record AccountDataDeletion(string Handle);
public sealed class AccountDataException(int statusCode, string message) : Exception(message)
{ public int StatusCode { get; } = statusCode; }
public interface IAccountDataService
{
    Task<AccountDataStatus?> StatusAsync(Guid userId, string stamp, string? grant, CancellationToken ct);
    Task<string?> ConfirmAsync(Guid userId, string stamp, AccountDataConfirmation request, CancellationToken ct);
    Task<string?> IssueExternalGrantAsync(Guid userId, string stamp, string operation, ExternalIdentity identity, CancellationToken ct);
    Task<Stream> ExportAsync(Guid userId, string stamp, string? grant, CancellationToken ct);
    Task DeleteAsync(Guid userId, string stamp, string? grant, string handle, CancellationToken ct);
}
public interface IAccountErasureMaintenance
{
    Task ReplayAsync(CancellationToken ct);
    Task RetainAsync(DateTimeOffset now, CancellationToken ct);
}
