namespace RateFreak.Application.Updates;
public sealed record InstalledBuild(string Version, string Revision, bool Official);
public sealed record UpdateStatus(InstalledBuild Installed, bool Enabled, string State, string? LatestVersion,
    string? ReleaseUrl, DateTimeOffset? LastAttemptAt, DateTimeOffset? LastSuccessAt,
    DateTimeOffset? NextCheckAt, bool Stale, bool CanCheck);
public interface IUpdateService
{
    Task<UpdateStatus> GetAsync(bool refresh, CancellationToken cancellationToken);
}
