using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RateFreak.Application.Catalog;
using RateFreak.Application.Identity;
using RateFreak.Domain.Catalog;
using RateFreak.Infrastructure.Authentication;
using RateFreak.Infrastructure.Persistence;
using SkiaSharp;

namespace RateFreak.IntegrationTests;

// These assertions inspect the process-wide temporary archive directory.
[CollectionDefinition("Account data archive files", DisableParallelization = true)]
public sealed class AccountDataArchiveFileCollection;

[Collection("Account data archive files")]
[Trait("Category", "Database")]
public sealed class AccountDataArtworkTests(AccountDatabase database) : IClassFixture<AccountDatabase>
{
    private const string Password = "Artwork-export-password-72";
    private const long Limit = 256L * 1024 * 1024;
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private ApiFactory Factory(Storage storage) => new(connectionString: database.ConnectionString,
        configureServices: services => services.AddSingleton<IArtworkStorage>(storage));

    private static async Task<AccountUser> Account(IServiceProvider services)
    {
        var name = "export_" + Guid.NewGuid().ToString("N")[..15];
        var user = new AccountUser { Id = Guid.NewGuid(), UserName = name, DisplayName = name, Email = name + "@example.test" };
        var result = await services.GetRequiredService<UserManager<AccountUser>>().CreateAsync(user, Password);
        Assert.True(result.Succeeded, string.Join(", ", result.Errors.Select(e => e.Description)));
        return user;
    }

    private static async Task<ArtworkAsset> Asset(IServiceProvider services, AccountUser user, string title, long bytes)
    {
        var db = services.GetRequiredService<RateFreakDbContext>();
        var category = new Category("Private export " + Guid.NewGuid(), "private", user.Id);
        var item = MediaItem.CreateCustom(user.Id, title, "Private description", category.Id);
        var asset = new ArtworkAsset { ItemId = item.Id, AssetId = Guid.NewGuid(), StorageKey = "secret-storage-" + Guid.NewGuid().ToString("N"),
            ContentType = "image/webp", Width = 2, Height = 2, ByteLength = bytes };
        db.AddRange(category, item, asset); await db.SaveChangesAsync(Ct);
        return asset;
    }

    private static async Task<string> Confirm(IServiceProvider services, AccountUser user)
    {
        var grant = await services.GetRequiredService<IAccountDataService>().ConfirmAsync(user.Id, user.SecurityStamp!, new("export", Password), Ct);
        Assert.NotNull(grant); return grant;
    }

    private static HashSet<string> TemporaryArchives() => Directory.GetFiles(Path.GetTempPath(), "ratefreak-export-*.zip").ToHashSet();

