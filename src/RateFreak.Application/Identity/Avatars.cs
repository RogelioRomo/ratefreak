using System.Text.Json.Serialization;
using RateFreak.Application.Catalog;

namespace RateFreak.Application.Identity;

public sealed record AvatarState(string? AvatarUrl, long Revision);
public enum AvatarStatus { Success, Invalid, NotFound, Conflict, Unavailable }
public sealed record AvatarResult(AvatarStatus Status, AvatarState? Value = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RemoveAvatarRequest(long? ExpectedRevision);
public interface IAvatarService
{
    Task<AvatarResult> PutAsync(Guid actorId, long expectedRevision, Stream input, CancellationToken ct);
    Task<AvatarResult> DeleteAsync(Guid actorId, long expectedRevision, CancellationToken ct);
    Task<ArtworkRead?> ReadAsync(Guid userId, Guid assetId, CancellationToken ct);
    Task<ArtworkRead?> ReadOwnAsync(Guid actorId, Guid assetId, CancellationToken ct);
    Task<ArtworkRead?> ReadForProfileAsync(Guid actorId, Guid userId, Guid assetId, CancellationToken ct);
    Task<ArtworkRead?> ReadForReportAsync(Guid actorId, Guid reportId, Guid assetId, CancellationToken ct);
}
