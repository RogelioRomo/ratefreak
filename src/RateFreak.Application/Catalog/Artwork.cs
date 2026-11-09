namespace RateFreak.Application.Catalog;

public sealed record DecodedArtwork(byte[] Bytes, string ContentType, int Width, int Height);
public sealed record StagedArtwork(string Key, IDisposable Lease) : IDisposable { public void Dispose() => Lease.Dispose(); }
public sealed record ArtworkRead(Stream Content, string ContentType);
public interface IArtworkStorage
{
    Task<StagedArtwork> StageAsync(Stream content, CancellationToken ct);
    Task<string> PromoteAsync(StagedArtwork staged, Guid assetId, CancellationToken ct);
    Task<Stream?> OpenReadAsync(string key, CancellationToken ct);
    Task DeleteAsync(string key, CancellationToken ct);
}
public interface IArtworkDecoder { Task<DecodedArtwork> DecodeAsync(Stream input, CancellationToken ct); }
public interface IArtworkService
{
    Task<CatalogResult<MediaDetails>> PutAsync(Guid actorId, Guid itemId, long expectedRevision, Stream input, CancellationToken ct);
    Task<CatalogResult<MediaDetails>> DeleteAsync(Guid actorId, Guid itemId, long expectedRevision, CancellationToken ct);
    Task<ArtworkRead?> ReadPublicForStaffAsync(Guid actorId, Guid itemId, Guid assetId, CancellationToken ct);
    Task<ArtworkRead?> ReadForReportAsync(Guid actorId, Guid reportId, Guid assetId, CancellationToken ct);
    Task<ArtworkRead?> ReadAsync(Guid? viewerId, Guid itemId, Guid assetId, CancellationToken ct);
}
