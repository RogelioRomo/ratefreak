using System.Data;
using System.IO.Compression;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RateFreak.Application.Identity;
using RateFreak.Domain.Catalog;
using RateFreak.Infrastructure.Catalog;
using RateFreak.Infrastructure.Moderation;
namespace RateFreak.Infrastructure.Authentication;

internal sealed partial class AccountDataService
{
    private const long ArchiveLimit = 256L * 1024 * 1024;
    public async Task<Stream> ExportAsync(Guid userId, string stamp, string? grant, CancellationToken ct)
    {
        await using (var authorization = await db.Database.BeginTransactionAsync(ct))
        {
            if (await LockAsync(userId, stamp, ct) == null) throw new AccountDataException(401, "Your session has expired.");
            await ConsumeAsync(userId, stamp, grant, "export", ct);
            var budget = await db.Set<AccountExportBudget>().SingleOrDefaultAsync(b => b.UserId == userId, ct); var now = DateTimeOffset.UtcNow;
            if (budget == null) { budget = new() { UserId = userId }; db.Add(budget); }
            if (budget.WindowEndsAt <= now) { budget.Attempts = 0; budget.WindowEndsAt = now.AddMinutes(10); }
            if (budget.Attempts >= 2) throw new AccountDataException(429, "You can export twice per ten minutes. Try again later.");
            budget.Attempts++; await db.SaveChangesAsync(ct); await authorization.CommitAsync(ct);
        }
        var path = Path.Combine(Path.GetTempPath(), "ratefreak-export-" + Guid.NewGuid().ToString("N") + ".zip");
        var fileOptions = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.ReadWrite, Share = FileShare.None, Options = FileOptions.Asynchronous | FileOptions.DeleteOnClose };
        if (!OperatingSystem.IsWindows()) fileOptions.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        var file = new FileStream(path, fileOptions);
        var coordinated = false;
        try
        {
            await db.Database.OpenConnectionAsync(ct);
            // Acquire before BEGIN: a repeatable-read snapshot taken while waiting for an xact lock could be stale.
            await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_lock(734188290134)", ct);
            coordinated = true;
            await using var snapshot = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
            // Database reads start only after catalog coordination; local artwork remains stable until archive is finished.
            var account = await db.Users.AsNoTracking().Where(u => u.Id == userId && u.SecurityStamp == stamp)
                .Select(u => new { u.Id, Handle = u.UserName, u.DisplayName, u.Email, u.Bio, u.CreatedAt, u.UpdatedAt }).SingleOrDefaultAsync(ct);
            if (account == null) throw new AccountDataException(401, "Your session has expired.");
            var readable = db.MediaItems.Where(CatalogAccess.Readable(userId));
            using (var zip = new ZipArchive(new ArchiveWriteStream(file, ArchiveLimit), ZipArchiveMode.Create, true))
            {
                async Task Json(string name, object value) { await using var output = zip.CreateEntry(name, CompressionLevel.NoCompression).Open(); await JsonSerializer.SerializeAsync(output, value, value.GetType(), new JsonSerializerOptions(JsonSerializerDefaults.Web), ct); }
                async Task Query<T>(string name, IQueryable<T> query) where T : class { await using var output = zip.CreateEntry(name, CompressionLevel.NoCompression).Open(); await JsonSerializer.SerializeAsync(output, query.AsNoTracking().AsAsyncEnumerable(), new JsonSerializerOptions(JsonSerializerDefaults.Web), ct); }
                await Json("manifest.json", new { formatVersion = 1, exportedAt = DateTimeOffset.UtcNow, accountId = userId, artworkMapping = "artwork.json" }); await Json("account.json", account);
                await Query("providers.json", db.UserLogins.Where(l => l.UserId == userId).Select(l => new { provider = l.LoginProvider }));
                await Query("library.json", db.LibraryEntries.Where(x => x.UserId == userId).Select(x => new { x.Id, x.MediaItemId, Title = readable.Where(m => m.Id == x.MediaItemId).Select(m => m.Title).FirstOrDefault(), x.Status, x.StartedAt, x.CompletedAt, x.CreatedAt, x.UpdatedAt }));
                await Query("ratings.json", db.Ratings.Where(x => x.UserId == userId).Select(x => new { x.Id, x.MediaItemId, Title = readable.Where(m => m.Id == x.MediaItemId).Select(m => m.Title).FirstOrDefault(), x.Score, x.CreatedAt, x.UpdatedAt }));
                await Query("reviews.json", db.Reviews.Where(x => x.UserId == userId).Select(x => new { x.Id, x.MediaItemId, Title = readable.Where(m => m.Id == x.MediaItemId).Select(m => m.Title).FirstOrDefault(), x.Body, x.IsSpoiler, x.IsHidden, x.CreatedAt, x.UpdatedAt }));
                await Query("comments.json", from comment in db.ReviewComments
                    join review in db.Reviews on comment.ReviewId equals review.Id
                    where comment.UserId == userId
                    select new { comment.Id, comment.ReviewId, review.MediaItemId,
                        Title = readable.Where(m => m.Id == review.MediaItemId).Select(m => m.Title).FirstOrDefault(),
                        comment.Body, comment.IsSpoiler, comment.IsHidden, comment.CreatedAt, comment.UpdatedAt });
                await Query("collections.json", db.Collections.Where(x => x.OwnerUserId == userId));
                await Query("collection-items.json", db.CollectionItems.Where(x => db.Collections.Any(c => c.Id == x.CollectionId && c.OwnerUserId == userId)).Select(x => new { x.Id, x.CollectionId, x.MediaItemId, x.Position, Title = readable.Where(m => m.Id == x.MediaItemId).Select(m => m.Title).FirstOrDefault() }));
                await Query("following.json", from f in db.UserFollows join u in db.Users on f.FolloweeId equals u.Id where f.FollowerId == userId select new { accountId = u.Id, handle = u.UserName, f.CreatedAt });
                await Query("followers.json", from f in db.UserFollows join u in db.Users on f.FollowerId equals u.Id where f.FolloweeId == userId select new { accountId = u.Id, handle = u.UserName, f.CreatedAt });
                await Query("items.json", db.MediaItems.Where(m => m.CreatorUserId == userId).Select(m => new { m.Id, m.Title, m.MediaType, m.Description, m.ReleaseDate, m.ImageReference, m.Metadata, m.CategoryId, m.Provenance, m.Visibility, m.IsArchived, m.IsHidden, m.Revision, m.CreatedAt, m.UpdatedAt }));
                await Query("categories.json", db.Categories.Where(c => c.OwnerUserId == userId));
                await Query("category-aliases.json", db.CategoryAliases.Where(a => db.Categories.Any(c => c.OwnerUserId == userId && (c.Id == a.SourceCategoryId || c.Id == a.TargetCategoryId))));
                await Query("recovery-events.json", db.Set<AccountRecoveryEvent>().Where(e => e.UserId == userId).Select(e => new { e.Action, e.CreatedAt }));
                await Query("reports.json", db.ContentReports.Where(r => r.ReporterId == userId).Select(r => new { r.Id, r.TargetId, r.TargetType, r.Reason, r.Details, r.Status, r.CreatedAt, r.ResolvedAt }));
                var assets = db.Set<ArtworkAsset>().Where(a => db.MediaItems.Any(m => m.Id == a.ItemId && m.CreatorUserId == userId));
                await Query("artwork.json", assets.Select(a => new { a.ItemId, a.AssetId, path = "artwork/" + a.AssetId.ToString() + ".webp", a.ContentType, a.Width, a.Height, a.ByteLength }));
                await foreach (var asset in assets.AsNoTracking().AsAsyncEnumerable().WithCancellation(ct))
                {
                    await using var source = await artwork.OpenReadAsync(asset.StorageKey, ct) ?? throw new AccountDataException(409, "A referenced artwork file is missing. Contact the operator before retrying export.");
                    await using var target = zip.CreateEntry("artwork/" + asset.AssetId + ".webp", CompressionLevel.NoCompression).Open(); await source.CopyToAsync(target, ct);
                }
            }
            await snapshot.CommitAsync(ct); file.Position = 0; return file;
        }
        catch { await file.DisposeAsync(); throw; }
        finally
        {
            try
            {
                try
                {
                    if (coordinated) await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_unlock(734188290134)", CancellationToken.None);
                }
                finally { await db.Database.CloseConnectionAsync(); }
            }
            catch { await file.DisposeAsync(); throw; }
        }
    }
    private sealed class ArchiveWriteStream(Stream inner, long maximum) : Stream
    {
        private long count;
        public override bool CanRead => false; public override bool CanSeek => false; public override bool CanWrite => true;
        public override long Length => count; public override long Position { get => count; set => throw new NotSupportedException(); }
        private void Check(int size) { if (size > maximum - count) throw new AccountDataException(413, "Export exceeds 256 MiB. Contact the operator for a larger export."); count += size; }
        public override void Write(byte[] buffer, int offset, int size) { Check(size); inner.Write(buffer, offset, size); }
        public override void Write(ReadOnlySpan<byte> buffer) { Check(buffer.Length); inner.Write(buffer); }
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct = default) { Check(buffer.Length); await inner.WriteAsync(buffer, ct); }
        public override void Flush() => inner.Flush(); public override Task FlushAsync(CancellationToken ct) => inner.FlushAsync(ct);
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException(); public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException(); public override void SetLength(long value) => throw new NotSupportedException();
    }
}
