using Microsoft.Extensions.Options;
using RateFreak.Application.Catalog;

namespace RateFreak.Infrastructure.Artwork;

public sealed class FileArtworkStorage(IOptions<ArtworkOptions> options) : IArtworkStorage
{
    internal string Root()
    {
        var path = options.Value.RootPath;
        if (!options.Value.Enabled || string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path)) throw new IOException("Artwork storage is unavailable.");
        path = Path.GetFullPath(path);
        for (var parent = new DirectoryInfo(path); parent is not null; parent = parent.Parent)
            if (parent.LinkTarget is not null) throw new IOException("Artwork storage must not traverse symbolic links.");
        Directory.CreateDirectory(path);
        return path;
    }
    private string Resolve(string key)
    {
        var extension = Path.GetExtension(key);
        if (extension is not (".webp" or ".stage" or ".lease") || !Guid.TryParseExact(Path.GetFileNameWithoutExtension(key), "N", out _) || key != Path.GetFileName(key)) throw new ArgumentException("Invalid storage key.");
        var path = Path.Combine(Root(), key);
        if (File.Exists(path) && new FileInfo(path).LinkTarget is not null) throw new IOException("Artwork storage must not traverse symbolic links.");
        return path;
    }
    public async Task<StagedArtwork> StageAsync(Stream content, CancellationToken ct)
    {
        var id = Guid.NewGuid().ToString("N"); var key = id + ".stage";
        var lease = new FileStream(Resolve(id + ".lease"), FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
        try
        {
            await using var output = new FileStream(Resolve(key), FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, FileOptions.Asynchronous);
            await content.CopyToAsync(output, ct); await output.FlushAsync(ct);
            return new(key, new StageLease(lease, Resolve(id + ".lease")));
        }
        catch { lease.Dispose(); File.Delete(Resolve(id + ".lease")); File.Delete(Resolve(key)); throw; }
    }
    private sealed class StageLease(FileStream stream, string path) : IDisposable
    {
        public void Dispose() { stream.Dispose(); try { File.Delete(path); } catch (IOException) { } }
    }
    public Task<string> PromoteAsync(StagedArtwork staged, Guid assetId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested(); var key = assetId.ToString("N") + ".webp";
        File.Move(Resolve(staged.Key), Resolve(key), false); return Task.FromResult(key);
    }
    public Task<Stream?> OpenReadAsync(string key, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested(); var path = Resolve(key);
        try { return Task.FromResult<Stream?>(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, 65536, FileOptions.Asynchronous)); }
        catch (FileNotFoundException) { return Task.FromResult<Stream?>(null); }
    }
    public Task DeleteAsync(string key, CancellationToken ct) { ct.ThrowIfCancellationRequested(); File.Delete(Resolve(key)); return Task.CompletedTask; }
}