    [Fact]
    public async Task Export_preserves_actual_WebP_bytes_and_safe_mapping_without_storage_keys_or_other_account_data()
    {
        using var bitmap = new SKBitmap(2, 2); bitmap.Erase(SKColors.DarkBlue);
        using var image = SKImage.FromBitmap(bitmap); using var encoded = image.Encode(SKEncodedImageFormat.Webp, 100);
        var bytes = encoded.ToArray();
        var storage = new Storage(_ => new MemoryStream(bytes, writable: false));
        await using var factory = Factory(storage); await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider; var user = await Account(services);
        var ownAsset = await Asset(services, user, "../unsafe-title/secret", bytes.Length);
        var other = await Account(services);
        var otherAsset = await Asset(services, other, "OTHER_PRIVATE_TITLE_" + Guid.NewGuid(), bytes.Length);
        var db = services.GetRequiredService<RateFreakDbContext>();
        var subject = "PRIVATE_PROVIDER_SUBJECT_" + Guid.NewGuid();
        db.UserLogins.Add(new IdentityUserLogin<Guid> { UserId = user.Id, LoginProvider = "google", ProviderDisplayName = "Google", ProviderKey = subject });
        await db.SaveChangesAsync(Ct);
        var grant = await Confirm(services, user);
        var before = TemporaryArchives();
        await using (var stream = await services.GetRequiredService<IAccountDataService>().ExportAsync(user.Id, user.SecurityStamp!, grant, Ct))
        {
            Assert.True(stream.CanSeek); Assert.Equal(0, stream.Position);
            using var zip = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
            var archivePath = "artwork/" + ownAsset.AssetId + ".webp";
            using var payload = new MemoryStream();
            await zip.GetEntry(archivePath)!.Open().CopyToAsync(payload, Ct);
            Assert.Equal(bytes, payload.ToArray());
            Assert.Single(zip.Entries, e => e.FullName.StartsWith("artwork/", StringComparison.Ordinal));
            Assert.All(zip.Entries, entry => Assert.DoesNotContain("..", entry.FullName));
            using var mappingStream = zip.GetEntry("artwork.json")!.Open();
            using var mappings = await JsonDocument.ParseAsync(mappingStream, cancellationToken: Ct);
            var mapping = Assert.Single(mappings.RootElement.EnumerateArray());
            Assert.Equal(ownAsset.ItemId, mapping.GetProperty("itemId").GetGuid());
            Assert.Equal(archivePath, mapping.GetProperty("path").GetString());
            Assert.Equal(bytes.Length, mapping.GetProperty("byteLength").GetInt64());
            var jsonText = new StringBuilder();
            foreach (var entry in zip.Entries.Where(e => e.FullName.EndsWith(".json", StringComparison.Ordinal)))
            {
                using var reader = new StreamReader(entry.Open()); jsonText.Append(await reader.ReadToEndAsync(Ct));
            }
            var text = jsonText.ToString();
            Assert.DoesNotContain(ownAsset.StorageKey, text); Assert.DoesNotContain(otherAsset.StorageKey, text);
            Assert.DoesNotContain(other.Email!, text); Assert.DoesNotContain(otherAsset.ItemId.ToString(), text);
            Assert.DoesNotContain(subject, text); Assert.DoesNotContain(user.PasswordHash!, text);
            Assert.DoesNotContain(user.SecurityStamp!, text); Assert.DoesNotContain("storageKey", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("google", text); Assert.Contains("../unsafe-title/secret", text);
        }
        Assert.Empty(TemporaryArchives().Except(before));
        Assert.Equal([ownAsset.StorageKey], storage.OpenedKeys);
    }

    [Fact]
    public async Task Missing_referenced_artwork_fails_explicitly_and_removes_temporary_archive()
    {
        var storage = new Storage(_ => null);
        await using var factory = Factory(storage); await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider; var user = await Account(services); await Asset(services, user, "Missing artwork", 20);
        var grant = await Confirm(services, user); var before = TemporaryArchives();
        var error = await Assert.ThrowsAsync<AccountDataException>(() => services.GetRequiredService<IAccountDataService>()
            .ExportAsync(user.Id, user.SecurityStamp!, grant, Ct));
        Assert.Equal(409, error.StatusCode); Assert.Contains("missing", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(TemporaryArchives().Except(before));
        Assert.True(await services.GetRequiredService<RateFreakDbContext>().Users.AnyAsync(u => u.Id == user.Id, Ct));
    }

    [Fact]
    public async Task Cancelling_artwork_copy_disposes_source_and_removes_private_temporary_archive()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        var reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var source = new BlockingStream(reached);
        var storage = new Storage(_ => source);
        await using var factory = Factory(storage); await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider; var user = await Account(services); await Asset(services, user, "Cancelled artwork", 20);
        var grant = await Confirm(services, user); var before = TemporaryArchives();
        var export = services.GetRequiredService<IAccountDataService>().ExportAsync(user.Id, user.SecurityStamp!, grant, cancellation.Token);
        await reached.Task.WaitAsync(TimeSpan.FromSeconds(15), Ct);
        var files = TemporaryArchives().Except(before).ToArray();
        if (!OperatingSystem.IsWindows())
            foreach (var path in files) Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(path));
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => export);
        Assert.True(source.Disposed); Assert.Empty(TemporaryArchives().Except(before));
    }

    [Fact]
    public async Task Oversize_artwork_stream_is_bounded_at_256_MiB_and_leaves_no_archive()
    {
        var source = new GeneratedStream(Limit + 1024 * 1024);
        var storage = new Storage(_ => source);
        await using var factory = Factory(storage); await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider; var user = await Account(services);
        // Deliberately small metadata proves the actual stream is bounded too.
        await Asset(services, user, "Large artwork", 20); var grant = await Confirm(services, user);
        var before = TemporaryArchives();
        var error = await Assert.ThrowsAsync<AccountDataException>(() => services.GetRequiredService<IAccountDataService>()
            .ExportAsync(user.Id, user.SecurityStamp!, grant, Ct));
        Assert.Equal(413, error.StatusCode);
        Assert.InRange(source.BytesRead, Limit - 1024 * 1024, Limit + 128 * 1024);
        Assert.True(source.Disposed); Assert.Empty(TemporaryArchives().Except(before));
    }

    private sealed class Storage(Func<string, Stream?> open) : IArtworkStorage
    {
        public List<string> OpenedKeys { get; } = [];
        public Task<Stream?> OpenReadAsync(string key, CancellationToken ct) { ct.ThrowIfCancellationRequested(); OpenedKeys.Add(key); return Task.FromResult(open(key)); }
        public Task<StagedArtwork> StageAsync(Stream stream, CancellationToken ct) => throw new NotSupportedException();
        public Task<string> PromoteAsync(StagedArtwork staged, Guid assetId, CancellationToken ct) => throw new NotSupportedException();
        public Task DeleteAsync(string key, CancellationToken ct) => throw new NotSupportedException();
    }

    private abstract class SourceStream : Stream
    {
        public bool Disposed { get; private set; }
        public override bool CanRead => true; public override bool CanSeek => false; public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException(); public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() => throw new NotSupportedException(); public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException(); public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing) { Disposed = true; base.Dispose(disposing); }
    }
    private sealed class BlockingStream(TaskCompletionSource reached) : SourceStream
    {
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        { reached.TrySetResult(); await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken); return 0; }
    }
    private sealed class GeneratedStream(long length) : SourceStream
    {
        public long BytesRead { get; private set; }
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var count = (int)Math.Min(buffer.Length, length - BytesRead);
            buffer.Span[..count].Fill(0x5a); BytesRead += count; return ValueTask.FromResult(count);
        }
    }
}
