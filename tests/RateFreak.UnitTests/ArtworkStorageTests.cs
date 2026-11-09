using Microsoft.Extensions.Options;
using RateFreak.Infrastructure.Artwork;

namespace RateFreak.UnitTests;

public sealed class ArtworkStorageTests
{
    [Fact]
    public async Task Promoted_file_persists_across_storage_instances_and_paths_cannot_escape_root()
    {
        var root = Path.Combine(Path.GetTempPath(), "ratefreak-art-" + Guid.NewGuid().ToString("N"));
        try
        {
            var options = Options.Create(new ArtworkOptions { Enabled = true, RootPath = root });
            var storage = new FileArtworkStorage(options);
            var stage = await storage.StageAsync(new MemoryStream([1, 2, 3]), TestContext.Current.CancellationToken);
            var id = Guid.NewGuid(); var key = await storage.PromoteAsync(stage, id, TestContext.Current.CancellationToken);
            var restarted = new FileArtworkStorage(options);
            await using var read = await restarted.OpenReadAsync(key, TestContext.Current.CancellationToken);
            Assert.NotNull(read); Assert.Equal(1, read.ReadByte());
            await Assert.ThrowsAsync<ArgumentException>(() => restarted.OpenReadAsync("../outside.webp", TestContext.Current.CancellationToken));
            await restarted.DeleteAsync(key, TestContext.Current.CancellationToken);
            Assert.Null(await restarted.OpenReadAsync(key, TestContext.Current.CancellationToken));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
