using Microsoft.Extensions.Options;
using RateFreak.Application.Catalog;

namespace RateFreak.Infrastructure.Artwork;

/// <summary>Separate namespace for account images; keys remain canonical leaf names.</summary>
public sealed class FileAvatarStorage : IArtworkStorage
{
    private readonly FileArtworkStorage storage;
    public FileAvatarStorage(IOptions<ArtworkOptions> options)
    {
        var root = options.Value.RootPath;
        storage = new(Options.Create(new ArtworkOptions
        {
            Enabled = options.Value.Enabled,
            RootPath = !string.IsNullOrWhiteSpace(root) && Path.IsPathFullyQualified(root) ? Path.Combine(root, "avatars") : null
        }));
    }
    internal string Root() => storage.Root();
    public Task<StagedArtwork> StageAsync(Stream content, CancellationToken ct) => storage.StageAsync(content, ct);
    public Task<string> PromoteAsync(StagedArtwork staged, Guid assetId, CancellationToken ct) => storage.PromoteAsync(staged, assetId, ct);
    public Task<Stream?> OpenReadAsync(string key, CancellationToken ct) => storage.OpenReadAsync(key, ct);
    public Task DeleteAsync(string key, CancellationToken ct) => storage.DeleteAsync(key, ct);
}
